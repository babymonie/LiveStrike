using System;
using System.Windows;
using System.Windows.Input;

namespace CS2Overlay.UI
{
    public partial class OverlayHandleWindow : Window
    {
        private readonly OverlayWindow _overlay;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var fade = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.25),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };
            BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private bool _isLocked = false;


        public OverlayHandleWindow(OverlayWindow overlay)
        {
            _overlay = overlay;
            InitializeComponent();
            UpdateTexts();

            // Keep handle always above overlay
            this.Topmost = true;

            // Follow overlay position/size
            _overlay.LocationChanged += (_, __) => RepositionNearOverlay();
            _overlay.SizeChanged += (_, __) => RepositionNearOverlay();
            RepositionNearOverlay();
        }

        private void RepositionNearOverlay()
        {
            try
            {
                // Place the handle on top-left of overlay with small offset
                var p = _overlay.PointToScreen(new System.Windows.Point(0, 0));
                this.Left = p.X + 8;
                this.Top = p.Y - this.Height - 6; // above overlay
            }
            catch { /* ignore */ }
        }

        private void UpdateTexts()
        {
            if (_overlay.IsClickThrough)
            {
                StatusText.Text = "Locked";
                ToggleBtn.Content = "Unlock";
            }
            else
            {
                StatusText.Text = "Unlocked";
                ToggleBtn.Content = "Lock";
            }
        }

        private void ToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            _overlay.SetClickThroughPublic(!_overlay.IsClickThrough);
            UpdateTexts();
        }

        private void DockPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                try { DragMove(); } catch { }
            }
        }
    }
}
