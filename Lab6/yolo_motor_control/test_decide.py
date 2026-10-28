RUN_CLASS = "mouse"            # 偵測到 → 運轉
STOP_CLASS = "cell phone"      # 偵測到 → 停止（優先）
RUN_PWM = 180
STOP_PWM = 0
CONF_THRESHOLD = 0.5           # 信心門檻：濾掉低信心的誤判

def decide_pwm(detected):
    """決策邏輯：根據偵測到的類別集合，回傳 PWM 目標值。"""
    if STOP_CLASS in detected:     # 停止類別優先（也涵蓋兩類同時出現）
        return STOP_PWM
    if RUN_CLASS in detected:
        return RUN_PWM
    return STOP_PWM 

assert decide_pwm(set()) == 0                       # Case D：沒有目標
assert decide_pwm({"mouse"}) == 180                 # Case A：運轉
assert decide_pwm({"cell phone"}) == 0              # Case B：停止
assert decide_pwm({"mouse", "cell phone"}) == 0     # Case D：兩類同時出現
assert decide_pwm({"person", "cup"}) == 0           # 只有不相關的類別
print("All decision tests passed")