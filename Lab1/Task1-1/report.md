**`Lab1/Task1-1/report.md`**

```markdown
# 課題報告：Task 1-1 Using the variable resistor to adjust LED lightness

- **學生姓名**：馮奕勛
- **學生學號**：113511108
- **完成日期**：2026-09-17

---

### 1. 實驗目標
- 使用可變電阻，並用analogRead來讀取值
- 用讀取到的值改變LED的亮度
- 讓序列埠一秒輸出一次讀到的值

### 2. 設備與元件
- Arduino Uno 開發板 x 1
- USB Type-B 傳輸線 x 1
- 個人電腦（已安裝 Arduino IDE）x 1
- 可變電阻 10k x 1
- LED燈 x 1
- 220 ohm 電阻 x 1

### 3. 操作說明與成果
1. **燒錄程式**：使用 USB 線連接 Arduino Uno 至電腦，開啟 `Task0-1.ino` 並點擊「上傳」。
2. **開啟監控器**：開啟 Arduino IDE 的 Serial Monitor，將鮑率（Baud rate）設為 **9600 baud**。
3. **實驗成果**：序列埠監控器成功每秒印出一次可變電阻改變的類比讀取值，並且LED能隨著讀取值改變明暗。
4. **操作影片**：請參閱同目錄下 `video/Task1-1.mov` 之實際操作畫面。