#include <Servo.h>

const int trigPin = 7;
const int echoPin = 8;
const unsigned long TIMEOUT_US = 30000UL;
Servo myservo;

const int MIN_CM = 5;
const int MAX_CM = 30;
float distanceCm;
float lastangle = -1;

const int N = 5;
float samples[N];
int idx = 0;
int count = 0;

float updateMovingAverage(float);
float readDistanceCm();

void setup() {
  Serial.begin(9600);
  myservo.attach(9);
  pinMode(trigPin, OUTPUT);
  pinMode(echoPin, INPUT);
  myservo.write(0);
  digitalWrite(trigPin, LOW);
}

void loop() {
  float d = readDistanceCm();
  Serial.print("Distance: ");
  Serial.print(d);
  Serial.println(" cm");
  if (d != -1) {
    float avg = updateMovingAverage(d);
    avg = constrain((int)avg, MIN_CM, MAX_CM);
    int angle = map(avg, MIN_CM, MAX_CM, 0, 180);
    if (lastangle == -1 || abs(lastangle - angle) < 70) myservo.write(angle);
    lastangle = angle;
  }
  delay(60);
}

float updateMovingAverage(float newVal) {
  samples[idx] = newVal;
  idx = (idx + 1) % N;
  if (count < N) count++;

  float sum = 0;
  for (int i = 0; i < count; i++) sum += samples[i];
  return sum / count;
}

float readDistanceCm() {
  digitalWrite(trigPin, LOW);
  delayMicroseconds(2);
  digitalWrite(trigPin, HIGH);
  delayMicroseconds(10);
  digitalWrite(trigPin, LOW);

  unsigned long duration = pulseIn(echoPin, HIGH, TIMEOUT_US);
  if (duration == 0) return -1;
  return duration * 0.0343 / 2;
}