const int rPin = 11, gPin = 10, bPin = 9;
const int nPin = A5;
const int buttonPin = 2;
int light = 1;
int state = 0;
int buttonState = 0; 

void setup() {
  Serial.begin(9600);
  pinMode(rPin, OUTPUT);
  pinMode(gPin, OUTPUT);
  pinMode(bPin, OUTPUT);
  pinMode(nPin, INPUT);
  pinMode(buttonPin, INPUT);
}

void loop() {
  buttonState = digitalRead(buttonPin);
  light = analogRead (nPin); // 1024;
  //state = Serial.parseInt();
  if(Serial.available() > 0){
    state = 1;
  }
  Serial.print("Data is ");
  Serial.print(light);
  Serial.print("\n");
  if(state == 0){
  if (buttonState == 1){
    analogWrite (rPin, 0);
    analogWrite (gPin, 1023);
    analogWrite (bPin, 0);
  }
  else{
    analogWrite (rPin, 1023);
    analogWrite (gPin, 1023);
    analogWrite (bPin, 1023-light);
    //Serial.print("get");
  }
  }
  else{
    analogWrite (rPin, 0);
    analogWrite (gPin, 0);
    analogWrite (bPin, 0);
  }
  delay(50);
}