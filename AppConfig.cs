using System;
using System.IO;
using System.Text.Json;

namespace BatteryGuard
{
    /// <summary>
    /// Persisted user settings. Stored as JSON under %AppData%\BatteryGuard\config.json
    /// so the app remembers thresholds across restarts without needing an installer.
    /// </summary>
    public class AppConfig
    {
        public int UpperThreshold { get; set; } = 80;   // unplug at/above this %
        public int LowerThreshold { get; set; } = 20;   // plug in at/below this %
        public int PollIntervalSeconds { get; set; } = 15;
        public int SnoozeMinutes { get; set; } = 5;
        public bool StartWithWindows { get; set; } = false;
        public bool EnableSoundAlerts { get; set; } = true;

        private static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryGuard");

        private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg != null) return cfg.Sanitized();
                }
            }
            catch
            {
                // Corrupt or unreadable config -> fall back to defaults rather than crash.
            }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Best-effort persistence; app still works in-memory for this session if this fails.
            }
        }

        /// <summary>Clamp values to sane ranges so a hand-edited config.json can't break the app.</summary>
        private AppConfig Sanitized()
        {
            UpperThreshold = Math.Clamp(UpperThreshold, 50, 100);
            LowerThreshold = Math.Clamp(LowerThreshold, 0, 50);
            if (LowerThreshold >= UpperThreshold) LowerThreshold = Math.Max(0, UpperThreshold - 10);
            PollIntervalSeconds = Math.Clamp(PollIntervalSeconds, 5, 300);
            SnoozeMinutes = Math.Clamp(SnoozeMinutes, 1, 60);
            return this;
        }
    }
}
