using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using CS2Overlay.Models;
using CS2Overlay.Services;
using System.Diagnostics;
using CS2Overlay.Infrastructure;
using System.Collections.Generic;

namespace CS2Overlay.UI
{
    public partial class MatchPickerWindow : Window
    {
        private const string DefaultServerBaseUrl = "http://localhost:3000";
        public string? SelectedMatchUrl { get; private set; }
        public string ServerBaseUrl => DefaultServerBaseUrl;

        public MatchPickerWindow()
        {
            InitializeComponent();
            _ = LoadMatches();
            this.Background = System.Windows.Media.Brushes.Transparent;
            this.AllowsTransparency = true;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var fade = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            this.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private async Task LoadMatches()
        {
            try
            {
                StatusText.Text = "Loading matches…";
                RefreshBtn.IsEnabled = false;
                // Ensure local server is running if pointing to localhost
                try
                {
                    await NodeServerManager.EnsureStartedAsync(ServerBaseUrl,
                        new NodeServerOptions
                        {
                            ScriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server", "server.js"),
                            WorkingDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server"),
                            NodePath = "node",
                            Port = new System.Uri(ServerBaseUrl).IsDefaultPort ? 3000 : new System.Uri(ServerBaseUrl).Port,
                            Headless = true
                        });
                }
                catch { /* remote or failed autostart: we'll attempt request anyway */ }

                var items = await MatchPickerWindowExtensions.FetchMatchesFromServerAsync(ServerBaseUrl);
                MatchesList.ItemsSource = items;
                StatusText.Text = "Loaded.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to load matches.";
                
                // Check if this is a Node.js related error
                if (IsNodeJsError(ex))
                {
                    // Node.js error dialog removed; handled by InstallingDependenciesWindow
                }
                else
                {
#if DEBUG
                    MessageBox.Show(ex.Message, "HLTV load error", MessageBoxButton.OK, MessageBoxImage.Warning);
#else
                    MessageBox.Show("Failed to connect to the data service. Please check your internet connection and try again.", 
                        "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
#endif
                }
            }
            finally
            {
                RefreshBtn.IsEnabled = true;
            }
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadMatches();
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MatchesList.SelectedItem is not MatchItem mi || string.IsNullOrWhiteSpace(mi.Url))
            {
                MessageBox.Show("Select a live match from the list.", "No selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 1) Ensure Node server is up (auto-start if pointing to localhost)
                StatusText.Text = "Starting local server (if needed)…";
                var ok = await NodeServerManager.EnsureStartedAsync(ServerBaseUrl,
                    new NodeServerOptions
                    {
                        // Adjust if your server.js is somewhere else:
                        ScriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server", "server.js"),
                        WorkingDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server"),
                        NodePath = "node",   // or absolute path to node.exe
                        Port = new Uri(ServerBaseUrl).Port == -1 ? 3000 : new Uri(ServerBaseUrl).Port,
                        Headless = true
                    });

                if (!ok)
                {
                    // If remote server, we just proceed; if local failed, bail
                    var host = new Uri(ServerBaseUrl).Host;
                    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1")
                        throw new InvalidOperationException("Local Node server did not come up.");
                }

                // 2) Send selected match URL via /url (starts if needed)
                StatusText.Text = "Sending match to server…";
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var resp = await http.PostAsJsonAsync($"{ServerBaseUrl}/url", new { url = mi.Url });
                resp.EnsureSuccessStatusCode();

                SelectedMatchUrl = mi.Url;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start server or send match:\n{ex.Message}",
                    "Start failed", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Failed.";
            }
        }


        // DTOs for /matches/live response
        internal class LiveMatchesResponse
        {
            public string? source { get; set; }
            public int count { get; set; }
            public List<LiveMatch>? matches { get; set; }
            public DateTime fetchedAt { get; set; }
        }

        internal class LiveMatch
        {
            public int id { get; set; }
            public string? slug { get; set; }
            public string? url { get; set; }
            public string? status { get; set; }
            public bool live { get; set; }
            public List<string>? teams { get; set; }
            public string? eventName { get; set; } // not used
            public string? @event { get; set; }
            public string? bo { get; set; }
            public string? score { get; set; }
            public string? time { get; set; }
            public int stars { get; set; }
        }

        internal static class MatchPickerWindowExtensions
        {
            public static async Task<List<MatchItem>> FetchMatchesFromServerAsync(string baseUrl)
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var resp = await http.GetFromJsonAsync<LiveMatchesResponse>($"{baseUrl.TrimEnd('/')}/matches/live");
                var list = new List<MatchItem>();
                if (resp?.matches == null) return list;
                foreach (var m in resp.matches)
                {
                    var t1 = (m.teams != null && m.teams.Count > 0) ? m.teams[0] : "";
                    var t2 = (m.teams != null && m.teams.Count > 1) ? m.teams[1] : "";
                    list.Add(new MatchItem
                    {
                        Id = m.id.ToString(),
                        Url = m.url ?? string.Empty,
                        Event = m.@event ?? m.eventName ?? string.Empty,
                        Team1 = t1,
                        Team2 = t2,
                        Status = (m.live || (m.status ?? "").ToUpperInvariant().Contains("LIVE")) ? "Live" : (m.status ?? ""),
                        Bo = m.bo ?? string.Empty,
                        ScoreNow = m.score ?? string.Empty,
                        Stars = m.stars
                    });
                }
                // Order: live first, then by stars
                list.Sort((a, b) =>
                {
                    int liveCmp = string.Equals(b.Status, "Live", StringComparison.OrdinalIgnoreCase)
                                   .CompareTo(string.Equals(a.Status, "Live", StringComparison.OrdinalIgnoreCase));
                    if (liveCmp != 0) return liveCmp;
                    int starCmp = b.Stars.CompareTo(a.Stars);
                    if (starCmp != 0) return starCmp;
                    return string.Compare(a.Event, b.Event, StringComparison.OrdinalIgnoreCase);
                });
                return list;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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

    }
}
