using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BatteryGuard
{
    /// <summary>
    /// System tray application context. Manages the dynamic battery icon,
    /// power event listeners, poll timer, notifications, and alert life-cycle.
    /// </summary>
    public class TrayAppContext : ApplicationContext
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private readonly NotifyIcon _trayIcon;
        private readonly System.Windows.Forms.Timer _pollTimer;
        private readonly AppConfig _config;

        private ToolStripMenuItem _statusMenuItem = null!;
        private ToolStripMenuItem _pauseMenuItem = null!;

        private AlertForm? _activeAlert;
        private DateTime _snoozedUntil = DateTime.MinValue;
        private AlertKind? _snoozedKind;
        private DateTime _pausedUntil = DateTime.MinValue;

        private Icon? _currentCustomIcon;

        public TrayAppContext()
        {
            _config = AppConfig.Load();

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Text = "Battery Guard",
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };
            _trayIcon.DoubleClick += (_, _) => OpenHud();

            _pollTimer = new System.Windows.Forms.Timer { Interval = _config.PollIntervalSeconds * 1000 };
            _pollTimer.Tick += (_, _) => CheckBattery();
            _pollTimer.Start();

            // Respond instantly to power cable connect/disconnect events
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            CheckBattery(); // run immediately on startup
        }

        private BatteryHudForm? _hudForm;

        private void OpenHud()
        {
            if (_hudForm == null || _hudForm.IsDisposed)
            {
                _hudForm = new BatteryHudForm(_config, () =>
                {
                    _pollTimer.Interval = _config.PollIntervalSeconds * 1000;
                    CheckBattery();
                },
                () => DateTime.Now < _pausedUntil,
                () => TogglePause());

                _hudForm.FormClosed += (_, _) => _hudForm = null;
                _hudForm.Show();
            }
            else
            {
                _hudForm.WindowState = FormWindowState.Normal;
                _hudForm.BringToFront();
                _hudForm.Activate();
                _hudForm.RefreshTelemetry();
            }
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            _statusMenuItem = new ToolStripMenuItem("Battery Guard") { Enabled = false };
            _statusMenuItem.Font = new Font(_statusMenuItem.Font, FontStyle.Bold);

            _pauseMenuItem = new ToolStripMenuItem("Pause alerts for 1 hour", null, (_, _) => TogglePause());

            menu.Items.Add(_statusMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("⚡ Battery HUD & Health Diagnostics...", null, (_, _) => OpenHud());
            menu.Items.Add("Check Status", null, (_, _) => ShowStatus());
            menu.Items.Add(_pauseMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
            menu.Items.Add(new ToolStripSeparator());
            var devItem = new ToolStripMenuItem("Developed by: M Hammad") { Enabled = false };
            devItem.Font = new Font(devItem.Font, FontStyle.Italic);
            menu.Items.Add(devItem);
            menu.Items.Add("Exit", null, (_, _) => ExitApp());
            return menu;
        }

        private void TogglePause()
        {
            if (DateTime.Now < _pausedUntil)
            {
                _pausedUntil = DateTime.MinValue;
                _pauseMenuItem.Text = "Pause alerts for 1 hour";
                _trayIcon.ShowBalloonTip(2000, "Battery Guard", "Alerts resumed.", ToolTipIcon.Info);
            }
            else
            {
                _pausedUntil = DateTime.Now.AddHours(1);
                _pauseMenuItem.Text = "Resume alerts (Paused)";
                CloseActiveAlert();
                _trayIcon.ShowBalloonTip(2000, "Battery Guard", "Alerts paused for 1 hour.", ToolTipIcon.Info);
            }
            CheckBattery();
        }

        private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.StatusChange)
            {
                CheckBattery();
            }
        }

        private void ShowStatus()
        {
            var status = SystemInformation.PowerStatus;
            if (status.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) || status.BatteryLifePercent < 0)
            {
                _trayIcon.ShowBalloonTip(3000, "Battery Guard", "No system battery detected on this device.", ToolTipIcon.Info);
                return;
            }

            int percent = (int)Math.Round(status.BatteryLifePercent * 100);
            string charging = status.PowerLineStatus == PowerLineStatus.Online ? "Plugged in (Charging/AC)" : "On battery";
            string soundState = _config.EnableSoundAlerts ? "Enabled" : "Muted";
            string pausedState = DateTime.Now < _pausedUntil ? " [Alerts Paused]" : "";

            _trayIcon.ShowBalloonTip(3000, "Battery Guard",
                $"{percent}% — {charging}{pausedState}\nUnplug at {_config.UpperThreshold}% · Plug in at {_config.LowerThreshold}%\nSound alerts: {soundState}",
                ToolTipIcon.Info);
        }

        private void OpenSettings()
        {
            using var form = new SettingsForm(_config);
            if (form.ShowDialog() == DialogResult.OK)
            {
                _pollTimer.Interval = _config.PollIntervalSeconds * 1000;
                CheckBattery();
            }
        }

        private void CheckBattery()
        {
            var status = SystemInformation.PowerStatus;

            // Handle non-battery systems (e.g., desktop computers)
            if (status.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) || status.BatteryLifePercent < 0)
            {
                _statusMenuItem.Text = "Battery: Not Detected";
                SetTrayText("Battery Guard — No battery detected");
                return;
            }

            int percent = (int)Math.Round(status.BatteryLifePercent * 100);
            bool isCharging = status.PowerLineStatus == PowerLineStatus.Online;

            // Update dynamic icon
            UpdateTrayIcon(percent, isCharging);

            // Update menu status
            string stateDesc = isCharging ? "Charging" : "Discharging";
            if (DateTime.Now < _pausedUntil)
            {
                stateDesc += " (Paused)";
            }
            _statusMenuItem.Text = $"🔋 {percent}% — {stateDesc}";

            // Safe tooltip text (Windows NotifyIcon max is 63 chars)
            string tip = $"Battery Guard — {percent}% ({(isCharging ? "charging" : "on battery")})";
            SetTrayText(tip);

            bool shouldUnplug = isCharging && percent >= _config.UpperThreshold;
            bool shouldPlugIn = !isCharging && percent <= _config.LowerThreshold;

            // Condition resolved or alerts paused -> dismiss open alert
            if (_activeAlert != null && (!shouldUnplug && !shouldPlugIn || DateTime.Now < _pausedUntil))
            {
                CloseActiveAlert();
            }

            if (DateTime.Now < _pausedUntil)
            {
                return;
            }

            if (shouldUnplug)
                MaybeShowAlert(AlertKind.Unplug, percent);
            else if (shouldPlugIn)
                MaybeShowAlert(AlertKind.PlugIn, percent);
        }

        private void SetTrayText(string text)
        {
            if (text.Length > 63)
                text = text.Substring(0, 63);
            _trayIcon.Text = text;
        }

        private void UpdateTrayIcon(int percent, bool isCharging)
        {
            try
            {
                var newIcon = CreateBatteryIcon(percent, isCharging);
                var oldIcon = _currentCustomIcon;
                _currentCustomIcon = newIcon;
                _trayIcon.Icon = newIcon;
                oldIcon?.Dispose();
            }
            catch
            {
                // Fallback to standard shield icon if GDI fails
                _trayIcon.Icon = SystemIcons.Shield;
            }
        }

        private Icon CreateBatteryIcon(int percent, bool isCharging)
        {
            using var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                var bodyRect = new Rectangle(2, 9, 23, 14);
                var capRect = new Rectangle(25, 13, 3, 6);

                using (var borderPen = new Pen(Color.White, 2f))
                {
                    g.DrawRectangle(borderPen, bodyRect);
                }
                using (var capBrush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(capBrush, capRect);
                }

                // Choose fill color
                Color fillColor;
                if (isCharging)
                {
                    fillColor = percent >= _config.UpperThreshold
                        ? Color.FromArgb(255, 80, 80)
                        : Color.FromArgb(60, 180, 255);
                }
                else
                {
                    if (percent <= _config.LowerThreshold)
                        fillColor = Color.FromArgb(255, 70, 70);
                    else if (percent <= 35)
                        fillColor = Color.FromArgb(255, 185, 45);
                    else
                        fillColor = Color.FromArgb(60, 220, 100);
                }

                int fillWidth = (int)Math.Round((bodyRect.Width - 4) * (Math.Clamp(percent, 0, 100) / 100.0));
                if (fillWidth > 0)
                {
                    using var fillBrush = new SolidBrush(fillColor);
                    g.FillRectangle(fillBrush, bodyRect.X + 2, bodyRect.Y + 2, fillWidth, bodyRect.Height - 4);
                }

                // Draw charging lightning bolt if plugged in
                if (isCharging)
                {
                    Point[] bolt = new Point[]
                    {
                        new Point(15, 8),
                        new Point(10, 16),
                        new Point(14, 16),
                        new Point(12, 23),
                        new Point(19, 14),
                        new Point(15, 14)
                    };
                    using var boltBrush = new SolidBrush(Color.Yellow);
                    g.FillPolygon(boltBrush, bolt);
                    using var boltPen = new Pen(Color.Black, 1f);
                    g.DrawPolygon(boltPen, bolt);
                }
            }

            IntPtr hIcon = bitmap.GetHicon();
            var icon = (Icon)Icon.FromHandle(hIcon).Clone();
            DestroyIcon(hIcon);
            return icon;
        }

        private void MaybeShowAlert(AlertKind kind, int percent)
        {
            if (_activeAlert != null && _activeAlert.Kind == kind) return;

            if (_snoozedKind == kind && DateTime.Now < _snoozedUntil) return;

            if (_activeAlert != null && _activeAlert.Kind != kind)
                CloseActiveAlert();

            int threshold = kind == AlertKind.Unplug ? _config.UpperThreshold : _config.LowerThreshold;
            var alert = new AlertForm(kind, threshold, _config.EnableSoundAlerts);
            alert.SnoozeRequested += (_, _) =>
            {
                _snoozedKind = kind;
                _snoozedUntil = DateTime.Now.AddMinutes(_config.SnoozeMinutes);
                CloseActiveAlert();
            };
            alert.FormClosed += (_, _) => { if (_activeAlert == alert) _activeAlert = null; };

            _activeAlert = alert;
            alert.Show();
            alert.Activate();
        }

        private void CloseActiveAlert()
        {
            _activeAlert?.CloseForced();
            _activeAlert = null;
        }

        private void ExitApp()
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            CloseActiveAlert();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _pollTimer.Stop();
            _pollTimer.Dispose();
            _currentCustomIcon?.Dispose();
            Application.Exit();
        }
    }
}
