import time
import serial

PORT = "COM5"
BAUD = 115200

def send(ser, cmd):
    ser.write((cmd + "\n").encode())                  # 字串 → bytes
    reply = ser.readline().decode(errors="replace").strip()
    print(f"TX: {cmd:<12} RX: {reply or '(timeout)'}")
    return reply

with serial.Serial(PORT, BAUD, timeout=1) as ser:
    time.sleep(2)
    ser.reset_input_buffer()
    for _ in range(3):                                # 重複開關三次，證明可以重複操作
        send(ser, "LED:ON")
        time.sleep(1)
        send(ser, "LED:OFF")
        time.sleep(1)
    send(ser, "LED:STATUS")