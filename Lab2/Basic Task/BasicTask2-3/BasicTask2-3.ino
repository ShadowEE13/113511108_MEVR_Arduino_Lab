const int buttonPinA = 3;
const int ledPinA = 4;
volatile bool ledStateA = 0;

void setup() {
  Serial.begin(9600);
  pinMode(ledPinA, OUTPUT);
  pinMode(buttonPinA, INPUT);
  attachInterrupt(digitalPinToInterrupt(buttonPinA), buttonISR, RISING);
  digitalWrite(ledPinA, ledStateA);
}

void buttonISR() {
  ledStateA = !ledStateA;
  digitalWrite(ledPinA, ledStateA);
}

void loop() {
  //digitalWrite(ledPinA, ledStateA);
}