#include <Servo.h>
Servo myservo;
void setup()
{
myservo.attach(9);
myservo.write(0);
}
void loop() {
int data = 90;
myservo.write(data);
}