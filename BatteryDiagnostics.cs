using System;
using System.IO;
using System.Management;
using System.Windows.Forms;
using System.Xml.Linq;

namespace BatteryGuard
{
    public class BatteryDiagnosticsData
    {
        public bool HasBattery { get; set; } = true;
        public int Percent { get; set; }
        public bool IsCharging { get; set; }
        public string PowerLineStatusText { get; set; } = "Unknown";
        public TimeSpan? EstimatedRunTime { get; set; }

        public string Manufacturer { get; set; } = "Generic / OEM";
        public string DeviceName { get; set; } = "Primary Battery";
        public string SerialNumber { get; set; } = "N/A";
        public string Chemistry { get; set; } = "LIon";

        public int DesignCapacityMwh { get; set; }
        public int FullChargeCapacityMwh { get; set; }
        public int RemainingCapacityMwh { get; set; }
        public double HealthPercent { get; set; } = 100.0;
        public int CycleCount { get; set; }

        public double VoltageVolts { get; set; }
        public double ChargeRateWatts { get; set; }
        public double DischargeRateWatts { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public static class BatteryDiagnostics
    {
        private static int _cachedDesignCapacity = 0;
        private static string _cachedManufacturer = "";
        private static string _cachedSerial = "";
        private static string _cachedChemistry = "";

        public static BatteryDiagnosticsData ReadDiagnostics()
        {
            var data = new BatteryDiagnosticsData();
            var power = SystemInformation.PowerStatus;

            if (power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) || power.BatteryLifePercent < 0)
            {
                data.HasBattery = false;
                data.PowerLineStatusText = "Desktop / No Battery";
                return data;
            }

            data.Percent = (int)Math.Round(power.BatteryLifePercent * 100);
            data.IsCharging = power.PowerLineStatus == PowerLineStatus.Online;
            data.PowerLineStatusText = data.IsCharging ? "Plugged in (AC Online)" : "On Battery (Discharging)";

            if (power.BatteryLifeRemaining > 0 && power.BatteryLifeRemaining < 86400)
            {
                data.EstimatedRunTime = TimeSpan.FromSeconds(power.BatteryLifeRemaining);
            }

            if (_cachedDesignCapacity == 0)
            {
                ReadStaticBatteryData();
            }

            data.DesignCapacityMwh = _cachedDesignCapacity;
            data.Manufacturer = string.IsNullOrEmpty(_cachedManufacturer) ? "HP / OEM" : _cachedManufacturer;
            data.SerialNumber = string.IsNullOrEmpty(_cachedSerial) ? "Primary" : _cachedSerial;
            data.Chemistry = string.IsNullOrEmpty(_cachedChemistry) ? "LIon" : _cachedChemistry;

            ReadLiveWmiMetrics(data);

            if (data.DesignCapacityMwh > 0 && data.FullChargeCapacityMwh > 0)
            {
                data.HealthPercent = Math.Clamp(Math.Round(((double)data.FullChargeCapacityMwh / data.DesignCapacityMwh) * 100, 1), 0, 100);
            }
            else
            {
                data.HealthPercent = 100.0;
            }

            data.LastUpdated = DateTime.Now;
            return data;
        }

        private static void ReadLiveWmiMetrics(BatteryDiagnosticsData data)
        {
            try
            {
                using var searcherCapacity = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM BatteryFullChargedCapacity");
                foreach (ManagementObject obj in searcherCapacity.Get())
                {
                    if (obj["FullChargedCapacity"] != null)
                    {
                        data.FullChargeCapacityMwh = Convert.ToInt32(obj["FullChargedCapacity"]);
                    }
                }
            }
            catch { }

            try
            {
                using var searcherCycle = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM BatteryCycleCount");
                foreach (ManagementObject obj in searcherCycle.Get())
                {
                    if (obj["CycleCount"] != null)
                    {
                        data.CycleCount = Convert.ToInt32(obj["CycleCount"]);
                    }
                }
            }
            catch { }

            try
            {
                using var searcherStatus = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM BatteryStatus");
                foreach (ManagementObject obj in searcherStatus.Get())
                {
                    if (obj["Voltage"] != null)
                    {
                        int mv = Convert.ToInt32(obj["Voltage"]);
                        data.VoltageVolts = Math.Round(mv / 1000.0, 2);
                    }
                    if (obj["RemainingCapacity"] != null)
                    {
                        data.RemainingCapacityMwh = Convert.ToInt32(obj["RemainingCapacity"]);
                    }
                    if (obj["ChargeRate"] != null)
                    {
                        int mw = Convert.ToInt32(obj["ChargeRate"]);
                        data.ChargeRateWatts = Math.Round(mw / 1000.0, 1);
                    }
                    if (obj["DischargeRate"] != null)
                    {
                        int mw = Convert.ToInt32(obj["DischargeRate"]);
                        data.DischargeRateWatts = Math.Round(mw / 1000.0, 1);
                    }
                }
            }
            catch { }

            if (data.FullChargeCapacityMwh == 0 && data.DesignCapacityMwh > 0)
            {
                data.FullChargeCapacityMwh = data.DesignCapacityMwh;
            }
        }

        private static void ReadStaticBatteryData()
        {
            try
            {
                string tempXml = Path.Combine(Path.GetTempPath(), "battery_report.xml");
                if (!File.Exists(tempXml) || (DateTime.Now - File.GetLastWriteTime(tempXml)).TotalHours > 12)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = $"/batteryreport /xml /output \"{tempXml}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    proc?.WaitForExit(3000);
                }

                if (File.Exists(tempXml))
                {
                    var doc = XDocument.Load(tempXml);
                    XNamespace ns = "http://schemas.microsoft.com/battery/2012";
                    var batteryNode = doc.Descendants(ns + "Battery");
                    foreach (var b in batteryNode)
                    {
                        var design = b.Element(ns + "DesignCapacity")?.Value;
                        if (int.TryParse(design, out int dCap))
                            _cachedDesignCapacity = dCap;

                        _cachedManufacturer = b.Element(ns + "Manufacturer")?.Value ?? "";
                        _cachedSerial = b.Element(ns + "SerialNumber")?.Value ?? "";
                        _cachedChemistry = b.Element(ns + "Chemistry")?.Value ?? "LIon";
                        break;
                    }
                }
            }
            catch { }

            if (_cachedDesignCapacity == 0)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["DesignCapacity"] != null && Convert.ToInt32(obj["DesignCapacity"]) > 0)
                            _cachedDesignCapacity = Convert.ToInt32(obj["DesignCapacity"]);

                        if (obj["Name"] != null && string.IsNullOrEmpty(_cachedManufacturer))
                            _cachedManufacturer = obj["Name"].ToString() ?? "";

                        if (obj["DeviceID"] != null && string.IsNullOrEmpty(_cachedSerial))
                            _cachedSerial = obj["DeviceID"].ToString() ?? "";
                    }
                }
                catch { }
            }

            if (_cachedDesignCapacity == 0)
            {
                _cachedDesignCapacity = 34200;
            }
        }
    }
}
