using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using Button = System.Windows.Controls.Button;
using Application = System.Windows.Application;
using Orientation = System.Windows.Controls.Orientation;
using Image = System.Windows.Controls.Image;

namespace CustomShell
{
    public class TaskbarGroup
    {
        public string? AppName { get; set; }
        public string? ExePath { get; set; }
        public List<IntPtr> Windows { get; set; } = new List<IntPtr>();
    }

    public class PreviewWindowModel
    {
        public string? Title { get; set; }
        public IntPtr Hwnd { get; set; }
        public int ProcessId { get; set; }
        public System.Windows.Media.ImageSource? Thumbnail { get; set; }
    }

    public partial class MainWindow : Window
    {
        private DispatcherTimer? timer;
        private Button? _hoveredButton;
        private DispatcherTimer HoverTimer;
        private ShellConfig? _currentConfig;
        private bool _isContextMenuOpen = false;

        public ObservableCollection<ShortcutItem>? StartMenuShortcuts { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadShortcuts();
            
            HoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            HoverTimer.Tick += HoverTimer_Tick;
            _currentConfig = ConfigManager.LoadConfig();
        }

        private void LoadShortcuts()
        {
            var items = ConfigManager.LoadShortcuts();
            foreach (var item in items)
            {
                var iconPath = !string.IsNullOrWhiteSpace(item.IconPath) ? item.IconPath : item.FilePath;
                if (!string.IsNullOrWhiteSpace(iconPath))
                {
                    item.Icon = GetIconForFile(iconPath);
                }
            }
            StartMenuShortcuts = new ObservableCollection<ShortcutItem>(items);
            if (StartMenuItemsControl != null)
            {
                StartMenuItemsControl.ItemsSource = StartMenuShortcuts;
            }
        }

        private const int WM_MOUSEHWHEEL = 0x020E;

        protected override void OnSourceInitialized(System.EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (PresentationSource.FromVisual(this) is System.Windows.Interop.HwndSource source)
            {
                source.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                if (TaskbarScrollViewer.IsMouseOver)
                {
                    int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    TaskbarScrollViewer.ScrollToHorizontalOffset(TaskbarScrollViewer.HorizontalOffset + delta);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = 0;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height;
            this.Width = SystemParameters.PrimaryScreenWidth;

            ToggleNativeTaskbar(false);

            StartButton.MouseMove += (s, ev) =>
            {
                if (StartButton.Background is System.Windows.Media.RadialGradientBrush rgb)
                {
                    var p = ev.GetPosition(StartButton);
                    rgb.Center = new System.Windows.Point(p.X / StartButton.ActualWidth, p.Y / StartButton.ActualHeight);
                    rgb.GradientOrigin = rgb.Center;
                }
            };

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, ev) => RefreshTaskbar();
            timer.Start();

            RefreshTaskbar();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            ToggleNativeTaskbar(true);
        }

        private void ToggleNativeTaskbar(bool show)
        {
            int SW_HIDE = 0;
            int SW_SHOW = 5;
            int command = show ? SW_SHOW : SW_HIDE;

            IntPtr trayHwnd = FindWindow("Shell_TrayWnd", null);
            if (trayHwnd != IntPtr.Zero)
            {
                ShowWindow(trayHwnd, command);
            }

            IntPtr secondaryTrayHwnd = FindWindow("Shell_SecondaryTrayWnd", null);
            if (secondaryTrayHwnd != IntPtr.Zero)
            {
                ShowWindow(secondaryTrayHwnd, command);
            }

            IntPtr progmanHwnd = FindWindow("Progman", null);
            if (progmanHwnd != IntPtr.Zero)
            {
                ShowWindow(progmanHwnd, command);
            }
        }

        private void RefreshTaskbar()
        {
            if (_isContextMenuOpen || StartMenu.IsOpen || RightClickMenu.IsOpen || PreviewPopup.IsOpen || TaskbarContextMenuPopup.IsOpen)
                return;

            IntPtr fgWindow = GetForegroundWindow();
            if (fgWindow != IntPtr.Zero && fgWindow != GetDesktopWindow() && fgWindow != GetShellWindow() && fgWindow != new System.Windows.Interop.WindowInteropHelper(this).Handle)
            {
                IntPtr hMonitor = MonitorFromWindow(fgWindow, 2); // MONITOR_DEFAULTTONEAREST
                if (hMonitor != IntPtr.Zero)
                {
                    MONITORINFO mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        if (GetWindowRect(fgWindow, out RECT rect))
                        {
                            if (rect.Left <= mi.rcMonitor.Left && rect.Top <= mi.rcMonitor.Top &&
                                rect.Right >= mi.rcMonitor.Right && rect.Bottom >= mi.rcMonitor.Bottom)
                            {
                                this.Visibility = Visibility.Hidden;
                                return;
                            }
                        }
                    }
                }
            }
            this.Visibility = Visibility.Visible;

            double scrollOffset = TaskbarScrollViewer.HorizontalOffset;
            TaskbarPanel.Children.Clear();
            _currentConfig = ConfigManager.LoadConfig();

            if (_currentConfig.Theme == "Windows10")
            {
                this.Height = 40;
                this.Top = SystemParameters.PrimaryScreenHeight - 40;
                TaskbarContainer.Margin = new Thickness(0);
                TaskbarContainer.CornerRadius = new CornerRadius(0);
                TaskbarContainer.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#111111"));
                StartButtonColumn.Width = GridLength.Auto;
                StartButton.Margin = new Thickness(0);
                StartButton.Height = 40;
                StartButton.Width = 48;
                StartButton.Style = (Style)FindResource("Win10Button");
                var spotlightBrush = new System.Windows.Media.RadialGradientBrush
                {
                    GradientStops = new System.Windows.Media.GradientStopCollection
                    {
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(40, 255, 255, 255), 0),
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(0, 255, 255, 255), 1)
                    },
                    RadiusX = 0.8,
                    RadiusY = 1.5
                };
                StartButton.Background = spotlightBrush;
            }
            else
            {
                this.Height = 55;
                this.Top = SystemParameters.PrimaryScreenHeight - 55;
                TaskbarContainer.Margin = new Thickness(6);
                TaskbarContainer.CornerRadius = new CornerRadius(6);
                TaskbarContainer.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#171717"));
                StartButtonColumn.Width = GridLength.Auto;
                StartButton.Margin = new Thickness(10, 0, 10, 0);
                StartButton.Height = 34;
                StartButton.Width = 34;
                StartButton.Style = (Style)FindResource("TransparentButton");
                StartButton.ClearValue(Button.BackgroundProperty);
                StartButton.ClearValue(Button.BorderThicknessProperty);
            }

            var groupedWindows = new Dictionary<string, TaskbarGroup>();
            var singleWindows = new List<IntPtr>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                IntPtr owner = GetWindow(hWnd, 4); // GW_OWNER
                if (owner != IntPtr.Zero)
                    return true;

                int exStyle = GetWindowLong(hWnd, -20); // GWL_EXSTYLE
                if ((exStyle & 0x00000080) != 0) // WS_EX_TOOLWINDOW
                    return true;

                string title = GetWindowTitle(hWnd);
                if (string.IsNullOrWhiteSpace(title) || title == "Program Manager")
                    return true;

                if (_currentConfig.GroupTaskbarWindows)
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    string exePath = GetProcessExePath(pid);
                    if (string.IsNullOrEmpty(exePath)) exePath = "Unknown";
                    
                    if (!groupedWindows.ContainsKey(exePath))
                    {
                        groupedWindows[exePath] = new TaskbarGroup { ExePath = exePath, AppName = GetAppName(exePath) };
                    }
                    groupedWindows[exePath].Windows.Add(hWnd);
                }
                else
                {
                    singleWindows.Add(hWnd);
                }
                return true;
            }, IntPtr.Zero);

            if (_currentConfig.GroupTaskbarWindows)
            {
                foreach (var group in groupedWindows.Values)
                {
                    TaskbarPanel.Children.Add(CreateTaskbarButton(group.Windows[0], group));
                }
            }
            else
            {
                foreach (var hWnd in singleWindows)
                {
                    TaskbarPanel.Children.Add(CreateTaskbarButton(hWnd, null));
                }
            }

            if (scrollOffset > 0)
            {
                Dispatcher.InvokeAsync(() => TaskbarScrollViewer.ScrollToHorizontalOffset(scrollOffset), DispatcherPriority.Loaded);
            }
        }

        private Button CreateTaskbarButton(IntPtr representativeHwnd, TaskbarGroup group)
        {
            var btn = new Button
            {
                Padding = new Thickness(8, 0, 8, 0),
                Height = _currentConfig?.Theme == "Windows10" ? 40 : 34,
                Margin = _currentConfig?.Theme == "Windows10" ? new Thickness(0) : new Thickness(2, 0, 2, 0),
                Style = _currentConfig?.Theme == "Windows10" ? (Style)FindResource("Win10Button") : (Style)FindResource("FlatButton"),
                Tag = group != null ? (object)group : representativeHwnd,
                MaxWidth = 150
            };

            if (_currentConfig?.Theme == "Windows10")
            {
                var spotlightBrush = new System.Windows.Media.RadialGradientBrush
                {
                    GradientStops = new System.Windows.Media.GradientStopCollection
                    {
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(40, 255, 255, 255), 0),
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(0, 255, 255, 255), 1)
                    },
                    RadiusX = 0.8,
                    RadiusY = 1.5
                };
                btn.Background = spotlightBrush;
                btn.MouseMove += (s, ev) =>
                {
                    var p = ev.GetPosition(btn);
                    spotlightBrush.Center = new System.Windows.Point(p.X / btn.ActualWidth, p.Y / btn.ActualHeight);
                    spotlightBrush.GradientOrigin = spotlightBrush.Center;
                };
            }

            btn.PreviewMouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                TaskbarContextMenuCloseText.Text = group != null && group.Windows.Count > 1 ? $"Close {group.Windows.Count} Windows" : "Close";
                TaskbarContextMenuPopup.PlacementTarget = btn;
                TaskbarContextMenuPopup.IsOpen = true;
                _isContextMenuOpen = true;
            };

            var contentPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var iconSrc = GetWindowIcon(representativeHwnd);
            if (iconSrc != null)
            {
                contentPanel.Children.Add(new Image { Source = iconSrc, Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) });
            }

            string title = group != null ? group.AppName : GetWindowTitle(representativeHwnd);
            if (group != null && group.Windows.Count > 1) title += $" ({group.Windows.Count})";

            contentPanel.Children.Add(new TextBlock
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            btn.Content = contentPanel;
            btn.Click += TaskbarButton_Click;
            btn.MouseEnter += TaskbarButton_MouseEnter;
            btn.MouseLeave += TaskbarButton_MouseLeave;
            return btn;
        }

        private void TaskbarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Tag is IntPtr hWnd)
                {
                    if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
                    SetForegroundWindow(hWnd);
                }
                else if (btn.Tag is TaskbarGroup group)
                {
                    var wnd = group.Windows.FirstOrDefault();
                    if (wnd != IntPtr.Zero)
                    {
                        if (IsIconic(wnd)) ShowWindow(wnd, SW_RESTORE);
                        SetForegroundWindow(wnd);
                    }
                }
            }
        }

        private string GetAppName(string exePath)
        {
            try
            {
                if (System.IO.File.Exists(exePath))
                {
                    var info = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(info.FileDescription))
                        return info.FileDescription;
                    if (!string.IsNullOrWhiteSpace(info.ProductName))
                        return info.ProductName;
                }
            }
            catch { }
            return System.IO.Path.GetFileNameWithoutExtension(exePath);
        }

        private string GetProcessExePath(uint pid)
        {
            try
            {
                var proc = Process.GetProcessById((int)pid);
                try 
                {
                    return proc.MainModule?.FileName ?? proc.ProcessName;
                }
                catch
                {
                    return proc.ProcessName;
                }
            }
            catch
            {
                return "Unknown_" + pid;
            }
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private DateTime _lastPopupCloseTime = DateTime.MinValue;

        private void StartMenu_Closed(object sender, EventArgs e)
        {
            _lastPopupCloseTime = DateTime.UtcNow;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if ((DateTime.UtcNow - _lastPopupCloseTime).TotalMilliseconds < 200)
            {
                return;
            }
            StartMenu.IsOpen = true;
        }

        private void StartButton_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RightClickMenu.IsOpen = true;
            e.Handled = true;
        }

        private void ContextMenuSettings_Click(object sender, RoutedEventArgs e)
        {
            RightClickMenu.IsOpen = false;
            var settings = new SettingsWindow();
            if (settings.ShowDialog() == true)
            {
                LoadShortcuts();
            }
        }

        private void ContextMenuTaskManager_Click(object sender, RoutedEventArgs e)
        {
            RightClickMenu.IsOpen = false;
            try { Process.Start("taskmgr.exe"); } catch { }
        }

        private void ContextMenuCmd_Click(object sender, RoutedEventArgs e)
        {
            RightClickMenu.IsOpen = false;
            try { Process.Start("cmd.exe"); } catch { }
        }

        private void ContextMenuRun_Click(object sender, RoutedEventArgs e)
        {
            RightClickMenu.IsOpen = false;
            try
            {
                Process.Start("explorer.exe", "Shell:::{2559a1f3-21d7-11d4-bdaf-00c04f60b9f0}");
            }
            catch { }
        }

        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        private void ContextMenuSleep_Click(object sender, RoutedEventArgs e)
        {
            RightClickMenu.IsOpen = false;
            SetSuspendState(false, true, true);
        }

        private void ContextMenuSignOut_Click(object sender, RoutedEventArgs e)
        {
            RightClickMenu.IsOpen = false;
            try { Process.Start(new ProcessStartInfo("shutdown.exe", "/l") { CreateNoWindow = true, UseShellExecute = false }); } catch { }
        }

        private void ContextMenuRestart_Click(object sender, RoutedEventArgs e)
        {
            RightClickMenu.IsOpen = false;
            try { Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false }); } catch { }
        }

        private void ContextMenuRestartBios_Click(object sender, RoutedEventArgs e)
        {
            RightClickMenu.IsOpen = false;
            try { Process.Start(new ProcessStartInfo("shutdown.exe", "/r /fw /t 0") { CreateNoWindow = true, UseShellExecute = false }); } catch { }
        }

        private void ContextMenuShutDown_Click(object sender, RoutedEventArgs e)
        {
            RightClickMenu.IsOpen = false;
            try { Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false }); } catch { }
        }

        private void ContextMenuQuit_Click(object sender, RoutedEventArgs e)
        {
            RightClickMenu.IsOpen = false;
            Application.Current.Shutdown();
        }

        private void TaskbarContextMenuPopup_Closed(object sender, EventArgs e)
        {
            _isContextMenuOpen = false;
            RefreshTaskbar();
        }

        private void TaskbarContextMenuClose_Click(object sender, RoutedEventArgs e)
        {
            TaskbarContextMenuPopup.IsOpen = false;
            if (TaskbarContextMenuPopup.PlacementTarget is Button btn)
            {
                if (btn.Tag is TaskbarGroup group)
                {
                    foreach (var w in group.Windows)
                        SendMessage(w, 0x0010, IntPtr.Zero, IntPtr.Zero);
                }
                else if (btn.Tag is IntPtr hwnd)
                {
                    SendMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
                }
            }
        }

        private void TaskbarContextMenuTerminate_Click(object sender, RoutedEventArgs e)
        {
            TaskbarContextMenuPopup.IsOpen = false;
            if (TaskbarContextMenuPopup.PlacementTarget is Button btn)
            {
                uint pid = 0;
                if (btn.Tag is TaskbarGroup group)
                {
                    GetWindowThreadProcessId(group.Windows.FirstOrDefault(), out pid);
                }
                else if (btn.Tag is IntPtr hwnd)
                {
                    GetWindowThreadProcessId(hwnd, out pid);
                }

                if (pid > 0)
                {
                    try { Process.GetProcessById((int)pid).Kill(); } catch { }
                }
            }
        }

        private void LaunchExplorer(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe");
            StartMenu.IsOpen = false;
        }

        private void LaunchWindowsSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true });
            }
            catch { }
            StartMenu.IsOpen = false;
        }

        private void ShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ShortcutItem item)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.FilePath,
                        Arguments = item.Arguments ?? "",
                        UseShellExecute = true
                    });
                }
                catch { }
                StartMenu.IsOpen = false;
            }
        }

        private void TaskbarScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            TaskbarScrollViewer.ScrollToHorizontalOffset(TaskbarScrollViewer.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        private void TaskbarButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _hoveredButton = sender as Button;
            HoverTimer.Start();
        }

        private void TaskbarButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            HoverTimer.Stop();
            
            Dispatcher.InvokeAsync(async () => {
                await System.Threading.Tasks.Task.Delay(100);
                if (!PreviewPopup.IsMouseOver && (_hoveredButton == null || !_hoveredButton.IsMouseOver))
                {
                    PreviewPopup.IsOpen = false;
                }
            });
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            HoverTimer.Stop();
            if (_hoveredButton != null && _hoveredButton.IsMouseOver)
            {
                var models = new List<PreviewWindowModel>();

                if (_hoveredButton.Tag is IntPtr hWnd)
                {
                    models.Add(CreatePreviewModel(hWnd));
                }
                else if (_hoveredButton.Tag is TaskbarGroup group)
                {
                    foreach (var wnd in group.Windows)
                    {
                        models.Add(CreatePreviewModel(wnd));
                    }
                }

                if (models.Count > 0)
                {
                    PreviewItemsControl.ItemsSource = models;
                    PreviewPopup.PlacementTarget = _hoveredButton;
                    PreviewPopup.IsOpen = true;
                }
            }
        }

        private PreviewWindowModel CreatePreviewModel(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            var model = new PreviewWindowModel
            {
                Hwnd = hWnd,
                ProcessId = (int)pid,
                Title = GetWindowTitle(hWnd)
            };

            var bmp = CaptureWindow(hWnd);
            if (bmp != null)
            {
                model.Thumbnail = bmp;
            }
            return model;
        }

        private void PreviewPopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_hoveredButton == null || !_hoveredButton.IsMouseOver)
            {
                PreviewPopup.IsOpen = false;
            }
        }

        private void PreviewImage_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is IntPtr hwnd)
            {
                if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
                PreviewPopup.IsOpen = false;
            }
        }

        private void PreviewShowExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is int pid)
            {
                string? path = GetProcessExePath((uint)pid);
                if (!string.IsNullOrEmpty(path) && !path.StartsWith("Unknown_"))
                {
                    try
                    {
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    catch { }
                }
                PreviewPopup.IsOpen = false;
            }
        }

        private const uint WM_CLOSE = 0x0010;

        private void PreviewClose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is IntPtr hwnd)
            {
                SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                PreviewPopup.IsOpen = false;
                RefreshTaskbar();
            }
        }

        private void PreviewTerminate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is int pid)
            {
                try
                {
                    Process.GetProcessById(pid).Kill();
                    PreviewPopup.IsOpen = false;
                    RefreshTaskbar();
                }
                catch { }
            }
        }

        private ImageSource CaptureWindow(IntPtr hWnd)
        {
            GetWindowRect(hWnd, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0) return null;

            try
            {
                using (var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var gfx = System.Drawing.Graphics.FromImage(bmp))
                    {
                        IntPtr hdc = gfx.GetHdc();
                        try
                        {
                            PrintWindow(hWnd, hdc, 2); // PW_RENDERFULLCONTENT
                        }
                        finally
                        {
                            gfx.ReleaseHdc(hdc);
                        }
                    }
                    
                    IntPtr hBitmap = bmp.GetHbitmap();
                    try
                    {
                        var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap,
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze();
                        return src;
                    }
                    finally
                    {
                        DeleteObject(hBitmap);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static ImageSource GetIconForFile(string filePath)
        {
            var shinfo = new SHFILEINFO();
            SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), 0x00000100 | 0x00000001); // SHGFI_ICON | SHGFI_SMALLICON
            if (shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    var imageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    return imageSource;
                }
                catch { }
                finally
                {
                    DestroyIcon(shinfo.hIcon);
                }
            }
            return null;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
        private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetClassLong")]
        private static extern int GetClassLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        public static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size > 4)
                return GetClassLongPtr64(hWnd, nIndex);
            else
                return new IntPtr(GetClassLong32(hWnd, nIndex));
        }

        private ImageSource GetWindowIcon(IntPtr hWnd)
        {
            IntPtr hIcon = SendMessage(hWnd, 0x007F, (IntPtr)2, IntPtr.Zero); // WM_GETICON, ICON_SMALL2
            if (hIcon == IntPtr.Zero)
                hIcon = SendMessage(hWnd, 0x007F, (IntPtr)0, IntPtr.Zero); // ICON_SMALL
            if (hIcon == IntPtr.Zero)
                hIcon = SendMessage(hWnd, 0x007F, (IntPtr)1, IntPtr.Zero); // ICON_BIG
            if (hIcon == IntPtr.Zero)
                hIcon = GetClassLongPtr(hWnd, -34); // GCLP_HICONSM
            if (hIcon == IntPtr.Zero)
                hIcon = GetClassLongPtr(hWnd, -14); // GCLP_HICON

            if (hIcon != IntPtr.Zero)
            {
                try
                {
                    var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        hIcon,
                        Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    return bmp;
                }
                catch { }
            }
            return null;
        }
    }
}