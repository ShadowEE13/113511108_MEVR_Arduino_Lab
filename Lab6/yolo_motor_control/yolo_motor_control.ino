const int EN_PIN  = 9;   // L293D 第 1 腳：PWM 調速
const int IN1_PIN = 8;   // L293D 第 2 腳：方向
const int IN2_PIN = 7;   // L293D 第 7 腳：方向

int currentPwm = 0;

void setMotor(int pwm) {
  pwm = constrain(pwm, 0, 255);        // 保險起見，限制在 0–255 之間
  analogWrite(EN_PIN, pwm);
  currentPwm = pwm;
}

bool isNumber(const String& s) {       // 確認字串全部都是數字
  if (s.length() == 0) return false;
  for (unsigned int i = 0; i < s.length(); i++) {
    if (!isDigit(s[i])) return false;
  }
  return true;
}

void setup() {
  Serial.begin(115200);
  pinMode(EN_PIN, OUTPUT);
  pinMode(IN1_PIN, OUTPUT);
  pinMode(IN2_PIN, OUTPUT);
  digitalWrite(IN1_PIN, HIGH);         // 固定一個轉向
  digitalWrite(IN2_PIN, LOW);
  setMotor(0);                         // 開機時馬達先停住
  Serial.println("READY");             // 每次重啟都會印出，方便觀察 Arduino 有沒有重開機
}

void loop() {
  if (Serial.available() > 0) {
    String cmd = Serial.readStringUntil('\n');
    cmd.trim();

    if (cmd.startsWith("PWM:")) {
      String valueStr = cmd.substring(4);          // 取出 "PWM:" 後面的部分
      if (isNumber(valueStr) && valueStr.toInt() <= 255) {
        setMotor(valueStr.toInt());
        Serial.print("ACK:PWM:");
        Serial.println(currentPwm);
      } else {
        Serial.print("ERR:BAD_VALUE:");
        Serial.println(cmd);
      }
    } else if (cmd.length() > 0) {
      Serial.print("ERR:UNKNOWN:");
      Serial.println(cmd);
    }
  }
}