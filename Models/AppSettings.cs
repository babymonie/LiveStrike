using System;
using System.IO;
using System.Text.Json;

namespace Models
{
    public class AppSettings
    {
        public bool EnableAnimations { get; set; } = true;
        public double HoverOpacity { get; set; } = 0.15;
        public int PollingIntervalMs { get; set; } = 2000;
        public bool StartOnLogin { get; set; } = false;
        public string Theme { get; set; } = "Dark";
        public bool MinimizeToTray { get; set; } = true;
        public bool AutoStartNodeServer { get; set; } = true;

        /// <summary>
        /// Gets the full path where LiveStrike settings are stored
        /// </summary>
        public static string ConfigDirectory => 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LiveStrike");
        private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "config.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }
}