using System;
using System.Drawing;
using System.Windows.Forms;

namespace BatteryGuard
{
    public class SettingsForm : Form
    {
        private readonly NumericUpDown _upperInput;
        private readonly NumericUpDown _lowerInput;
        private readonly NumericUpDown _snoozeInput;
        private readonly CheckBox _startupCheck;
        private readonly CheckBox _soundCheck;
        private readonly AppConfig _config;

        public SettingsForm(AppConfig config)
        {
            _config = config;

            Text = "Battery Guard — Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(360, 310);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            var power = SystemInformation.PowerStatus;
            int currentPct = (int)Math.Round(power.BatteryLifePercent * 100);
            string powerState = power.PowerLineStatus == PowerLineStatus.Online ? "Plugged in (Charging/AC)" : "On battery";

            var statusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                Padding = new Padding(12, 10, 12, 0)
            };
            var statusLabel = new Label
            {
                Text = $"Current Battery: {currentPct}% ({powerState})",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 80, 150),
                Dock = DockStyle.Fill
            };
            statusPanel.Controls.Add(statusLabel);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 5, 15, 10),
                ColumnCount = 2,
                RowCount = 5,
                AutoSize = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _upperInput = new NumericUpDown { Minimum = 50, Maximum = 100, Value = config.UpperThreshold, Dock = DockStyle.Fill };
            _lowerInput = new NumericUpDown { Minimum = 0, Maximum = 50, Value = config.LowerThreshold, Dock = DockStyle.Fill };
            _snoozeInput = new NumericUpDown { Minimum = 1, Maximum = 60, Value = config.SnoozeMinutes, Dock = DockStyle.Fill };
            // Read the actual Run key so the checkbox also reflects installer choices.
            _startupCheck = new CheckBox { Text = "Start with Windows", Checked = StartupManager.IsEnabled(), Dock = DockStyle.Fill };
            _soundCheck = new CheckBox { Text = "Play sound with alerts", Checked = config.EnableSoundAlerts, Dock = DockStyle.Fill };

            layout.Controls.Add(new Label { Text = "Unplug at (%):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            layout.Controls.Add(_upperInput, 1, 0);
            layout.Controls.Add(new Label { Text = "Plug in at (%):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            layout.Controls.Add(_lowerInput, 1, 1);
            layout.Controls.Add(new Label { Text = "Snooze (minutes):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            layout.Controls.Add(_snoozeInput, 1, 2);
            layout.Controls.Add(_soundCheck, 0, 3);
            layout.SetColumnSpan(_soundCheck, 2);
            layout.Controls.Add(_startupCheck, 0, 4);
            layout.SetColumnSpan(_startupCheck, 2);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 45,
                Padding = new Padding(10, 5, 10, 5)
            };
            var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK, Width = 85, Height = 30 };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 85, Height = 30 };
            var watermark = new Label
            {
                Text = "Developed by: M Hammad",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.Gray,
                AutoSize = false,
                Size = new Size(155, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            saveButton.Click += (_, _) => SaveAndClose();
            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(watermark);

            Controls.Add(layout);
            Controls.Add(statusPanel);
            Controls.Add(buttonPanel);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private void SaveAndClose()
        {
            if (_lowerInput.Value >= _upperInput.Value)
            {
                MessageBox.Show(this, "\"Plug in at\" must be lower than \"Unplug at\".", "Invalid settings",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            _config.UpperThreshold = (int)_upperInput.Value;
            _config.LowerThreshold = (int)_lowerInput.Value;
            _config.SnoozeMinutes = (int)_snoozeInput.Value;
            _config.EnableSoundAlerts = _soundCheck.Checked;
            _config.StartWithWindows = _startupCheck.Checked;
            _config.Save();
            StartupManager.SetEnabled(_config.StartWithWindows);
        }
    }
}
