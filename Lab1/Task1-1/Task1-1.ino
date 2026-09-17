const int ledPin = 11;
const int readPin = A0;
int light;
unsigned long lastPrint = 0;

void setup() {
  Serial.begin(9600);
  pinMode(ledPin, OUTPUT);
  pinMode(readPin, INPUT);
}

void loop() {
  light = analogRead(readPin);  
  analogWrite (ledPin, light / 4);
  if(millis() - lastPrint >= 1000){
    Serial.print("Data is ");
    Serial.println(light);
    lastPrint += 1000;
  }
}