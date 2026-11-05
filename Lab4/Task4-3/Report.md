# 課題報告：Task 4-3 Ball-on-Beam C# 調參儀表板

- **學生姓名**：馮奕勛
- **學生學號**：113511108
- **完成日期**：2026-11-05

---

### 1. 實驗目標
- 以 Task 4-2 的 Ball-on-Beam PID 位置控制為基礎，加上自己設計的功能
- 透過 USB 序列埠把 Arduino 接到電腦，用 GUI **即時調整** Kp / Ki / Kd、目標位置、水平角
- 用 C# Windows Forms 做一個**即時曲線圖**，比較目標與球的實際位置，並畫出 P / I / D 各項的貢獻
- 加入**自動階躍測試**：一鍵量測超越量、上升時間、安定時間、穩態誤差、IAE，用數據比較不同參數
- 參數可存入 Arduino 的 **EEPROM**，調好後拔掉電腦也能用

### 2. 設備與元件
- Arduino Uno x 1、USB Type-B 傳輸線 x 1
- HC-SR04 超音波感測器 x 1
- SG90 舵機 x 1（建議外接 5V 供電並共地，或並 470µF 電容）
- 紙板軌道、鐵絲、乒乓球
- 個人電腦（Arduino IDE、Visual Studio 2026 / .NET 10）

### 3. 接線（和 Task 4-2 相同）

| 元件 | 腳位 | Arduino |
|---|---|---|
| HC-SR04 | Trig / Echo | D10 / D11 |
| SG90 | 訊號（橘） | D9 |
| 全部 | VCC / GND | 5V / GND |
| 電腦 | USB | Arduino USB 埠（供電 + 序列通訊） |

### 4. 系統架構
```
HC-SR04 ─► 三段濾波（剔除 → 中位數 → 移動平均）─► PID ─► SG90
                                                  │
                         $ms,target,pos,tilt,P,I,D │ 每 50 ms
                                                  ▼
                              Serial (USB, 115200 baud) ◄──► 電腦 COM port
                                                                  │
                 C# 儀表板：SerialPort 背景執行緒讀行 → ConcurrentQueue → UI Timer（30 ms）
                            ├─ 即時曲線（目標 vs 位置、傾角與 P/I/D）
                            ├─ 滑桿改參數 → 每 100 ms 最多送一次 p/i/d/s/b 指令
                            └─ 階躍測試：s起點 → 等穩定 → s終點 → 記錄 → 分析
```

### 5. 通訊協定（文字，一行一筆，`\n` 結尾）

**電腦 → Arduino**（沿用 Task 4-2，新增 g / e / w）

| 指令 | 功能 |
|---|---|
| `p2.5` `i0.8` `d4.5` | 設定 Kp / Ki / Kd |
| `s12` | 目標距離 (cm) |
| `b129` | 水平角 (°) |
| `t50` | 每 50 ms 送一筆資料（`t0` 停止） |
| `r` | 積分歸零 |
| `g1` / `g0` | 開 / 關 GUI 模式 |
| `e1` / `e0` | 啟動 / 停止控制（停止時軌道回水平） |
| `w` | 目前參數存入 EEPROM，開機自動載入 |
| `?` | 回報目前參數 |

**Arduino → 電腦**

| 類型 | 格式 |
|---|---|
| 參數（每個指令後都會回） | `# Kp=2.000 Ki=0.800 Kd=4.500 set=10.50 base=129.00 print=50 en=1 gui=1` |
| GUI 模式資料 | `$123456,10.50,10.32,-1.23,0.36,0.12,-1.70`（ms, 目標, 位置, 傾角, P, I, D） |
| 一般模式資料 | `target:10.50,pos:10.32,tilt:-1.23`（Arduino IDE 的 Serial Plotter 可直接畫） |

設計重點：
- **握手**：電腦開 USB port 時 UNO 可能會被重置，開機的 1~2 秒內送的指令會被 bootloader 吃掉。
  所以 C# 連上後送 `t50` + `g1`，收到 `gui=1` 才算連線成功，沒回應每 2 秒重送；之後只要看到 `gui=0`（Arduino 重開機）就自動再握手。
- **時間戳用 Arduino 的 `millis()`**：USB 驅動會把資料一批一批交給程式，用電腦收到的時間畫圖會一頓一頓的。
- **GUI 模式不暫停**：Task 4-2 下指令後會停印 3 秒方便看 Serial Monitor，但拖滑桿時圖會卡住，所以 GUI 模式關掉這個功能。
- **不卡控制迴圈**：一筆 GUI 資料約 45 bytes，115200 baud 約 4 ms 送完，Arduino 的 64 byte 傳送緩衝區放得下，`Serial.print` 不會等待。
- **參數同步**：每個指令 Arduino 都會回報完整參數，GUI 用回報值更新滑桿，所以畫面上看到的一定是 Arduino 真正在用的值。
- 舊版 Task4-2.ino 也能連：儀表板看得懂 Serial Plotter 格式，只是沒有 P/I/D 和時間戳。

### 6. 操作說明
1. **燒錄程式**：用 USB 線連接 Arduino Uno，開啟 `Task4-3.ino` 並上傳。
2. **關掉序列埠監控視窗**：同一個 COM port 只能給一個程式開，Arduino IDE 的序列埠監控視窗 / 繪圖器要先關掉。
3. **執行 C# 程式**：開 `Lab4/Visual Studio/BallBeamDashboard/BallBeamDashboard.slnx`，按執行。
4. **連線**：選 Arduino 的 COM port、鮑率 115200，按「連線」，狀態變成綠色「已連線」。
5. **調參**：拖滑桿或輸入數字，參數立即生效；圖表即時顯示目標、位置、傾角與 P / I / D。
   點圖例可以隱藏某條曲線，「暫停」可以停住畫面仔細看，「匯出 CSV」存下所有資料做報告。
6. **階躍測試**：設定起點、終點（例如 8 → 14 cm），按「開始階躍測試」。程式會自動移到起點、等穩定、
   跳到終點並記錄，結果顯示在「階躍比較」分頁（最多疊 8 條曲線比較）。
7. **存檔**：調到滿意後按「存入 EEPROM」。下次開機或重新連線（UNO 被重置）都會自動載入。
8. **沒有硬體時**：連接埠選「模擬（無硬體）」，可以先練習操作介面（模擬器的物理參數是估的，調出來的值不等於實體）。
9. **操作影片**：請參閱同目錄下 `DemoVideo/Task4-3.mov`。

### 7. 檔案說明

| 檔案 | 說明 |
|---|---|
| `Lab4/Task4-3/Task4-3.ino` | Arduino 程式（PID + 序列協定 + EEPROM） |
| `Lab4/Visual Studio/BallBeamDashboard/` | C# 儀表板方案 |
| ├ `MainForm.cs` | 主視窗：介面、連線、握手、參數同步、階躍測試流程 |
| ├ `PlotView.cs` | 自己畫的折線圖控制項（GDI+，可捲動、自動縮放、點圖例切換） |
| ├ `ArduinoLink.cs` | 序列埠讀寫執行緒、模擬器連線 |
| ├ `Protocol.cs` | 解析資料行 / 參數行、Arduino 時間軸換算 |
| ├ `StepTest.cs` | 階躍測試流程與分析（超越量、上升、安定、穩態誤差、IAE） |
| └ `BallBeamSim.cs` | 模擬器：照 Task4-3.ino 寫的濾波 / PID + 簡化物理 |
