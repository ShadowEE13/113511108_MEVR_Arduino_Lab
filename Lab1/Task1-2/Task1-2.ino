const int rPin = 11, gPin = 10, bPin = 9;
const int readPin = A0;
const int buttonPin = 2;
bool state = false;
unsigned long lastPrint = 0;

void setup() {
  Serial.begin(9600);
  pinMode(rPin, OUTPUT);
  pinMode(gPin, OUTPUT);
  pinMode(bPin, OUTPUT);
  pinMode(readPin, INPUT);
  pinMode(buttonPin, INPUT);
}

void loop() {
  int light, buttonState;
  buttonState = digitalRead(buttonPin);
  light = analogRead (readPin);
  if(Serial.available() > 0){
    char c = Serial.read();
    switch (c) {
      case '1':
        state = true;
        break;
      case '0':
        state = false;
         break;
       default:
          break;
    }
  }
  analogWrite(gPin, state ? 0 : 255);
  analogWrite(rPin, buttonState == 1 ? 0 : 255);
  analogWrite (bPin, 255 - light / 4);
  if(millis() - lastPrint >= 1000){
    Serial.print("Data is ");
    Serial.println(light);
    lastPrint += 1000;
  }
}