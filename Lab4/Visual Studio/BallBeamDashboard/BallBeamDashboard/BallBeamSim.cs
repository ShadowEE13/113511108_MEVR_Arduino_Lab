using System.Globalization;

namespace BallBeamDashboard;

/// <summary>
/// 假裝成一台跑 Task4-3.ino 的 Ball-on-Beam，沒有硬體時拿來測試介面。
///   Arduino 端：指令、三段濾波、PID、輸出格式都照 Task4-3.ino 寫
///   真實世界：簡化物理（球滾動、舵機轉速限制、水平角有 1° 誤差、超音波雜訊與亂跳）
/// 物理參數是估的，模擬出來的最佳 PID 參數不等於實體的最佳值。
/// </summary>
public sealed class BallBeamSim
{
    public const int SampleMs = 50;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ================= Arduino 端（和 Task4-3.ino 相同） =================
    float kp = 2.0f, ki = 0.8f, kd = 4.5f, setpoint = 10.5f, baseAngle = 129.0f;
    long printMs = 200;
    bool controlOn = true, guiMode;
    const float TiltLimit = 17, MinCm = 2, MaxCm = 25, ILimit = 5, IBand = 3, DAlpha = 0.5f, JumpCm = 3;
    const int JumpMax = 3, NAvg = 4;
    const long HoldMs = 3000;

    float lastValid;
    int jumpCount;
    readonly float[] hist = new float[3];
    readonly float[] avgBuf = new float[NAvg];
    int avgIdx;
    float avgSum;
    float integral, prevPos, dFilt, termP, termI, termD;
    long ms = 1000, lastPrintMs, holdStart;

    // ================= 真實世界（Arduino 看不到） =================
    const double TrueLevel = 130.0;     // 真正的水平角比 baseAngle 多 1° → 要靠積分項補
    const double Linkage = 0.15;        // 舵機轉 1°，軌道約轉 0.15°（鐵絲連桿）
    const double BallAccel = 0.6 * 981; // 乒乓球是薄殼球，滾動加速度 a = 3/5·g·sinθ (cm/s²)
    const double ServoRate = 500;       // SG90 約 0.12 s / 60°
    const double Rolling = 0.3;         // 滾動阻力 (1/s)
    const double NearEnd = 3, FarEnd = 22;
    double x = 6, v, servo, servoCmd;
    readonly Random rng = new(4321);
    readonly Queue<string> inbox = new();

    public BallBeamSim()
    {
        servo = servoCmd = baseAngle;
        float d = (float)x;                 // 跟 setup() 一樣，用第一筆讀值填滿濾波器
        lastValid = d;
        Array.Fill(hist, d);
        Array.Fill(avgBuf, d);
        avgSum = d * NAvg;
        prevPos = d;
    }

    public void Enqueue(string command)
    {
        lock (inbox) inbox.Enqueue(command);
    }

    public List<string> Boot()
    {
        var output = new List<string> { "# boot: default params (simulator)" };
        PrintParams(output);
        return output;
    }

    /// <summary>前進 50 ms（一次控制週期），回傳這段時間 Arduino 會印出的行</summary>
    public List<string> Tick()
    {
        var output = new List<string>();
        lock (inbox)
        {
            while (inbox.Count > 0) RunCommand(inbox.Dequeue().Trim(), output);
        }

        for (int k = 0; k < SampleMs; k++) Physics(0.001);   // 物理用 1 ms 小步積分
        ms += SampleMs;

        // ---- 以下對應 Task4-3.ino 的 loop() ----
        float pos = FilterDistance(ReadSensor());
        float tilt = 0;
        if (controlOn)
        {
            tilt = Math.Clamp(ComputePid(pos, SampleMs / 1000f), -TiltLimit, TiltLimit);
        }
        else
        {
            integral = 0;
            dFilt = 0;
            prevPos = pos;
            termP = termI = termD = 0;
        }
        servoCmd = baseAngle + tilt;

        bool holding = !guiMode && ms - holdStart < HoldMs;
        if (printMs > 0 && !holding && ms - lastPrintMs >= printMs)
        {
            lastPrintMs = ms;
            output.Add(guiMode
                ? string.Create(Inv, $"${ms},{setpoint:F2},{pos:F2},{tilt:F2},{termP:F2},{termI:F2},{termD:F2}")
                : string.Create(Inv, $"target:{setpoint:F2},pos:{pos:F2},tilt:{tilt:F2}"));
        }
        return output;
    }

    void Physics(double dt)
    {
        double step = ServoRate * dt;
        servo += Math.Clamp(servoCmd - servo, -step, step);
        double beamRad = (servo - TrueLevel) * Linkage * Math.PI / 180;
        double a = BallAccel * Math.Sin(beamRad) - Rolling * v;
        v += a * dt;
        x += v * dt;
        if (x < NearEnd) { x = NearEnd; v = -0.3 * v; }   // 撞到軌道兩端
        if (x > FarEnd) { x = FarEnd; v = -0.3 * v; }
    }

    float ReadSensor()
    {
        double r = rng.NextDouble();
        if (r < 0.01) return -1;                                    // 逾時
        if (r < 0.03) return (float)(2 + rng.NextDouble() * 28);   // 亂跳
        double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();    // 常態分布雜訊（Box-Muller）
        double noise = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        return (float)(x + 0.12 * noise);
    }

    float FilterDistance(float raw)
    {
        if (raw < MinCm || raw > MaxCm)
        {
            raw = lastValid;
        }
        else if (Math.Abs(raw - lastValid) > JumpCm && jumpCount < JumpMax)
        {
            raw = lastValid;
            jumpCount++;
        }
        else
        {
            jumpCount = 0;
        }
        lastValid = raw;

        hist[0] = hist[1];
        hist[1] = hist[2];
        hist[2] = raw;
        float m = Math.Max(Math.Min(hist[0], hist[1]), Math.Min(Math.Max(hist[0], hist[1]), hist[2]));

        avgSum -= avgBuf[avgIdx];
        avgBuf[avgIdx] = m;
        avgSum += m;
        avgIdx = (avgIdx + 1) % NAvg;
        return avgSum / NAvg;
    }

    float ComputePid(float pos, float dt)
    {
        float error = setpoint - pos;
        termP = kp * error;

        if (Math.Abs(error) < IBand) integral += error * dt;
        if (ki > 0) integral = Math.Clamp(integral, -ILimit / ki, ILimit / ki);
        termI = ki * integral;

        float vel = (pos - prevPos) / dt;
        prevPos = pos;
        dFilt += DAlpha * (vel - dFilt);
        termD = -kd * dFilt;

        return termP + termI + termD;
    }

    void RunCommand(string s, List<string> output)
    {
        if (s.Length == 0) return;
        float.TryParse(s.AsSpan(1), NumberStyles.Float, Inv, out float v);   // 跟 atof() 一樣，看不懂就是 0
        switch (s[0])
        {
            case 'p': kp = v; break;
            case 'i': ki = v; integral = 0; break;
            case 'd': kd = v; break;
            case 's': setpoint = v; break;
            case 'b': baseAngle = v; break;
            case 't': printMs = v < 0 ? 0 : (long)v; break;
            case 'r': integral = 0; break;
            case 'g': guiMode = v != 0; break;
            case 'e': controlOn = v != 0; integral = 0; break;
            case 'w': output.Add("# saved to EEPROM (simulator: not persisted)"); break;
            case '?': break;
            default:
                output.Add("# 指令: p i d s b t r g e w ?");
                holdStart = ms;
                return;
        }
        PrintParams(output);
    }

    void PrintParams(List<string> output)
    {
        output.Add(string.Create(Inv,
            $"# Kp={kp:F3} Ki={ki:F3} Kd={kd:F3} set={setpoint:F2} base={baseAngle:F2} print={printMs} en={(controlOn ? 1 : 0)} gui={(guiMode ? 1 : 0)}"));
        holdStart = ms;
    }
}
