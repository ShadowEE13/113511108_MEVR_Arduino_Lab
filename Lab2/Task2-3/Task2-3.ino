const int buttonPinA = 3;
const int ledPinA = 4;
volatile bool ledStateA = 0;

const int buttonPinB = 5;
const int ledPinB = 2;
bool ledStateB = 0;
bool lastButtonStateB = 0;
bool ButtonStateB = 0;

void setup() {
  Serial.begin(9600);
  pinMode(ledPinA, OUTPUT);
  pinMode(buttonPinA, INPUT);
  pinMode(ledPinB, OUTPUT);
  pinMode(buttonPinB, INPUT);
  attachInterrupt(digitalPinToInterrupt(buttonPinA), buttonISR, RISING);
  digitalWrite(ledPinA, ledStateA);
  digitalWrite(ledPinB, ledStateB);
}

void buttonISR() {
  ledStateA = !ledStateA;
  digitalWrite(ledPinA, ledStateA);
}

void loop() {
  ButtonStateB = digitalRead(buttonPinB);
  if (ButtonStateB && (ButtonStateB != lastButtonStateB)) {
    ledStateB = !ledStateB;
    digitalWrite(ledPinB, ledStateB);
    lastButtonStateB = ButtonStateB;
  } 
  else if (ButtonStateB != lastButtonStateB) lastButtonStateB = 0;
  delay(2000);
}