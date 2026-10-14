**`Lab3/Task3-1/report.md`（完整示範報告）**

```markdown
# 課題報告：Task 3-1 Timer Interrupt vs Blocking Delay

- **學生姓名**：馮奕勛
- **學生學號**：113511108
- **完成日期**：2026-10-01

---

### 1. 實驗目標
- 學習使用TimerOne達成固定時間的Interrupt
- 比較non-blocking與blocking寫法，最終LED燈反應的差別

### 2. 設備與元件
- Arduino Uno 開發板 x 1
- USB Type-B 傳輸線 x 1
- 個人電腦（已安裝 Arduino IDE）x 1
- 按鈕 x 2
- LED x 2
- 電阻 220 ohm x 2
- 電阻 10k ohm x 2

### 3. 操作說明與成果
1. **燒錄程式**：使用 USB 線連接 Arduino Uno 至電腦，開啟 `Task3-1.ino` 並點擊「上傳」。
2. **操作TimerOne Interrupt組**：可以發現操作按鈕時幾乎沒有延遲，按鈕並不會被delay的blocking擋住。
3. **操作Polling組**：可以發現操作按鈕時常常會遇到延遲，因為偵測按鈕狀態被delay給擋住，必須要等到delay結束才會改變按鈕狀態。
4. **操作影片**：請參閱同目錄下 `video/Task3-1.mov` 之實際操作畫面。
