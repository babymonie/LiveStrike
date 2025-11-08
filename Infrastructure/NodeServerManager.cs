using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CS2Overlay.Infrastructure
{
    public sealed class NodeServerOptions
    {
        public string NodePath { get; set; } = "node"; // assumes Node in PATH
        public string ScriptPath { get; set; } = GetDefaultScriptPath();
        public string WorkingDirectory { get; set; } = GetDefaultWorkingDirectory();
        public int Port { get; set; } = 3000;
        public bool Headless { get; set; } = true; // passed as env if you want

        private static string GetDefaultScriptPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Walk up to 6 levels to find a folder named 'server' containing server.js
            var serverDir = TryFindServerDirectory(baseDir);
            if (serverDir != null)
            {
                var candidate = Path.Combine(serverDir, "server.js");
                if (File.Exists(candidate))
                    return candidate;
            }

            // Fallback to build output directory
            return Path.Combine(baseDir, "server", "server.js");
        }

        private static string GetDefaultWorkingDirectory()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            var serverDir = TryFindServerDirectory(baseDir);
            if (serverDir != null)
                return serverDir;

            // Fallback to build output directory
            return Path.Combine(baseDir, "server");
        }

        private static string? TryFindServerDirectory(string baseDir)
        {
            try
            {
                var dir = new DirectoryInfo(baseDir);
                for (int i = 0; i < 6 && dir != null; i++)
                {
                    var candidate = Path.Combine(dir.FullName, "server");
                    if (Directory.Exists(candidate))
                        return Path.GetFullPath(candidate);
                    dir = dir.Parent;
                }
            }
            catch { }
            return null;
        }
    }

    public static class NodeServerManager
    {
        private static Process? _proc;
        private static readonly HttpClient _http = new HttpClient();

        public static async Task<bool> EnsureStartedAsync(string baseUrl, NodeServerOptions? options = null, int startupTimeoutMs = 15000)
        {
            options ??= new NodeServerOptions();
            TryLog($"EnsureStartedAsync called for {baseUrl}");

            // 1) Already up?
            if (await IsServerUp(baseUrl))
            {
                TryLog("Server already running");
                return true;
            }

            // Only auto-start if pointing to localhost/127.0.0.1
            var uri = new Uri(baseUrl);
            var host = uri.Host;
            if (!(string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1"))
            {
                TryLog($"Not auto-starting server for non-localhost host: {host}");
                return false;
            }

            // 2) Check for node_modules in server folder
            TryLog("Checking for node_modules...");
            TryLog("Working directory: " + options.WorkingDirectory);

            var nodeModulesPath = Path.Combine(options.WorkingDirectory, "node_modules");
            TryLog(nodeModulesPath);
            if (!Directory.Exists(nodeModulesPath))
            {
                var msg = "Node server failed to start due to missing modules.\nWould you like LiveStrike to run 'npm install' for you, or will you do it manually?";
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var result = System.Windows.MessageBox.Show(msg, "Node Server Error", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        // Run npm install automatically in the actual project server folder
                        try
                        {
                            var projectServerDir = options.WorkingDirectory; // Prefer discovered project server directory
                            if (!Directory.Exists(projectServerDir))
                                throw new DirectoryNotFoundException($"Server folder not found: {projectServerDir}");

                            // On Windows, invoke via cmd to ensure npm.cmd is resolved when UseShellExecute=false
                            bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                            var psi = new ProcessStartInfo
                            {
                                FileName = isWindows ? "cmd.exe" : "npm",
                                Arguments = isWindows ? "/c npm install" : "install",
                                WorkingDirectory = projectServerDir,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };

                            using var npmProc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start npm process");
                            var stdOut = npmProc.StandardOutput.ReadToEndAsync();
                            var stdErr = npmProc.StandardError.ReadToEndAsync();
                            npmProc.WaitForExit();

                            if (!npmProc.ExitCode == 0)
                            {   
                                var err = stdErr.GetAwaiter().GetResult();
                                TryLog($"npm install failed with exit code {npmProc.ExitCode}: {err}");
                                System.Windows.MessageBox.Show($"npm install failed (code {npmProc.ExitCode}). Please open the 'server' folder and run 'npm install' manually.\n\n{Truncate(err, 900)}", "npm install failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"Failed to run npm install: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Please run 'npm install' in the server folder manually, then restart LiveStrike.", "Manual Install Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                });
            }
            public static class NodeServerManager
            {
                private static Process? _proc;
                private static readonly HttpClient _http = new HttpClient();

                public static async Task<bool> EnsureStartedAsync(string baseUrl, NodeServerOptions? options = null, int startupTimeoutMs = 15000)
                {
                    options ??= new NodeServerOptions();
                    TryLog($"EnsureStartedAsync called for {baseUrl}");

                    // 1) Already up?
                    if (await IsServerUp(baseUrl))
                    {
                        TryLog("Server already running");
                        return true;
                    }

                    // Only auto-start if pointing to localhost/127.0.0.1
                    var uri = new Uri(baseUrl);
                    var host = uri.Host;
                    if (!(string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1"))
                    {
                        TryLog($"Not auto-starting server for non-localhost host: {host}");
                        return false;
                    }

                    // 2) Check for node_modules in server folder
                    TryLog("Checking for node_modules...");
                    TryLog("Working directory: " + options.WorkingDirectory);

                    var nodeModulesPath = Path.Combine(options.WorkingDirectory, "node_modules");
                    TryLog(nodeModulesPath);
                    if (!Directory.Exists(nodeModulesPath))
                    {
                        var msg = "Node server failed to start due to missing modules.\nWould you like LiveStrike to run 'npm install' for you, or will you do it manually?";

                        // Prompt on the UI thread (use InvokeAsync so we can await the result)
                        System.Windows.MessageBoxResult result;
                        var dispatcher = System.Windows.Application.Current?.Dispatcher;
                        if (dispatcher != null)
                        {
                            var op = dispatcher.InvokeAsync(() => System.Windows.MessageBox.Show(msg, "Node Server Error", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question));
                            result = await op.Task.ConfigureAwait(true);
                        }
                        else
                        {
                            result = System.Windows.MessageBox.Show(msg, "Node Server Error", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                        }

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            // Run npm install automatically in the project server folder (off the UI thread)
                            try
                            {
                                var projectServerDir = options.WorkingDirectory; // Prefer discovered project server directory
                                if (!Directory.Exists(projectServerDir))
                                    throw new DirectoryNotFoundException($"Server folder not found: {projectServerDir}");

                                bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                                var npmPsi = new ProcessStartInfo
                                {
                                    FileName = isWindows ? "cmd.exe" : "npm",
                                    Arguments = isWindows ? "/c npm install" : "install",
                                    WorkingDirectory = projectServerDir,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true
                                };

                                using var npmProc = Process.Start(npmPsi) ?? throw new InvalidOperationException("Failed to start npm process");
                                var stdErrTask = npmProc.StandardError.ReadToEndAsync();
                                npmProc.WaitForExit();

                                if (npmProc.ExitCode == 0)
                                {
                                    if (dispatcher != null)
                                        await dispatcher.InvokeAsync(() => System.Windows.MessageBox.Show("npm install completed. Please restart LiveStrike.", "Install Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information)).Task.ConfigureAwait(true);
                                    else
                                        System.Windows.MessageBox.Show("npm install completed. Please restart LiveStrike.", "Install Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                                }
                                else
                                {
                                    var err = stdErrTask.GetAwaiter().GetResult();
                                    TryLog($"npm install failed with exit code {npmProc.ExitCode}: {err}");
                                    var errMsg = $"npm install failed (code {npmProc.ExitCode}). Please open the 'server' folder and run 'npm install' manually.\n\n{Truncate(err, 900)}";
                                    if (dispatcher != null)
                                        await dispatcher.InvokeAsync(() => System.Windows.MessageBox.Show(errMsg, "npm install failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error)).Task.ConfigureAwait(true);
                                    else
                                        System.Windows.MessageBox.Show(errMsg, "npm install failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                                }
                            }
                            catch (Exception ex)
                            {
                                if (System.Windows.Application.Current?.Dispatcher != null)
                                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => System.Windows.MessageBox.Show($"Failed to run npm install: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error)).Task.ConfigureAwait(true);
                                else
                                    System.Windows.MessageBox.Show($"Failed to run npm install: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                            if (dispatcher != null)
                                await dispatcher.InvokeAsync(() => System.Windows.MessageBox.Show("Please run 'npm install' in the server folder manually, then restart LiveStrike.", "Manual Install Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information)).Task.ConfigureAwait(true);
                            else
                                System.Windows.MessageBox.Show("Please run 'npm install' in the server folder manually, then restart LiveStrike.", "Manual Install Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        }
                    }

                    // 3) Start node server
                    TryLog("Starting Node server...");
                    TryKillOld(); // safety
                    Directory.CreateDirectory(options.WorkingDirectory);

                    var psi = new ProcessStartInfo
                    {
                        FileName = options.NodePath,                    // "node"
                        Arguments = $"\"{options.ScriptPath}\"",        // "server.js"
                        WorkingDirectory = options.WorkingDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };

                    // Pass port via env so your server respects it (PORT)
                    psi.Environment["PORT"] = uri.Port == -1 ? options.Port.ToString() : uri.Port.ToString();
                    // Optional: control headless with env var if you wire it in Node
                    psi.Environment["HEADLESS"] = options.Headless ? "1" : "0";

                    TryLogToFile($"Starting process: {psi.FileName} {psi.Arguments} in {psi.WorkingDirectory}");

                    try
                    {
                        _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = IsNodeNotFoundError(ex)
                            ? "Node.js is not installed or not found in PATH. Please install Node.js from https://nodejs.org"
                            : $"Failed to create Node.js process: {ex.Message}";
                        TryLog($"Process creation failed: {errorMsg}");
                        throw new InvalidOperationException(errorMsg, ex);
                    }

                    _proc.OutputDataReceived += (_, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            Debug.WriteLine("[node] " + e.Data);
                            // Also log to file for debugging
                            TryLog("[node] " + e.Data);
                        }
                    };
                    _proc.ErrorDataReceived += (_, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            Debug.WriteLine("[node:err] " + e.Data);
                            TryLog("[node:err] " + e.Data);
                        }
                    };
                    _proc.Exited += (_, e) =>
                    {
                        TryLog($"[node] Process exited with code: {_proc?.ExitCode}");
                        // Only log exit, do not show npm install modal here to avoid duplicate dialogs
                    };

                    try
                    {
                        if (!_proc.Start())
                        {
                            var errorMsg = "Node.js process failed to start. Please ensure Node.js is installed from https://nodejs.org";
                            TryLog(errorMsg);
                            throw new InvalidOperationException(errorMsg);
                        }
                    }
                    catch (Exception ex) when (IsNodeNotFoundError(ex))
                    {
                        var errorMsg = "Node.js is not installed or not found in PATH. Please install Node.js from https://nodejs.org and restart LiveStrike.";
                        TryLog($"Node.js not found: {errorMsg}");
                        throw new InvalidOperationException(errorMsg, ex);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Failed to start Node.js process: {ex.Message}. Please ensure Node.js is installed from https://nodejs.org";
                        TryLog($"Process start failed: {errorMsg}");
                        throw new InvalidOperationException(errorMsg, ex);
                    }

                    TryLog($"Node process started with PID: {_proc.Id}");
                    TryLog("Node server auto-started successfully");
                    _proc.BeginOutputReadLine();
                    _proc.BeginErrorReadLine();

                    // 4) Wait until /status responds
                    TryLog("Waiting for server to respond...");
                    var sw = Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < startupTimeoutMs)
                    {
                        if (await IsServerUp(baseUrl))
                        {
                            TryLog($"Server is up after {sw.ElapsedMilliseconds}ms");
                            return true;
                        }
                        await Task.Delay(500);
                    }

                    TryLog($"Server failed to start within {startupTimeoutMs}ms timeout");
                    return false;
                }

                public static async Task<bool> IsServerUp(string baseUrl)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(2500);
                        var res = await _http.GetAsync(new Uri(new Uri(baseUrl), "/status"), cts.Token);
                        if (!res.IsSuccessStatusCode) return false;

                        using var s = await res.Content.ReadAsStreamAsync();
                        using var json = await JsonDocument.ParseAsync(s);
                        return json.RootElement.TryGetProperty("status", out var st) &&
                               string.Equals(st.GetString(), "running", StringComparison.OrdinalIgnoreCase);
                    }
                    catch { return false; }
                }

                public static void Stop()
                {
                    TryKillOld();
                }

                private static bool IsNodeNotFoundError(Exception ex)
                {
                    // Check for common Node.js not found error patterns
                    var message = ex.Message?.ToLowerInvariant() ?? "";
                    return message.Contains("system cannot find the file specified") ||
                           message.Contains("'node' is not recognized") ||
                           message.Contains("no such file or directory") ||
                           ex is System.ComponentModel.Win32Exception;
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

                private static void TryLogToFile(string message)
                {
                    try
                    {
                        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CS2Overlay");
                        Directory.CreateDirectory(dir);
                        var file = Path.Combine(dir, "app.log");
                        File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
                    }
                    catch { /* ignore logging errors */ }
                }

                private static void TryKillOld()
                {
                    try
                    {
                        if (_proc == null) return;
                        if (!_proc.HasExited)
                        {
                            _proc.Kill(entireProcessTree: true);
                            _proc.WaitForExit(2000);
                        }
                        _proc.Dispose();
                    }
                    catch { /* ignore */ }
                    finally { _proc = null; }
                }

                private static string Truncate(string value, int max)
                {
                    if (string.IsNullOrEmpty(value)) return string.Empty;
                    return value.Length > max ? value.Substring(0, max) + "…" : value;
                }
            }
        }
