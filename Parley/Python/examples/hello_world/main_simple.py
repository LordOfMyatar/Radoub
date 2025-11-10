"""
Hello World Plugin - gRPC version

Demonstrates basic plugin functionality with gRPC communication.
"""

import time
from parley_plugin import ParleyClient

print("="*50)
print("HELLO WORLD PLUGIN STARTED!")
print("="*50)
print("Plugin ID: org.parley.examples.helloworld")
print("="*50)

try:
    with ParleyClient() as client:
        print("[OK] Connected to Parley gRPC server")

        # Show notification
        success = client.show_notification(
            "Hello World",
            "This plugin is now running with gRPC communication!"
        )

        if success:
            print("[OK] Notification sent successfully")
        else:
            print("[FAIL] Notification failed")

        # Query current dialog (returns empty in POC)
        print("\nQuerying current dialog...")
        dialog_id, dialog_name = client.get_current_dialog()
        if dialog_id:
            print(f"Current dialog: {dialog_name} ({dialog_id})")
        else:
            print("No dialog loaded (expected in POC)")

        # Keep running briefly to show plugin stays alive
        print("\nPlugin running...")
        for i in range(3):
            print(f"  Heartbeat {i+1}/3")
            time.sleep(1)

except Exception as e:
    print(f"[ERROR] {e}")
    import traceback
    traceback.print_exc()

print("="*50)
print("HELLO WORLD PLUGIN EXITING NORMALLY")
print("="*50)
