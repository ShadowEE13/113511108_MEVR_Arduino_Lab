**`Lab2/Task2-3/report.md`（完整示範報告）**

```markdown
# 課題報告：Task 2-3 External Interrupt vs Polling

- **學生姓名**：馮奕勛
- **學生學號**：113511108
- **完成日期**：2026-09-18

---

### 1. 實驗目標
- 建立兩個用按鈕控制的LED組，並觀察其差別
- A組採用External Interrupt
- B組採用Polling

### 2. 設備與元件
- Arduino Uno 開發板 x 1
- USB Type-B 傳輸線 x 1
- 個人電腦（已安裝 Arduino IDE）x 1
- 按鈕 x 2
- LED x 2
- 220 ohm 電阻 x 2

### 3. 操作說明與成果
1. **燒錄程式**：使用 USB 線連接 Arduino Uno 至電腦，開啟 `Task0-1.ino` 並點擊「上傳」。
2. **按下A組按鈕**：因為A組使用的是External Interrupt，所以可以在執行loop裡的delay時，立刻中斷並執行ISR，立即改變LED的狀態。
3. **按下B組按鈕**：因為B組使用的是Polling，所以必須等到delay結束，直到程式跑到判斷B組按鈕狀時，才會改變LED的狀態，無法及時響應。
4. **操作影片**：請參閱同目錄下 `video/Task2-3.mov` 之實際操作畫面。
