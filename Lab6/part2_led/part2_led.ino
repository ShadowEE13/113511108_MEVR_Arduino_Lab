const int LED_PIN = LED_BUILTIN;   // UNO 板上內建的 LED，接在 D13
bool ledOn = false;                // 軟體記錄的 LED 狀態

void setup() {
  Serial.begin(115200);
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, LOW);
}

void loop() {
  if (Serial.available() > 0) {
    String cmd = Serial.readStringUntil('\n');
    cmd.trim();

    if (cmd == "LED:ON") {
      digitalWrite(LED_PIN, HIGH);
      ledOn = true;
      Serial.println("ACK:ON");
    } else if (cmd == "LED:OFF") {
      digitalWrite(LED_PIN, LOW);
      ledOn = false;
      Serial.println("ACK:OFF");
    } else if (cmd == "LED:STATUS") {
      Serial.println(ledOn ? "STATE:ON" : "STATE:OFF");
    } else if (cmd.length() > 0) {       // 不認得的指令：回傳錯誤，不要默默忽略
      Serial.print("ERR:UNKNOWN:");
      Serial.println(cmd);
    }
  }
}