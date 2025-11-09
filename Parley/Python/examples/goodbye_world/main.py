"""
Goodbye World Plugin - Intentionally unstable for testing crash recovery.

WARNING: This plugin simulates various failure modes:
- Random crashes
- Excessive API calls (rate limiting)
- Slow responses
- Unhandled exceptions

DO NOT USE IN PRODUCTION - FOR TESTING ONLY
"""

from parley_plugin import ParleyPlugin
import asyncio
import random
import sys

plugin = ParleyPlugin()


@plugin.on_activate()
async def on_activate():
    """Called when plugin is loaded - simulates unstable behavior."""
    print("Goodbye World plugin activating (unstable mode)...")

    await plugin.ui.show_notification(
        "⚠️ Unstable Plugin Active",
        "Goodbye World is running - expect problems!"
    )

    # Simulate one of several failure modes
    failure_mode = random.randint(1, 5)

    if failure_mode == 1:
        # Mode 1: Crash immediately
        print("Simulating immediate crash...")
        await plugin.ui.show_notification(
            "💥 Crash Mode",
            "About to crash in 2 seconds..."
        )
        await asyncio.sleep(2)
        raise Exception("Simulated crash - testing crash recovery system")

    elif failure_mode == 2:
        # Mode 2: Spam notifications (rate limiting test)
        print("Simulating notification spam (rate limiting test)...")
        await plugin.ui.show_notification(
            "📢 Spam Mode",
            "About to spam notifications..."
        )
        await asyncio.sleep(1)

        try:
            for i in range(2000):  # Way over the 1000/min limit
                await plugin.ui.show_notification(
                    f"Spam {i}",
                    f"Message #{i}"
                )
        except Exception as e:
            print(f"Caught rate limit exception (expected): {e}")
            await plugin.ui.show_notification(
                "🛑 Rate Limited",
                "Plugin hit rate limit (as expected)"
            )

    elif failure_mode == 3:
        # Mode 3: Slow startup (timeout test)
        print("Simulating slow startup...")
        await plugin.ui.show_notification(
            "🐌 Slow Mode",
            "This will take a while..."
        )
        await asyncio.sleep(20)  # Way longer than reasonable
        await plugin.ui.show_notification(
            "Finally Done",
            "Took way too long!"
        )

    elif failure_mode == 4:
        # Mode 4: Crash after some time
        print("Simulating delayed crash...")
        await plugin.ui.show_notification(
            "⏰ Time Bomb Mode",
            "Will crash in 5 seconds..."
        )
        await asyncio.sleep(5)
        raise RuntimeError("Delayed crash - testing crash handler")

    else:
        # Mode 5: Actually behave (sometimes)
        print("Behaving normally this time...")
        await plugin.ui.show_notification(
            "😇 Behaving Mode",
            "Being nice for once!"
        )
        await asyncio.sleep(2)
        await plugin.ui.show_notification(
            "Goodbye! 👋",
            "That's all folks!"
        )

    print("Goodbye World plugin finished (if it made it this far)")


@plugin.on_deactivate()
async def on_deactivate():
    """Called when plugin is unloaded."""
    print("Goodbye World plugin deactivating...")

    try:
        await plugin.ui.show_notification(
            "👋 Shutting Down",
            "Goodbye World plugin is exiting"
        )
    except:
        pass  # Might not work if already crashed

    print("Goodbye World plugin deactivated")


if __name__ == "__main__":
    plugin.run()
