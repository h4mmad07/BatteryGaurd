using System;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace BatteryGuard
{
    public enum AlertKind { Unplug, PlugIn }

    /// <summary>
    /// Deliberately hard to ignore: no title bar close button, always-on-top,
    /// re-asserts focus, and only goes away when the underlying condition is
    /// resolved (charger actually unplugged/plugged) or the user hits Snooze.
    /// </summary>
    public class AlertForm : Form
    {
        public AlertKind Kind { get; }
        private readonly Label _messageLabel;
        private readonly System.Windows.Forms.Timer _refocusTimer;
        private readonly System.Windows.Forms.Timer _reminderBeepTimer;

        public event EventHandler? SnoozeRequested;
        private bool _allowClose;

        private readonly bool _enableSound;

        public AlertForm(AlertKind kind, int thresholdPercent, bool enableSound = true)
        {
            Kind = kind;
            _enableSound = enableSound;

            // --- Window chrome: borderless, no system close button, always on top ---
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = true; // keep it visible in taskbar so alt-tab still finds it
            Size = new Size(440, 230);
            BackColor = kind == AlertKind.Unplug ? Color.FromArgb(178, 34, 34) : Color.FromArgb(30, 120, 60);
            KeyPreview = true;

            var title = new Label
            {
                Text = kind == AlertKind.Unplug ? "🔌 UNPLUG THE CHARGER" : "🔋 PLUG IN THE CHARGER",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 55,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _messageLabel = new Label
            {
                Text = kind == AlertKind.Unplug
                    ? $"Battery reached {thresholdPercent}%.\nDisconnect the charger to prolong battery lifespan."
                    : $"Battery dropped to {thresholdPercent}%.\nConnect your charger before battery drains further.",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var snoozeButton = new Button
            {
                Text = "Remind me later (Esc)",
                Dock = DockStyle.Bottom,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            snoozeButton.FlatAppearance.BorderSize = 0;
            snoozeButton.Click += (_, _) => SnoozeRequested?.Invoke(this, EventArgs.Empty);

            var watermark = new Label
            {
                Text = "BatteryGuard • Developed by: M Hammad",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(220, 220, 220),
                Dock = DockStyle.Bottom,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter
            };

            Controls.Add(_messageLabel);
            Controls.Add(watermark);
            Controls.Add(snoozeButton);
            Controls.Add(title);

            // Block Alt+F4 — this alert should only close via Snooze or condition resolution.
            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing && !_allowClose)
                    e.Cancel = true;
            };

            // Re-assert top-most visibility periodically
            _refocusTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _refocusTimer.Tick += (_, _) =>
            {
                if (!IsDisposed)
                {
                    TopMost = false;
                    TopMost = true;
                }
            };

            // Gentle repeated beep if sound is enabled
            _reminderBeepTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            _reminderBeepTimer.Tick += (_, _) =>
            {
                if (_enableSound)
                    SystemSounds.Exclamation.Play();
            };

            Shown += (_, _) =>
            {
                if (_enableSound)
                    SystemSounds.Exclamation.Play();
                _refocusTimer.Start();
                _reminderBeepTimer.Start();
            };

            FormClosed += (_, _) =>
            {
                _refocusTimer.Stop();
                _reminderBeepTimer.Stop();
            };
        }

        /// <summary>Force-close programmatically (condition resolved, or user snoozed) bypassing the guard.</summary>
        public void CloseForced()
        {
            _allowClose = true;
            base.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refocusTimer.Dispose();
                _reminderBeepTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Allow quick dismissal via Esc or Space
            if (keyData == Keys.Escape || keyData == Keys.Space)
            {
                SnoozeRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }
            // Block Alt+F4 explicitly
            if (keyData == (Keys.Alt | Keys.F4)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
