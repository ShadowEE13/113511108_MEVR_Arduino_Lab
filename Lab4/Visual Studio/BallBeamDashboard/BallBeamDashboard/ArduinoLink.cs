using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;

namespace BallBeamDashboard;

/// <summary>和 Arduino 溝通的管道：USB 序列埠，或沒有硬體時用的模擬器</summary>
public interface IArduinoLink : IDisposable
{
    /// <summary>收到一行文字（在背景執行緒觸發，不能直接碰 UI）</summary>
    event Action<string>? LineReceived;

    /// <summary>連線出錯、中斷（在背景執行緒觸發）</summary>
    event Action<string>? Faulted;

    /// <summary>開啟連線。可能會等一下（驅動程式開 port），請在背景執行緒呼叫</summary>
    void Open();

    /// <summary>送一行指令（會自動加 \n）。只是排進佇列，不會卡住 UI</summary>
    void Send(string command);
}

/// <summary>
/// 序列埠連線。讀、寫各用一條背景執行緒：
///   讀：ReadLine() 一次一行 → LineReceived
///   寫：從佇列拿指令送出；USB 線被拔掉時 Write 可能卡住，所以不在 UI 執行緒寫
/// </summary>
public sealed class SerialLink : IArduinoLink
{
    public event Action<string>? LineReceived;
    public event Action<string>? Faulted;

    readonly SerialPort port;
    readonly BlockingCollection<string> outbox = new();
    Thread? reader, writer;
    volatile bool running;

    public SerialLink(string portName, int baudRate)
    {
        port = new SerialPort(portName, baudRate)
        {
            NewLine = "\n",                 // Arduino println() 結尾是 \r\n，ReadLine 讀到 \n 為止，\r 另外去掉
            Encoding = Encoding.UTF8,       // Arduino 印的中文是 UTF-8
            ReadTimeout = 500,
            WriteTimeout = 2000,
        };
    }

    public void Open()
    {
        port.Open();
        port.DiscardInBuffer();
        running = true;
        reader = new Thread(ReadLoop) { IsBackground = true, Name = "serial-read" };
        writer = new Thread(WriteLoop) { IsBackground = true, Name = "serial-write" };
        reader.Start();
        writer.Start();
    }

    public void Send(string command)
    {
        try { outbox.Add(command); }
        catch (InvalidOperationException) { }   // 已經在關閉（CompleteAdding 之後）
    }

    void ReadLoop()
    {
        while (running)
        {
            try
            {
                string line = port.ReadLine().TrimEnd('\r');
                LineReceived?.Invoke(line);
            }
            catch (TimeoutException) { }        // 0.5 秒沒資料，繼續等
            catch (Exception ex)
            {
                if (running) { running = false; Faulted?.Invoke(ex.Message); }
                return;
            }
        }
    }

    void WriteLoop()
    {
        try
        {
            foreach (string cmd in outbox.GetConsumingEnumerable())
                port.Write(cmd + "\n");
        }
        catch (Exception ex)
        {
            if (running) { running = false; Faulted?.Invoke("傳送失敗：" + ex.Message); }
        }
    }

    public void Dispose()
    {
        outbox.CompleteAdding();
        writer?.Join(1500);                     // 讓最後排進去的指令（例如 g0）送出去
        running = false;
        try { port.Close(); } catch { }
        reader?.Join(1000);
        port.Dispose();
    }
}

/// <summary>模擬器連線：用 BallBeamSim 假裝成一台 Arduino，每 50 ms 前進一步</summary>
public sealed class SimulatedLink : IArduinoLink
{
    public event Action<string>? LineReceived;
    public event Action<string>? Faulted { add { } remove { } }   // 模擬器不會斷線

    readonly BallBeamSim sim = new();
    Thread? thread;
    volatile bool running;

    public void Open()
    {
        running = true;
        thread = new Thread(Run) { IsBackground = true, Name = "simulator" };
        thread.Start();
    }

    public void Send(string command) => sim.Enqueue(command);

    void Run()
    {
        Emit(sim.Boot());
        var clock = Stopwatch.StartNew();
        long ticks = 0;
        while (running)
        {
            long due = clock.ElapsedMilliseconds / BallBeamSim.SampleMs;   // 照實際時間追上進度
            while (ticks < due && running)
            {
                ticks++;
                Emit(sim.Tick());
            }
            Thread.Sleep(5);
        }
    }

    void Emit(List<string> lines)
    {
        foreach (string line in lines) LineReceived?.Invoke(line);
    }

    public void Dispose()
    {
        running = false;
        thread?.Join(500);
    }
}
