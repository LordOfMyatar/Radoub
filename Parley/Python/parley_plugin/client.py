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
    SelectNodeRequest,
    GetDialogStructureRequest,
    RegisterPanelRequest,
    UpdatePanelContentRequest,
    ClosePanelRequest,
    IsPanelOpenRequest,
    GetPanelSettingRequest,
    GetThemeRequest,
    GetSpeakerColorsRequest,
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

    def get_theme(self) -> dict:
        """
        Get the current theme settings from Parley.

        Returns:
            Dict with theme_id, theme_name, is_dark
        """
        try:
            request = GetThemeRequest()
            response = self.ui.GetTheme(request)
            return {
                "theme_id": response.theme_id,
                "theme_name": response.theme_name,
                "is_dark": response.is_dark,
            }
        except grpc.RpcError as e:
            print(f"gRPC error getting theme: {e}")
            return {"theme_id": "unknown", "theme_name": "unknown", "is_dark": True}

    def get_speaker_colors(self) -> dict:
        """
        Get the speaker color and shape settings from Parley.

        Returns:
            Dict with:
                - pc_color: Hex color for PC nodes (e.g., "#4FC3F7")
                - owner_color: Hex color for Owner/default NPC nodes
                - speaker_colors: Dict of speaker_tag -> hex color for named NPCs
                - pc_shape: Shape name for PC nodes (e.g., "Circle")
                - owner_shape: Shape name for Owner/default NPC nodes (e.g., "Square")
                - speaker_shapes: Dict of speaker_tag -> shape name for named NPCs
        """
        try:
            request = GetSpeakerColorsRequest()
            response = self.ui.GetSpeakerColors(request)
            return {
                "pc_color": response.pc_color,
                "owner_color": response.owner_color,
                "speaker_colors": dict(response.speaker_colors),
                "pc_shape": response.pc_shape if response.pc_shape else "Circle",
                "owner_shape": response.owner_shape if response.owner_shape else "Square",
                "speaker_shapes": dict(response.speaker_shapes) if response.speaker_shapes else {},
            }
        except grpc.RpcError as e:
            print(f"gRPC error getting speaker colors: {e}")
            return {
                "pc_color": "#4FC3F7",
                "owner_color": "#FF8A65",
                "speaker_colors": {},
                "pc_shape": "Circle",
                "owner_shape": "Square",
                "speaker_shapes": {},
            }

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

    def select_node(self, node_id: str) -> tuple:
        """
        Select a node in Parley's tree view (Epic 40 Phase 3 / #234).

        Args:
            node_id: Node ID to select (e.g., "entry_0", "reply_3", "root")

        Returns:
            Tuple of (success: bool, error_message: str)
        """
        try:
            request = SelectNodeRequest(node_id=node_id)
            response = self.dialog.SelectNode(request)
            return (response.success, response.error_message)
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return (False, str(e))

    def get_dialog_structure(self) -> dict:
        """
        Get the full dialog structure for flowchart visualization.

        Returns:
            Dict with keys:
                - success: bool
                - error_message: str (if not successful)
                - dialog_id: str
                - dialog_name: str
                - nodes: list of dicts with id, type, text, speaker, is_link, link_target,
                         has_condition, has_action, condition_script, action_script
                - links: list of dicts with source, target, has_condition, condition_script
        """
        try:
            request = GetDialogStructureRequest()
            response = self.dialog.GetDialogStructure(request)

            if not response.success:
                return {
                    "success": False,
                    "error_message": response.error_message,
                    "nodes": [],
                    "links": [],
                }

            # Phase 2: Include script indicator fields (#228-#232)
            nodes = [
                {
                    "id": node.id,
                    "type": node.type,
                    "text": node.text,
                    "speaker": node.speaker,
                    "is_link": node.is_link,
                    "link_target": node.link_target,
                    "has_condition": node.has_condition,
                    "has_action": node.has_action,
                    "condition_script": node.condition_script,
                    "action_script": node.action_script,
                }
                for node in response.nodes
            ]

            links = [
                {
                    "source": link.source,
                    "target": link.target,
                    "has_condition": link.has_condition,
                    "condition_script": link.condition_script,
                }
                for link in response.links
            ]

            return {
                "success": True,
                "dialog_id": response.dialog_id,
                "dialog_name": response.dialog_name,
                "nodes": nodes,
                "links": links,
            }
        except grpc.RpcError as e:
            print(f"gRPC error: {e}")
            return {
                "success": False,
                "error_message": str(e),
                "nodes": [],
                "links": [],
            }

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

    def is_panel_open(self, panel_id: str) -> bool:
        """
        Check if a panel is currently open (#235).

        Args:
            panel_id: ID of the panel to check

        Returns:
            True if panel window is open
        """
        try:
            request = IsPanelOpenRequest(panel_id=panel_id)
            response = self.ui.IsPanelOpen(request)
            return response.is_open
        except grpc.RpcError as e:
            # Connection error likely means Parley is closed
            return False

    def get_panel_setting(self, panel_id: str, setting_name: str) -> tuple:
        """
        Get a panel setting value from Parley UI (#235).

        Used to retrieve UI toggle states like sync_selection, auto_refresh
        that persist across page re-renders.

        Args:
            panel_id: ID of the panel
            setting_name: Name of the setting (e.g., "sync_selection", "auto_refresh")

        Returns:
            Tuple of (found: bool, value: str)
            - found: True if setting exists
            - value: Setting value, empty string if not found
        """
        try:
            request = GetPanelSettingRequest(panel_id=panel_id, setting_name=setting_name)
            response = self.ui.GetPanelSetting(request)
            return (response.found, response.value)
        except grpc.RpcError as e:
            # Connection error or API not available
            return (False, "")

    def close(self):
        """Close the gRPC channel."""
        if self.channel:
            self.channel.close()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()
