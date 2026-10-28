void setup() {
  Serial.begin(115200);                          // ① baud rate：兩端必須一致
}

void loop() {
  if (Serial.available() > 0) {                  // ② 有資料進來才處理
    String cmd = Serial.readStringUntil('\n');   // ③ 一直讀到換行符號，算一筆完整指令
    cmd.trim();                                  // ④ 去掉頭尾的空白和 \r
    if (cmd == "HELLO") {
      Serial.println("ACK");                     // ⑤ 回傳 ACK，結尾自動加上換行
    }
  }
}