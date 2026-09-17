const int ledPin = 11;
int light = 1;
void setup() {
pinMode(ledPin, OUTPUT);
analogWrite (ledPin, 35);
}
void loop() {
  light += 10;
  analogWrite (ledPin, light);
  if (light == 1000) light = 1;
  delay(50);
}