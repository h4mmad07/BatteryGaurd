using Microsoft.Win32;

namespace BatteryGuard
{
    /// <summary>
    /// Registers/unregisters the app to launch at Windows login via the standard
    /// per-user Run key. No admin rights needed, no installer required.
    /// </summary>
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "BatteryGuard";

        public static void SetEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = System.Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath;
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue(ValueName) != null)
                    key.DeleteValue(ValueName);
            }
        }

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) != null;
        }
    }
}
