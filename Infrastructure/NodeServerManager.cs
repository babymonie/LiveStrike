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
            // Try to find server.js in project directory structure
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var projectServerPath = Path.Combine(baseDir, "..", "..", "..", "server", "server.js");
            if (File.Exists(projectServerPath))
                return Path.GetFullPath(projectServerPath);
            
            // Fallback to build output directory
            return Path.Combine(baseDir, "server", "server.js");
        }
        
        private static string GetDefaultWorkingDirectory()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var projectServerDir = Path.Combine(baseDir, "..", "..", "..", "server");
            if (Directory.Exists(projectServerDir))
                return Path.GetFullPath(projectServerDir);
            
            // Fallback to build output directory
            return Path.Combine(baseDir, "server");
        }
    }

    public static class NodeServerManager
    {
        private static Process? _proc;
        private static readonly HttpClient _http = new HttpClient();

        public static async Task<bool> EnsureStartedAsync(string baseUrl, NodeServerOptions? options = null, int startupTimeoutMs = 15000)
        {
            options ??= new NodeServerOptions();
            TryLogToFile($"EnsureStartedAsync called for {baseUrl}");
            
            // 1) Already up?
            if (await IsServerUp(baseUrl)) 
            {
                TryLogToFile("Server already running");
                return true;
            }

            // Only auto-start if pointing to localhost/127.0.0.1
            var uri = new Uri(baseUrl);
            var host = uri.Host;
            if (!(string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1"))
            {
                TryLogToFile($"Not auto-starting server for non-localhost host: {host}");
                return false;
            }

            // 2) Start node server
            TryLogToFile("Starting Node server...");
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
                TryLogToFile($"Process creation failed: {errorMsg}");
                throw new InvalidOperationException(errorMsg, ex);
            }

            _proc.OutputDataReceived += (_, e) => { 
                if (!string.IsNullOrWhiteSpace(e.Data)) 
                {
                    Debug.WriteLine("[node] " + e.Data);
                    // Also log to file for debugging
                    TryLogToFile("[node] " + e.Data);
                }
            };
            _proc.ErrorDataReceived +=  (_, e) => { 
                if (!string.IsNullOrWhiteSpace(e.Data)) 
                {
                    Debug.WriteLine("[node:err] " + e.Data);
                    TryLogToFile("[node:err] " + e.Data);
                }
            };
            _proc.Exited += (_, e) => {
                TryLogToFile($"[node] Process exited with code: {_proc?.ExitCode}");
            };

            try
            {
                if (!_proc.Start())
                {
                    var errorMsg = "Node.js process failed to start. Please ensure Node.js is installed from https://nodejs.org";
                    TryLogToFile(errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }
            }
            catch (Exception ex) when (IsNodeNotFoundError(ex))
            {
                var errorMsg = "Node.js is not installed or not found in PATH. Please install Node.js from https://nodejs.org and restart LiveStrike.";
                TryLogToFile($"Node.js not found: {errorMsg}");
                throw new InvalidOperationException(errorMsg, ex);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to start Node.js process: {ex.Message}. Please ensure Node.js is installed from https://nodejs.org";
                TryLogToFile($"Process start failed: {errorMsg}");
                throw new InvalidOperationException(errorMsg, ex);
            }

            TryLogToFile($"Node process started with PID: {_proc.Id}");
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            // 3) Wait until /status responds
            TryLogToFile("Waiting for server to respond...");
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < startupTimeoutMs)
            {
                if (await IsServerUp(baseUrl)) 
                {
                    TryLogToFile($"Server is up after {sw.ElapsedMilliseconds}ms");
                    return true;
                }
                await Task.Delay(500);
            }

            TryLogToFile($"Server failed to start within {startupTimeoutMs}ms timeout");
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
    }
}
