"""
gRPC client for Parley plugin communication.
"""

import os
import grpc
from .plugin_pb2 import (
    PlayAudioRequest,
    StopAudioRequest,
    ShowNotificationRequest,
    ShowDialogRequest,
    GetCurrentDialogRequest,
    GetSelectedNodeRequest,
)
from .plugin_pb2_grpc import (
    AudioServiceStub,
    UIServiceStub,
    DialogServiceStub,
    FileServiceStub,
)


class ParleyClient:
    """
    gRPC client for communicating with Parley.

    Usage:
        client = ParleyClient()
        client.show_notification("Hello", "Plugin started!")
    """

    def __init__(self):
        # Get gRPC port from environment variable set by Parley
        port = os.environ.get("PARLEY_GRPC_PORT")
        if not port:
            raise RuntimeError("PARLEY_GRPC_PORT environment variable not set")

        # Connect to Parley's gRPC server
        self.channel = grpc.insecure_channel(f"localhost:{port}")

        # Create service stubs
        self.audio = AudioServiceStub(self.channel)
        self.ui = UIServiceStub(self.channel)
        self.dialog = DialogServiceStub(self.channel)
        self.file = FileServiceStub(self.channel)

    def show_notification(self, title: str, message: str) -> bool:
        """
        Show a notification window in Parley.

        Args:
            title: Notification title
            message: Notification message

        Returns:
            True if successful
        """
        try:
            request = ShowNotificationRequest(title=title, message=message)
            response = self.ui.ShowNotification(request)
            return response.success
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return False

    def show_dialog(self, title: str, message: str, buttons: list = None) -> int:
        """
        Show a dialog with buttons in Parley.

        Args:
            title: Dialog title
            message: Dialog message
            buttons: List of button labels (optional)

        Returns:
            Index of clicked button, or -1 if error
        """
        try:
            request = ShowDialogRequest(
                title=title,
                message=message,
                buttons=buttons or []
            )
            response = self.ui.ShowDialog(request)
            return response.button_index
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return -1

    def play_audio(self, file_path: str) -> bool:
        """
        Request audio playback in Parley.

        Args:
            file_path: Path to audio file

        Returns:
            True if successful
        """
        try:
            request = PlayAudioRequest(file_path=file_path)
            response = self.audio.PlayAudio(request)
            return response.success
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return False

    def stop_audio(self) -> bool:
        """
        Stop audio playback in Parley.

        Returns:
            True if successful
        """
        try:
            request = StopAudioRequest()
            response = self.audio.StopAudio(request)
            return response.success
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return False

    def get_current_dialog(self) -> tuple:
        """
        Get the currently loaded dialog.

        Returns:
            Tuple of (dialog_id, dialog_name)
        """
        try:
            request = GetCurrentDialogRequest()
            response = self.dialog.GetCurrentDialog(request)
            return (response.dialog_id, response.dialog_name)
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return ("", "")

    def get_selected_node(self) -> tuple:
        """
        Get the currently selected dialog node.

        Returns:
            Tuple of (node_id, node_text)
        """
        try:
            request = GetSelectedNodeRequest()
            response = self.dialog.GetSelectedNode(request)
            return (response.node_id, response.node_text)
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return ("", "")

    def close(self):
        """Close the gRPC channel."""
        if self.channel:
            self.channel.close()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()
