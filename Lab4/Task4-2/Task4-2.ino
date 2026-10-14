/*
 * Task 4-2  Ball-on-Beam：PID 位置控制
 * ------------------------------------------------------------
 * 硬體：Arduino UNO + HC-SR04 超音波 + SG90 舵機 + 乒乓球
 *
 * 接線
 *   HC-SR04 : VCC→5V   GND→GND   Trig→D7   Echo→D8
 *   SG90    : 紅→5V    棕→GND    橘(訊號)→D9
 *   ※ 舵機建議用外部 5V 供電（GND 要和 Arduino 共地），
 *     或至少在舵機電源兩端並一顆 470µF 電容，避免電壓掉下去造成重開機/量測亂跳。
 *
 * 即時調參（Serial Monitor，115200 baud，行尾選 LF 或 Both NL & CR）
 *   p3.5   設定 Kp        i0.8   設定 Ki        d2.5   設定 Kd
 *   s15    設定目標距離 (cm)                    b90    設定水平角 (度)
 *   r      積分歸零        ?      印出目前參數
 *
 * 輸出：Serial Plotter 會畫出 target / pos / tilt 三條線
 */
#include <Servo.h>

// ================= 腳位 =================
const uint8_t TRIG_PIN  = 7;
const uint8_t ECHO_PIN  = 8;
const uint8_t SERVO_PIN = 9;

// ================= 機構參數（依實體調整） =================
float baseAngle = 90.0;            // 軌道「真正水平」時的舵機角度，要實測校正（指令 b）
const float TILT_LIMIT = 15.0;     // 最多偏離水平 ±15°，即 75°~105°
const int   DIR = +1;              // 球往「離目標更遠」的方向跑 → 改成 -1
const float MIN_CM = 2.0;          // HC-SR04 最近約 2 cm
const float MAX_CM = 40.0;         // 比軌道長一點；超過就當成錯誤讀值

// ================= PID 參數 =================
float setpoint = 15.0;             // 目標：球距離感測器 15 cm
// 起始值來自模擬（舵機臂 2 cm、連到離支點 15 cm 處）；實體一定要再微調
float Kp = 2.0;                    // 單位：度 / cm
float Ki = 0.8;                    // 單位：度 / (cm·s)
float Kd = 2.0;                    // 單位：度 / (cm/s)
const float I_LIMIT  = 5.0;        // 積分項最多貢獻 ±5°（anti-windup）
const float I_BAND   = 4.0;        // 誤差小於 4 cm 才累積積分（積分分離）
const float D_ALPHA  = 0.5;        // 微分項低通濾波係數（0~1，越小越平滑但越慢）

// ================= 取樣 / 濾波 =================
const unsigned long SAMPLE_MS = 50;  // 20 Hz 控制迴圈
const uint8_t N_AVG = 5;             // 移動平均長度

Servo servo;

// 濾波器狀態
float lastValid = 15.0;
float hist[3];                       // 中位數用：最近 3 筆
float avgBuf[N_AVG];                 // 移動平均用：環形緩衝區
uint8_t avgIdx = 0;
float avgSum = 0;

// PID 狀態
float integral = 0;
float prevPos  = 0;
float dFilt    = 0;
unsigned long lastMs = 0;

// 序列指令
char cmdBuf[16];
uint8_t cmdLen = 0;

// ------------------------------------------------------------
// HC-SR04：回傳距離 (cm)，讀不到回傳 -1
// ------------------------------------------------------------
float readDistanceCm() {
  digitalWrite(TRIG_PIN, LOW);
  delayMicroseconds(2);
  digitalWrite(TRIG_PIN, HIGH);      // 10 µs 觸發脈衝
  delayMicroseconds(10);
  digitalWrite(TRIG_PIN, LOW);

  // Echo 高電位的時間 = 聲波來回時間；6 ms 逾時（約 100 cm），避免卡住控制迴圈
  unsigned long us = pulseIn(ECHO_PIN, HIGH, 6000UL);
  if (us == 0) return -1;
  return us / 58.0;                  // 聲速 343 m/s、來回兩趟 → 每 58 µs ≈ 1 cm
}

float median3(float a, float b, float c) {
  return max(min(a, b), min(max(a, b), c));
}

// ------------------------------------------------------------
// 三段濾波：剔除錯誤值 → 中位數(3) 去尖峰 → 移動平均(N) 壓雜訊
// ------------------------------------------------------------
float filterDistance(float raw) {
  if (raw < MIN_CM || raw > MAX_CM) raw = lastValid;   // 1) 錯誤值沿用上一筆
  lastValid = raw;

  hist[0] = hist[1];                                  // 2) 中位數：單點尖峰直接消失
  hist[1] = hist[2];
  hist[2] = raw;
  float m = median3(hist[0], hist[1], hist[2]);

  avgSum -= avgBuf[avgIdx];                           // 3) 移動平均（O(1) 更新總和）
  avgBuf[avgIdx] = m;
  avgSum += m;
  avgIdx = (avgIdx + 1) % N_AVG;
  return avgSum / N_AVG;
}

// ------------------------------------------------------------
// PID：回傳「要偏離水平幾度」
// ------------------------------------------------------------
float computePID(float pos, float dt) {
  float error = setpoint - pos;      // > 0：球太靠近感測器

  // P：誤差越大，傾斜越多
  float P = Kp * error;

  // I：消除穩態誤差（例如水平角沒校準準）
  if (fabs(error) < I_BAND) integral += error * dt;   // 遠離目標時不累積
  if (Ki > 0) integral = constrain(integral, -I_LIMIT / Ki, I_LIMIT / Ki);
  float I = Ki * integral;

  // D：對「量測值」微分（不是對誤差），改 setpoint 時不會瞬間暴衝
  float vel = (pos - prevPos) / dt;                    // 球速 (cm/s)
  prevPos = pos;
  dFilt += D_ALPHA * (vel - dFilt);                    // 一階低通，壓掉微分放大的雜訊
  float D = -Kd * dFilt;                               // 球往外衝 → 提前反向修正

  return P + I + D;
}

// write() 只能寫整數度；改用微秒，解析度約 0.1°
void writeServo(float deg) {
  int us = 544 + (int)(deg / 180.0 * (2400 - 544));   // 與 Servo 函式庫預設範圍一致
  servo.writeMicroseconds(us);
}

// ------------------------------------------------------------
// 序列指令（非阻塞：一次讀一個字元，收到換行才執行）
// ------------------------------------------------------------
void printParams() {
  Serial.print(F("# Kp=")); Serial.print(Kp);
  Serial.print(F(" Ki="));  Serial.print(Ki);
  Serial.print(F(" Kd="));  Serial.print(Kd);
  Serial.print(F(" set=")); Serial.print(setpoint);
  Serial.print(F(" base="));Serial.println(baseAngle);
}

void runCommand(char *s) {
  float v = atof(s + 1);
  switch (s[0]) {
    case 'p': Kp = v; break;
    case 'i': Ki = v; integral = 0; break;
    case 'd': Kd = v; break;
    case 's': setpoint = v; break;
    case 'b': baseAngle = v; break;
    case 'r': integral = 0; break;
    case '?': break;
    default:  Serial.println(F("# 指令: p i d s b r ?")); return;
  }
  printParams();
}

void handleSerial() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n' || c == '\r') {
      if (cmdLen > 0) {
        cmdBuf[cmdLen] = '\0';
        runCommand(cmdBuf);
        cmdLen = 0;
      }
    } else if (cmdLen < sizeof(cmdBuf) - 1) {
      cmdBuf[cmdLen++] = c;
    }
  }
}

// ------------------------------------------------------------
void setup() {
  Serial.begin(115200);
  pinMode(TRIG_PIN, OUTPUT);
  pinMode(ECHO_PIN, INPUT);

  servo.attach(SERVO_PIN);
  writeServo(baseAngle);             // 先放水平
  delay(1000);

  // 用第一筆讀值把濾波器填滿，避免一開始平均值從 0 爬上來
  float d = readDistanceCm();
  if (d < MIN_CM || d > MAX_CM) d = setpoint;
  lastValid = d;
  for (uint8_t i = 0; i < 3; i++) hist[i] = d;
  for (uint8_t i = 0; i < N_AVG; i++) avgBuf[i] = d;
  avgSum = d * N_AVG;
  prevPos = d;

  printParams();
  lastMs = millis();
}

void loop() {
  handleSerial();

  unsigned long now = millis();
  if (now - lastMs < SAMPLE_MS) return;          // 固定週期執行（不用 delay）
  float dt = (now - lastMs) / 1000.0;
  lastMs = now;

  float pos  = filterDistance(readDistanceCm());
  float tilt = constrain(computePID(pos, dt), -TILT_LIMIT, TILT_LIMIT);
  writeServo(baseAngle + DIR * tilt);

  // Serial Plotter 格式 → label:value
  Serial.print(F("target:")); Serial.print(setpoint);
  Serial.print(F(",pos:"));   Serial.print(pos);
  Serial.print(F(",tilt:"));  Serial.println(tilt);
}
