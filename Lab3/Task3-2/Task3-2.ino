

const int LED_PIN = 7;

void setup() {
  pinMode(LED_PIN, OUTPUT);
  Serial.begin(9600);          
  Serial.println("READY");       // USB 開 port 會重置 UNO，開機後主動報到
}

void loop() {
  if (Serial.available() > 0) {                   
    String cmd = Serial.readStringUntil('\n');     // 讀到 \n 立刻回來（framing）
    cmd.trim();                                    // 去掉 \r 和空白

    if (cmd == "PING") {
      Serial.println("READY");                  
    } 
    else if (cmd == "ON") {
      digitalWrite(LED_PIN, HIGH);
      Serial.println("LED:ON");                 
    } 
    else if (cmd == "OFF") {
      digitalWrite(LED_PIN, LOW);
      Serial.println("LED:OFF");
    } 
    else if (cmd.length() > 0) {
      Serial.print("UNKNOWN:");
      Serial.println(cmd);
    }
  }
}