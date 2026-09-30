// Advanced Task 3-2: LED Control with Serial Communication
// 協定（每行以 \n 結尾）：
//   PC → Arduino : PING / ON / OFF
//   Arduino → PC : READY / LED:ON / LED:OFF / UNKNOWN:xxx

const int LED_PIN = 7;

void setup() {
  pinMode(LED_PIN, OUTPUT);
  Serial.begin(9600);            // 必須跟 C# 的 BaudRate 一致
  Serial.println("READY");       // USB 開 port 會重置 UNO，開機後主動報到
}

void loop() {
  if (Serial.available() > 0) {                    // non-blocking：有資料才處理
    String cmd = Serial.readStringUntil('\n');     // 讀到 \n 立刻回來（framing）
    cmd.trim();                                    // 去掉 \r 和空白

    if (cmd == "PING") {
      Serial.println("READY");                     // 握手：C# 主動詢問時回應
    } else if (cmd == "ON") {
      digitalWrite(LED_PIN, HIGH);
      Serial.println("LED:ON");                    // ACK：回報實際狀態
    } else if (cmd == "OFF") {
      digitalWrite(LED_PIN, LOW);
      Serial.println("LED:OFF");
    } else if (cmd.length() > 0) {
      Serial.print("UNKNOWN:");
      Serial.println(cmd);
    }
  }
}