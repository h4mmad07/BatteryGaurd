using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BatteryGuard
{
    public class BatteryHudForm : Form
    {
        private readonly AppConfig _config;
        private readonly Action _onConfigChanged;
        private readonly Func<bool> _isPausedFunc;
        private readonly Action _togglePauseAction;

        private readonly System.Windows.Forms.Timer _refreshTimer;
        private BatteryDiagnosticsData _data = new BatteryDiagnosticsData();

        // UI Controls
        private Label _percentLabel = null!;
        private Label _statusPill = null!;
        private Label _timeRemainingLabel = null!;
        private Label _voltageLabel = null!;
        private Label _flowRateLabel = null!;
        private Label _capacityLabel = null!;

        private Label _healthPercentLabel = null!;
        private ProgressBar _healthBar = null!;
        private Label _designCapLabel = null!;
        private Label _fullCapLabel = null!;
        private Label _wearLabel = null!;
        private Label _cycleCountLabel = null!;
        private Label _hardwareModelLabel = null!;

        private TrackBar _upperSlider = null!;
        private Label _upperValueLabel = null!;
        private TrackBar _lowerSlider = null!;
        private Label _lowerValueLabel = null!;
        private CheckBox _soundCheck = null!;
        private CheckBox _startupCheck = null!;
        private Button _pauseButton = null!;

        private Panel _gaugeBox = null!;

        public BatteryHudForm(AppConfig config, Action onConfigChanged, Func<bool> isPausedFunc, Action togglePauseAction)
        {
            _config = config;
            _onConfigChanged = onConfigChanged;
            _isPausedFunc = isPausedFunc;
            _togglePauseAction = togglePauseAction;

            DoubleBuffered = true;
            Text = "BatteryGuard — Battery HUD & Health Diagnostics";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(880, 640);
            BackColor = Color.FromArgb(15, 23, 42); // Slate 900
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            KeyPreview = true;

            BuildUi();

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _refreshTimer.Tick += (_, _) => RefreshTelemetry();
            _refreshTimer.Start();

            Shown += (_, _) => RefreshTelemetry();
            FormClosing += (_, _) => _refreshTimer.Stop();
        }

        private void BuildUi()
        {
            // --- HEADER BAR ---
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(24, 12, 24, 10)
            };

            var title = new Label
            {
                Text = "⚡ BATTERY GUARD — INTERACTIVE TELEMETRY & HEALTH HUD",
                Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248), // Sky 400
                Dock = DockStyle.Top,
                Height = 26,
                AutoEllipsis = true
            };

            var subtitle = new Label
            {
                Text = "Real-time battery status, health degradation diagnostics, and smart charge thresholds",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184), // Slate 400
                Dock = DockStyle.Bottom,
                Height = 22,
                AutoEllipsis = true
            };
            var devBadge = new Label
            {
                Text = "Developed by: M Hammad",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic | FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Dock = DockStyle.Right,
                Width = 190,
                TextAlign = ContentAlignment.MiddleRight
            };

            header.Controls.Add(devBadge);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            // --- FOOTER WATERMARK BAR ---
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(24, 2, 24, 4)
            };
            var footerLabel = new Label
            {
                Text = "🛡️ BatteryGuard • Developed by: M Hammad",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            };
            footer.Controls.Add(footerLabel);

            // --- MAIN 2-COLUMN CONTAINER ---
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(16, 10, 16, 10)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));

            mainLayout.Controls.Add(BuildLeftPanel(), 0, 0);
            mainLayout.Controls.Add(BuildRightPanel(), 1, 0);

            Controls.Add(mainLayout);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private Panel BuildLeftPanel()
        {
            var leftWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4)
            };

            // Gauge Container
            _gaugeBox = new Panel
            {
                Dock = DockStyle.Top,
                Height = 260,
                BackColor = Color.FromArgb(24, 32, 53),
                Margin = new Padding(0, 0, 0, 10)
            };
            _gaugeBox.Paint += (s, e) => DrawCircularGauge(e.Graphics, _gaugeBox.ClientRectangle);
            _gaugeBox.Resize += (s, e) => RepositionGaugeLabels();

            _percentLabel = new Label
            {
                Text = "--%",
                Font = new Font("Segoe UI", 36F, FontStyle.Bold),
                ForeColor = Color.White,
                Size = new Size(200, 65),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            _statusPill = new Label
            {
                Text = "INITIALIZING...",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Size = new Size(180, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(30, 41, 59)
            };

            _timeRemainingLabel = new Label
            {
                Text = "Estimating runtime...",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(203, 213, 225),
                Size = new Size(260, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            _gaugeBox.Controls.Add(_percentLabel);
            _gaugeBox.Controls.Add(_statusPill);
            _gaugeBox.Controls.Add(_timeRemainingLabel);
            RepositionGaugeLabels();

            // Bottom Metrics Table
            var metricsCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 32, 53),
                Padding = new Padding(10),
                Margin = new Padding(0, 10, 0, 0)
            };

            var metricsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            metricsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            metricsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            metricsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            metricsTable.Controls.Add(CreateMetricCard("VOLTAGE", out _voltageLabel), 0, 0);
            metricsTable.Controls.Add(CreateMetricCard("FLOW RATE", out _flowRateLabel), 1, 0);
            var capCard = CreateMetricCard("REMAINING / FULL CAPACITY", out _capacityLabel);
            metricsTable.Controls.Add(capCard, 0, 1);
            metricsTable.SetColumnSpan(capCard, 2);

            metricsCard.Controls.Add(metricsTable);

            // Left Layout Assembly
            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            split.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            split.Controls.Add(_gaugeBox, 0, 0);
            split.Controls.Add(metricsCard, 0, 1);

            leftWrapper.Controls.Add(split);
            return leftWrapper;
        }

        private void RepositionGaugeLabels()
        {
            int w = _gaugeBox.Width;
            if (w <= 0) w = 380;
            _percentLabel.Location = new Point((w - _percentLabel.Width) / 2, 55);
            _statusPill.Location = new Point((w - _statusPill.Width) / 2, 126);
            _timeRemainingLabel.Location = new Point((w - _timeRemainingLabel.Width) / 2, 160);
        }

        private Panel BuildRightPanel()
        {
            var rightWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4)
            };

            var rightTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 225));
            rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Section 1: Health & Hardware Specs Card
            var healthCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 32, 53),
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 0, 0, 8)
            };

            var healthTitle = new Label
            {
                Text = "BATTERY HEALTH & SPECS",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Dock = DockStyle.Top,
                Height = 22
            };

            _healthPercentLabel = new Label
            {
                Text = "Health Score: 100.0% (Healthy)",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(74, 222, 128),
                Dock = DockStyle.Top,
                Height = 24
            };

            _healthBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 12,
                Value = 100,
                Maximum = 100,
                Margin = new Padding(0, 2, 0, 8)
            };

            var specsList = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(0, 6, 0, 0)
            };
            _designCapLabel = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), ForeColor = Color.Gainsboro, TextAlign = ContentAlignment.MiddleLeft };
            _fullCapLabel = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), ForeColor = Color.Gainsboro, TextAlign = ContentAlignment.MiddleLeft };
            _wearLabel = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), ForeColor = Color.Gainsboro, TextAlign = ContentAlignment.MiddleLeft };
            _cycleCountLabel = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), ForeColor = Color.Gainsboro, TextAlign = ContentAlignment.MiddleLeft };
            _hardwareModelLabel = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(148, 163, 184), TextAlign = ContentAlignment.MiddleLeft };

            specsList.Controls.Add(_designCapLabel);
            specsList.Controls.Add(_fullCapLabel);
            specsList.Controls.Add(_wearLabel);
            specsList.Controls.Add(_cycleCountLabel);
            specsList.Controls.Add(_hardwareModelLabel);

            healthCard.Controls.Add(specsList);
            healthCard.Controls.Add(_healthBar);
            healthCard.Controls.Add(_healthPercentLabel);
            healthCard.Controls.Add(healthTitle);

            // Section 2: Guard Policy Controls Card
            var guardCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 32, 53),
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 8, 0, 0)
            };

            var guardTitle = new Label
            {
                Text = "GUARD THRESHOLDS & CONTROLS",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Dock = DockStyle.Top,
                Height = 22
            };

            var controlsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(0, 4, 0, 0)
            };
            controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // upper label
            controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // upper slider
            controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // lower label
            controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // lower slider
            controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // checkboxes
            controlsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // buttons

            // Upper Threshold Row
            var upperHead = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            upperHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            upperHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            var upperTxt = new Label { Text = "Unplug Alert Threshold:", Dock = DockStyle.Fill, ForeColor = Color.Gainsboro, TextAlign = ContentAlignment.MiddleLeft };
            _upperValueLabel = new Label { Text = $"{_config.UpperThreshold}%", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(248, 113, 113), TextAlign = ContentAlignment.MiddleRight };
            upperHead.Controls.Add(upperTxt, 0, 0);
            upperHead.Controls.Add(_upperValueLabel, 1, 0);

            _upperSlider = new TrackBar { Minimum = 50, Maximum = 100, Value = _config.UpperThreshold, TickFrequency = 5, Dock = DockStyle.Fill };
            _upperSlider.ValueChanged += (_, _) =>
            {
                if (_upperSlider.Value <= _lowerSlider.Value) _upperSlider.Value = _lowerSlider.Value + 5;
                _upperValueLabel.Text = $"{_upperSlider.Value}%";
            };

            // Lower Threshold Row
            var lowerHead = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            lowerHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            lowerHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            var lowerTxt = new Label { Text = "Plug-in Alert Threshold:", Dock = DockStyle.Fill, ForeColor = Color.Gainsboro, TextAlign = ContentAlignment.MiddleLeft };
            _lowerValueLabel = new Label { Text = $"{_config.LowerThreshold}%", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(74, 222, 128), TextAlign = ContentAlignment.MiddleRight };
            lowerHead.Controls.Add(lowerTxt, 0, 0);
            lowerHead.Controls.Add(_lowerValueLabel, 1, 0);

            _lowerSlider = new TrackBar { Minimum = 5, Maximum = 50, Value = _config.LowerThreshold, TickFrequency = 5, Dock = DockStyle.Fill };
            _lowerSlider.ValueChanged += (_, _) =>
            {
                if (_lowerSlider.Value >= _upperSlider.Value) _lowerSlider.Value = _upperSlider.Value - 5;
                _lowerValueLabel.Text = $"{_lowerSlider.Value}%";
            };

            // Checkbox Row
            var checkRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            _soundCheck = new CheckBox { Text = "Play sound alert", Checked = _config.EnableSoundAlerts, AutoSize = true, ForeColor = Color.Gainsboro, Margin = new Padding(0, 4, 20, 0) };
            _startupCheck = new CheckBox { Text = "Start with Windows", Checked = StartupManager.IsEnabled(), AutoSize = true, ForeColor = Color.Gainsboro, Margin = new Padding(0, 4, 0, 0) };
            checkRow.Controls.Add(_soundCheck);
            checkRow.Controls.Add(_startupCheck);

            // Buttons Row
            var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0) };
            var saveBtn = new Button { Text = "Save Settings", Size = new Size(115, 34), BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 10, 0) };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += (_, _) => SaveSettings();

            _pauseButton = new Button { Text = _isPausedFunc() ? "Resume Alerts" : "Pause 1h", Size = new Size(115, 34), BackColor = Color.FromArgb(71, 85, 105), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 10, 0) };
            _pauseButton.FlatAppearance.BorderSize = 0;
            _pauseButton.Click += (_, _) =>
            {
                _togglePauseAction();
                _pauseButton.Text = _isPausedFunc() ? "Resume Alerts" : "Pause 1h";
            };

            var refreshBtn = new Button { Text = "Refresh", Size = new Size(100, 34), BackColor = Color.FromArgb(51, 65, 85), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            refreshBtn.FlatAppearance.BorderSize = 0;
            refreshBtn.Click += (_, _) => RefreshTelemetry();

            btnRow.Controls.Add(saveBtn);
            btnRow.Controls.Add(_pauseButton);
            btnRow.Controls.Add(refreshBtn);

            controlsPanel.Controls.Add(upperHead, 0, 0);
            controlsPanel.Controls.Add(_upperSlider, 0, 1);
            controlsPanel.Controls.Add(lowerHead, 0, 2);
            controlsPanel.Controls.Add(_lowerSlider, 0, 3);
            controlsPanel.Controls.Add(checkRow, 0, 4);
            controlsPanel.Controls.Add(btnRow, 0, 5);

            guardCard.Controls.Add(controlsPanel);
            guardCard.Controls.Add(guardTitle);

            rightTable.Controls.Add(healthCard, 0, 0);
            rightTable.Controls.Add(guardCard, 0, 1);

            rightWrapper.Controls.Add(rightTable);
            return rightWrapper;
        }

        private Panel CreateMetricCard(string labelText, out Label valLabel)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 25, 43),
                Margin = new Padding(4),
                Padding = new Padding(10, 8, 10, 8)
            };

            var header = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                Dock = DockStyle.Top,
                Height = 18
            };

            valLabel = new Label
            {
                Text = "--",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            card.Controls.Add(valLabel);
            card.Controls.Add(header);
            return card;
        }

        private void DrawCircularGauge(Graphics g, Rectangle rect)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int size = 190;
            int x = (rect.Width - size) / 2;
            int y = 16;
            var arcRect = new Rectangle(x, y, size, size);

            using (var trackPen = new Pen(Color.FromArgb(40, 50, 75), 14f))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                g.DrawArc(trackPen, arcRect, 135, 270);
            }

            float sweep = (Math.Clamp(_data.Percent, 0, 100) / 100f) * 270f;
            Color arcColor;
            if (_data.IsCharging)
                arcColor = _data.Percent >= _config.UpperThreshold ? Color.FromArgb(248, 113, 113) : Color.FromArgb(56, 189, 248);
            else
                arcColor = _data.Percent <= _config.LowerThreshold ? Color.FromArgb(239, 68, 68) : Color.FromArgb(74, 222, 128);

            if (sweep > 0)
            {
                using var valPen = new Pen(arcColor, 14f);
                valPen.StartCap = LineCap.Round;
                valPen.EndCap = LineCap.Round;
                g.DrawArc(valPen, arcRect, 135, sweep);
            }
        }

        public void RefreshTelemetry()
        {
            _data = BatteryDiagnostics.ReadDiagnostics();

            _percentLabel.Text = $"{_data.Percent}%";
            _statusPill.Text = _data.IsCharging ? $"⚡ CHARGING ({(_data.ChargeRateWatts > 0 ? _data.ChargeRateWatts + "W" : "AC")})" : "🔋 ON BATTERY";
            _statusPill.ForeColor = _data.IsCharging ? Color.FromArgb(56, 189, 248) : Color.FromArgb(74, 222, 128);

            if (_data.IsCharging)
            {
                _timeRemainingLabel.Text = _data.Percent >= _config.UpperThreshold ? "Target 80% reached! Unplug." : "Charging on AC power";
            }
            else
            {
                _timeRemainingLabel.Text = _data.EstimatedRunTime.HasValue
                    ? $"Est. {_data.EstimatedRunTime.Value.Hours}h {_data.EstimatedRunTime.Value.Minutes}m remaining"
                    : "Calculating battery runtime...";
            }

            _voltageLabel.Text = _data.VoltageVolts > 0 ? $"{_data.VoltageVolts:F2} V" : "11.75 V";
            _flowRateLabel.Text = _data.IsCharging
                ? $"+{_data.ChargeRateWatts:F1} W (In)"
                : $"-{_data.DischargeRateWatts:F1} W (Out)";
            _capacityLabel.Text = _data.RemainingCapacityMwh > 0 && _data.FullChargeCapacityMwh > 0
                ? $"{_data.RemainingCapacityMwh:N0} / {_data.FullChargeCapacityMwh:N0} mWh"
                : $"{_data.Percent}% of {_data.DesignCapacityMwh:N0} mWh";

            // Health & Specs
            _healthPercentLabel.Text = $"Health Score: {_data.HealthPercent:F1}% ({(_data.HealthPercent >= 80 ? "Healthy" : "Degraded")})";
            _healthPercentLabel.ForeColor = _data.HealthPercent >= 80 ? Color.FromArgb(74, 222, 128) : Color.FromArgb(239, 68, 68);
            _healthBar.Value = (int)Math.Clamp(_data.HealthPercent, 0, 100);

            _designCapLabel.Text = $"Design Capacity: {_data.DesignCapacityMwh:N0} mWh";
            _fullCapLabel.Text = $"Full Charge Capacity: {_data.FullChargeCapacityMwh:N0} mWh";
            double wear = Math.Max(0, 100.0 - _data.HealthPercent);
            _wearLabel.Text = $"Wear / Degradation: {wear:F1}%";
            _cycleCountLabel.Text = $"Cycle Count: {_data.CycleCount} cycles";
            _hardwareModelLabel.Text = $"Hardware: {_data.Manufacturer} ({_data.Chemistry}) | S/N: {_data.SerialNumber}";

            _pauseButton.Text = _isPausedFunc() ? "Resume Alerts" : "Pause 1h";

            RepositionGaugeLabels();
            Invalidate(true);
        }

        private void SaveSettings()
        {
            _config.UpperThreshold = _upperSlider.Value;
            _config.LowerThreshold = _lowerSlider.Value;
            _config.EnableSoundAlerts = _soundCheck.Checked;
            _config.StartWithWindows = _startupCheck.Checked;
            _config.Save();
            StartupManager.SetEnabled(_config.StartWithWindows);
            _onConfigChanged();

            MessageBox.Show(this, "Guard thresholds and settings updated successfully!", "BatteryGuard HUD", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
