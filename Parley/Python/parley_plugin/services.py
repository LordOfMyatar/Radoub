"""
Service wrappers for Parley plugin APIs
"""

from typing import Optional
import grpc
from . import plugin_pb2
from . import plugin_pb2_grpc


class AudioService:
    """Audio playback service"""

    def __init__(self, channel: grpc.Channel):
        self._stub = plugin_pb2_grpc.AudioServiceStub(channel)

    async def play(self, file_path: str) -> bool:
        """
        Play an audio file

        Args:
            file_path: Path to audio file

        Returns:
            True if successful

        Raises:
            grpc.RpcError: If permission denied or other error
        """
        try:
            response = await self._stub.PlayAudio(
                plugin_pb2.PlayAudioRequest(file_path=file_path)
            )
            return response.success
        except grpc.RpcError as e:
            if e.code() == grpc.StatusCode.PERMISSION_DENIED:
                raise PermissionError(f"Permission denied: audio.play") from e
            raise

    async def stop(self) -> bool:
        """
        Stop audio playback

        Returns:
            True if successful
        """
        response = await self._stub.StopAudio(plugin_pb2.StopAudioRequest())
        return response.success


class UIService:
    """UI service for notifications and dialogs"""

    def __init__(self, channel: grpc.Channel):
        self._stub = plugin_pb2_grpc.UIServiceStub(channel)

    async def show_notification(self, message: str, title: str = "") -> bool:
        """
        Show a notification to the user

        Args:
            message: Notification message
            title: Notification title

        Returns:
            True if successful
        """
        try:
            response = await self._stub.ShowNotification(
                plugin_pb2.ShowNotificationRequest(message=message, title=title)
            )
            return response.success
        except grpc.RpcError as e:
            if e.code() == grpc.StatusCode.PERMISSION_DENIED:
                raise PermissionError("Permission denied: ui.show_notification") from e
            raise

    async def show_dialog(
        self, message: str, title: str = "", buttons: Optional[list[str]] = None
    ) -> int:
        """
        Show a dialog with buttons

        Args:
            message: Dialog message
            title: Dialog title
            buttons: List of button labels (default: ["OK"])

        Returns:
            Index of button clicked

        Raises:
            PermissionError: If permission denied
        """
        if buttons is None:
            buttons = ["OK"]

        try:
            response = await self._stub.ShowDialog(
                plugin_pb2.ShowDialogRequest(
                    message=message, title=title, buttons=buttons
                )
            )
            return response.button_index
        except grpc.RpcError as e:
            if e.code() == grpc.StatusCode.PERMISSION_DENIED:
                raise PermissionError("Permission denied: ui.show_dialog") from e
            raise


class DialogService:
    """Dialog data access service"""

    def __init__(self, channel: grpc.Channel):
        self._stub = plugin_pb2_grpc.DialogServiceStub(channel)

    async def get_current_dialog(self) -> tuple[str, str]:
        """
        Get the currently loaded dialog

        Returns:
            Tuple of (dialog_id, dialog_name)

        Raises:
            PermissionError: If permission denied
        """
        try:
            response = await self._stub.GetCurrentDialog(
                plugin_pb2.GetCurrentDialogRequest()
            )
            return (response.dialog_id, response.dialog_name)
        except grpc.RpcError as e:
            if e.code() == grpc.StatusCode.PERMISSION_DENIED:
                raise PermissionError("Permission denied: dialog.read") from e
            raise

    async def get_selected_node(self) -> tuple[str, str]:
        """
        Get the currently selected node

        Returns:
            Tuple of (node_id, node_text)

        Raises:
            PermissionError: If permission denied
        """
        try:
            response = await self._stub.GetSelectedNode(
                plugin_pb2.GetSelectedNodeRequest()
            )
            return (response.node_id, response.node_text)
        except grpc.RpcError as e:
            if e.code() == grpc.StatusCode.PERMISSION_DENIED:
                raise PermissionError("Permission denied: dialog.read") from e
            raise


class FileService:
    """File access service (sandboxed)"""

    def __init__(self, channel: grpc.Channel):
        self._stub = plugin_pb2_grpc.FileServiceStub(channel)

    async def open_file_dialog(self, title: str = "", file_filter: str = "") -> Optional[str]:
        """
        Show file open dialog

        Args:
            title: Dialog title
            file_filter: File filter (e.g., "*.txt")

        Returns:
            Selected file path, or None if cancelled

        Raises:
            PermissionError: If permission denied
        """
        try:
            response = await self._stub.OpenFileDialog(
                plugin_pb2.OpenFileDialogRequest(title=title, filter=file_filter)
            )
            return None if response.cancelled else response.file_path
        except grpc.RpcError as e:
            if e.code() == grpc.StatusCode.PERMISSION_DENIED:
                raise PermissionError("Permission denied: file.dialog") from e
            raise

    async def save_file_dialog(
        self, title: str = "", file_filter: str = "", default_name: str = ""
    ) -> Optional[str]:
        """
        Show file save dialog

        Args:
            title: Dialog title
            file_filter: File filter
            default_name: Default filename

        Returns:
            Selected file path, or None if cancelled
        """
        try:
            response = await self._stub.SaveFileDialog(
                plugin_pb2.SaveFileDialogRequest(
                    title=title, filter=file_filter, default_name=default_name
                )
            )
            return None if response.cancelled else response.file_path
        except grpc.RpcError as e:
            if e.code() == grpc.StatusCode.PERMISSION_DENIED:
                raise PermissionError("Permission denied: file.dialog") from e
            raise

    async def read_file(self, file_path: str) -> bytes:
        """
        Read a file (sandboxed to plugin data directory)

        Args:
            file_path: Path to file (relative or absolute within sandbox)

        Returns:
            File contents as bytes

        Raises:
            PermissionError: If permission denied
            FileNotFoundError: If file not found
        """
        try:
            response = await self._stub.ReadFile(
                plugin_pb2.ReadFileRequest(file_path=file_path)
            )
            if not response.success:
                if "not found" in response.error_message.lower():
                    raise FileNotFoundError(response.error_message)
                raise IOError(response.error_message)
            return response.content
        except grpc.RpcError as e:
            if e.code() == grpc.StatusCode.PERMISSION_DENIED:
                raise PermissionError("Permission denied: file.read") from e
            raise

    async def write_file(self, file_path: str, content: bytes) -> bool:
        """
        Write a file (sandboxed to plugin data directory)

        Args:
            file_path: Path to file (relative or absolute within sandbox)
            content: File contents as bytes

        Returns:
            True if successful

        Raises:
            PermissionError: If permission denied
        """
        try:
            response = await self._stub.WriteFile(
                plugin_pb2.WriteFileRequest(file_path=file_path, content=content)
            )
            if not response.success:
                raise IOError(response.error_message)
            return response.success
        except grpc.RpcError as e:
            if e.code() == grpc.StatusCode.PERMISSION_DENIED:
                raise PermissionError("Permission denied: file.write") from e
            raise
