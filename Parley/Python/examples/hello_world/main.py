"""
Hello World Plugin - Simple demonstration of Parley plugin capabilities.

Shows a welcome notification when activated and a goodbye when deactivated.
"""

from parley_plugin import ParleyPlugin
import asyncio

plugin = ParleyPlugin()


@plugin.on_activate()
async def on_activate():
    """Called when plugin is loaded."""
    print("Hello World plugin activating...")

    await plugin.ui.show_notification(
        "Hello World! 👋",
        "The Hello World plugin is now active and ready to use!"
    )

    # Wait a moment, then show another message
    await asyncio.sleep(2)

    await plugin.ui.show_notification(
        "Plugin Status",
        "Everything is working perfectly!"
    )

    print("Hello World plugin activated successfully")


@plugin.on_deactivate()
async def on_deactivate():
    """Called when plugin is unloaded."""
    print("Hello World plugin deactivating...")

    await plugin.ui.show_notification(
        "Goodbye! 👋",
        "Hello World plugin is shutting down."
    )

    print("Hello World plugin deactivated")


if __name__ == "__main__":
    plugin.run()
