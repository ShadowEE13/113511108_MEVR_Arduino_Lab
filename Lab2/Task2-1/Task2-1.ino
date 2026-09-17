const int PIN_1A = 5;
const int PIN_2A = 6;
int t = 300;
void setup() {
pinMode(PIN_1A, 5);
pinMode(PIN_2A, 6);
}
void loop() {
if(t != 1020) t += 1;
else t = 300;
analogWrite(PIN_1A, 1023);
analogWrite(PIN_2A, 0);
delay(50);
}
