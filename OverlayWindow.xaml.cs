using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Models;

namespace CS2Overlay
{
    public partial class OverlayWindow : Window
    {
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private const double CompactWidthThreshold = 360;  // when <= this, switch to widget mode
        private const double CompactHeightThreshold = 220;

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_TOOLWINDOW = 0x00000080;

        private bool isClickThrough = false;
        private bool _shownLockHint = false;
        private readonly string _serverBase;
        private AppSettings settings;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PositionBottomRight();
            ApplyAdaptiveSizing();
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.OpacityProperty, fadeIn);

            PositionBottomRight();
            ApplyAdaptiveSizing();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyAdaptiveSizing();
        }

        private void PositionBottomRight()
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - ActualWidth - 16;
            Top = wa.Bottom - ActualHeight - 16;
        }

        private void ApplyAdaptiveSizing()
        {
            bool compact = ActualWidth <= CompactWidthThreshold || ActualHeight <= CompactHeightThreshold;

            // Increase font sizes automatically when compact
            Resources["fs.title"] = compact ? 20.0 : 16.0;
            Resources["fs.score"] = compact ? 26.0 : 18.0;
            Resources["fs.body"] = compact ? 15.0 : 12.0;

            // Collapse side panes in compact mode (keeps a tiny widget with big text)
            if (FindName("KillFeedColumn") is ColumnDefinition c0)
                c0.Width = compact ? new GridLength(0) : new GridLength(150);
            if (FindName("PlayersColLeft") is ColumnDefinition c1)
                c1.Width = compact ? new GridLength(0) : new GridLength(180);
            if (FindName("PlayersColRight") is ColumnDefinition c2)
                c2.Width = compact ? new GridLength(0) : new GridLength(180);
            if (FindName("WinProbColumn") is ColumnDefinition c3)
                c3.Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(160);
        }
        public OverlayWindow(string serverBase = "http://localhost:3000")
        {
            _serverBase = serverBase.Trim().TrimEnd('/');
            settings = AppSettings.Load(); // Load settings on initialization
            InitializeComponent();

            Loaded += (_, __) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
                SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
                SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, 0x0003);
            };

            _ = UpdateLoop();
        }

        private async Task UpdateLoop()
        {
            var client = new HttpClient();
            while (true)
            {
                try
                {
                    var json = await client.GetStringAsync($"{_serverBase}/gsi");
                    var data = JsonDocument.Parse(json).RootElement;

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Top scoreboard - animate changes
                        AnimateTextChange(SbTeam1, (data.TryGetProperty("team1", out var t1) ? t1.GetString() : "Team A") ?? "Team A");
                        AnimateTextChange(SbTeam2, (data.TryGetProperty("team2", out var t2) ? t2.GetString() : "Team B") ?? "Team B");
                        var s1 = data.TryGetProperty("score1", out var s1e) ? s1e.GetInt32() : 0;
                        var s2 = data.TryGetProperty("score2", out var s2e) ? s2e.GetInt32() : 0;
                        AnimateTextChange(SbScore1, s1.ToString());
                        AnimateTextChange(SbScore2, s2.ToString());
                        AnimateTextChange(SbTime, data.TryGetProperty("timeLeft", out var tl) ? (tl.GetString() ?? "") : "");
                        AnimateTextChange(SbMap, (data.TryGetProperty("mapName", out var mn) ? mn.GetString() : "").ToUpperInvariant());

                        // Kill feed
                        KillFeed.Items.Clear();
                        if (data.TryGetProperty("killFeed", out var feed))
                        {
                            foreach (var line in feed.EnumerateArray())
                                KillFeed.Items.Add(line.GetString());
                            // 🔽 Auto-scroll to the latest kill
                            if (KillFeed.Items.Count > 0)
                            {
                                KillFeed.ScrollIntoView(KillFeed.Items[KillFeed.Items.Count - 1]);
                            }

                        }


                        // Win probability
                        if (data.TryGetProperty("winProbability", out var winp))
                        {
                            var p1 = winp.TryGetProperty("team1", out var t1p) ? (double)(t1p.ValueKind == JsonValueKind.String ? double.TryParse(t1p.GetString(), out var dv1) ? dv1 : 0 : t1p.GetDouble()) : 0;
                            var p2 = winp.TryGetProperty("team2", out var t2p) ? (double)(t2p.ValueKind == JsonValueKind.String ? double.TryParse(t2p.GetString(), out var dv2) ? dv2 : 0 : t2p.GetDouble()) : 0;
                            // Normalize
                            var sum = p1 + p2;
                            if (sum > 0)
                            {
                                var v1 = Math.Round(100.0 * p1 / sum);
                                var v2 = Math.Round(100.0 * p2 / sum);
                                AnimateTextChange(WpTeam1Pct, v1 + "%");
                                AnimateTextChange(WpTeam2Pct, v2.ToString());
                                AnimateProgressBar(WpBarTeam1, v1);
                                AnimateProgressBar(WpBarTeam2, v2);
                            }
                        }
                    });
                }
                catch
                {
                    // swallow; retry
                }

                await Task.Delay(2000);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Reset to default comfortable size
                Width = 340;
                Height = 480;
            }
            else
            {
                try { DragMove(); } catch { /* non-fatal: user may be dragging too fast */ }
            }
        }

        private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isClickThrough) return; // cannot drag when click-through is enabled
            if (e.LeftButton != MouseButtonState.Pressed) return;

            if (e.ClickCount == 2)
            {
                Width = 640;
                Height = 360;
                return;
            }

            try 
            { 
                DragMove(); 
            } 
            catch (InvalidOperationException) 
            { 
                // Ignore: can occur if window is maximized or during certain states
            }
        }

        private void DockButton_Click(object sender, RoutedEventArgs e) => ToggleClickThrough();

        private void ToggleClickThrough()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;

            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);

            if (isClickThrough)
            {
                SetWindowLong(handle, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
                isClickThrough = false;
            }
            else
            {
                // Inform the user how to unlock before enabling click-through
                if (!_shownLockHint)
                {
                    try
                    {
                        MessageBox.Show(
                            "Overlay is now locked (click-through).\nPress Ctrl+Alt+U to unlock.",
                            "Overlay Locked",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch { /* messagebox may fail if no UI thread focus */ }
                    _shownLockHint = true;
                }
                SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
                isClickThrough = true;
            }
        }

        private void RootBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            // When locked, fade out on hover to see through
            if (isClickThrough)
            {
                AnimateOpacity(settings.HoverOpacity, 200);
            }
        }

        private void RootBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            // Restore full visibility when mouse leaves
            AnimateOpacity(1.0, 200);
        }

        private void AnimateOpacity(double to, int durationMs)
        {
            var anim = new DoubleAnimation
            {
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            this.BeginAnimation(Window.OpacityProperty, anim);
        }

        private void AnimateTextChange(TextBlock textBlock, string newText)
        {
            if (textBlock.Text == newText) return;

            // Skip animation if disabled in settings
            if (!settings.EnableAnimations)
            {
                textBlock.Text = newText;
                return;
            }

            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                textBlock.Text = newText;
                var fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                textBlock.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };

            textBlock.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void AnimateProgressBar(System.Windows.Controls.ProgressBar progressBar, double newValue)
        {
            if (Math.Abs(progressBar.Value - newValue) < 0.01) return;

            // Skip animation if disabled in settings
            if (!settings.EnableAnimations)
            {
                progressBar.Value = newValue;
                return;
            }

            var anim = new DoubleAnimation
            {
                From = progressBar.Value,
                To = newValue,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            progressBar.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, anim);
        }

        public bool IsClickThrough => isClickThrough;

        public void SetClickThroughPublic(bool enable)
        {
            if (enable == isClickThrough) return;
            ToggleClickThrough();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Close();
                Application.Current.Shutdown();
            }
            catch { /* ignore */ }
        }

        public void ToggleClickThroughPublic() => ToggleClickThrough();

        public void ApplySettings(AppSettings newSettings)
        {
            settings = newSettings;
            
            // Update polling interval
            // Note: This would require refactoring UpdateLoop to support cancellation and restart
            // For now, just store the setting - it will be used on next app restart
            
            // Apply hover opacity immediately - update the animation method to use settings
            // The AnimateOpacity method will now use settings.HoverOpacity
        }
    }
}
