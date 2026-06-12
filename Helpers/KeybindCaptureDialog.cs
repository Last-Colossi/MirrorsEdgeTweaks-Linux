using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MirrorsEdgeTweaks.Helpers
{
    /// <summary>
    /// Captures a single key, mouse button, or scrollwheel input and closes the hosting
    /// DialogHost with the corresponding UE3 key name. Mirrors the WPF original:
    /// Escape cancels (null), Backspace/Delete clears (empty string).
    /// </summary>
    public class KeybindCaptureDialog : UserControl
    {
        private readonly Dictionary<string, string> _ue3KeyMap;

        public KeybindCaptureDialog(Dictionary<string, string> ue3KeyMap)
        {
            _ue3KeyMap = ue3KeyMap;
            Focusable = true;

            var rootBorder = DialogChrome.CreateRoot();
            var stack = new StackPanel();

            stack.Children.Add(DialogChrome.CreateTitle("Set Keybind"));
            stack.Children.Add(new TextBlock
            {
                Text = "Press any key, mouse button, or scrollwheel.\n\nPress Escape to cancel.\n\nPress Backspace or Delete to clear.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = DialogChrome.TextBrush,
                Margin = new Thickness(0, 0, 0, 16),
                MaxWidth = 450
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var clearButton = new Button { Content = "Clear", Margin = new Thickness(0, 0, 8, 0) };
            clearButton.Click += (s, e) => DialogHost.Close("");

            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (s, e) => DialogHost.Close(null);

            buttonPanel.Children.Add(clearButton);
            buttonPanel.Children.Add(cancelButton);

            stack.Children.Add(buttonPanel);
            rootBorder.Child = stack;
            Content = rootBorder;

            AttachedToVisualTree += (s, e) =>
            {
                Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
            };

            AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
            AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);
            AddHandler(PointerWheelChangedEvent, OnPreviewPointerWheel, RoutingStrategies.Tunnel);
        }

        private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            e.Handled = true;

            var key = e.Key;

            if (key == Key.Escape)
            {
                DialogHost.Close(null);
                return;
            }

            if (key == Key.Back || key == Key.Delete)
            {
                DialogHost.Close("");
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            string keyString = key.ToString();
            string ue3Key;

            if (_ue3KeyMap.TryGetValue(keyString, out ue3Key!))
            {
            }
            else if (keyString.Length == 1 && char.IsLetter(keyString[0]))
            {
                ue3Key = keyString.ToUpper();
            }
            else
            {
                return;
            }

            DialogHost.Close(ue3Key);
        }

        private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (IsWithinButton(e.Source as Visual))
                return;

            e.Handled = true;

            var props = e.GetCurrentPoint(this).Properties;
            string ue3Key;

            if (props.IsLeftButtonPressed)
                ue3Key = "LeftMouseButton";
            else if (props.IsRightButtonPressed)
                ue3Key = "RightMouseButton";
            else if (props.IsMiddleButtonPressed)
                ue3Key = "MiddleMouseButton";
            else if (props.IsXButton1Pressed)
                ue3Key = "ThumbMouseButton";
            else if (props.IsXButton2Pressed)
                ue3Key = "ThumbMouseButton2";
            else
                return;

            DialogHost.Close(ue3Key);
        }

        private void OnPreviewPointerWheel(object? sender, PointerWheelEventArgs e)
        {
            e.Handled = true;
            string ue3Key = e.Delta.Y > 0 ? "MouseScrollUp" : "MouseScrollDown";
            DialogHost.Close(ue3Key);
        }

        private static bool IsWithinButton(Visual? source)
        {
            var current = source;
            while (current != null)
            {
                if (current is Button)
                    return true;
                current = current.GetVisualParent();
            }
            return false;
        }
    }
}
