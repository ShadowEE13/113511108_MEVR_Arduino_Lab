using System.Diagnostics;
using System.Globalization;

namespace BallBeamDashboard;

/// <summary>Arduino 送來的一筆資料（還沒換算成圖上的時間）</summary>
public readonly record struct RawSample(
    long? ArduinoMs, double Target, double Pos, double Tilt, double P, double I, double D, bool HasTerms);

/// <summary>畫圖 / 存檔用的一筆資料，Time 從連線開始算（秒）</summary>
public readonly record struct Sample(
    double Time, double Target, double Pos, double Tilt, double P, double I, double D, bool HasTerms)
{
    public double Error => Target - Pos;
}

/// <summary>
/// 解析 Task4-3.ino 的輸出：
///   GUI 資料行  $ms,target,pos,tilt,P,I,D
///   舊版資料行  target:10.50,pos:10.32,tilt:-1.23   （Task 4-2 的 Serial Plotter 格式也看得懂）
///   參數行      # Kp=2.000 Ki=0.800 Kd=4.500 set=10.50 base=129.00 print=50 en=1 gui=1
/// </summary>
public static class Protocol
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static bool TryParseData(string line, out RawSample sample)
    {
        sample = default;
        line = line.Trim();

        if (line.StartsWith('$'))
        {
            string[] f = line[1..].Split(',');
            if (f.Length < 7) return false;
            if (!long.TryParse(f[0], NumberStyles.Integer, Inv, out long ms)) return false;
            var v = new double[6];
            for (int i = 0; i < 6; i++)
                if (!double.TryParse(f[i + 1], NumberStyles.Float, Inv, out v[i])) return false;
            sample = new RawSample(ms, v[0], v[1], v[2], v[3], v[4], v[5], true);
            return true;
        }

        if (line.Contains("target:") && line.Contains("pos:"))
        {
            double target = double.NaN, pos = double.NaN, tilt = 0;
            foreach (string part in line.Split(','))
            {
                int c = part.IndexOf(':');
                if (c <= 0 || !double.TryParse(part[(c + 1)..], NumberStyles.Float, Inv, out double x)) continue;
                switch (part[..c].Trim())
                {
                    case "target": target = x; break;
                    case "pos": pos = x; break;
                    case "tilt": tilt = x; break;
                }
            }
            if (double.IsNaN(target) || double.IsNaN(pos)) return false;
            sample = new RawSample(null, target, pos, tilt, 0, 0, 0, false);
            return true;
        }
        return false;
    }

    /// <summary>參數行 → { "Kp": 2.0, "Ki": 0.8, ... }；不是參數行就回傳 false</summary>
    public static bool TryParseParams(string line, out Dictionary<string, double> values)
    {
        values = new Dictionary<string, double>();
        line = line.Trim();
        if (!line.StartsWith('#')) return false;
        foreach (string token in line[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = token.IndexOf('=');
            if (eq > 0 && double.TryParse(token[(eq + 1)..], NumberStyles.Float, Inv, out double v))
                values[token[..eq]] = v;
        }
        return values.ContainsKey("Kp");
    }
}

/// <summary>
/// 把 Arduino 的 millis() 換成連續的時間軸（秒）。
/// 用 Arduino 的時間而不是電腦收到的時間：USB 驅動會把資料一批一批交給程式，電腦時間不準。
/// </summary>
public sealed class TimeBase
{
    long? lastMs;
    double t;
    readonly Stopwatch clock = Stopwatch.StartNew();
    double lastPc;

    public void Reset()
    {
        lastMs = null;
        t = 0;
        clock.Restart();
        lastPc = 0;
    }

    public double Next(long? arduinoMs)
    {
        if (arduinoMs is long ms)
        {
            if (lastMs is long prev)
            {
                long d = ms - prev;
                t += d > 0 && d < 5000 ? d / 1000.0 : 0.05;   // Arduino 重開機（millis 歸零）→ 接著畫下去
            }
            lastMs = ms;
            return t;
        }

        // 舊版格式沒有時間戳 → 只好用電腦時間
        double now = clock.Elapsed.TotalSeconds;
        t += now - lastPc;
        lastPc = now;
        return t;
    }
}
