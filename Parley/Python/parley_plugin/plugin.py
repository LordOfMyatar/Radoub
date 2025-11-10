"""
Base plugin class and utilities
"""

import sys
import argparse
import asyncio
import logging
from typing import Optional
from abc import ABC, abstractmethod

import grpc
from . import plugin_pb2
from . import plugin_pb2_grpc
from .services import AudioService, UIService, DialogService, FileService


class PluginBase(ABC):
    """
    Base class for Parley plugins
    """

    def __init__(self):
        self.logger = logging.getLogger(self.__class__.__name__)
        self._pipe_name: Optional[str] = None
        self._channel: Optional[grpc.Channel] = None

        # Services (initialized in connect())
        self.audio: Optional[AudioService] = None
        self.ui: Optional[UIService] = None
        self.dialog: Optional[DialogService] = None
        self.file: Optional[FileService] = None

    @abstractmethod
    async def on_initialize(self) -> bool:
        """
        Called when plugin is initialized.
        Override this to perform setup tasks.

        Returns:
            True if initialization successful, False otherwise
        """
        pass

    @abstractmethod
    async def on_shutdown(self, reason: str):
        """
        Called when plugin is shutting down.
        Override this to perform cleanup tasks.

        Args:
            reason: Reason for shutdown
        """
        pass

    async def on_dialog_changed(self, dialog_id: str, change_type: str):
        """
        Called when the current dialog changes.
        Override this to react to dialog changes.

        Args:
            dialog_id: ID of the changed dialog
            change_type: Type of change (e.g., "loaded", "saved", "modified")
        """
        pass

    async def on_node_selected(self, node_id: str):
        """
        Called when a dialog node is selected.
        Override this to react to node selection.

        Args:
            node_id: ID of the selected node
        """
        pass

    async def connect(self, pipe_name: str) -> bool:
        """
        Connect to Parley host via gRPC

        Args:
            pipe_name: Named pipe for communication

        Returns:
            True if connected successfully
        """
        self._pipe_name = pipe_name

        try:
            # Determine pipe address based on platform
            if sys.platform == "win32":
                address = f"npipe://{pipe_name}"
            else:
                address = f"unix:/tmp/{pipe_name}"

            self.logger.info(f"Connecting to {address}")

            # Create gRPC channel
            self._channel = grpc.aio.insecure_channel(address)

            # Test connection with ping
            stub = plugin_pb2_grpc.PluginStub(self._channel)
            response = await stub.Ping(plugin_pb2.PingRequest(timestamp=0))

            if response.status != "ok":
                self.logger.error(f"Ping failed: {response.status}")
                return False

            # Initialize service wrappers
            self.audio = AudioService(self._channel)
            self.ui = UIService(self._channel)
            self.dialog = DialogService(self._channel)
            self.file = FileService(self._channel)

            self.logger.info("Connected to Parley")
            return True

        except Exception as e:
            self.logger.error(f"Connection failed: {e}")
            return False

    async def disconnect(self):
        """Disconnect from Parley host"""
        if self._channel:
            await self._channel.close()
            self._channel = None

    async def run(self):
        """
        Main plugin run loop.
        Handles connection, initialization, and event loop.
        """
        # Parse command line args
        parser = argparse.ArgumentParser()
        parser.add_argument("--pipe", required=True, help="Named pipe for communication")
        args = parser.parse_args()

        # Connect to host
        if not await self.connect(args.pipe):
            self.logger.error("Failed to connect to Parley")
            return 1

        # Initialize plugin
        try:
            if not await self.on_initialize():
                self.logger.error("Plugin initialization failed")
                await self.disconnect()
                return 1
        except Exception as e:
            self.logger.error(f"Exception during initialization: {e}")
            await self.disconnect()
            return 1

        self.logger.info("Plugin initialized successfully")

        # Keep alive loop (wait for shutdown signal)
        try:
            # In a real implementation, this would handle events from Parley
            # For now, just keep the plugin alive
            while True:
                await asyncio.sleep(1)
        except KeyboardInterrupt:
            self.logger.info("Received shutdown signal")
        except Exception as e:
            self.logger.error(f"Error in main loop: {e}")
        finally:
            await self.on_shutdown("shutdown")
            await self.disconnect()

        return 0


# Convenience alias
Plugin = PluginBase


def run_plugin(plugin_class):
    """
    Run a plugin class

    Args:
        plugin_class: Plugin class to instantiate and run

    Example:
        if __name__ == "__main__":
            run_plugin(MyPlugin)
    """
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    )

    plugin = plugin_class()
    exit_code = asyncio.run(plugin.run())
    sys.exit(exit_code)
