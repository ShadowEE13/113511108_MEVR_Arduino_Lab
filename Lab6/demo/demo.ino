const int LED_PIN = LED_BUILTIN;   // D13
const int EN_PIN  = 9;             // L293D 第 1 腳（PWM）
const int IN1_PIN = 8;             // L293D 第 2 腳
const int IN2_PIN = 7;             // L293D 第 7 腳

bool ledOn = false;
int currentPwm = 0;

void setMotor(int pwm) {
  pwm = constrain(pwm, 0, 255);
  analogWrite(EN_PIN, pwm);
  currentPwm = pwm;
}

bool isNumber(const String& s) {
  if (s.length() == 0) return false;
  for (unsigned int i = 0; i < s.length(); i++) {
    if (!isDigit(s[i])) return false;
  }
  return true;
}

void setup() {
  Serial.begin(115200);
  pinMode(LED_PIN, OUTPUT);
  pinMode(EN_PIN, OUTPUT);
  pinMode(IN1_PIN, OUTPUT);
  pinMode(IN2_PIN, OUTPUT);
  digitalWrite(LED_PIN, LOW);
  digitalWrite(IN1_PIN, HIGH);
  digitalWrite(IN2_PIN, LOW);
  setMotor(0);
  Serial.println("READY");
}

void loop() {
  if (Serial.available() > 0) {
    String cmd = Serial.readStringUntil('\n');
    cmd.trim();

    if (cmd == "HELLO") {                               // Part 1
      Serial.println("ACK");
    } else if (cmd == "LED:ON") {                       // Part 2
      digitalWrite(LED_PIN, HIGH); ledOn = true;
      Serial.println("ACK:ON");
    } else if (cmd == "LED:OFF") {
      digitalWrite(LED_PIN, LOW); ledOn = false;
      Serial.println("ACK:OFF");
    } else if (cmd == "LED:STATUS") {
      Serial.println(ledOn ? "STATE:ON" : "STATE:OFF");
    } else if (cmd.startsWith("PWM:")) {                // Final
      String v = cmd.substring(4);
      if (isNumber(v) && v.toInt() <= 255) {
        setMotor(v.toInt());
        Serial.print("ACK:PWM:"); Serial.println(currentPwm);
      } else {
        Serial.print("ERR:BAD_VALUE:"); Serial.println(cmd);
      }
    } else if (cmd.length() > 0) {
      Serial.print("ERR:UNKNOWN:"); Serial.println(cmd);
    }
  }
}