const int ledPin = 11;
const int nPin = A5;

int light = 1;
void setup() {
Serial.begin(9600);
pinMode(ledPin, OUTPUT);
pinMode(nPin, INPUT);
}
void loop() {
  light = analogRead (nPin); // 1024;
  Serial.print("Data is ");
  Serial.print(light);
  Serial.print("\n");
  analogWrite (ledPin, light);
  //if (light == 1000) light = 1;
  delay(50);
}