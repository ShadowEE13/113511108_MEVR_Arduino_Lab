**`Lab3/Task3-2/report.md`（完整示範報告）**

```markdown
# 課題報告：Task 3-2 LED Control with Serial Communication

- **學生姓名**：馮奕勛
- **學生學號**：113511108
- **完成日期**：2026-10-01

---

### 1. 實驗目標
- 學習使用 Visual Studio 建置操作環境(Button, Label, ComboBox)
- 使用 Visual Studio（C# Windows Forms）建立 GUI，透過 UART 序列通訊控制 Arduino 上的 LED
- 設計以 \n 結尾的字串通訊協定，並實作雙向回傳（ACK），讓 GUI 顯示 LED 的實際狀態

### 2. 設備與元件
- Arduino Uno 開發板 x 1
- USB Type-B 傳輸線 x 1
- 個人電腦（已安裝 Arduino IDE, Visual Studio）x 1
- LED x 1
- 電阻 220 ohm x 1

### 3. 系統架構
- 按鈕事件 → WriteLine → UART → readStringUntil('\n') → digitalWrite → 回傳 ACK → DataReceived → BeginInvoke 更新 Label

### 4. 操作說明與成果
1. **燒錄程式**：使用 USB 線連接 Arduino Uno 至電腦，開啟 `Task3-2.ino` 並點擊「上傳」。
2. **執行c#程式**：會跳出GUI，上面有ComboBox可以選要使用的port，以及連線按鈕、On按鈕、Off按鈕，並且Label會顯示Arduino回傳的狀態。
3. **點擊On/Off按鈕**：實體LED燈可以隨著GUI的按鈕改變對應狀態。
4. **操作影片**：請參閱同目錄下 `video/Task3-2.mov` 之實際操作畫面。
