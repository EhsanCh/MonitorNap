using System;
using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

/// <summary>
/// Defines user-selected standby behavior.
/// </summary>
public enum SleepAction {
    DimOnly = 1,
    DimThenOff = 2,
    BlackScreen = 3
}

/// <summary>
/// Represents runtime tracking state and selection for each connected display.
/// </summary>
public class MonState {
    public string DeviceName;
    public Rectangle Bounds;
    public int IdleSeconds = 0;    public bool IsOff = false;
    public bool IsDimmed = false;
    public uint OriginalBrightness = 100;
    public bool IsSelected = true;
    public Form WarningFormRef = null;
}/// <summary>
/// Global application settings and Windows Registry persistence manager.
/// </summary>
public static class Config {
    public const string APP_NAME = "MonitorNap";
    private const string REG_PATH = @"Software\MonitorNap";
    private const string REG_RUN_PATH = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Runtime configuration defaults
    public static List<MonState> Monitors = new List<MonState>();
    public static int IdleLimitSeconds = 300; // 5 minutes default
    public static int CheckInterval = 2;     // 2 seconds polling cycle
    public static bool DoubleClickToggles = true;
    public static bool IgnoreOnFullscreen = true;
    public static bool IsPaused = false;

    // Advanced standby and interaction preferences
    public static SleepAction CurrentSleepAction = SleepAction.DimThenOff;
    public static int HotkeyModifiers = 0; // 0 = Disabled, 1 = Alt, 2 = Ctrl, 4 = Shift, 8 = Win
    public static int HotkeyKey = 0;       // Windows Forms Keys enum value, 0 = Disabled
    public static string IgnoreKeywords = "YouTube,VLC,PotPlayer,Media Player";
    public static bool ShowWarningNotification = true;
    public static int WarningLeadSeconds = 30; // 30 seconds warning before sleep

    /// <summary>
    /// Loads saved user configuration from CurrentUser Registry hive.    /// </summary>
    public static void LoadSettings() {
        try {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_PATH)) {
                if (key != null) {                    IdleLimitSeconds = (int)key.GetValue("IdleLimitSeconds", 300);
                    CheckInterval = (int)key.GetValue("CheckInterval", 2);
                    DoubleClickToggles = (int)key.GetValue("DoubleClickToggles", 1) == 1;
                    IgnoreOnFullscreen = (int)key.GetValue("IgnoreOnFullscreen", 1) == 1;
                    CurrentSleepAction = (SleepAction)(int)key.GetValue("SleepAction", (int)SleepAction.DimThenOff);
                    HotkeyModifiers = (int)key.GetValue("HotkeyModifiers", 0);
                    HotkeyKey = (int)key.GetValue("HotkeyKey", 0);
                    IgnoreKeywords = (string)key.GetValue("IgnoreKeywords", "YouTube,VLC,PotPlayer,Media Player");
                    ShowWarningNotification = (int)key.GetValue("ShowWarningNotification", 1) == 1;
                    WarningLeadSeconds = (int)key.GetValue("WarningLeadSeconds", 30);

                    object targetsObj = key.GetValue("TargetMonitors", null);
                    if (targetsObj != null) {                        string targets = targetsObj.ToString();                        string[] arr = targets.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var m in Monitors) {
                            m.IsSelected = Array.IndexOf(arr, m.DeviceName) >= 0;
                        }
                    }
                }
            }
        } catch (Exception ex) {
            Trace.WriteLine("Failed to load settings: " + ex.Message);
        }
    }

    /// <summary>    /// Persists current configuration to CurrentUser Registry hive.
    /// </summary>
    public static void SaveSettings() {
        try {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REG_PATH)) {
                if (key != null) {
                    key.SetValue("IdleLimitSeconds", IdleLimitSeconds);
                    key.SetValue("CheckInterval", CheckInterval);
                    key.SetValue("DoubleClickToggles", DoubleClickToggles ? 1 : 0);
                    key.SetValue("IgnoreOnFullscreen", IgnoreOnFullscreen ? 1 : 0);
                    key.SetValue("SleepAction", (int)CurrentSleepAction);
                    key.SetValue("HotkeyModifiers", HotkeyModifiers);
                    key.SetValue("HotkeyKey", HotkeyKey);
                    key.SetValue("IgnoreKeywords", IgnoreKeywords ?? "");
                    key.SetValue("ShowWarningNotification", ShowWarningNotification ? 1 : 0);
                    key.SetValue("WarningLeadSeconds", WarningLeadSeconds);

                    List<string> selected = new List<string>();
                    foreach (var m in Monitors) {                        if (m.IsSelected) selected.Add(m.DeviceName);                    }
                    key.SetValue("TargetMonitors", string.Join(",", selected.ToArray()));
                }
            }
        } catch (Exception ex) {
            Trace.WriteLine("Failed to save settings: " + ex.Message);
        }
    }

    /// <summary>    /// Checks whether the application is registered to run on Windows startup.
    /// </summary>
    public static bool IsStartupEnabled() {
        try {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_RUN_PATH, false)) {
                return key != null && key.GetValue(APP_NAME) != null;
            }
        } catch (Exception ex) {
            Trace.WriteLine("Failed to check startup registration: " + ex.Message);
            return false;
        }
    }

    /// <summary>    /// Enables or disables automatic startup with Windows via Registry Run key.
    /// </summary>
    public static void SetStartup(bool enable) {
        try {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_RUN_PATH, true)) {
                if (key != null) {
                    if (enable) key.SetValue(APP_NAME, "\"" + Application.ExecutablePath + "\"");
                    else key.DeleteValue(APP_NAME, false);
                }
            }
        } catch (Exception ex) {
            Trace.WriteLine("Failed to update startup registration: " + ex.Message);
        }
    }
}
