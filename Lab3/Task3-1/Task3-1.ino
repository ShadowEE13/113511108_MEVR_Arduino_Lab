#include <TimerOne.h>

const int BTN_A = 2;
const int LED_A = 7;
const int BTN_B = 4;
const int LED_B = 8;

const int BTN_MODE = INPUT;
const int PRESSED  = HIGH;

void timerISR() {
  if (digitalRead(BTN_A) == PRESSED) digitalWrite(LED_A, HIGH);
  else                               digitalWrite(LED_A, LOW);
}

void setup() {
  pinMode(BTN_A, BTN_MODE);
  pinMode(BTN_B, BTN_MODE);
  pinMode(LED_A, OUTPUT);
  pinMode(LED_B, OUTPUT);

  Timer1.initialize(50000);  // 50 ms = 50000 us
  Timer1.attachInterrupt(timerISR);
}

void loop() {
  if (digitalRead(BTN_B) == PRESSED) digitalWrite(LED_B, HIGH);
  else                               digitalWrite(LED_B, LOW);

  delay(1000);   
}