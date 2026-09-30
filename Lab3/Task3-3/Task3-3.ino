// Advanced Task 3-3: HC-05 Wireless LED Control
// 協定同 3-2：PING / ON / OFF → READY / LED:ON / LED:OFF

#include <SoftwareSerial.h>
SoftwareSerial bt(12, 3);          // (RX, TX)：D0/D1 留給 USB 上傳與除錯

const int LED_PIN = 7;

void setup() {
  pinMode(LED_PIN, OUTPUT);
  Serial.begin(9600);             // USB：除錯用
  bt.begin(9600);                 // 藍牙：必須等於 AT+UART 設的值
  Serial.println("Arduino started");
  bt.println("READY");            // 藍牙沒有 DTR，這行 C# 多半收不到，靠 PING 握手
}

void loop() {
  if (bt.available() > 0) {
    String cmd = bt.readStringUntil('\n');
    cmd.trim();

    Serial.print("BT 收到：");    // 在 Serial Monitor 看得到收了什麼
    Serial.println(cmd);

    if (cmd == "PING") {
      bt.println("READY");
    } else if (cmd == "ON") {
      digitalWrite(LED_PIN, HIGH);
      bt.println("LED:ON");
    } else if (cmd == "OFF") {
      digitalWrite(LED_PIN, LOW);
      bt.println("LED:OFF");
    } else if (cmd.length() > 0) {
      bt.print("UNKNOWN:");
      bt.println(cmd);
    }
  }
}