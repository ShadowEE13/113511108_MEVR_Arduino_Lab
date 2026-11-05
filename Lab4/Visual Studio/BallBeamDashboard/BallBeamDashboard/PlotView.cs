using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace BallBeamDashboard;

/// <summary>一條曲線。點要依 X 遞增加入（時間序列本來就是）</summary>
public sealed class PlotSeries(string name, Color color, float width = 1.6f, DashStyle dash = DashStyle.Solid)
{
    public string Name { get; set; } = name;
    public Color Color { get; } = color;
    public float Width { get; } = width;
    public DashStyle Dash { get; } = dash;
    public bool Visible { get; set; } = true;
    public List<PointF> Points { get; } = new();

    /// <summary>刪掉 X 小於 minX 的點（舊資料），避免越存越多</summary>
    public void TrimBefore(double minX)
    {
        int i = PlotView.LowerBound(Points, minX);
        if (i > 0) Points.RemoveRange(0, i);
    }
}

/// <summary>
/// 簡單的折線圖控制項（GDI+ 自己畫，不用額外套件）：
///   - XWindow > 0 時只顯示最新的 XWindow 秒，畫面跟著捲動
///   - Y 軸自動縮放，取整數刻度，並加一點遲滯避免每一格都在跳
///   - 點圖例可以隱藏 / 顯示該曲線
/// </summary>
[DesignerCategory("")]
public sealed class PlotView : Control
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<PlotSeries> Series { get; } = new();

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title { get; set; } = "";

    /// <summary>&gt; 0：只顯示最後 XWindow 秒</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double XWindow { get; set; }

    /// <summary>暫停時固定右邊界</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double? FreezeXMax { get; set; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double? XMinFixed { get; set; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double? XMaxFixed { get; set; }

    /// <summary>Y 軸最少要顯示的範圍，避免雜訊被放大成滿版</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double MinYSpan { get; set; } = 1;

    readonly List<(RectangleF Rect, PlotSeries Series)> legendHits = new();
    double holdYMin = double.NaN, holdYMax = double.NaN;

    public PlotView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        foreach (var (rect, series) in legendHits)
        {
            if (!rect.Contains(e.Location)) continue;
            series.Visible = !series.Visible;
            holdYMin = holdYMax = double.NaN;       // 曲線變了，Y 軸重新算
            Invalidate();
            return;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = legendHits.Any(h => h.Rect.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        float k = DeviceDpi / 96f;
        using var small = new Font(Font.FontFamily, Font.Size * 0.9f);
        using var bold = new Font(Font, FontStyle.Bold);
        float top = Math.Max(26 * k, bold.Height + 10 * k);
        var area = new RectangleF(54 * k, top, Width - 54 * k - 14 * k, Height - top - small.Height - 10 * k);
        if (area.Width < 40 || area.Height < 30) return;

        // ---------- X 範圍 ----------
        double latest = double.NaN, earliest = double.NaN;
        foreach (var s in Series)
        {
            if (s.Points.Count == 0) continue;
            latest = double.IsNaN(latest) ? s.Points[^1].X : Math.Max(latest, s.Points[^1].X);
            earliest = double.IsNaN(earliest) ? s.Points[0].X : Math.Min(earliest, s.Points[0].X);
        }
        double xMax = XMaxFixed ?? FreezeXMax ?? (double.IsNaN(latest) ? Math.Max(XWindow, 1) : latest);
        if (XWindow > 0 && XMaxFixed is null && FreezeXMax is null) xMax = Math.Max(xMax, XWindow);
        double xMin = XMinFixed ?? (XWindow > 0 ? xMax - XWindow : double.IsNaN(earliest) ? 0 : earliest);
        if (xMax - xMin < 1e-6) xMax = xMin + 1;

        // ---------- Y 範圍（只看畫面內、有顯示的曲線） ----------
        double dMin = double.PositiveInfinity, dMax = double.NegativeInfinity;
        foreach (var s in Series)
        {
            if (!s.Visible) continue;
            for (int i = LowerBound(s.Points, xMin); i < s.Points.Count && s.Points[i].X <= xMax; i++)
            {
                dMin = Math.Min(dMin, s.Points[i].Y);
                dMax = Math.Max(dMax, s.Points[i].Y);
            }
        }
        if (double.IsInfinity(dMin)) { dMin = 0; dMax = MinYSpan; }
        double span = dMax - dMin;
        if (span < MinYSpan)
        {
            double mid = (dMin + dMax) / 2;
            dMin = mid - MinYSpan / 2;
            dMax = mid + MinYSpan / 2;
        }
        else
        {
            dMin -= span * 0.08;
            dMax += span * 0.08;
        }

        int maxYTicks = Math.Max(2, (int)(area.Height / (30 * k)));
        double yMin, yMax;
        bool keepHold = !double.IsNaN(holdYMin) && dMin >= holdYMin && dMax <= holdYMax &&
                        dMax - dMin > 0.45 * (holdYMax - holdYMin);
        if (keepHold)
        {
            yMin = holdYMin;
            yMax = holdYMax;
        }
        else
        {
            double st = NiceStep(dMax - dMin, maxYTicks);
            yMin = Math.Floor(dMin / st) * st;
            yMax = Math.Ceiling(dMax / st) * st;
            holdYMin = yMin;
            holdYMax = yMax;
        }
        double yStep = NiceStep(yMax - yMin, maxYTicks);
        double xStep = NiceStep(xMax - xMin, Math.Max(2, (int)(area.Width / (70 * k))));

        float X(double v) => area.Left + (float)((v - xMin) / (xMax - xMin) * area.Width);
        float Y(double v) => area.Bottom - (float)((v - yMin) / (yMax - yMin) * area.Height);

        // ---------- 格線與刻度 ----------
        using var gridPen = new Pen(Color.FromArgb(234, 234, 234));
        using var zeroPen = new Pen(Color.FromArgb(190, 190, 190), 1.2f * k);
        using var framePen = new Pen(Color.FromArgb(175, 175, 175));
        using var textBrush = new SolidBrush(Color.FromArgb(95, 95, 95));
        using var farCenter = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        using var centerTop = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

        for (double y = Math.Ceiling(yMin / yStep) * yStep; y <= yMax + yStep * 1e-6; y += yStep)
        {
            float py = Y(y);
            g.DrawLine(gridPen, area.Left, py, area.Right, py);
            g.DrawString(Fmt(y, yStep), small, textBrush, area.Left - 4 * k, py, farCenter);
        }
        for (double x = Math.Ceiling(xMin / xStep) * xStep; x <= xMax + xStep * 1e-6; x += xStep)
        {
            float px = X(x);
            g.DrawLine(gridPen, px, area.Top, px, area.Bottom);
            g.DrawString(Fmt(x, xStep), small, textBrush, px, area.Bottom + 3 * k, centerTop);
        }
        if (yMin < 0 && yMax > 0) g.DrawLine(zeroPen, area.Left, Y(0), area.Right, Y(0));
        g.DrawRectangle(framePen, area.X, area.Y, area.Width, area.Height);

        // ---------- 曲線 ----------
        g.SetClip(area);
        foreach (var s in Series)
        {
            if (!s.Visible || s.Points.Count < 2) continue;
            int start = Math.Max(0, LowerBound(s.Points, xMin) - 1);
            int end = Math.Min(s.Points.Count - 1, LowerBound(s.Points, xMax));
            int n = end - start + 1;
            if (n < 2) continue;
            var pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                var p = s.Points[start + i];
                pts[i] = new PointF(X(p.X), Y(p.Y));
            }
            using var pen = new Pen(s.Color, s.Width * k) { DashStyle = s.Dash, LineJoin = LineJoin.Round };
            g.DrawLines(pen, pts);
        }
        g.ResetClip();

        // ---------- 標題與圖例（點圖例可切換顯示） ----------
        using var titleBrush = new SolidBrush(Color.FromArgb(45, 45, 45));
        float titleY = (top - bold.Height) / 2;
        g.DrawString(Title, bold, titleBrush, area.Left - 2 * k, titleY);

        legendHits.Clear();
        float lx = area.Right;
        float ly = (top - small.Height) / 2;
        using var hiddenBrush = new SolidBrush(Color.FromArgb(185, 185, 185));
        for (int i = Series.Count - 1; i >= 0; i--)
        {
            var s = Series[i];
            float w = g.MeasureString(s.Name, small).Width;
            float itemW = 22 * k + w + 8 * k;
            lx -= itemW;
            float cy = ly + small.Height / 2f;
            using var pen = new Pen(s.Visible ? s.Color : Color.FromArgb(205, 205, 205), Math.Max(2f, s.Width) * k) { DashStyle = s.Dash };
            g.DrawLine(pen, lx, cy, lx + 18 * k, cy);
            g.DrawString(s.Name, small, s.Visible ? textBrush : hiddenBrush, lx + 21 * k, ly);
            legendHits.Add((new RectangleF(lx - 2 * k, ly - 3 * k, itemW, small.Height + 6 * k), s));
        }
    }

    /// <summary>1、2、5 × 10ⁿ 的刻度間距</summary>
    static double NiceStep(double span, int maxTicks)
    {
        double raw = span / Math.Max(1, maxTicks);
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double n = raw / mag;
        return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * mag;
    }

    static string Fmt(double v, double step)
    {
        int dec = step >= 1 ? 0 : Math.Min(3, (int)Math.Ceiling(-Math.Log10(step) - 1e-9));
        if (Math.Abs(v) < step * 1e-6) v = 0;
        return v.ToString("F" + dec, Inv);
    }

    /// <summary>第一個 X ≥ x 的索引（二分搜尋）</summary>
    internal static int LowerBound(List<PointF> pts, double x)
    {
        int lo = 0, hi = pts.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (pts[mid].X < x) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}
