**`Lab2/Task2-2/report.md`（完整示範報告）**

```markdown
# 課題報告：Task2-2 Ultrasonic Sensor Drives Servo Angle

- **學生姓名**：馮奕勛
- **學生學號**：113511108
- **完成日期**：2026-09-18

---

### 1. 實驗目標
- 使用Ultrasonic Sensor 量測距離
- 將量測到的距離轉換為角度並控制Servo Motor

### 2. 設備與元件
- Arduino Uno 開發板 x 1
- USB Type-B 傳輸線 x 1
- 個人電腦（已安裝 Arduino IDE）x 1
- Servo Motor x 1
- Ultrasonic Sensor x 1

### 3. 操作說明與成果
1. **燒錄程式**：使用 USB 線連接 Arduino Uno 至電腦，開啟 `Task2-2.ino` 並點擊「上傳」。
2. **開啟監控器**：開啟 Arduino IDE 的 Serial Monitor，將鮑率（Baud rate）設為 **9600 baud**。
3. **實驗成果**：序列埠監控器成功每秒印出一次當前量測到的距離。
4. **伺服馬達**：Servo Motor會隨著量到的距離做角度的變化(5cm ~ 30cm用map對應到0 ~ 180度)
5. **操作影片**：請參閱同目錄下 `video/Task2-2.mov` 之實際操作畫面。
