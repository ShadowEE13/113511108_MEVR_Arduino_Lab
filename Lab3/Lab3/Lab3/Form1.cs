namespace Lab3
{
    using System.IO.Ports;

    public partial class Form1 : Form
    {
        private readonly SerialPort port = new SerialPort();

        public Form1()
        {
            InitializeComponent();

            // 註冊事件（callback），概念同 Arduino 的 attachInterrupt
            Load += Form1_Load;
            btnConnect.Click += BtnConnect_Click;
            btnOn.Click += BtnOn_Click;
            btnOff.Click += BtnOff_Click;
            port.DataReceived += Port_DataReceived;
        }

        // ① 啟動：列出所有 COM port（不寫死）
        private void Form1_Load(object? sender, EventArgs e)
        {
            comboPorts.Items.AddRange(SerialPort.GetPortNames());
            if (comboPorts.Items.Count > 0) comboPorts.SelectedIndex = 0;

            btnOn.Enabled = false;          // 握手成功前不能按
            btnOff.Enabled = false;
            lblStatus.Text = "請選擇 COM port 後按連線";
        }

        // ② 連線：只開一次 port（開 port 會讓 UNO 重置）
        private void BtnConnect_Click(object? sender, EventArgs e)
        {
            if (port.IsOpen) return;
            if (string.IsNullOrEmpty(comboPorts.Text))
            {
                lblStatus.Text = "請先選擇 COM port";
                return;
            }

            try
            {
                port.PortName = comboPorts.Text;
                port.BaudRate = 9600;     // 跟 Arduino 一致
                port.NewLine = "\n";     // framing
                port.ReadTimeout = 1000;
                port.Open();

                btnConnect.Enabled = false;
                lblStatus.Text = "已開啟，等待 Arduino READY...";
                port.WriteLine("PING");      // 主動詢問（藍牙版也適用）
            }
            catch (Exception ex)
            {
                // 最常見：Serial Monitor 還開著 → 拒絕存取
                lblStatus.Text = "連線失敗：" + ex.Message;
            }
        }

        // ③ 按鈕送指令
        private void BtnOn_Click(object? sender, EventArgs e) => Send("ON");
        private void BtnOff_Click(object? sender, EventArgs e) => Send("OFF");

        private void Send(string cmd)
        {
            if (port.IsOpen) port.WriteLine(cmd);
        }

        // ④ 收到 Arduino 回傳（在背景執行緒被呼叫）
        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string msg;
            try { msg = port.ReadLine().Trim(); }   // Trim 去掉 println 的 \r
            catch { return; }

            // UI 元件只能由 UI 執行緒改 → 用 BeginInvoke 排隊過去
            BeginInvoke(new Action(() =>
            {
                if (msg == "READY")
                {
                    btnOn.Enabled = true;
                    btnOff.Enabled = true;
                }
                lblStatus.Text = "Arduino 回應：" + msg;
            }));
        }

        // ⑤ 關視窗時釋放 port（否則下次開不了）
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (port.IsOpen) port.Close();
            base.OnFormClosing(e);
        }
    }
}
