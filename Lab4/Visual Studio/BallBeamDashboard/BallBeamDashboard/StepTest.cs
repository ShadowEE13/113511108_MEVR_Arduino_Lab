using System.Globalization;

namespace BallBeamDashboard;

/// <summary>階躍響應的分析結果</summary>
/// <param name="OvershootPct">超越量：衝過終點多少（佔步階大小的 %）</param>
/// <param name="RiseTime">上升時間：10% → 90%（秒），沒到 90% 為 NaN</param>
/// <param name="SettlingTime">安定時間：誤差進入 ±Band 後不再離開的時刻（秒），到最後都沒穩定為 NaN</param>
/// <param name="SteadyError">穩態誤差：最後 1 秒的平均 (位置 − 終點)（cm）</param>
/// <param name="Iae">誤差絕對值積分 ∫|e|dt（cm·s），越小越好，適合一個數字比較</param>
/// <param name="Band">安定帶寬（cm）</param>
public sealed record StepResult(
    double OvershootPct, double RiseTime, double SettlingTime, double SteadyError, double Iae, double Band)
{
    public bool Settled => !double.IsNaN(SettlingTime);
}

public static class StepAnalyzer
{
    /// <param name="data">(從改變目標開始算的秒數, 球的位置 cm)，時間要遞增</param>
    public static StepResult? Analyze(IReadOnlyList<(double T, double Pos)> data, double from, double to)
    {
        double delta = to - from;
        if (data.Count < 5 || Math.Abs(delta) < 0.5) return null;

        double band = Math.Max(0.5, 0.05 * Math.Abs(delta));
        double peak = double.NegativeInfinity;
        double t10 = double.NaN, t90 = double.NaN, iae = 0;
        int lastOutside = -1;

        for (int i = 0; i < data.Count; i++)
        {
            var (t, pos) = data[i];
            double y = (pos - from) / delta;              // 正規化：0 = 起點、1 = 終點（往哪個方向都適用）
            peak = Math.Max(peak, y);
            if (double.IsNaN(t10) && y >= 0.1) t10 = t;
            if (double.IsNaN(t90) && y >= 0.9) t90 = t;
            if (Math.Abs(pos - to) > band) lastOutside = i;
            if (i > 0) iae += Math.Abs(pos - to) * (t - data[i - 1].T);
        }

        double settling =
            lastOutside == data.Count - 1 ? double.NaN :   // 最後一筆還在帶外 → 沒穩定
            lastOutside < 0 ? 0 :
            data[lastOutside + 1].T;

        double tEnd = data[^1].T, sum = 0;
        int n = 0;
        foreach (var (t, pos) in data)
            if (t >= tEnd - 1.0) { sum += pos - to; n++; }

        return new StepResult(
            OvershootPct: Math.Max(0, peak - 1) * 100,
            RiseTime: double.IsNaN(t10) || double.IsNaN(t90) ? double.NaN : t90 - t10,
            SettlingTime: settling,
            SteadyError: sum / n,
            Iae: iae,
            Band: band);
    }
}

/// <summary>
/// 自動做一次階躍測試：
///   1. 送 s起點，等球在起點穩定
///   2. 送 s終點，從 Arduino 回報的 target 變成終點那一刻開始記錄
///   3. 記錄滿指定秒數後分析
/// 每收到一筆資料呼叫 OnSample()；State 變成 Done 就可以拿 Result。
/// </summary>
public sealed class StepRunner
{
    public enum Phase { MovingToStart, Recording, Done }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const double SettledCm = 0.6;        // 球離起點 0.6 cm 內才算到位
    const double MaxExtraWait = 6;       // 等太久就不管了，直接開始

    readonly Action<string> send;
    double? phaseStart;

    public double From { get; }
    public double To { get; }
    public double WaitSeconds { get; }
    public double Duration { get; }
    public Phase State { get; private set; } = Phase.MovingToStart;
    public List<(double T, double Pos)> Data { get; } = new();
    public StepResult? Result { get; private set; }
    public string Status { get; private set; }

    public StepRunner(double from, double to, double waitSeconds, double duration, Action<string> send)
    {
        From = from;
        To = to;
        WaitSeconds = waitSeconds;
        Duration = duration;
        this.send = send;
        send("s" + from.ToString("0.##", Inv));
        Status = string.Create(Inv, $"移到起點 {from:0.#} cm…");
    }

    public void OnSample(Sample s)
    {
        switch (State)
        {
            case Phase.MovingToStart:
            {
                if (Math.Abs(s.Target - From) > 0.01) return;      // Arduino 還沒套用起點
                phaseStart ??= s.Time;
                double waited = s.Time - phaseStart.Value;
                bool settled = Math.Abs(s.Pos - From) <= SettledCm;
                Status = string.Create(Inv, $"等球在起點穩定… {waited:0.0} / {WaitSeconds:0.#} s");
                if (waited >= WaitSeconds && (settled || waited >= WaitSeconds + MaxExtraWait))
                {
                    send("s" + To.ToString("0.##", Inv));
                    State = Phase.Recording;
                    phaseStart = null;
                }
                break;
            }
            case Phase.Recording:
            {
                if (phaseStart is null)
                {
                    if (Math.Abs(s.Target - To) > 0.01) return;    // 第一筆 target = 終點的資料就是 t = 0
                    phaseStart = s.Time;
                }
                double t = s.Time - phaseStart.Value;
                Data.Add((t, s.Pos));
                Status = string.Create(Inv, $"記錄中… {t:0.0} / {Duration:0.#} s");
                if (t >= Duration)
                {
                    State = Phase.Done;
                    Result = StepAnalyzer.Analyze(Data, From, To);
                    Status = "完成";
                }
                break;
            }
        }
    }
}
