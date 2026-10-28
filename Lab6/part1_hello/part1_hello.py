import time
import serial

PORT = "/dev/ttyACM0"   # 明天用 ls /dev/ttyACM* /dev/ttyUSB* 確認實際的名稱
BAUD = 115200           # 必須跟 Arduino 的 Serial.begin 一樣

with serial.Serial(PORT, BAUD, timeout=1) as ser:
    time.sleep(2)               # ① 等 Arduino 重啟完成
    ser.reset_input_buffer()    # ② 清掉開機時殘留在緩衝區的資料
    ser.write(b"HELLO\n")       # ③ 送出 bytes，結尾要有 \n
    reply = ser.readline().decode(errors="replace").strip()   # ④
    print("Sent : HELLO")
    print("Reply:", reply if reply else "(timeout, no reply)")