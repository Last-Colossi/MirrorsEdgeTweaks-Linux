using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace MirrorsEdgeTweaks.Helpers
{
    /// <summary>
    /// Avalonia stand-in for MaterialDesignThemes' DialogHost. Shows arbitrary content
    /// in a modal, borderless window centered over the main window, and resolves the
    /// returned task with whatever value the content passes to CloseDialogCommand.
    /// </summary>
    public static class DialogHost
    {
        private static TaskCompletionSource<object?>? _tcs;
        private static Window? _dialogWindow;

        public static readonly DialogCloseCommand CloseDialogCommand = new();

        public class DialogCloseCommand
        {
            public void Execute(object? result, object? _) => Close(result);
        }

        public static async Task<object?> Show(object content, string identifier = "RootDialog")
        {
            var owner = GetMainWindow();
            if (owner == null)
            {
                return null;
            }

            _tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcs = _tcs;

            _dialogWindow = new Window
            {
                Content = content,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.None,
                CanResize = false,
                ShowInTaskbar = false,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                Background = Brushes.Transparent
            };
            _dialogWindow.Closed += (s, e) => tcs.TrySetResult(null);

            await _dialogWindow.ShowDialog(owner);
            return await tcs.Task;
        }

        public static void Close(object? result)
        {
            _tcs?.TrySetResult(result);
            var window = _dialogWindow;
            _dialogWindow = null;
            window?.Close();
        }

        private static Window? GetMainWindow() =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }

    public static class DialogHelper
    {
        private static bool _isDialogOpen = false;
        private static readonly object _dialogLock = new object();

        public static async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            lock (_dialogLock)
            {
                if (_isDialogOpen)
                {
                    return false;
                }
                _isDialogOpen = true;
            }

            try
            {
                var result = await DialogHost.Show(new ConfirmationDialog(title, message), "RootDialog");
                return result is bool boolResult && boolResult;
            }
            finally
            {
                lock (_dialogLock)
                {
                    _isDialogOpen = false;
                }
            }
        }

        public static async Task ShowMessageAsync(string title, string message, MessageType messageType = MessageType.Information)
        {
            lock (_dialogLock)
            {
                if (_isDialogOpen)
                {
                    return;
                }
                _isDialogOpen = true;
            }

            try
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await DialogHost.Show(new MessageDialog(title, message, messageType), "RootDialog");
                });
            }
            finally
            {
                lock (_dialogLock)
                {
                    _isDialogOpen = false;
                }
            }
        }

        public static async void ShowMessage(string title, string message, MessageType messageType = MessageType.Information)
        {
            await ShowMessageAsync(title, message, messageType);
        }

        public enum MessageType
        {
            Information,
            Warning,
            Error,
            Success
        }
    }

    internal static class DialogChrome
    {
        public static readonly IBrush Background = Brushes.White;
        public static readonly IBrush BorderBrush = Brushes.LightGray;
        public static readonly IBrush TitleBrush = new SolidColorBrush(Color.Parse("#DD000000"));
        public static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#DD000000"));

        public static Border CreateRoot()
        {
            return new Border
            {
                BorderBrush = BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Background = Background,
                Padding = new Thickness(20),
                MaxWidth = 520,
                MinWidth = 300
            };
        }

        public static TextBlock CreateTitle(string title)
        {
            return new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = TitleBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
        }

        /// <summary>
        /// Renders the message body; URLs become clickable link buttons below the text,
        /// mirroring the hyperlink support in the WPF original.
        /// </summary>
        public static Control CreateMessageBody(string message)
        {
            var panel = new StackPanel();

            panel.Children.Add(new SelectableTextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = TextBrush,
                Margin = new Thickness(0, 0, 0, 16),
                MaxWidth = 470
            });

            foreach (Match match in Regex.Matches(message, @"(https?://[^\s]+)"))
            {
                string url = match.Value.TrimEnd('.', ')', ',');
                var link = new Button
                {
                    Content = url,
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.Parse("#1976D2")),
                    Padding = new Thickness(0, 0, 0, 8),
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                };
                link.Click += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "xdg-open",
                            Arguments = url,
                            UseShellExecute = false
                        });
                    }
                    catch
                    {
                        // Opening a browser is best-effort.
                    }
                };
                panel.Children.Add(link);
            }

            return panel;
        }
    }

    public class ConfirmationDialog : UserControl
    {
        public ConfirmationDialog(string title, string message)
        {
            var border = DialogChrome.CreateRoot();
            var stackPanel = new StackPanel();

            stackPanel.Children.Add(DialogChrome.CreateTitle(title));
            stackPanel.Children.Add(DialogChrome.CreateMessageBody(message));

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var yesButton = new Button { Content = "Yes", Margin = new Thickness(0, 0, 8, 0) };
            yesButton.Classes.Add("raised");
            yesButton.Click += (s, e) => DialogHost.Close(true);

            var noButton = new Button { Content = "No" };
            noButton.Click += (s, e) => DialogHost.Close(false);

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            stackPanel.Children.Add(buttonPanel);

            border.Child = stackPanel;
            Content = border;
        }
    }

    public class MessageDialog : UserControl
    {
        public MessageDialog(string title, string message, DialogHelper.MessageType messageType)
        {
            var border = DialogChrome.CreateRoot();
            var stackPanel = new StackPanel();

            stackPanel.Children.Add(DialogChrome.CreateTitle(title));
            stackPanel.Children.Add(DialogChrome.CreateMessageBody(message));

            var okButton = new Button
            {
                Content = "OK",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            okButton.Classes.Add("raised");
            okButton.Click += (s, e) => DialogHost.Close(null);

            stackPanel.Children.Add(okButton);
            border.Child = stackPanel;
            Content = border;
        }
    }
}
