"""
Minimal Example Plugin for Parley

This is the simplest possible plugin that demonstrates the basic structure.
"""

from parley_plugin import Plugin, run_plugin


class MinimalPlugin(Plugin):
    """A minimal plugin that just logs initialization and shutdown"""

    async def on_initialize(self):
        """Initialize the plugin"""
        self.logger.info("Minimal plugin initialized!")
        self.logger.info(f"Services available: audio={self.audio is not None}")
        return True

    async def on_shutdown(self, reason):
        """Clean up when shutting down"""
        self.logger.info(f"Shutting down: {reason}")


if __name__ == "__main__":
    run_plugin(MinimalPlugin)
