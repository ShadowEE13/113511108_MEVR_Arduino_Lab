const int PIN_1A = 5;
const int PIN_2A = 6;
void setup() {
pinMode(PIN_1A, 5);
pinMode(PIN_2A, 6);
}
void loop() {
digitalWrite(PIN_1A, HIGH);
digitalWrite(PIN_2A, LOW);
}
