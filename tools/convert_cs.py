#!/usr/bin/env python3
"""Transform the WPF MainWindow.xaml.cs into Avalonia MainWindow.axaml.cs."""
import re

SRC = 'MirrorsEdgeTweaks-original/MirrorsEdgeTweaks/MainWindow.xaml.cs'
DST = 'MirrorsEdgeTweaksLinux/MainWindow.axaml.cs'

src = open(SRC).read()

# --- using directives ---
src = src.replace(
    'using System.Windows;\nusing System.Windows.Controls;\nusing System.Windows.Media;',
    'using Avalonia;\nusing Avalonia.Controls;\nusing Avalonia.Controls.Primitives;\n'
    'using Avalonia.Input;\nusing Avalonia.Interactivity;\nusing Avalonia.Layout;\n'
    'using Avalonia.Media;\nusing Avalonia.Platform.Storage;\nusing Avalonia.Threading;\n'
    'using Avalonia.VisualTree;\nusing MirrorsEdgeTweaks.Platform;')

# --- MaterialDesign DialogHost -> our shim ---
src = src.replace('MaterialDesignThemes.Wpf.DialogHost', 'Helpers.DialogHost')

# --- brushes / media ---
src = src.replace('System.Windows.Media.Brushes', 'Brushes')
src = src.replace('System.Windows.Media.Brush', 'IBrush')
src = src.replace('System.Windows.Media.Color', 'Color')
src = src.replace('System.Windows.Media.SolidColorBrush', 'SolidColorBrush')
src = re.sub(r'\bFontWeights\.', 'FontWeight.', src)

# --- Visibility -> IsVisible ---
src = re.sub(r'([\w.\[\]]+)\.Visibility\s*=\s*Visibility\.Visible', r'\1.IsVisible = true', src)
src = re.sub(r'([\w.\[\]]+)\.Visibility\s*=\s*Visibility\.(Collapsed|Hidden)', r'\1.IsVisible = false', src)
src = re.sub(r'([\w.\[\]]+)\.Visibility\s*==\s*Visibility\.Visible', r'\1.IsVisible', src)
src = re.sub(r'([\w.\[\]]+)\.Visibility\s*!=\s*Visibility\.Visible', r'!\1.IsVisible', src)
src = re.sub(r'([\w.\[\]]+)\.Visibility\s*==\s*Visibility\.(Collapsed|Hidden)', r'!\1.IsVisible', src)

# --- dispatcher ---
src = src.replace('System.Windows.Application.Current.Dispatcher.InvokeAsync', 'Dispatcher.UIThread.InvokeAsync')
src = src.replace('Application.Current.Dispatcher.InvokeAsync', 'Dispatcher.UIThread.InvokeAsync')
src = src.replace('Dispatcher.Invoke(', 'Dispatcher.UIThread.Invoke(')
src = src.replace('Dispatcher.BeginInvoke(', 'Dispatcher.UIThread.Post(')
src = src.replace('Dispatcher.InvokeAsync(', 'Dispatcher.UIThread.InvokeAsync(')
src = src.replace('Dispatcher.UIThread.UIThread', 'Dispatcher.UIThread')  # de-dupe if double-applied

# --- System.Windows.Controls.* / System.Windows.* type prefixes ---
src = src.replace('System.Windows.Controls.Primitives.', 'Avalonia.Controls.Primitives.')
src = src.replace('System.Windows.Controls.', '')
src = src.replace('System.Windows.Input.MouseButtonEventArgs', 'PointerPressedEventArgs')
src = src.replace('System.Windows.Input.MouseWheelEventArgs', 'PointerWheelEventArgs')
src = src.replace('System.Windows.Threading.DispatcherPriority', 'DispatcherPriority')
src = src.replace('System.Windows.RoutedPropertyChangedEventArgs<double>', 'RangeBaseValueChangedEventArgs')
src = src.replace('RoutedPropertyChangedEventArgs<double>', 'RangeBaseValueChangedEventArgs')
src = src.replace('System.Windows.HorizontalAlignment', 'HorizontalAlignment')
src = src.replace('System.Windows.VerticalAlignment', 'VerticalAlignment')
src = src.replace('System.Windows.Thickness', 'Thickness')
src = src.replace('System.Windows.CornerRadius', 'CornerRadius')
src = src.replace('System.Windows.TextWrapping', 'TextWrapping')
src = src.replace('System.Windows.Application.Current', 'Avalonia.Application.Current')
src = src.replace('System.Windows.MessageBox', 'Helpers.DialogHelper /* MessageBox */')

# --- drop FindResource style assignments on dynamically built buttons ---
src = re.sub(r'\s*Style = \(Style\)Avalonia\.Application\.Current\.FindResource\("[^"]+"\),?', '', src)
src = re.sub(r'\s*Style = \(Style\)[\w.]*Application\.Current\.FindResource\("[^"]+"\),?', '', src)

# --- Documents path -> Proton prefix ---
src = src.replace('Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)',
                  'SteamEnvironment.DocumentsPath')

open(DST, 'w').write(src)
print('written', DST)
