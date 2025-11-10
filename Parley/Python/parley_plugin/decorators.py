"""
Decorators for plugin development
"""

import functools
from typing import Callable


def requires_permission(permission: str) -> Callable:
    """
    Decorator to mark methods that require specific permissions.

    Note: Permission checking is enforced on the C# side.
    This decorator is primarily for documentation and future client-side checks.

    Args:
        permission: Required permission (e.g., "audio.play", "ui.show_dialog")

    Example:
        @requires_permission("audio.play")
        async def play_sound(self, path: str):
            await self.audio.play(path)
    """
    def decorator(func: Callable) -> Callable:
        @functools.wraps(func)
        async def wrapper(*args, **kwargs):
            # Future: Could add client-side permission tracking here
            return await func(*args, **kwargs)

        # Attach permission metadata
        wrapper._required_permission = permission
        return wrapper

    return decorator


def event_handler(event_type: str) -> Callable:
    """
    Decorator to mark event handler methods.

    Args:
        event_type: Type of event (e.g., "dialog_changed", "node_selected")

    Example:
        @event_handler("dialog_changed")
        async def handle_dialog_change(self, dialog_id: str, change_type: str):
            print(f"Dialog {dialog_id} changed: {change_type}")
    """
    def decorator(func: Callable) -> Callable:
        @functools.wraps(func)
        async def wrapper(*args, **kwargs):
            return await func(*args, **kwargs)

        # Attach event metadata
        wrapper._event_type = event_type
        return wrapper

    return decorator
