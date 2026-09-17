const int ledPin = 11;
int light = 0;

void setup() {
  pinMode(ledPin, OUTPUT);
  analogWrite (ledPin, 255);
}

void loop() {
  light += 1;
  analogWrite (ledPin, light);
  if (light >= 255) light = 0;
  delay(5);
}