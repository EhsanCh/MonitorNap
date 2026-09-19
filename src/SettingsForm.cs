using System;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Graphical user interface for user preferences and target monitor selection.
/// </summary>
public class SettingsForm : Form {
    private CheckedListBox clbMonitors;
    private ComboBox cbSleepAction;
    private NumericUpDown numTimeout;
    private NumericUpDown numInterval;
    private CheckBox chkPreDim;
    private NumericUpDown numDimLead;
    private CheckBox chkDoubleClick;
    private CheckBox chkFullscreen;
    private CheckBox chkStartup;
    private CheckBox chkWarning;
    private NumericUpDown numWarningLead;
    private CheckBox chkCtrl;
    private CheckBox chkAlt;
    private CheckBox chkShift;
    private ComboBox cbKey;
    private TextBox txtKeywords;

    public SettingsForm() {
        this.Text = "MonitorNap - Settings";
        this.Icon = Program.AppIcon;
        this.Size = new Size(480, 680);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ShowInTaskbar = true;

        // Target Monitors Group
        GroupBox gbMonitors = new GroupBox { Text = "Target Secondary Monitors", Location = new Point(15, 12), Size = new Size(435, 105) };
        clbMonitors = new CheckedListBox { Location = new Point(15, 20), Size = new Size(405, 74) };
        for (int i = 0; i < Config.Monitors.Count; i++) {
            var m = Config.Monitors[i];
            string text = string.Format("Monitor {0} ({1}x{2}) - Pos: [{3},{4}]", i + 2, m.Bounds.Width, m.Bounds.Height, m.Bounds.Left, m.Bounds.Top);
            clbMonitors.Items.Add(text, m.IsSelected);
        }
        if (Config.Monitors.Count == 0) {
            clbMonitors.Items.Add("No secondary display detected.", false);
            clbMonitors.Enabled = false;
        }
        gbMonitors.Controls.Add(clbMonitors);
        // Standby Mode & Timing Group
        GroupBox gbTiming = new GroupBox { Text = "Standby Action & Timing", Location = new Point(15, 125), Size = new Size(435, 150) };
        Label lblSleepAction = new Label { Text = "Standby Mode:", Location = new Point(15, 26), AutoSize = true };
        cbSleepAction = new ComboBox { Location = new Point(220, 22), Size = new Size(200, 22), DropDownStyle = ComboBoxStyle.DropDownList };
        cbSleepAction.Items.Add("Sleep (Standby via DDC/CI)");
        cbSleepAction.Items.Add("Dim Only (Minimum brightness)");
        cbSleepAction.Items.Add("Black Screen (Curtain overlay)");
        if (Config.CurrentSleepAction == SleepAction.DimOnly) {
            cbSleepAction.SelectedIndex = 1;
        } else if (Config.CurrentSleepAction == SleepAction.BlackScreen) {
            cbSleepAction.SelectedIndex = 2;
        } else {
            cbSleepAction.SelectedIndex = 0;
        }

        Label lblTimeout = new Label { Text = "Idle Timeout (Minutes):", Location = new Point(15, 56), AutoSize = true };
        numTimeout = new NumericUpDown { Location = new Point(220, 53), Size = new Size(200, 22), Minimum = 1, Maximum = 120, Value = Math.Max(1, Config.IdleLimitSeconds / 60) };
        Label lblInterval = new Label { Text = "Check Interval (Seconds):", Location = new Point(15, 87), AutoSize = true };
        numInterval = new NumericUpDown { Location = new Point(220, 84), Size = new Size(200, 22), Minimum = 1, Maximum = 10, Value = Config.CheckInterval };

        chkPreDim = new CheckBox {
            Text = "Dim screen before standby (seconds):",
            Location = new Point(15, 117),
            Size = new Size(260, 24),
            Checked = Config.DimLeadSeconds > 0
        };
        numDimLead = new NumericUpDown {
            Location = new Point(285, 118),
            Size = new Size(135, 22),
            Minimum = 1,
            Maximum = 300,
            Value = (Config.DimLeadSeconds > 0) ? Config.DimLeadSeconds : 60,
            Enabled = Config.DimLeadSeconds > 0
        };
        chkPreDim.CheckedChanged += (s, e) => numDimLead.Enabled = chkPreDim.Checked;

        gbTiming.Controls.AddRange(new Control[] { lblSleepAction, cbSleepAction, lblTimeout, numTimeout, lblInterval, numInterval, chkPreDim, numDimLead });

        // Behavior Options Group
        GroupBox gbBehavior = new GroupBox { Text = "Behavior & Options", Location = new Point(15, 283), Size = new Size(435, 130) };
        chkDoubleClick = new CheckBox {
            Text = "Double-click tray icon toggles monitor power",
            Location = new Point(15, 22),
            Size = new Size(410, 24),
            Checked = Config.DoubleClickToggles
        };
        chkFullscreen = new CheckBox {
            Text = "Prevent standby when video/game is running in fullscreen",
            Location = new Point(15, 48),
            Size = new Size(410, 24),
            Checked = Config.IgnoreOnFullscreen
        };
        chkStartup = new CheckBox {
            Text = "Start automatically with Windows",
            Location = new Point(15, 74),
            Size = new Size(410, 24),
            Checked = Config.IsStartupEnabled()
        };
        chkWarning = new CheckBox {
            Text = "Show countdown warning before standby (sec):",
            Location = new Point(15, 98),
            Size = new Size(310, 24),
            Checked = Config.ShowWarningNotification
        };
        numWarningLead = new NumericUpDown {
            Location = new Point(330, 99),
            Size = new Size(90, 22),
            Minimum = 5,
            Maximum = 120,
            Value = Math.Max(5, Config.WarningLeadSeconds),
            Enabled = Config.ShowWarningNotification
        };
        chkWarning.CheckedChanged += (s, e) => numWarningLead.Enabled = chkWarning.Checked;
        gbBehavior.Controls.AddRange(new Control[] { chkDoubleClick, chkFullscreen, chkStartup, chkWarning, numWarningLead });

        // Hotkey & Smart Detection Group
        GroupBox gbAdvanced = new GroupBox { Text = "Hotkey & Video Detection", Location = new Point(15, 421), Size = new Size(435, 145) };
        Label lblHotkey = new Label { Text = "Global Sleep Toggle Hotkey:", Location = new Point(15, 24), AutoSize = true };
        chkCtrl = new CheckBox { Text = "Ctrl", Location = new Point(15, 47), AutoSize = true, Checked = (Config.HotkeyModifiers & 2) != 0 };
        chkAlt = new CheckBox { Text = "Alt", Location = new Point(70, 47), AutoSize = true, Checked = (Config.HotkeyModifiers & 1) != 0 };
        chkShift = new CheckBox { Text = "Shift", Location = new Point(125, 47), AutoSize = true, Checked = (Config.HotkeyModifiers & 4) != 0 };
        Label lblPlus = new Label { Text = "+", Location = new Point(185, 47), AutoSize = true };
        cbKey = new ComboBox { Location = new Point(205, 44), Size = new Size(95, 22), DropDownStyle = ComboBoxStyle.DropDownList };
        cbKey.Items.Add("None");
        for (char c = 'A'; c <= 'Z'; c++) {
            cbKey.Items.Add(c.ToString());
        }
        for (int i = 1; i <= 12; i++) {
            cbKey.Items.Add("F" + i);
        }

        string keyName = (Config.HotkeyKey != 0) ? ((Keys)Config.HotkeyKey).ToString() : "None";
        int keyIdx = cbKey.Items.IndexOf(keyName);
        cbKey.SelectedIndex = (keyIdx >= 0) ? keyIdx : 0;

        Label lblKeywords = new Label { Text = "Prevent sleep if window title contains (comma-separated):", Location = new Point(15, 78), AutoSize = true };
        txtKeywords = new TextBox { Location = new Point(15, 102), Size = new Size(405, 22), Text = Config.IgnoreKeywords ?? "" };

        gbAdvanced.Controls.AddRange(new Control[] { lblHotkey, chkCtrl, chkAlt, chkShift, lblPlus, cbKey, lblKeywords, txtKeywords });

        // Author Credit
        LinkLabel lblAuthor = new LinkLabel {
            Text = "MonitorNap by Ehsan Chavoshi",
            Location = new Point(15, 587),
            AutoSize = true,
            LinkColor = Color.FromArgb(0, 102, 204),
            ActiveLinkColor = Color.Blue,
            LinkBehavior = LinkBehavior.HoverUnderline
        };
        lblAuthor.LinkClicked += (s, e) => {
            try {
                System.Diagnostics.Process.Start("https://github.com/EhsanCh/MonitorNap");
            } catch { }
        };

        // Action Buttons
        Button btnSave = new Button { Text = "Save & Apply", Location = new Point(240, 579), Size = new Size(100, 32) };
        Button btnCancel = new Button { Text = "Cancel", Location = new Point(350, 579), Size = new Size(100, 32) };
        btnSave.Click += (s, e) => {
            if (cbSleepAction.SelectedIndex == 1) {
                Config.CurrentSleepAction = SleepAction.DimOnly;
            } else if (cbSleepAction.SelectedIndex == 2) {
                Config.CurrentSleepAction = SleepAction.BlackScreen;
            } else {
                Config.CurrentSleepAction = SleepAction.Sleep;
            }

            Config.IdleLimitSeconds = (int)numTimeout.Value * 60;
            Config.CheckInterval = (int)numInterval.Value;
            Config.DimLeadSeconds = chkPreDim.Checked ? (int)numDimLead.Value : 0;
            Config.DoubleClickToggles = chkDoubleClick.Checked;
            Config.IgnoreOnFullscreen = chkFullscreen.Checked;

            int mod = 0;
            if (chkAlt.Checked) {
                mod |= 1;
            }
            if (chkCtrl.Checked) {
                mod |= 2;
            }
            if (chkShift.Checked) {
                mod |= 4;
            }

            int selectedVk = 0;
        if (cbKey.SelectedIndex > 0) {
            try {
                    selectedVk = (int)(Keys)Enum.Parse(typeof(Keys), cbKey.SelectedItem.ToString());
                } catch {
                    selectedVk = 0;
                }
            }
            Config.HotkeyModifiers = (selectedVk != 0) ? mod : 0;
            Config.HotkeyKey = selectedVk;

            Config.IgnoreKeywords = txtKeywords.Text.Trim();

            int limit = Math.Min(Config.Monitors.Count, clbMonitors.Items.Count);
            for (int i = 0; i < limit; i++) {
                bool isChecked = clbMonitors.GetItemChecked(i);
                if (!isChecked && (Config.Monitors[i].IsOff || Config.Monitors[i].IsDimmed)) {
                    Program.WakeMonitor(Config.Monitors[i]);
                }
                Config.Monitors[i].IsSelected = isChecked;
            }

            Config.ShowWarningNotification = chkWarning.Checked;
            Config.WarningLeadSeconds = (int)numWarningLead.Value;
            Config.SetStartup(chkStartup.Checked);
            Config.SaveSettings();
            Program.UpdateTimerInterval();
            Program.UpdateHotkey();
            this.Close();
        };
        btnCancel.Click += (s, e) => this.Close();

        this.Controls.AddRange(new Control[] { gbMonitors, gbTiming, gbBehavior, gbAdvanced, lblAuthor, btnSave, btnCancel });
    }
}
