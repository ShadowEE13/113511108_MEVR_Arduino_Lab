from pathlib import Path
import time

import serial
from picamera2 import Picamera2
from ultralytics import YOLO

# ===== 設定 =====
PORT = "/dev/ttyACM0"          # 依實際裝置修改
BAUD = 115200

RUN_CLASS = "mouse"            # 偵測到 → 運轉
STOP_CLASS = "cell phone"      # 偵測到 → 停止（優先）
RUN_PWM = 180
STOP_PWM = 0
CONF_THRESHOLD = 0.5           # 信心門檻：濾掉低信心的誤判

# 模型沿用 Lab 2 的權重：src/class3/ → src/ → src/class2/models/
MODEL_PATH = Path(__file__).resolve().parent.parent / "class2" / "models" / "yolo11n.pt"


def decide_pwm(detected):
    """決策邏輯：根據偵測到的類別集合，回傳 PWM 目標值。"""
    if STOP_CLASS in detected:     # 停止類別優先（也涵蓋兩類同時出現）
        return STOP_PWM
    if RUN_CLASS in detected:
        return RUN_PWM
    return STOP_PWM                # 沒有指定的目標 → 停止


def send_pwm(ser, pwm):
    """送出 PWM 指令，讀取 Arduino 的回覆。"""
    cmd = f"PWM:{pwm}"
    ser.write((cmd + "\n").encode())
    reply = ser.readline().decode(errors="replace").strip()
    return cmd, reply


model = YOLO(MODEL_PATH)

picam2 = Picamera2()
config = picam2.create_preview_configuration(main={"size": (640, 480), "format": "RGB888"})
picam2.configure(config)

with serial.Serial(PORT, BAUD, timeout=1) as ser:
    time.sleep(2)                  # 等 Arduino 自動重啟完成
    ser.reset_input_buffer()

    picam2.start()
    time.sleep(2)                  # 等相機曝光、自動對焦穩定
    print(f"Running. RUN={RUN_CLASS} → {RUN_PWM}, STOP={STOP_CLASS} → {STOP_PWM}. Ctrl+C to quit.\n")

    try:
        while True:
            t0 = time.perf_counter()

            frame = picam2.capture_array()
            result = model(frame, conf=CONF_THRESHOLD, verbose=False)[0]
            detected = {result.names[int(c)] for c in result.boxes.cls}

            pwm = decide_pwm(detected)
            cmd, reply = send_pwm(ser, pwm)

            loop_ms = (time.perf_counter() - t0) * 1000
            print(f"detected={sorted(detected) or ['none']}  "
                  f"PWM target={pwm}  TX={cmd}  RX={reply or '(timeout)'}  "
                  f"({loop_ms:.0f} ms)")

    except KeyboardInterrupt:
        print("\nStopped by user.")

    finally:
        ser.write(b"PWM:0\n")      # 安全停車：不管程式怎麼結束，都先讓馬達停下來
        picam2.stop()
        print("Safety stop sent (PWM:0). Camera stopped. Serial closed.")