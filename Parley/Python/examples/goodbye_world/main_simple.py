"""
Goodbye World Plugin - Minimal POC version for testing crashes.

Simulates random failure modes to test plugin crash recovery.
"""

import time
import random
import sys

print("="*50)
print("GOODBYE WORLD PLUGIN STARTED (UNSTABLE)")
print("="*50)
print("Plugin ID: org.parley.examples.goodbyeworld")
print("WARNING: This plugin intentionally misbehaves!")
print("="*50)

# Random failure mode
failure_mode = random.randint(1, 5)

if failure_mode == 1:
    # Mode 1: Crash immediately
    print("Mode 1: IMMEDIATE CRASH")
    time.sleep(1)
    raise Exception("Simulated crash - testing crash recovery")

elif failure_mode == 2:
    # Mode 2: Print spam (simulates rate limiting)
    print("Mode 2: SPAM OUTPUT")
    for i in range(100):
        print(f"Spam message {i}")
        time.sleep(0.01)
    print("Spam complete")

elif failure_mode == 3:
    # Mode 3: Exit with error code
    print("Mode 3: NON-ZERO EXIT CODE")
    time.sleep(2)
    print("Exiting with error code 1")
    sys.exit(1)

elif failure_mode == 4:
    # Mode 4: Delayed crash
    print("Mode 4: DELAYED CRASH")
    for i in range(5):
        print(f"Countdown to crash: {5-i}")
        time.sleep(1)
    raise RuntimeError("Delayed crash - boom!")

else:
    # Mode 5: Actually behave
    print("Mode 5: BEHAVING NORMALLY (rare!)")
    for i in range(5):
        print(f"Goodbye World running normally... ({i+1}/5)")
        time.sleep(1)
    print("Successfully completed!")

print("="*50)
print("GOODBYE WORLD PLUGIN EXITING")
print("="*50)
