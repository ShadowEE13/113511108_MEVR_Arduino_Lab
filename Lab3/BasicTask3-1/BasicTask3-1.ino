#include <TimerOne.h>

volatile int timercount = 0;   // ISR 改、loop 讀 → 必須 volatile

void timer_int() {             // ISR：越短越好
  timercount++;
}

void setup() {
  Serial.begin(9600);
  Timer1.initialize(500000);           // 500 ms = 500000 us
  Timer1.attachInterrupt(timer_int);
}

void loop() {
  static int last = -1;

  // int 是 2 bytes，關中斷複製一份，避免讀到一半被改
  noInterrupts();
  int count = timercount;
  interrupts();

  if (count != last) {         // 有變才印，不洗版
    Serial.println(count);
    last = count;
  }
}