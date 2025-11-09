"""
Hello World Plugin - Minimal POC version.

Just prints to console to verify plugin system works.
"""

import time

print("="*50)
print("HELLO WORLD PLUGIN STARTED!")
print("="*50)
print("Plugin ID: org.parley.examples.helloworld")
print("This is a proof-of-concept that plugins can run.")
print("Full functionality (notifications, gRPC) coming soon.")
print("="*50)

# Keep running for 10 seconds to verify process stays alive
for i in range(10):
    print(f"Hello World plugin running... ({i+1}/10)")
    time.sleep(1)

print("="*50)
print("HELLO WORLD PLUGIN EXITING NORMALLY")
print("="*50)
