using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO.Ports;
using System.Text;

namespace BallBeamDashboard;

/// <summary>
/// Ball-on-Beam PID 調參儀表板（Task 4-3）
///   上方：連線列、即時數值看板
///   左邊：PID 參數滑桿、控制按鈕、階躍測試、指令視窗
///   右邊：即時曲線（目標 vs 位置、傾角與 P/I/D）、階躍響應比較
/// 介面全部用程式碼建立（沒有 Designer 檔），每個控制項怎麼來的都看得到。
/// </summary>
[System.ComponentModel.DesignerCategory("")]
public sealed class MainForm : Form
{
    public const string SimPort = "模擬（無硬體）";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly Color Good = Color.FromArgb(22, 128, 61);
    static readonly Color Warn = Color.FromArgb(190, 110, 0);
    static readonly Color Bad = Color.FromArgb(200, 40, 40);
    static readonly Color Muted = Color.FromArgb(105, 105, 105);
    static readonly int[] RateOptions = [50, 100, 200];
    static readonly Color[] RunColors =
    [
        Color.FromArgb(31, 119, 180), Color.FromArgb(214, 39, 40), Color.FromArgb(44, 160, 44),
        Color.FromArgb(255, 127, 14), Color.FromArgb(148, 103, 189), Color.FromArgb(23, 190, 207),
        Color.FromArgb(140, 86, 75), Color.FromArgb(227, 119, 194),
    ];
    const int MaxStepCurves = 8;
    const double StaleSeconds = 4;      // 這麼久沒收到任何東西就警告（舊版韌體下指令後會停 3 秒）

    // ================= 連線狀態 =================
    IArduinoLink? link;
    bool isSim;
    int linkGen;                        // 每次連線 +1，丟掉上一次連線殘留的資料
    readonly ConcurrentQueue<(int Gen, string Line, bool Fault)> rxQueue = new();
    bool handshakeDone, legacyFirmware, stale;
    DateTime lastHandshake = DateTime.MinValue, lastRx = DateTime.MinValue;
    DateTime lastFlush = DateTime.MinValue, lastTrim = DateTime.MinValue, rateWindowStart = DateTime.Now;
    int samplesInWindow;
    string lastParamsLine = "";

    // ================= 資料 =================
    readonly TimeBase timeBase = new();
    readonly List<Sample> history = new();
    Sample? latest;

    // ================= PID 參數列 =================
    sealed class ParamRow(char cmd, string key, TrackBar bar, NumericUpDown num, int scale)
    {
        public char Cmd { get; } = cmd;           // 送給 Arduino 的指令字母
        public string Key { get; } = key;         // 參數行裡的名稱（Kp、set…）
        public TrackBar Bar { get; } = bar;
        public NumericUpDown Num { get; } = num;
        public int Scale { get; } = scale;        // TrackBar 只能放整數 → 數值 × Scale
        public bool Pending { get; set; }         // 使用者改了、還沒送出
        public DateTime LastEdit { get; set; } = DateTime.MinValue;
        public double Value => (double)Num.Value;
    }
    readonly List<ParamRow> rows = new();
    bool syncing;                       // 程式在更新控制項 → 不要觸發送指令
    bool controlOn = true;

    // ================= 階躍測試 =================
    StepRunner? stepRun;
    double stepRestoreSetpoint;
    (double Kp, double Ki, double Kd) stepGains;
    int stepCount;
    readonly List<PlotSeries> stepCurves = new();
    readonly HashSet<double> stepTargets = new();

    // ================= 控制項 =================
    readonly ComboBox cboPort = new(), cboBaud = new(), cboRate = new(), cboWindow = new();
    readonly Button btnConnect = new(), btnEnable = new(), btnStep = new();
    readonly Label lblStatus = new(), lblStepStatus = new();
    readonly Label valTarget = new(), valPos = new(), valErr = new(), valTilt = new(), valRate = new();
    readonly TextBox txtLog = new(), txtCmd = new();
    readonly ListView lvSteps = new();
    readonly NumericUpDown numFrom = new(), numTo = new(), numWait = new(), numDur = new();
    readonly CheckBox chkPause = new();
    readonly TabControl tabs = new();
    readonly TabPage tabLive = new(), tabStep = new();
    GroupBox grpPid = null!;
    readonly PlotView plotPos = new(), plotCtl = new(), plotStep = new();
    readonly PlotSeries sTarget = new("目標", Color.FromArgb(120, 120, 120), 2f, DashStyle.Dash);
    readonly PlotSeries sPos = new("球的位置", Color.FromArgb(31, 119, 180), 2.2f);
    readonly PlotSeries sTilt = new("傾角（輸出）", Color.FromArgb(35, 35, 35), 2f);
    readonly PlotSeries sP = new("P", Color.FromArgb(214, 39, 40), 1.4f);
    readonly PlotSeries sI = new("I", Color.FromArgb(44, 160, 44), 1.4f);
    readonly PlotSeries sD = new("D", Color.FromArgb(148, 103, 189), 1.4f);

    readonly System.Windows.Forms.Timer uiTimer = new() { Interval = 30 };

    public MainForm()
    {
        Text = "Ball-on-Beam PID 調參儀表板（Task 4-3）";
        Font = new Font("Microsoft JhengHei UI", 9.5f);
        AutoScaleMode = AutoScaleMode.None;     // 尺寸都用 S() 自己依 DPI 放大
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(S(1360), S(840));
        MinimumSize = new Size(S(1000), S(640));
        BackColor = Color.White;

        BuildUi();
        RefreshPorts();

        uiTimer.Tick += (_, _) => OnUiTick();
        uiTimer.Start();
    }

    int S(int v) => LogicalToDeviceUnits(v);

    // =====================================================================
    //  介面
    // =====================================================================
    void BuildUi()
    {
        // ---------- 上方：連線列 ----------
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, WrapContents = false,
            Padding = new Padding(S(8), S(6), S(8), S(4)),
        };
        cboPort.DropDownStyle = ComboBoxStyle.DropDownList;
        cboPort.Width = S(170);
        cboPort.DropDown += (_, _) => { if (link is null) RefreshPorts(); };   // 展開時重抓：先開程式、後插 USB 也抓得到
        cboBaud.DropDownStyle = ComboBoxStyle.DropDownList;
        cboBaud.Width = S(90);
        cboBaud.Items.AddRange(["9600", "38400", "57600", "115200"]);
        cboBaud.SelectedItem = "115200";
        cboBaud.SelectedIndexChanged += (_, _) =>
        {
            if (cboBaud.Text == "9600" && cboRate.SelectedIndex == 0) cboRate.SelectedIndex = 1;   // 9600 傳不完 20 筆/秒
        };
        StyleButton(btnConnect, "連線", async (_, _) => await ToggleConnectionAsync());
        btnConnect.Name = "btnConnect";
        lblStatus.AutoSize = true;
        lblStatus.Margin = new Padding(S(10), S(8), 0, 0);
        SetStatus("選擇 Arduino 的 COM port 後按「連線」（先關掉 Arduino IDE 的序列埠監控視窗；沒有硬體可選「模擬」）", Muted);
        top.Controls.AddRange([Cap("連接埠"), cboPort, Cap("鮑率"), cboBaud, btnConnect, lblStatus]);

        // ---------- 數值看板 ----------
        var board = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = S(68), ColumnCount = 5, RowCount = 1,
            BackColor = Color.FromArgb(244, 246, 249), Padding = new Padding(S(10), S(4), S(10), S(4)),
        };
        for (int i = 0; i < 5; i++) board.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        board.Controls.Add(Readout("目標", valTarget), 0, 0);
        board.Controls.Add(Readout("球的位置", valPos), 1, 0);
        board.Controls.Add(Readout("誤差（目標 − 位置）", valErr), 2, 0);
        board.Controls.Add(Readout("傾角（偏離水平）", valTilt), 3, 0);
        board.Controls.Add(Readout("資料更新率", valRate), 4, 0);

        // ---------- 左側：控制面板 ----------
        var left = new Panel { Dock = DockStyle.Left, Width = S(410), AutoScroll = true, Padding = new Padding(0, S(4), 0, S(4)) };
        var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grpPid = Group("PID 參數（拖曳 / 輸入後立即送出）", BuildPidTable());
        stack.Controls.Add(grpPid);
        stack.Controls.Add(Group("控制", BuildControlPanel()));
        stack.Controls.Add(Group("階躍測試（比較不同參數）", BuildStepPanel()));
        stack.Controls.Add(Group("指令 / 訊息", BuildConsole()));
        left.Controls.Add(stack);

        // ---------- 右側：圖表 ----------
        tabs.Dock = DockStyle.Fill;
        tabs.Name = "tabs";
        BuildLiveTab();
        BuildStepTab();
        tabs.TabPages.Add(tabLive);
        tabs.TabPages.Add(tabStep);

        // Dock 排列順序 = 加入順序反過來：上方列 → 看板 → 左側 → 中間填滿
        Controls.Add(tabs);
        Controls.Add(left);
        Controls.Add(board);
        Controls.Add(top);
    }

    Control BuildPidTable()
    {
        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 3 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(84)));
        AddParamRow(t, "Kp", 'p', "Kp", 0, 10, 2.0, 0.05m, 2);
        AddParamRow(t, "Ki", 'i', "Ki", 0, 5, 0.8, 0.01m, 3);
        AddParamRow(t, "Kd", 'd', "Kd", 0, 15, 4.5, 0.05m, 2);
        AddParamRow(t, "目標 (cm)", 's', "set", 4, 20, 10.5, 0.1m, 1);
        AddParamRow(t, "水平角 (°)", 'b', "base", 110, 150, 129, 0.1m, 1);
        return t;
    }

    void AddParamRow(TableLayoutPanel t, string label, char cmd, string key,
                     double min, double max, double init, decimal increment, int decimals)
    {
        int scale = (int)Math.Round(1 / (double)increment);
        var num = new NumericUpDown
        {
            Minimum = (decimal)min, Maximum = (decimal)max, Increment = increment, DecimalPlaces = decimals,
            Value = (decimal)init, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Right,
            Margin = new Padding(S(2), S(4), 0, 0),
        };
        var bar = new TrackBar
        {
            Minimum = (int)Math.Round(min * scale), Maximum = (int)Math.Round(max * scale),
            Value = (int)Math.Round(init * scale), TickStyle = TickStyle.None, AutoSize = false,
            Dock = DockStyle.Fill, SmallChange = 1, LargeChange = scale,
        };
        var row = new ParamRow(cmd, key, bar, num, scale);

        bar.Scroll += (_, _) =>
        {
            if (syncing) return;
            syncing = true;
            num.Value = Math.Clamp((decimal)bar.Value / scale, num.Minimum, num.Maximum);
            syncing = false;
            MarkEdited(row);
        };
        num.ValueChanged += (_, _) =>
        {
            if (syncing) return;
            syncing = true;
            bar.Value = Math.Clamp((int)Math.Round((double)num.Value * scale), bar.Minimum, bar.Maximum);
            syncing = false;
            MarkEdited(row);
        };

        int r = rows.Count;
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, S(34)));
        t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, S(4), 0) }, 0, r);
        t.Controls.Add(bar, 1, r);
        t.Controls.Add(num, 2, r);
        rows.Add(row);
    }

    Control BuildControlPanel()
    {
        // 兩列各自不換行：FlowLayoutPanel 自動換行時 GroupBox 不會跟著長高
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        buttons.Controls.Add(StyleButton(new Button(), "讀取參數", (_, _) => Send("?")));
        buttons.Controls.Add(StyleButton(new Button(), "積分歸零", (_, _) => Send("r")));
        buttons.Controls.Add(StyleButton(new Button(), "存入 EEPROM", (_, _) => { FlushPending(); Send("w"); }));
        buttons.Controls.Add(StyleButton(btnEnable, "停止控制", (_, _) => Send(controlOn ? "e0" : "e1")));

        var rate = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        cboRate.DropDownStyle = ComboBoxStyle.DropDownList;
        cboRate.Width = S(84);
        cboRate.Margin = new Padding(S(3), S(6), S(3), S(3));
        foreach (int ms in RateOptions) cboRate.Items.Add($"{ms} ms");
        cboRate.SelectedIndex = 0;
        cboRate.SelectedIndexChanged += (_, _) => { if (!syncing) Send("t" + RateMs()); };
        rate.Controls.AddRange([Cap("資料更新間隔"), cboRate]);

        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 1 };
        t.Controls.Add(buttons, 0, 0);
        t.Controls.Add(rate, 0, 1);
        return t;
    }

    Control BuildStepPanel()
    {
        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 4 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        SetupNum(numFrom, 4, 20, 8, 0.5m, 1);
        SetupNum(numTo, 4, 20, 14, 0.5m, 1);
        SetupNum(numWait, 1, 20, 3, 0.5m, 1);
        SetupNum(numDur, 2, 30, 10, 1m, 0);
        t.Controls.Add(Cap("起點 (cm)"), 0, 0);
        t.Controls.Add(numFrom, 1, 0);
        t.Controls.Add(Cap("終點 (cm)"), 2, 0);
        t.Controls.Add(numTo, 3, 0);
        t.Controls.Add(Cap("先等 (s)"), 0, 1);
        t.Controls.Add(numWait, 1, 1);
        t.Controls.Add(Cap("記錄 (s)"), 2, 1);
        t.Controls.Add(numDur, 3, 1);
        StyleButton(btnStep, "開始階躍測試", (_, _) => StartOrAbortStep());
        btnStep.Name = "btnStep";
        t.Controls.Add(btnStep, 0, 2);
        t.SetColumnSpan(btnStep, 4);
        lblStepStatus.AutoSize = true;
        lblStepStatus.ForeColor = Muted;
        lblStepStatus.Margin = new Padding(S(4), S(4), 0, S(2));
        lblStepStatus.Text = "結果會出現在「階躍比較」分頁";
        t.Controls.Add(lblStepStatus, 0, 3);
        t.SetColumnSpan(lblStepStatus, 4);
        return t;
    }

    Control BuildConsole()
    {
        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 2 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, S(150)));
        txtCmd.Dock = DockStyle.Fill;
        txtCmd.Margin = new Padding(S(3), S(6), S(3), S(3));
        txtCmd.PlaceholderText = "例如 p2.5、s12、?";
        txtCmd.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            SendTyped();
        };
        txtLog.Multiline = true;
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.Dock = DockStyle.Fill;
        txtLog.BackColor = Color.White;
        txtLog.Font = new Font("Consolas", 9f);
        t.Controls.Add(txtCmd, 0, 0);
        t.Controls.Add(StyleButton(new Button(), "送出", (_, _) => SendTyped()), 1, 0);
        t.Controls.Add(txtLog, 0, 1);
        t.SetColumnSpan(txtLog, 2);
        return t;
    }

    void BuildLiveTab()
    {
        tabLive.Text = "即時曲線";
        tabLive.BackColor = Color.White;
        tabLive.Padding = new Padding(S(4));

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        cboWindow.DropDownStyle = ComboBoxStyle.DropDownList;
        cboWindow.Width = S(70);
        cboWindow.Margin = new Padding(S(3), S(6), S(3), S(3));
        cboWindow.Items.AddRange(["5 s", "10 s", "20 s", "30 s"]);
        cboWindow.SelectedIndex = 1;
        cboWindow.SelectedIndexChanged += (_, _) =>
        {
            double w = double.Parse(cboWindow.Text.Split(' ')[0], Inv);
            plotPos.XWindow = plotCtl.XWindow = w;
            plotPos.Invalidate();
            plotCtl.Invalidate();
        };
        chkPause.Text = "暫停";
        chkPause.AutoSize = true;
        chkPause.Margin = new Padding(S(10), S(8), S(3), 0);
        chkPause.CheckedChanged += (_, _) =>
        {
            double? freeze = chkPause.Checked ? latest?.Time : null;
            plotPos.FreezeXMax = plotCtl.FreezeXMax = freeze;
            plotPos.Invalidate();
            plotCtl.Invalidate();
        };
        var hint = new Label
        {
            Text = "點圖例可隱藏 / 顯示曲線", AutoSize = true, ForeColor = Muted,
            Margin = new Padding(S(12), S(9), 0, 0),
        };
        bar.Controls.AddRange([Cap("時間視窗"), cboWindow, chkPause,
            StyleButton(new Button(), "清除", (_, _) => ClearLive()),
            StyleButton(new Button(), "匯出 CSV", (_, _) => ExportCsv()), hint]);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        plotPos.Title = "球的位置 (cm)";
        plotPos.Series.AddRange([sTarget, sPos]);
        plotPos.MinYSpan = 4;
        plotPos.XWindow = 10;
        plotPos.Dock = DockStyle.Fill;
        plotCtl.Title = "舵機傾角與 P / I / D 各項 (°)";
        plotCtl.Series.AddRange([sTilt, sP, sI, sD]);
        plotCtl.MinYSpan = 4;
        plotCtl.XWindow = 10;
        plotCtl.Dock = DockStyle.Fill;
        grid.Controls.Add(plotPos, 0, 0);
        grid.Controls.Add(plotCtl, 0, 1);

        tabLive.Controls.Add(grid);
        tabLive.Controls.Add(bar);
    }

    void BuildStepTab()
    {
        tabStep.Text = "階躍比較";
        tabStep.BackColor = Color.White;
        tabStep.Padding = new Padding(S(4));

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        plotStep.Title = "階躍響應（t = 0 時改變目標）";
        plotStep.XMinFixed = 0;
        plotStep.XMaxFixed = 10;
        plotStep.MinYSpan = 4;
        plotStep.Dock = DockStyle.Fill;

        lvSteps.View = View.Details;
        lvSteps.FullRowSelect = true;
        lvSteps.GridLines = true;
        lvSteps.Dock = DockStyle.Fill;
        lvSteps.Name = "lvSteps";
        (string Title, int Width)[] cols =
        [
            ("#", 36), ("Kp", 60), ("Ki", 60), ("Kd", 60), ("起點→終點", 96), ("超越量", 80),
            ("上升時間", 84), ("安定時間", 84), ("穩態誤差", 90), ("IAE (cm·s)", 90),
        ];
        foreach (var (title, width) in cols) lvSteps.Columns.Add(title, S(width));

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        buttons.Controls.Add(StyleButton(new Button(), "清除結果", (_, _) => ClearSteps()));
        buttons.Controls.Add(new Label
        {
            AutoSize = true, ForeColor = Muted, Margin = new Padding(S(10), S(9), 0, 0),
            Text = "上升：10%→90%　安定：誤差進入 ±max(0.5 cm, 5%) 後不再離開　IAE：∫|誤差|dt，越小越好",
        });

        grid.Controls.Add(plotStep, 0, 0);
        grid.Controls.Add(lvSteps, 0, 1);
        grid.Controls.Add(buttons, 0, 2);
        tabStep.Controls.Add(grid);
    }

    // ---------- 小工具 ----------
    Label Cap(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(S(6), S(9), S(2), 0) };

    Button StyleButton(Button b, string text, EventHandler onClick)
    {
        b.Text = text;
        b.AutoSize = true;
        b.MinimumSize = new Size(S(64), S(30));
        b.Margin = new Padding(S(3));
        b.Click += onClick;
        return b;
    }

    GroupBox Group(string title, Control content)
    {
        var gb = new GroupBox
        {
            Text = title, Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(S(8), S(6), S(8), S(8)), Margin = new Padding(S(8), S(4), S(8), S(6)),
        };
        content.Dock = DockStyle.Top;
        gb.Controls.Add(content);
        return gb;
    }

    Control Readout(string caption, Label value)
    {
        var p = new Panel { Dock = DockStyle.Fill, Margin = new Padding(S(4), 0, S(4), 0) };
        var cap = new Label { Text = caption, Dock = DockStyle.Top, Height = S(20), ForeColor = Muted };
        value.Dock = DockStyle.Fill;
        value.Font = new Font("Segoe UI", 17f, FontStyle.Bold);
        value.TextAlign = ContentAlignment.MiddleLeft;
        value.Text = "—";
        p.Controls.Add(value);
        p.Controls.Add(cap);
        return p;
    }

    void SetupNum(NumericUpDown n, decimal min, decimal max, decimal value, decimal inc, int dec)
    {
        n.Minimum = min;
        n.Maximum = max;
        n.Value = value;
        n.Increment = inc;
        n.DecimalPlaces = dec;
        n.Dock = DockStyle.Fill;
        n.TextAlign = HorizontalAlignment.Right;
        n.Margin = new Padding(S(3), S(5), S(6), S(3));
    }

    // =====================================================================
    //  連線
    // =====================================================================
    void RefreshPorts()
    {
        string current = cboPort.SelectedItem as string ?? "";
        cboPort.Items.Clear();
        foreach (string p in SerialPort.GetPortNames().Distinct().OrderBy(n => n.Length).ThenBy(n => n))
            cboPort.Items.Add(p);
        cboPort.Items.Add(SimPort);
        int idx = cboPort.Items.IndexOf(current);
        cboPort.SelectedIndex = idx >= 0 ? idx : 0;
    }

    async Task ToggleConnectionAsync()
    {
        if (link is not null)
        {
            Disconnect("已中斷連線", Muted);
            return;
        }
        if (cboPort.SelectedItem is not string port)
        {
            SetStatus("請先選擇連接埠", Bad);
            return;
        }
        await ConnectAsync(port, int.Parse(cboBaud.Text, Inv));
    }

    /// <summary>連到指定的 COM port（或 SimPort = 模擬器）</summary>
    public async Task ConnectAsync(string portName, int baud)
    {
        if (link is not null) return;
        int gen = ++linkGen;
        isSim = portName == SimPort;
        IArduinoLink newLink = isSim ? new SimulatedLink() : new SerialLink(portName, baud);
        newLink.LineReceived += line => rxQueue.Enqueue((gen, line, false));
        newLink.Faulted += msg => rxQueue.Enqueue((gen, msg, true));

        btnConnect.Enabled = cboPort.Enabled = cboBaud.Enabled = false;
        SetStatus(isSim ? "啟動模擬器…" : "連線中…", Warn);
        try
        {
            await Task.Run(newLink.Open);
        }
        catch (Exception ex)
        {
            newLink.Dispose();
            btnConnect.Enabled = cboPort.Enabled = cboBaud.Enabled = true;
            SetStatus(ex is UnauthorizedAccessException
                ? $"{portName} 被其他程式佔用：先關掉 Arduino IDE 的序列埠監控視窗 / 繪圖器"
                : "連線失敗：" + ex.Message, Bad);
            return;
        }

        link = newLink;
        handshakeDone = legacyFirmware = stale = false;
        lastRx = DateTime.Now;
        lastParamsLine = "";
        ClearLive();
        btnConnect.Text = "中斷";
        btnConnect.Enabled = true;
        SetStatus("已開啟連接埠，等待 Arduino 回應…（UNO 可能正在重開機，約 2 秒）", Warn);
        Log(isSim ? "== 連線：模擬器 ==" : $"== 連線：{portName} @ {baud} ==");
        SendHandshake();
    }

    void Disconnect(string message, Color color)
    {
        var old = link;
        if (old is null) return;
        link = null;
        linkGen++;
        if (stepRun is not null) AbortStep("連線中斷，測試中止", restore: false);
        old.Send("g0");                 // 讓 Arduino 回到一般模式（Serial Monitor 看得懂的格式）
        Task.Run(old.Dispose);          // 在背景關 port，不要卡住畫面
        handshakeDone = false;
        btnConnect.Text = "連線";
        btnConnect.Enabled = cboPort.Enabled = cboBaud.Enabled = true;
        SetStatus(message, color);
        Log("== 中斷連線 ==");
    }

    /// <summary>
    /// 握手：設定更新率 + 開 GUI 模式。Arduino 回傳 gui=1 的參數行就算成功。
    /// 開 USB port 時 UNO 可能被重置，開機的 1~2 秒內送的指令會被 bootloader 吃掉，
    /// 所以沒回應就每 2 秒重送；之後看到 gui=0（Arduino 重開機）也會自動再握手。
    /// </summary>
    void SendHandshake()
    {
        lastHandshake = DateTime.Now;
        Send("t" + RateMs());
        Send("g1");
    }

    void Send(string command)
    {
        if (link is null || command.Length == 0) return;
        link.Send(command);
    }

    void SendTyped()
    {
        string cmd = txtCmd.Text.Trim();
        if (cmd.Length == 0) return;
        if (link is null) { Log("（尚未連線）"); return; }
        Log("→ " + cmd);
        Send(cmd);
        txtCmd.Clear();
    }

    int RateMs() => RateOptions[Math.Max(0, cboRate.SelectedIndex)];

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        uiTimer.Stop();
        var old = link;
        link = null;
        if (old is not null)
        {
            old.Send("g0");
            old.Dispose();
        }
        base.OnFormClosing(e);
    }

    // =====================================================================
    //  每 30 ms：處理收到的資料、送出改過的參數、更新畫面
    // =====================================================================
    void OnUiTick()
    {
        var now = DateTime.Now;
        bool gotSample = false;
        int budget = 2000;
        while (budget-- > 0 && rxQueue.TryDequeue(out var item))
        {
            if (item.Gen != linkGen || link is null) continue;    // 上一次連線的殘留
            if (item.Fault)
            {
                Disconnect("連線中斷：" + item.Line, Bad);
                break;
            }
            lastRx = now;
            gotSample |= HandleLine(item.Line);
        }

        if (link is not null)
        {
            if ((now - lastFlush).TotalMilliseconds >= 100)
            {
                lastFlush = now;
                FlushPending();
            }
            if (!handshakeDone && (now - lastHandshake).TotalSeconds > 2)
            {
                SendHandshake();
                SetStatus("等待 Arduino 回應…（確認 COM port、鮑率、是否已燒錄 Task4-3.ino）", Warn);
            }
            bool nowStale = (now - lastRx).TotalSeconds > StaleSeconds;
            if (nowStale != stale)
            {
                stale = nowStale;
                if (handshakeDone)
                    SetStatus(stale ? $"超過 {StaleSeconds:0} 秒沒收到資料（Arduino 當機或 USB 接觸不良？）" : LinkedText(), stale ? Warn : LinkedColor());
                if (stale && stepRun is not null) AbortStep("沒有收到資料，測試中止");
            }
        }

        double elapsed = (now - rateWindowStart).TotalSeconds;
        if (elapsed >= 1)
        {
            valRate.Text = link is null ? "—" : $"{samplesInWindow / elapsed:0} Hz";
            samplesInWindow = 0;
            rateWindowStart = now;
        }

        if (stepRun is not null) lblStepStatus.Text = stepRun.Status;

        if (gotSample)
        {
            UpdateReadouts();
            if (!chkPause.Checked && (now - lastTrim).TotalSeconds > 1 && latest is Sample s)
            {
                lastTrim = now;
                foreach (var series in new[] { sTarget, sPos, sTilt, sP, sI, sD }) series.TrimBefore(s.Time - 65);
            }
            plotPos.Invalidate();
            plotCtl.Invalidate();
        }
    }

    /// <summary>處理一行；是資料行就回傳 true</summary>
    bool HandleLine(string line)
    {
        if (Protocol.TryParseData(line, out var raw))
        {
            AddSample(raw);
            return true;
        }
        if (Protocol.TryParseParams(line, out var values))
        {
            ApplyParams(values);
            if (line != lastParamsLine) Log("← " + line.Trim());
            lastParamsLine = line;
            return false;
        }
        if (line.Trim().Length > 0) Log("← " + line.Trim());
        return false;
    }

    void AddSample(RawSample raw)
    {
        double t = timeBase.Next(raw.ArduinoMs);
        var s = new Sample(t, raw.Target, raw.Pos, raw.Tilt, raw.P, raw.I, raw.D, raw.HasTerms);
        latest = s;
        samplesInWindow++;
        history.Add(s);
        if (history.Count > 200_000) history.RemoveRange(0, 20_000);    // 約 2.7 小時（20 Hz）

        float x = (float)t;
        sTarget.Points.Add(new PointF(x, (float)s.Target));
        sPos.Points.Add(new PointF(x, (float)s.Pos));
        sTilt.Points.Add(new PointF(x, (float)s.Tilt));
        if (s.HasTerms)
        {
            sP.Points.Add(new PointF(x, (float)s.P));
            sI.Points.Add(new PointF(x, (float)s.I));
            sD.Points.Add(new PointF(x, (float)s.D));
        }

        if (stepRun is not null)
        {
            stepRun.OnSample(s);
            if (stepRun.State == StepRunner.Phase.Done) FinishStep(stepRun);
        }
    }

    void ApplyParams(Dictionary<string, double> p)
    {
        var now = DateTime.Now;
        syncing = true;
        foreach (var row in rows)
        {
            // 使用者剛改過的不要蓋掉（Arduino 的回報可能是舊的）
            if (p.TryGetValue(row.Key, out double v) && !row.Pending && (now - row.LastEdit).TotalMilliseconds > 800)
                SetRow(row, v);
        }
        if (p.TryGetValue("en", out double en))
        {
            controlOn = en != 0;
            btnEnable.Text = controlOn ? "停止控制" : "啟動控制";
        }
        syncing = false;

        if (p.TryGetValue("gui", out double gui))
        {
            if (gui != 0)
            {
                if (!handshakeDone)
                {
                    handshakeDone = true;
                    SetStatus(LinkedText(), LinkedColor());
                }
            }
            else if ((now - lastHandshake).TotalMilliseconds > 800)
            {
                // gui=0：Arduino 重開機了（例如 USB 開 port 時被重置）→ 再握手一次
                handshakeDone = false;
                SendHandshake();
            }
        }
        else if (!handshakeDone)
        {
            handshakeDone = true;
            legacyFirmware = true;
            SetStatus(LinkedText(), LinkedColor());
        }
    }

    string LinkedText() =>
        legacyFirmware ? "已連線（舊版韌體：沒有 P/I/D 與時間戳，建議燒錄 Task4-3.ino）" :
        isSim ? "已連線：模擬器（只用來測試介面，參數不代表實體）" : "已連線";

    Color LinkedColor() => legacyFirmware ? Warn : Good;

    void SetRow(ParamRow row, double v)
    {
        decimal d = (decimal)Math.Round(v, row.Num.DecimalPlaces);
        if (d > row.Num.Maximum)
        {
            row.Num.Maximum = d;
            row.Bar.Maximum = (int)Math.Ceiling((double)d * row.Scale);
        }
        if (d < row.Num.Minimum)
        {
            row.Num.Minimum = d;
            row.Bar.Minimum = (int)Math.Floor((double)d * row.Scale);
        }
        row.Num.Value = d;
        row.Bar.Value = Math.Clamp((int)Math.Round(v * row.Scale), row.Bar.Minimum, row.Bar.Maximum);
    }

    void MarkEdited(ParamRow row)
    {
        row.Pending = true;
        row.LastEdit = DateTime.Now;
    }

    /// <summary>把改過的參數送出（每 100 ms 最多一次，拖滑桿時不會塞爆 Arduino 的 64 byte 接收緩衝區）</summary>
    void FlushPending()
    {
        foreach (var row in rows)
        {
            if (!row.Pending) continue;
            row.Pending = false;
            Send(string.Create(Inv, $"{row.Cmd}{row.Num.Value:0.###}"));
        }
    }

    void UpdateReadouts()
    {
        if (latest is not Sample s) return;
        valTarget.Text = s.Target.ToString("0.00", Inv) + " cm";
        valPos.Text = s.Pos.ToString("0.00", Inv) + " cm";
        double e = s.Error;
        valErr.Text = e.ToString("+0.00;-0.00;0.00", Inv) + " cm";
        valErr.ForeColor = Math.Abs(e) < 0.5 ? Good : Math.Abs(e) < 2 ? Warn : Bad;
        valTilt.Text = s.Tilt.ToString("+0.0;-0.0;0.0", Inv) + "°";
    }

    void ClearLive()
    {
        history.Clear();
        foreach (var series in new[] { sTarget, sPos, sTilt, sP, sI, sD }) series.Points.Clear();
        latest = null;
        timeBase.Reset();
        chkPause.Checked = false;
        foreach (var l in new[] { valTarget, valPos, valErr, valTilt }) l.Text = "—";
        valErr.ForeColor = ForeColor;
        plotPos.Invalidate();
        plotCtl.Invalidate();
    }

    void ExportCsv()
    {
        if (history.Count == 0)
        {
            MessageBox.Show(this, "目前沒有資料可以匯出。", "匯出 CSV");
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV 檔 (*.csv)|*.csv",
            FileName = $"ballbeam_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var sb = new StringBuilder("time_s,target_cm,pos_cm,error_cm,tilt_deg,P_deg,I_deg,D_deg\n");
        foreach (var s in history)
            sb.Append(string.Create(Inv,
                $"{s.Time:0.000},{s.Target:0.00},{s.Pos:0.00},{s.Error:0.00},{s.Tilt:0.00},{s.P:0.00},{s.I:0.00},{s.D:0.00}\n"));
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(false));
        Log($"已匯出 {history.Count} 筆 → {dlg.FileName}");
    }

    // =====================================================================
    //  階躍測試
    // =====================================================================
    void StartOrAbortStep()
    {
        if (stepRun is not null)
        {
            AbortStep("已手動中止");
            return;
        }
        if (link is null || !handshakeDone)
        {
            lblStepStatus.Text = "請先連線";
            return;
        }
        if (legacyFirmware)
        {
            lblStepStatus.Text = "需要 Task4-3.ino 韌體";
            return;
        }
        double from = (double)numFrom.Value, to = (double)numTo.Value;
        if (Math.Abs(to - from) < 1)
        {
            lblStepStatus.Text = "起點和終點至少要差 1 cm";
            return;
        }

        FlushPending();
        if (!controlOn) Send("e1");
        stepRestoreSetpoint = Row("set").Value;
        stepGains = (Row("Kp").Value, Row("Ki").Value, Row("Kd").Value);
        stepRun = new StepRunner(from, to, (double)numWait.Value, (double)numDur.Value, Send);
        grpPid.Enabled = false;                 // 測試中改參數會讓結果不準
        btnStep.Text = "中止測試";
        lblStepStatus.ForeColor = Warn;
        Log(string.Create(Inv, $"階躍測試開始：{from:0.#} → {to:0.#} cm（Kp={stepGains.Kp:0.##} Ki={stepGains.Ki:0.###} Kd={stepGains.Kd:0.##}）"));
    }

    void AbortStep(string reason, bool restore = true)
    {
        stepRun = null;
        EndStepUi();
        if (restore) Send(string.Create(Inv, $"s{stepRestoreSetpoint:0.##}"));
        lblStepStatus.Text = reason;
        Log("階躍測試：" + reason);
    }

    void EndStepUi()
    {
        grpPid.Enabled = true;
        btnStep.Text = "開始階躍測試";
        lblStepStatus.ForeColor = Muted;
    }

    void FinishStep(StepRunner run)
    {
        stepRun = null;
        EndStepUi();
        Send(string.Create(Inv, $"s{stepRestoreSetpoint:0.##}"));   // 目標改回測試前的值
        var r = run.Result;
        if (r is null)
        {
            lblStepStatus.Text = "資料不足，無法分析";
            return;
        }

        stepCount++;
        var color = RunColors[(stepCount - 1) % RunColors.Length];
        var curve = new PlotSeries(
            string.Create(Inv, $"#{stepCount} Kp={stepGains.Kp:0.##} Ki={stepGains.Ki:0.###} Kd={stepGains.Kd:0.##}"), color, 2f);
        foreach (var (t, pos) in run.Data) curve.Points.Add(new PointF((float)t, (float)pos));
        stepCurves.Add(curve);
        plotStep.Series.Add(curve);
        if (stepCurves.Count > MaxStepCurves)
        {
            plotStep.Series.Remove(stepCurves[0]);
            stepCurves.RemoveAt(0);
        }
        if (stepTargets.Add(run.To))
        {
            var line = new PlotSeries(string.Create(Inv, $"目標 {run.To:0.#}"), Color.FromArgb(150, 150, 150), 1.5f, DashStyle.Dash);
            line.Points.Add(new PointF(0, (float)run.To));
            line.Points.Add(new PointF(600, (float)run.To));
            plotStep.Series.Insert(0, line);
        }
        plotStep.XMaxFixed = Math.Max(plotStep.XMaxFixed ?? 0, run.Duration);
        plotStep.Invalidate();

        string Sec(double v) => double.IsNaN(v) ? "—" : v.ToString("0.00", Inv) + " s";
        var item = new ListViewItem(stepCount.ToString(Inv)) { ForeColor = color };
        item.SubItems.AddRange(
        [
            stepGains.Kp.ToString("0.##", Inv),
            stepGains.Ki.ToString("0.###", Inv),
            stepGains.Kd.ToString("0.##", Inv),
            string.Create(Inv, $"{run.From:0.#} → {run.To:0.#}"),
            r.OvershootPct.ToString("0.0", Inv) + " %",
            Sec(r.RiseTime),
            r.Settled ? Sec(r.SettlingTime) : "未穩定",
            r.SteadyError.ToString("+0.00;-0.00;0.00", Inv) + " cm",
            r.Iae.ToString("0.00", Inv),
        ]);
        lvSteps.Items.Add(item);
        item.EnsureVisible();
        tabs.SelectedTab = tabStep;

        string summary = string.Create(Inv,
            $"#{stepCount}：超越量 {r.OvershootPct:0.0}%，安定 {(r.Settled ? Sec(r.SettlingTime) : "未穩定")}，IAE {r.Iae:0.00}");
        lblStepStatus.Text = summary;
        Log("階躍測試完成 " + summary);
    }

    void ClearSteps()
    {
        lvSteps.Items.Clear();
        plotStep.Series.Clear();
        stepCurves.Clear();
        stepTargets.Clear();
        stepCount = 0;
        plotStep.XMaxFixed = (double)numDur.Value;
        plotStep.Invalidate();
    }

    ParamRow Row(string key) => rows.First(r => r.Key == key);

    // =====================================================================
    void SetStatus(string text, Color color)
    {
        lblStatus.Text = text;
        lblStatus.ForeColor = color;
    }

    void Log(string text)
    {
        if (txtLog.TextLength > 40_000) txtLog.Text = txtLog.Text[^20_000..];
        txtLog.AppendText($"{DateTime.Now:HH:mm:ss} {text}{Environment.NewLine}");
    }
}
