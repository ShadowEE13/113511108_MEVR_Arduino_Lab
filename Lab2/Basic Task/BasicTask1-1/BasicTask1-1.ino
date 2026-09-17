const int led_pin = 13; // BUILTIN

void setup(){
  pinMode(led_pin, 1);
}

void loop(){
  digitalWrite(led_pin, 1);
  delay(500); //500ms
  digitalWrite(led_pin, 0);
  delay(500);
}