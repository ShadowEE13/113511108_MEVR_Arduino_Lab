namespace Lab3
{

    using System.IO.Ports;

    public partial class Form1 : Form
    {
        private readonly SerialPort port = new SerialPort();

        public Form1()
        {
            InitializeComponent();

            // 註冊事件（callback）
            Load += Form1_Load;
            comboPorts.DropDown += ComboPorts_DropDown;   // 展開選單時重新抓 port
            btnConnect.Click += BtnConnect_Click;
            btnOn.Click += BtnOn_Click;
            btnOff.Click += BtnOff_Click;
            port.DataReceived += Port_DataReceived;
        }

        // ① 啟動：列出 COM port
        private void Form1_Load(object? sender, EventArgs e)
        {
            RefreshPorts();
            btnOn.Enabled = false;
            btnOff.Enabled = false;
            lblStatus.Text = "選擇 COM port 後按連線（藍牙請選「傳出」的 port）";
        }

        // 展開下拉選單時更新清單：先配對藍牙、後開程式都抓得到
        private void ComboPorts_DropDown(object? sender, EventArgs e)
        {
            if (!port.IsOpen) RefreshPorts();
        }

        private void RefreshPorts()
        {
            string current = comboPorts.Text;
            comboPorts.Items.Clear();
            comboPorts.Items.AddRange(SerialPort.GetPortNames());
            if (comboPorts.Items.Contains(current)) comboPorts.Text = current;
            else if (comboPorts.Items.Count > 0) comboPorts.SelectedIndex = 0;
        }

        // ② 連線：第一次按 → 開 port + PING；已開啟再按 → 重送 PING
        private void BtnConnect_Click(object? sender, EventArgs e)
        {
            if (port.IsOpen)
            {
                port.WriteLine("PING");
                lblStatus.Text = "重新詢問 Arduino...";
                return;
            }

            if (string.IsNullOrEmpty(comboPorts.Text))
            {
                lblStatus.Text = "請先選擇 COM port";
                return;
            }

            try
            {
                lblStatus.Text = "連線中（藍牙可能要等幾秒）...";
                lblStatus.Refresh();                  // 立即更新畫面，Open 期間視窗會暫時卡住

                port.PortName = comboPorts.Text;
                port.BaudRate = 9600;              // 等於 Arduino 的 bt.begin() 和 AT+UART 設定
                port.NewLine = "\n";              // framing
                port.ReadTimeout = 1000;
                port.Open();

                comboPorts.Enabled = false;           // 連線中不能換 port
                lblStatus.Text = "已開啟，等待 Arduino READY（沒反應可再按一次連線）";
                port.WriteLine("PING");               // 藍牙沒有 DTR，不會重置 UNO → 必須主動詢問
            }
            catch (Exception ex)
            {
                lblStatus.Text = "連線失敗：" + ex.Message;
            }
        }

        // ③ 送指令
        private void BtnOn_Click(object? sender, EventArgs e) => Send("ON");
        private void BtnOff_Click(object? sender, EventArgs e) => Send("OFF");

        private void Send(string cmd)
        {
            try
            {
                if (port.IsOpen) port.WriteLine(cmd);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "傳送失敗：" + ex.Message;   // 藍牙斷線時會到這裡
            }
        }

        // ④ 收到回傳（背景執行緒）
        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string msg;
            try { msg = port.ReadLine().Trim(); }
            catch { return; }

            BeginInvoke(new Action(() =>
            {
                if (msg == "READY")
                {
                    btnOn.Enabled = true;
                    btnOff.Enabled = true;
                    btnConnect.Enabled = false;       // 握手成功才停用
                }
                lblStatus.Text = "Arduino 回應：" + msg;
            }));
        }

        // ⑤ 關視窗時釋放 port
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (port.IsOpen) port.Close();
            base.OnFormClosing(e);
        }
    }
}