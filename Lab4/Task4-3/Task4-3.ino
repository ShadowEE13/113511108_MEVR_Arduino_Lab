/*
 * Task 4-3  Ball-on-Beam：C# 調參儀表板（Arduino 端）
 * ------------------------------------------------------------
 * 以 Task 4-2 的 PID 位置控制為基礎，加上：
 *   1. 透過 USB 序列埠連到電腦端 C# 儀表板：即時調參、即時畫圖
 *   2. GUI 模式（g1）：精簡資料格式、不暫停印資料，附 Arduino 時間戳與 P/I/D 各項
 *   3. 控制開關（e0/e1）、參數存入 EEPROM（w），下次開機自動載入
 *
 * 硬體：Arduino UNO + HC-SR04 超音波 + SG90 舵機 + 乒乓球
 *
 * 接線（和 Task 4-2 相同）
 *   HC-SR04 : VCC→5V   GND→GND   Trig→D10  Echo→D11
 *   SG90    : 紅→5V    棕→GND    橘(訊號)→D9
 *   USB     : 接電腦，C# 儀表板選這個 COM port
 *   ※ 同一個 COM port 只能給一個程式開：用儀表板前先關掉 Arduino IDE 的序列埠監控視窗 / 繪圖器
 *   ※ 電腦開 port 時 UNO 可能會重開機（約 2 秒），沒存進 EEPROM 的參數會回到預設值
 *
 * 序列指令（115200 baud，以換行結尾；Serial Monitor 或 C# 儀表板都能下）
 *   p3.5   設定 Kp        i0.8   設定 Ki        d2.5   設定 Kd
 *   s15    設定目標距離 (cm)                    b90    設定水平角 (度)
 *   t500   每 500 ms 印一筆資料（t0 = 停止印）
 *   r      積分歸零        ?      印出目前參數
 *   g1/g0  開/關 GUI 模式  e1/e0  啟動/停止控制（停止時軌道回水平）
 *   w      目前參數存入 EEPROM（開機自動載入）
 *
 * 輸出格式
 *   參數  : # Kp=2.000 Ki=0.800 Kd=4.500 set=10.50 base=129.00 print=200 en=1 gui=0
 *   一般  : target:10.50,pos:10.32,tilt:-1.23        ← Serial Plotter 可直接畫
 *   GUI   : $ms,target,pos,tilt,P,I,D               ← C# 儀表板解析用
 *           例：$123456,10.50,10.32,-1.23,0.36,0.12,-1.70
 */
#include <Servo.h>
#include <EEPROM.h>

// ================= 序列埠 =================
const unsigned long BAUD = 115200;  // C# 儀表板的鮑率要選一樣的

// ================= 腳位 =================
const uint8_t TRIG_PIN  = 10;
const uint8_t ECHO_PIN  = 11;
const uint8_t SERVO_PIN = 9;       // Servo 函式庫佔用 Timer1 → D9、D10 不能再用 analogWrite()，
                                   // 但 digitalWrite() 照常可用，所以 Trig 接 D10 沒問題

// ================= 機構參數（依實體調整） =================
float baseAngle = 129.0;           // 軌道水平時的舵機角度（實測：球放著不會自己滾）
const float TILT_LIMIT = 17.0;     // 最多偏離水平 ±17°（baseAngle=129 時即 112°~146°）
const int   DIR = +1;              // 球往「離目標更遠」的方向跑 → 改成 -1
const float MIN_CM = 2.0;          // HC-SR04 最近約 2 cm
const float MAX_CM = 25.0;         // 比軌道長一點；超過就當成錯誤讀值

// ================= PID 參數 =================
float setpoint = 10.5;             // 目標：軌道中間，球距離感測器 10.5 cm（實測）
float Kp = 2.0;                    // 單位：度 / cm
float Ki = 0.8;                    // 單位：度 / (cm·s)
float Kd = 4.5;                    // 單位：度 / (cm/s)
const float I_LIMIT  = 5.0;        // 積分項最多貢獻 ±5°（anti-windup）
const float I_BAND   = 3.0;        // 誤差小於 3 cm 才累積積分（積分分離）
const float D_ALPHA  = 0.5;        // 微分項低通濾波係數（0~1，越小越平滑但越慢）

// ================= 取樣 / 濾波 =================
const unsigned long SAMPLE_MS = 50;  // 20 Hz 控制迴圈
const uint8_t N_AVG = 4;             // 移動平均長度（4 筆 → 平均延遲約 75 ms）
const float JUMP_CM = 3.0;           // 50 ms 內球不可能移動超過 3 cm，超過就先當錯誤讀值
const uint8_t JUMP_MAX = 3;          // 連續 3 筆都跳 → 代表球真的在那裡，接受新值

Servo servo;

// 濾波器狀態
float lastValid = 0;                 // setup() 會用第一筆讀值填入
uint8_t jumpCount = 0;               // 連續「跳太多」的次數
float hist[3];                       // 中位數用：最近 3 筆
float avgBuf[N_AVG];                 // 移動平均用：環形緩衝區
uint8_t avgIdx = 0;
float avgSum = 0;

// PID 狀態
float integral = 0;
float prevPos  = 0;
float dFilt    = 0;
float termP = 0, termI = 0, termD = 0;   // 最近一次的 P/I/D 各項（給 GUI 畫圖）
unsigned long lastMs = 0;

// 模式
bool controlOn = true;               // false：軌道停在水平，不做 PID
bool guiMode   = false;              // true：精簡輸出、不暫停印資料

// 序列指令
char cmdBuf[16];
uint8_t cmdLen = 0;

// 印資料控制（和控制迴圈分開：印慢一點不影響 PID 的 50 ms 週期）
unsigned long printMs = 200;         // 印資料間隔 (ms)，0 = 不印；最快就是 SAMPLE_MS
unsigned long lastPrintMs = 0;
unsigned long holdStart = 0;         // 最近一次回報指令的時間
const unsigned long HOLD_MS = 3000;  // 一般模式：回報後暫停印資料 3 秒（GUI 模式不暫停）

// ================= EEPROM =================
struct SavedParams {
  uint16_t magic;
  float kp, ki, kd, set, base;
};
const uint16_t EE_MAGIC = 0x4B43;    // 用來判斷 EEPROM 裡是不是我們存的資料
const int EE_ADDR = 0;

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
// 三段濾波：剔除錯誤值（超出範圍 / 跳太多）→ 中位數(3) 去尖峰 → 移動平均(N) 壓雜訊
// ------------------------------------------------------------
float filterDistance(float raw) {
  if (raw < MIN_CM || raw > MAX_CM) {                 // 1) 超出範圍：沿用上一筆
    raw = lastValid;
  } else if (fabs(raw - lastValid) > JUMP_CM && jumpCount < JUMP_MAX) {
    raw = lastValid;                                  //    突然跳太多：先當錯誤值
    jumpCount++;
  } else {
    jumpCount = 0;
  }
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
// PID：回傳「要偏離水平幾度」，同時記下 P/I/D 各項
// ------------------------------------------------------------
float computePID(float pos, float dt) {
  float error = setpoint - pos;      // > 0：球太靠近感測器

  // P：誤差越大，傾斜越多
  termP = Kp * error;

  // I：消除穩態誤差（例如水平角沒校準準）
  if (fabs(error) < I_BAND) integral += error * dt;   // 遠離目標時不累積
  if (Ki > 0) integral = constrain(integral, -I_LIMIT / Ki, I_LIMIT / Ki);
  termI = Ki * integral;

  // D：對「量測值」微分（不是對誤差），改 setpoint 時不會瞬間暴衝
  float vel = (pos - prevPos) / dt;                    // 球速 (cm/s)
  prevPos = pos;
  dFilt += D_ALPHA * (vel - dFilt);                    // 一階低通，壓掉微分放大的雜訊
  termD = -Kd * dFilt;                                 // 球往外衝 → 提前反向修正

  return termP + termI + termD;
}

// write() 只能寫整數度；改用微秒，解析度約 0.1°
void writeServo(float deg) {
  int us = 544 + (int)(deg / 180.0 * (2400 - 544));   // 與 Servo 函式庫預設範圍一致
  servo.writeMicroseconds(us);
}

// ------------------------------------------------------------
// EEPROM：存 / 讀參數
// ------------------------------------------------------------
void saveParams() {
  SavedParams s = { EE_MAGIC, Kp, Ki, Kd, setpoint, baseAngle };
  EEPROM.put(EE_ADDR, s);            // put() 只寫有變動的 byte，不會白白消耗寫入壽命
}

bool loadParams() {
  SavedParams s;
  EEPROM.get(EE_ADDR, s);
  if (s.magic != EE_MAGIC) return false;              // 沒存過
  if (isnan(s.kp) || isnan(s.ki) || isnan(s.kd) || isnan(s.set) || isnan(s.base)) return false;
  if (s.base < 90 || s.base > 170) return false;      // 水平角不合理 → 不用，避免軌道直接撞到底
  Kp = s.kp; Ki = s.ki; Kd = s.kd; setpoint = s.set; baseAngle = s.base;
  return true;
}

// ------------------------------------------------------------
// 序列指令（非阻塞：一次讀一個字元，收到換行才執行）
// ------------------------------------------------------------
void printParams() {
  Serial.print(F("# Kp="));   Serial.print(Kp, 3);
  Serial.print(F(" Ki="));    Serial.print(Ki, 3);
  Serial.print(F(" Kd="));    Serial.print(Kd, 3);
  Serial.print(F(" set="));   Serial.print(setpoint, 2);
  Serial.print(F(" base="));  Serial.print(baseAngle, 2);
  Serial.print(F(" print=")); Serial.print(printMs);
  Serial.print(F(" en="));    Serial.print(controlOn ? 1 : 0);
  Serial.print(F(" gui="));   Serial.println(guiMode ? 1 : 0);
  holdStart = millis();              // 一般模式：開始暫停印資料
}

void runCommand(char *s) {
  float v = atof(s + 1);
  switch (s[0]) {
    case 'p': Kp = v; break;
    case 'i': Ki = v; integral = 0; break;
    case 'd': Kd = v; break;
    case 's': setpoint = v; break;
    case 'b': baseAngle = v; break;
    case 't': printMs = (v < 0) ? 0 : (unsigned long)v; break;
    case 'r': integral = 0; break;
    case 'g': guiMode = (v != 0); break;
    case 'e': controlOn = (v != 0); integral = 0; break;
    case 'w':
      saveParams();
      Serial.println(F("# saved to EEPROM"));
      break;
    case '?': break;
    default:
      Serial.println(F("# 指令: p i d s b t r g e w ?"));
      holdStart = millis();
      return;
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
  Serial.begin(BAUD);
  pinMode(TRIG_PIN, OUTPUT);
  pinMode(ECHO_PIN, INPUT);

  bool loaded = loadParams();        // EEPROM 有存過就用存的參數

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

  Serial.println(loaded ? F("# boot: params loaded from EEPROM") : F("# boot: default params"));
  printParams();
  lastMs = millis();
}

void loop() {
  handleSerial();

  unsigned long now = millis();
  if (now - lastMs < SAMPLE_MS) return;          // 固定週期執行（不用 delay）
  float dt = (now - lastMs) / 1000.0;
  lastMs = now;

  float pos = filterDistance(readDistanceCm());
  float tilt = 0;
  if (controlOn) {
    tilt = constrain(computePID(pos, dt), -TILT_LIMIT, TILT_LIMIT);
  } else {
    // 停止控制：軌道回水平；持續更新 prevPos，重新啟動時 D 項才不會暴衝
    integral = 0;
    dFilt = 0;
    prevPos = pos;
    termP = termI = termD = 0;
  }
  writeServo(baseAngle + DIR * tilt);

  // 印資料：有設間隔、（一般模式下）不在暫停時間內、而且到時間了才印
  bool holding = !guiMode && (now - holdStart < HOLD_MS);
  if (printMs > 0 && !holding && now - lastPrintMs >= printMs) {
    lastPrintMs = now;
    if (guiMode) {
      // $ms,target,pos,tilt,P,I,D（約 45 bytes；115200 baud 只要 4 ms，不會卡住控制迴圈）
      Serial.print('$');
      Serial.print(now);             Serial.print(',');
      Serial.print(setpoint, 2);     Serial.print(',');
      Serial.print(pos, 2);          Serial.print(',');
      Serial.print(tilt, 2);         Serial.print(',');
      Serial.print(termP, 2);        Serial.print(',');
      Serial.print(termI, 2);        Serial.print(',');
      Serial.println(termD, 2);
    } else {
      // Serial Plotter 格式 → label:value
      Serial.print(F("target:")); Serial.print(setpoint);
      Serial.print(F(",pos:"));   Serial.print(pos);
      Serial.print(F(",tilt:"));  Serial.println(tilt);
    }
  }
}
