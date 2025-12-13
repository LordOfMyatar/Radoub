using System;
using System.Collections.Generic;
using Avalonia.Controls;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Utility class to eliminate repetitive FindControl null-check patterns.
    /// Provides safe access to named controls with fluent API for common operations.
    ///
    /// Issue #342 - Reduces ~109 FindControl calls with null checks to cleaner patterns.
    ///
    /// Usage patterns:
    /// 1. WithControl - Execute action only if control exists
    ///    _controls.WithControl&lt;TextBox&gt;("Name", tb => tb.Text = value);
    ///
    /// 2. WithControls - Execute action with multiple controls (all must exist)
    ///    _controls.WithControls&lt;TextBox, Button&gt;("Name", "Save", (tb, btn) => { ... });
    ///
    /// 3. Get - Get control with null propagation
    ///    var text = _controls.Get&lt;TextBox&gt;("Name")?.Text;
    /// </summary>
    public class SafeControlFinder
    {
        private readonly Control _root;
        private readonly Dictionary<string, Control?> _cache = new();
        private bool _cachingEnabled;

        /// <summary>
        /// Creates a new SafeControlFinder for the given root control.
        /// </summary>
        /// <param name="root">The root control (typically a Window) to search within.</param>
        /// <param name="enableCaching">
        /// If true, caches control lookups. Only use for controls that exist for the lifetime of the root.
        /// Default is false for safety with dynamic controls.
        /// </param>
        public SafeControlFinder(Control root, bool enableCaching = false)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _cachingEnabled = enableCaching;
        }

        /// <summary>
        /// Gets a control by name, returning null if not found or if type doesn't match.
        /// Use for optional chaining: _controls.Get&lt;TextBox&gt;("Name")?.Text
        /// </summary>
        public T? Get<T>(string name) where T : Control
        {
            if (_cachingEnabled && _cache.TryGetValue(name, out var cached))
            {
                return cached as T;
            }

            T? control;
            try
            {
                control = _root.FindControl<T>(name);
            }
            catch (InvalidOperationException)
            {
                // Avalonia throws when control exists but type doesn't match
                return null;
            }

            if (_cachingEnabled)
            {
                _cache[name] = control;
            }

            return control;
        }

        /// <summary>
        /// Executes an action on a control if it exists.
        /// Eliminates the common pattern:
        ///   var ctrl = this.FindControl&lt;T&gt;("Name");
        ///   if (ctrl != null) { ... }
        /// </summary>
        /// <returns>True if control was found and action executed, false otherwise.</returns>
        public bool WithControl<T>(string name, Action<T> action) where T : Control
        {
            var control = Get<T>(name);
            if (control != null)
            {
                action(control);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Executes an action on two controls if both exist.
        /// Useful for coordinated updates (e.g., textbox + button enable state).
        /// </summary>
        /// <returns>True if both controls were found and action executed, false otherwise.</returns>
        public bool WithControls<T1, T2>(string name1, string name2, Action<T1, T2> action)
            where T1 : Control
            where T2 : Control
        {
            var control1 = Get<T1>(name1);
            var control2 = Get<T2>(name2);
            if (control1 != null && control2 != null)
            {
                action(control1, control2);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Executes an action on three controls if all exist.
        /// </summary>
        public bool WithControls<T1, T2, T3>(string name1, string name2, string name3, Action<T1, T2, T3> action)
            where T1 : Control
            where T2 : Control
            where T3 : Control
        {
            var control1 = Get<T1>(name1);
            var control2 = Get<T2>(name2);
            var control3 = Get<T3>(name3);
            if (control1 != null && control2 != null && control3 != null)
            {
                action(control1, control2, control3);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Executes an action on four controls if all exist.
        /// </summary>
        public bool WithControls<T1, T2, T3, T4>(string name1, string name2, string name3, string name4, Action<T1, T2, T3, T4> action)
            where T1 : Control
            where T2 : Control
            where T3 : Control
            where T4 : Control
        {
            var control1 = Get<T1>(name1);
            var control2 = Get<T2>(name2);
            var control3 = Get<T3>(name3);
            var control4 = Get<T4>(name4);
            if (control1 != null && control2 != null && control3 != null && control4 != null)
            {
                action(control1, control2, control3, control4);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the Text property of a TextBox if it exists.
        /// Common shorthand for: WithControl&lt;TextBox&gt;(name, tb => tb.Text = value)
        /// </summary>
        public bool SetText(string name, string? value)
        {
            return WithControl<TextBox>(name, tb => tb.Text = value ?? "");
        }

        /// <summary>
        /// Gets the Text property of a TextBox if it exists.
        /// </summary>
        public string? GetText(string name)
        {
            return Get<TextBox>(name)?.Text;
        }

        /// <summary>
        /// Sets the IsChecked property of a CheckBox if it exists.
        /// </summary>
        public bool SetChecked(string name, bool? value)
        {
            return WithControl<CheckBox>(name, cb => cb.IsChecked = value);
        }

        /// <summary>
        /// Gets the IsChecked property of a CheckBox if it exists.
        /// </summary>
        public bool? GetChecked(string name)
        {
            return Get<CheckBox>(name)?.IsChecked;
        }

        /// <summary>
        /// Sets the SelectedIndex of a ComboBox if it exists.
        /// </summary>
        public bool SetSelectedIndex(string name, int index)
        {
            return WithControl<ComboBox>(name, cb => cb.SelectedIndex = index);
        }

        /// <summary>
        /// Sets the IsEnabled property of any control if it exists.
        /// </summary>
        public bool SetEnabled(string name, bool enabled)
        {
            return WithControl<Control>(name, c => c.IsEnabled = enabled);
        }

        /// <summary>
        /// Sets the IsVisible property of any control if it exists.
        /// </summary>
        public bool SetVisible(string name, bool visible)
        {
            return WithControl<Control>(name, c => c.IsVisible = visible);
        }

        /// <summary>
        /// Clears the control cache. Call this if controls may have been recreated.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Enables or disables caching at runtime.
        /// </summary>
        public void SetCachingEnabled(bool enabled)
        {
            _cachingEnabled = enabled;
            if (!enabled)
            {
                _cache.Clear();
            }
        }
    }
}
