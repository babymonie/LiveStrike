using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;
using UI;
using CS2Overlay.Infrastructure;
using CS2Overlay;
using CS2Overlay.UI;
using Models;

namespace LiveStrike
{
    public partial class App : Application
    {
    private OverlayWindow? overlay;
    private Forms.NotifyIcon? tray;
    public static AppSettings Settings { get; private set; } = new AppSettings();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load settings
            Settings = AppSettings.Load();

            // Global exception logging to help diagnose crashes
            this.DispatcherUnhandledException += (s, exArgs) =>
            {
                TryLog($"DispatcherUnhandledException: {exArgs.Exception}");
                exArgs.Handled = true; // prevent hard crash, app can continue or exit gracefully
            };
            AppDomain.CurrentDomain.UnhandledException += (s, exArgs) =>
            {
                TryLog($"UnhandledException: {exArgs.ExceptionObject}");
            };
            TaskScheduler.UnobservedTaskException += (s, exArgs) =>
            {
                TryLog($"UnobservedTaskException: {exArgs.Exception}");
                exArgs.SetObserved();
            };

            // Auto-start Node server if enabled - and wait for it
            if (Settings.AutoStartNodeServer)
            {
                try
                {
                    TryLog("Auto-starting Node server...");
                    await NodeServerManager.EnsureStartedAsync("http://localhost:3000");
                    TryLog("Node server auto-started successfully");
                }
                catch (Exception ex)
                {
                    TryLog($"Failed to auto-start Node server: {ex.Message}");
                    
                    // Show user-friendly error for Node.js issues
                    if (IsNodeJsError(ex))
                    {
                        var result = MessageBox.Show(
                            "LiveStrike requires Node.js to fetch live match data from HLTV.\n\n" +
                            "Node.js is not installed on your system or not found in PATH.\n\n" +
                            "The application will continue, but you'll need to install Node.js to fetch live matches.\n\n" +
                            "Would you like to open the Node.js download page now?",
                            "Node.js Required - LiveStrike",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "https://nodejs.org/en/download/",
                                    UseShellExecute = true
                                });
                            }
                            catch { /* ignore browser launch errors */ }
                        }
                    }
                    // Continue anyway - MatchPickerWindow will handle the error gracefully
                }
            }

            // 1) Show picker, post /start to Node server
            var picker = new MatchPickerWindow();
            var ok = picker.ShowDialog() == true;
            if (!ok)
            {
                Shutdown();
                return;
            }

            // 2) Open overlay (polls /gsi)
            overlay = new OverlayWindow(picker.ServerBaseUrl);
            MainWindow = overlay; // set explicitly so shutdown behavior is deterministic
            overlay.Show();

            // 3) Tray icon with quick actions
            SetupTrayIcon();

            // hotkeys
            HotkeyManager.Register(overlay,
                () =>
                {
                    overlay.Visibility = overlay.Visibility == Visibility.Visible
                        ? Visibility.Collapsed : Visibility.Visible;
                },
                () => overlay.ToggleClickThroughPublic(),
                () =>
                {
                    overlay.Close();
                    Current.Shutdown();
                });
        }
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            NodeServerManager.Stop(); // kill local Node if we started it
            try
            {
                if (tray != null)
                {
                    tray.Visible = false;
                    tray.Dispose();
                }
            }
            catch { /* safe to ignore on shutdown */ }
        }

        private static void TryLog(string message)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LiveStrike");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "app.log");
                File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { /* ignore logging errors */ }
        }

        private static bool IsNodeJsError(Exception ex)
        {
            var message = ex.Message?.ToLowerInvariant() ?? "";
            var innerMessage = ex.InnerException?.Message?.ToLowerInvariant() ?? "";
            
            return message.Contains("node.js") ||
                   message.Contains("'node' is not recognized") ||
                   message.Contains("system cannot find the file specified") ||
                   message.Contains("no such file or directory") ||
                   innerMessage.Contains("node.js") ||
                   innerMessage.Contains("'node' is not recognized") ||
                   ex.InnerException is System.ComponentModel.Win32Exception;
        }

        private void SetupTrayIcon()
        {
            try
            {
                tray = new Forms.NotifyIcon
                {
                    Icon = System.Drawing.SystemIcons.Application,
                    Visible = true,
                    Text = "LiveStrike"
                };

                var menu = new Forms.ContextMenuStrip();

                var showHide = new Forms.ToolStripMenuItem("Show/Hide Overlay", null, (s, e) =>
                {
                    if (overlay == null) return;
                    overlay.Visibility = overlay.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                });
                menu.Items.Add(showHide);

                var lockUnlock = new Forms.ToolStripMenuItem("Lock/Unlock", null, (s, e) =>
                {
                    if (overlay == null) return;
                    overlay.SetClickThroughPublic(!overlay.IsClickThrough);
                });
                menu.Items.Add(lockUnlock);

                menu.Items.Add(new Forms.ToolStripSeparator());

                var settings = new Forms.ToolStripMenuItem("Settings", null, (s, e) =>
                {
                    ShowSettingsWindow();
                });
                menu.Items.Add(settings);

                var exit = new Forms.ToolStripMenuItem("Exit", null, (s, e) =>
                {
                    try { overlay?.Close(); } catch { /* safe on exit */ }
                    Current.Shutdown();
                });
                menu.Items.Add(new Forms.ToolStripSeparator());
                menu.Items.Add(exit);

                tray.ContextMenuStrip = menu;

                tray.DoubleClick += (s, e) =>
                {
                    if (overlay == null) return;
                    overlay.Visibility = Visibility.Visible;
                };
            }
            catch { /* ignore tray errors */ }
        }

        private void ShowSettingsWindow()
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.Owner = overlay; // Set overlay as owner if available
                
                if (settingsWindow.ShowDialog() == true)
                {
                    // Settings were saved, update static reference
                    Settings = settingsWindow.Settings;
                    
                    // Apply settings immediately to overlay
                    overlay?.ApplySettings(Settings);
                    
                    TryLog("Settings updated and applied");
                }
            }
            catch (Exception ex)
            {
                TryLog($"Failed to show settings window: {ex.Message}");
            }
        }

    }
}
