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
    RegisterPanelRequest,
    UpdatePanelContentRequest,
    ClosePanelRequest,
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

    def register_panel(
        self,
        panel_id: str,
        title: str,
        position: str = "right",
        render_mode: str = "webview",
        initial_width: int = 0,
        initial_height: int = 0,
        can_float: bool = True,
        can_close: bool = True,
    ) -> tuple:
        """
        Register a dockable/floating panel in Parley.

        Args:
            panel_id: Unique identifier for the panel
            title: Display title for the panel
            position: Panel position - "left", "right", "bottom", or "float"
            render_mode: Rendering mode - "webview" (recommended) or "native"
            initial_width: Initial width in pixels (0 = auto)
            initial_height: Initial height in pixels (0 = auto)
            can_float: Whether panel can be undocked to floating window
            can_close: Whether user can close the panel

        Returns:
            Tuple of (success: bool, error_message: str, actual_panel_id: str)
        """
        try:
            request = RegisterPanelRequest(
                panel_id=panel_id,
                title=title,
                position=position,
                render_mode=render_mode,
                initial_width=initial_width,
                initial_height=initial_height,
                can_float=can_float,
                can_close=can_close,
            )
            response = self.ui.RegisterPanel(request)
            return (response.success, response.error_message, response.actual_panel_id)
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return (False, str(e), "")

    def update_panel_content(
        self, panel_id: str, content_type: str, content: str
    ) -> tuple:
        """
        Update the content of a registered panel.

        Args:
            panel_id: ID of the panel to update
            content_type: Type of content - "html", "url", or "json"
            content: HTML string, URL, or JSON descriptor depending on content_type

        Returns:
            Tuple of (success: bool, error_message: str)
        """
        try:
            request = UpdatePanelContentRequest(
                panel_id=panel_id,
                content_type=content_type,
                content=content,
            )
            response = self.ui.UpdatePanelContent(request)
            return (response.success, response.error_message)
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return (False, str(e))

    def close_panel(self, panel_id: str) -> bool:
        """
        Close a registered panel.

        Args:
            panel_id: ID of the panel to close

        Returns:
            True if successful
        """
        try:
            request = ClosePanelRequest(panel_id=panel_id)
            response = self.ui.ClosePanel(request)
            return response.success
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return False

    def close(self):
        """Close the gRPC channel."""
        if self.channel:
            self.channel.close()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()
