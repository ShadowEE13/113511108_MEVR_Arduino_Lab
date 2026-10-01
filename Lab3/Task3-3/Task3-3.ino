#include <SoftwareSerial.h>
SoftwareSerial bt(12, 3);        
const int LED_PIN = 7;

void setup() {
  pinMode(LED_PIN, OUTPUT);
  Serial.begin(9600);             
  bt.begin(9600);                
  Serial.println("Arduino started");
  bt.println("READY");          
}

void loop() {
  if (bt.available() > 0) {
    String cmd = bt.readStringUntil('\n');
    cmd.trim();

    Serial.print("BT 收到：");   
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