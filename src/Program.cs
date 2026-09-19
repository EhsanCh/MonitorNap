using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;using System.Windows.Forms;
using System.Threading;
using Microsoft.Win32;
// Assembly metadata embedded into the compiled Windows executable
[assembly: AssemblyTitle("MonitorNap")]
[assembly: AssemblyProduct("MonitorNap")]
[assembly: AssemblyCompany("Ehsan Chavoshi")]
[assembly: AssemblyCopyright("Copyright © 2026 Ehsan Chavoshi")]
[assembly: AssemblyDescription("Lightweight multi-monitor power management utility using VESA DDC/CI")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

/// <summary>
/// Application entry point, system tray lifecycle, and mouse idle polling loop.
/// </summary>
static class Program {
    public static NotifyIcon TrayIcon;
    public static Icon AppIcon;
    private static ToolStripMenuItem toggleItem;
    private static System.Windows.Forms.Timer timer;
    private static Mutex singleInstanceMutex;
    private static SettingsForm currentSettingsForm = null;
    private static AboutForm currentAboutForm = null;
    private static Dictionary<string, Form> activeCurtains = new Dictionary<string, Form>();
    private static HotkeyMessageSink hotkeySink = null;
    private static NativeMethods.EnumWindowsProc scanWindowsDelegate = ScanWindowsCallback;
    private static Rectangle currentScanBounds;
    private static string[] currentScanKeywords;
    private static bool scanWindowFound;

    /// <summary>
    /// Scans connected monitors, identifies non-primary displays, and loads user selections.    /// </summary>
    public static void RefreshMonitors() {
        Dictionary<string, bool> previousPowerStates = new Dictionary<string, bool>();
        Dictionary<string, bool> previousDimStates = new Dictionary<string, bool>();
        Dictionary<string, uint> previousBrightness = new Dictionary<string, uint>();

        foreach (var m in Config.Monitors) {
            previousPowerStates[m.DeviceName] = m.IsOff;
            previousDimStates[m.DeviceName] = m.IsDimmed;
            previousBrightness[m.DeviceName] = m.OriginalBrightness;
        }

        Config.Monitors.Clear();
        foreach (Screen s in Screen.AllScreens) {
            if (!s.Primary) {
                bool wasOff = false;
                bool wasDimmed = false;
                uint origBright = 100;
                previousPowerStates.TryGetValue(s.DeviceName, out wasOff);
                previousDimStates.TryGetValue(s.DeviceName, out wasDimmed);
                previousBrightness.TryGetValue(s.DeviceName, out origBright);

                Config.Monitors.Add(new MonState {
                    DeviceName = s.DeviceName,
                    Bounds = s.Bounds,
                    IsOff = wasOff,
                    IsDimmed = wasDimmed,
                    OriginalBrightness = origBright,
                    IsSelected = true
                });
            }
        }

        // Synchronize or cleanup active curtain overlays
        List<string> orphanCurtains = new List<string>();
        foreach (var kvp in activeCurtains) {
            MonState matchingMon = null;
            foreach (var m in Config.Monitors) {
                if (m.DeviceName == kvp.Key) { matchingMon = m; break; }
            }
            if (matchingMon != null) kvp.Value.Bounds = matchingMon.Bounds;
            else orphanCurtains.Add(kvp.Key);
        }
        foreach (var name in orphanCurtains) {
            try { activeCurtains[name].Close(); activeCurtains[name].Dispose(); } catch { }
            activeCurtains.Remove(name);
        }

        Config.LoadSettings();
        UpdateStatus();
    }
    /// <summary>
    /// Synchronizes system tray tooltip text with current operational state.
    /// </summary>
    public static void UpdateStatus() {
        if (TrayIcon == null) return;
        if (Config.Monitors.Count == 0) {
            TrayIcon.Text = "MonitorNap: No Secondary Display";
        } else if (Config.IsPaused) {
            TrayIcon.Text = "MonitorNap: Paused";
        } else {
            TrayIcon.Text = "MonitorNap: Active";
        }
    }

    /// <summary>
    /// Displays or brings existing SettingsForm dialog to the front.
    /// </summary>
    public static void ShowSettings() {
        if (currentSettingsForm == null || currentSettingsForm.IsDisposed) {
            currentSettingsForm = new SettingsForm();
            currentSettingsForm.Show();
        } else {
            currentSettingsForm.BringToFront();
            currentSettingsForm.Activate();
        }
    }

    /// <summary>
    /// Displays or brings existing AboutForm dialog to the front.
    /// </summary>
    public static void ShowAbout() {
        if (currentAboutForm == null || currentAboutForm.IsDisposed) {
            currentAboutForm = new AboutForm();
            currentAboutForm.Show();
        } else {
            currentAboutForm.BringToFront();
            currentAboutForm.Activate();
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [STAThread]
    static void Main() {
        NativeMethods.InitializeDpiAwareness();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            MessageBox.Show("Unexpected Error: " + e.ExceptionObject.ToString(), "MonitorNap Error", MessageBoxButtons.OK, MessageBoxIcon.Error);        };

        // Enforce single instance execution via system Mutex
        bool createdNew;
        singleInstanceMutex = new Mutex(true, "MonitorNap_SingleInstance_Mutex", out createdNew);
        if (!createdNew) {
            MessageBox.Show("MonitorNap is already running in the system tray.", "MonitorNap", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Load embedded application icon directly from current module in memory
        try {
            IntPtr hModule = GetModuleHandle(null);
            IntPtr hIcon = LoadIcon(hModule, new IntPtr(32512));
            if (hIcon == IntPtr.Zero) hIcon = LoadIcon(hModule, new IntPtr(1));
            AppIcon = (hIcon != IntPtr.Zero) ? (Icon)Icon.FromHandle(hIcon).Clone() : SystemIcons.Application;
        } catch {
            AppIcon = SystemIcons.Application;
        }

        // Initialize system tray notification icon
        TrayIcon = new NotifyIcon {
            Icon = AppIcon,
            Visible = true
        };

        RefreshMonitors();
        SystemEvents.DisplaySettingsChanged += (s, e) => RefreshMonitors();
        SystemEvents.SessionEnding += (s, e) => {
            foreach (var m in Config.Monitors) {
                if (m.IsSelected && (m.IsOff || m.IsDimmed)) WakeMonitor(m);
            }
        };
        // Build context menu
        ContextMenuStrip menu = new ContextMenuStrip();        ToolStripMenuItem onItem = (ToolStripMenuItem)menu.Items.Add("Turn Secondary ON");
        ToolStripMenuItem offItem = (ToolStripMenuItem)menu.Items.Add("Turn Secondary OFF");
        menu.Items.Add(new ToolStripSeparator());
        toggleItem = (ToolStripMenuItem)menu.Items.Add("Pause Auto-Sleep");
        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem settingsItem = (ToolStripMenuItem)menu.Items.Add("Settings...");
        settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);
        ToolStripMenuItem aboutItem = (ToolStripMenuItem)menu.Items.Add("About...");
        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem exitItem = (ToolStripMenuItem)menu.Items.Add("Exit");
        TrayIcon.ContextMenuStrip = menu;

        // Initialize global hotkey listener
        hotkeySink = new HotkeyMessageSink();
        UpdateHotkey();

        // Handle tray icon double-click
        TrayIcon.DoubleClick += (s, e) => {
            if (Config.DoubleClickToggles) {
                ToggleMonitorsPower();
            } else {
                ShowSettings();
            }        };
        onItem.Click += (s, e) => {
            foreach (var m in Config.Monitors) {
                if (m.IsSelected) WakeMonitor(m);
            }
            UpdateStatus();
        };

        offItem.Click += (s, e) => {
            foreach (var m in Config.Monitors) {
                if (m.IsSelected) SleepMonitor(m);
            }
            UpdateStatus();
        };
        toggleItem.Click += (s, e) => {
            Config.IsPaused = !Config.IsPaused;
            toggleItem.Text = Config.IsPaused ? "Resume Auto-Sleep" : "Pause Auto-Sleep";
            if (Config.IsPaused) {
                foreach (var m in Config.Monitors) {
                    if (m.IsSelected && (m.IsOff || m.IsDimmed)) {
                        WakeMonitor(m);
                    }
                }
            }            UpdateStatus();
        };

        settingsItem.Click += (s, e) => ShowSettings();
        aboutItem.Click += (s, e) => ShowAbout();

        exitItem.Click += (s, e) => {
            if (hotkeySink != null) {                hotkeySink.Dispose();
                hotkeySink = null;
            }
            foreach (var m in Config.Monitors) {
                if (m.IsSelected && (m.IsOff || m.IsDimmed)) WakeMonitor(m);
            }            if (timer != null) timer.Stop();
            TrayIcon.Visible = false;            TrayIcon.Dispose();
            Application.Exit();
        };

        // Initialize mouse activity and idle tracking timer
        timer = new System.Windows.Forms.Timer();
        timer.Interval = Config.CheckInterval * 1000;
        timer.Tick += (s, e) => {
            if (Config.IsPaused) return;

            Point cursor = Cursor.Position;
            int warningThreshold = Math.Max(0, Config.IdleLimitSeconds - 60);

            foreach (var m in Config.Monitors) {
                if (!m.IsSelected) continue;

                if (m.Bounds.Contains(cursor)) {
                    // Mouse cursor entered target monitor - wake immediately
                    if (m.IsOff || m.IsDimmed) {
                        WakeMonitor(m);
                    } else {
                        m.IdleSeconds = 0;
                    }
                } else {
                    if (!m.IsOff) {
                        // Suppress sleep when target monitor has a full-screen window
                        if (Config.IgnoreOnFullscreen && NativeMethods.IsWindowFullscreenOnScreen(m.Bounds)) {
                            if (m.IsDimmed) WakeMonitor(m);
                            else m.IdleSeconds = 0;
                            continue;
                        }

                        // Suppress sleep when target monitor contains a window matching user keywords
                        if (IsIgnoredWindowOnScreen(m.Bounds)) {
                            if (m.IsDimmed) WakeMonitor(m);
                            else m.IdleSeconds = 0;
                            continue;
                        }

                        m.IdleSeconds += Config.CheckInterval;

                        // Warning dimming for DimThenOff mode (1 minute before standby)
                        if (Config.CurrentSleepAction == SleepAction.DimThenOff &&
                            m.IdleSeconds >= warningThreshold && m.IdleSeconds < Config.IdleLimitSeconds) {                            if (!m.IsDimmed) DimMonitor(m);
                        }

                        if (m.IdleSeconds >= Config.IdleLimitSeconds) {
                            // Inactivity limit exceeded - apply configured sleep action
                            SleepMonitor(m);
                        }
                    }
                }            }
        };
        timer.Start();

        Application.Run();
        if (hotkeySink != null) {
            hotkeySink.Dispose();
            hotkeySink = null;
        }
        try {
            singleInstanceMutex.ReleaseMutex();
        } catch { }        singleInstanceMutex.Close();
    }

    public static void UpdateTimerInterval() {
        if (timer != null) timer.Interval = Config.CheckInterval * 1000;
    }

    public static void UpdateHotkey() {
        if (hotkeySink != null) {
            hotkeySink.Register(Config.HotkeyModifiers, Config.HotkeyKey);
        }
    }

    public static void ToggleMonitorsPower() {
        bool anyOff = false;
        foreach (var m in Config.Monitors) {
            if (m.IsSelected && (m.IsOff || m.IsDimmed)) { anyOff = true; break; }
        }
        foreach (var m in Config.Monitors) {
            if (m.IsSelected) {
                if (anyOff) WakeMonitor(m);
                else SleepMonitor(m);
            }
        }
        UpdateStatus();
    }

    public static void DimMonitor(MonState m) {        uint curBrightness, maxBrightness;
        if (NativeMethods.GetMonitorBrightness(m.Bounds, out curBrightness, out maxBrightness)) {
            m.OriginalBrightness = curBrightness;
        }
        NativeMethods.SetMonitorBrightness(m.Bounds, 0);
        m.IsDimmed = true;
    }

    public static void SleepMonitor(MonState m) {
        if (Config.CurrentSleepAction == SleepAction.DimOnly) {
            if (!m.IsDimmed) DimMonitor(m);
            m.IsOff = true;
        } else if (Config.CurrentSleepAction == SleepAction.BlackScreen) {
            ShowCurtain(m);
            m.IsOff = true;
        } else {
            NativeMethods.SetMonitorPower(m.Bounds, 4);
            m.IsOff = true;
        }
    }

    public static void WakeMonitor(MonState m) {
        m.IdleSeconds = 0;
        HideCurtain(m);

        bool powerRestored = false;
        if (m.IsOff && Config.CurrentSleepAction != SleepAction.BlackScreen && Config.CurrentSleepAction != SleepAction.DimOnly) {
            NativeMethods.SetMonitorPower(m.Bounds, 1);
            powerRestored = true;
        }

        // Allow monitor microcontroller to complete power-state transition before sending DDC/CI commands
        if (powerRestored) {
            Thread.Sleep(750);
        }

        if (m.IsDimmed) {
            uint targetBrightness = (m.OriginalBrightness == 0) ? 100 : m.OriginalBrightness;
            if (RestoreBrightnessWithVerification(m.Bounds, targetBrightness)) {
                m.IsDimmed = false;
            }
        }

        m.IsOff = false;
    }

    private static bool RestoreBrightnessWithVerification(Rectangle bounds, uint targetBrightness) {
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++) {
            NativeMethods.SetMonitorBrightness(bounds, targetBrightness);
            Thread.Sleep(100);
            uint currentBrightness, maxBrightness;
            if (NativeMethods.GetMonitorBrightness(bounds, out currentBrightness, out maxBrightness) &&
                Math.Abs((int)currentBrightness - (int)targetBrightness) <= 2) {
                return true;
            }
        }        return false;
    }

    private static void ShowCurtain(MonState m) {        if (!activeCurtains.ContainsKey(m.DeviceName)) {
            CurtainForm curtain = new CurtainForm(m.Bounds);
            activeCurtains[m.DeviceName] = curtain;
            curtain.Show();
        }
    }

    private static void HideCurtain(MonState m) {
        Form curtain;
        if (activeCurtains.TryGetValue(m.DeviceName, out curtain)) {
            activeCurtains.Remove(m.DeviceName);
            try {
                curtain.Close();
                curtain.Dispose();
            } catch { }
        }
    }

    /// <summary>
    /// Lightweight non-activating full-screen black overlay for Curtain/BlackScreen mode.
    /// </summary>
    private class CurtainForm : Form {
        public CurtainForm(Rectangle bounds) {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = bounds;
            this.BackColor = Color.Black;
            this.ShowInTaskbar = false;
            this.TopMost = true;
        }

        protected override bool ShowWithoutActivation {
            get { return true; }
        }
    }

    private static bool ScanWindowsCallback(IntPtr hWnd, IntPtr lParam) {
        try {
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;

            NativeMethods.RECT rect;
            if (!NativeMethods.GetWindowRect(hWnd, out rect)) return true;

            Rectangle wRect = new Rectangle(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
            Rectangle intersection = Rectangle.Intersect(currentScanBounds, wRect);
            if (intersection.Width < 100 || intersection.Height < 100) return true;

            int len = NativeMethods.GetWindowTextLength(hWnd);
            if (len <= 0) return true;

            StringBuilder sb = new StringBuilder(len + 16);
            NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrEmpty(title)) return true;

            for (int i = 0; i < currentScanKeywords.Length; i++) {
                string kw = currentScanKeywords[i];
                if (kw.Length > 0 && title.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) {
                    scanWindowFound = true;
                    return false;
                }
            }
        } catch { }
        return true;
    }

    public static bool IsIgnoredWindowOnScreen(Rectangle bounds) {
        if (string.IsNullOrEmpty(Config.IgnoreKeywords)) return false;

        string[] raw = Config.IgnoreKeywords.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> cleanKeywords = new List<string>();
        for (int i = 0; i < raw.Length; i++) {
            string trimmed = raw[i].Trim();
            if (trimmed.Length > 0) cleanKeywords.Add(trimmed);
        }
        if (cleanKeywords.Count == 0) return false;

        currentScanBounds = bounds;
        currentScanKeywords = cleanKeywords.ToArray();
        scanWindowFound = false;

        try {
            NativeMethods.EnumWindows(scanWindowsDelegate, IntPtr.Zero);
        } catch { }

        return scanWindowFound;
    }

    /// <summary>
    /// Invisible Win32 message-sink window dedicated to capturing system-wide hotkeys.
    /// </summary>
    private class HotkeyMessageSink : NativeWindow, IDisposable {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9001;
        private bool isRegistered = false;

        public HotkeyMessageSink() {
            CreateParams cp = new CreateParams {
                Caption = "MonitorNap_HotkeySink"
            };
            CreateHandle(cp);
        }

        public void Register(int modifiers, int vk) {
            Unregister();
            if (modifiers != 0 && vk != 0 && Handle != IntPtr.Zero) {
                try {
                    isRegistered = NativeMethods.RegisterHotKey(Handle, HOTKEY_ID, (uint)modifiers, (uint)vk);
                } catch { }
            }
        }

        public void Unregister() {
            if (isRegistered && Handle != IntPtr.Zero) {
                try {
                    NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID);
                } catch { }
                isRegistered = false;
            }
        }

        protected override void WndProc(ref Message m) {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) {
                Program.ToggleMonitorsPower();
            }
            base.WndProc(ref m);
        }

        public void Dispose() {
            Unregister();
            DestroyHandle();
        }
    }
}
