void setup() {
  Serial.begin(115200);                          
}

void loop() {
  if (Serial.available() > 0) {                 
    String cmd = Serial.readStringUntil('\n');  
    cmd.trim();                                  
    if (cmd == "HELLO") {
      Serial.println("ACK");
    }
  }
}