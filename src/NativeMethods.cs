using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Direct Win32 and DirectX VESA DDC/CI hardware interop definitions.
/// Sends low-level monitor power commands without resetting Windows desktop layout.
/// </summary>
public static class NativeMethods {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct PHYSICAL_MONITOR {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("dxva2.dll", SetLastError = true)] public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("dxva2.dll", SetLastError = true)] public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] array);
    [DllImport("dxva2.dll", SetLastError = true)] public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hPhysicalMonitor, byte bVCPCode, out uint pdwVCPCodeType, out uint pdwCurrentValue, out uint pdwMaximumValue);
    [DllImport("dxva2.dll", SetLastError = true)] public static extern bool SetVCPFeature(IntPtr hPhysicalMonitor, byte bVCPCode, uint dwNewValue);
    [DllImport("dxva2.dll", SetLastError = true)] public static extern bool DestroyPhysicalMonitors(uint count, [In] PHYSICAL_MONITOR[] array);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

    private enum MonitorOp { SetPower, SetBrightness, GetBrightness }
    private static MonitorOp currentOp;
    // Retain a static reference to the callback delegate to prevent garbage collection during unmanaged enumeration
    private static MonitorEnumProc enumCallbackDelegate = EnumCallback;
    private static int targetLeft;
    private static int targetTop;
    private static uint targetValue;
    private static uint outCurrentValue;
    private static uint outMaxValue;
    private static bool opSucceeded;
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    /// <summary>
    /// Callback executed by EnumDisplayMonitors to identify and manipulate target physical panels.
    /// </summary>
    private static bool EnumCallback(IntPtr hMon, IntPtr hdc, ref RECT rect, IntPtr data) {
        try {
            if (rect.left == targetLeft && rect.top == targetTop) {
                uint count = 0;
                if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out count) && count > 0) {
                    PHYSICAL_MONITOR[] phys = new PHYSICAL_MONITOR[count];
                    if (GetPhysicalMonitorsFromHMONITOR(hMon, count, phys)) {
                        for (int i = 0; i < count; i++) {
                            try {
                                if (currentOp == MonitorOp.SetPower) {
                                    // VESA VCP Code 0xD6: Power Mode (1 = Power On, 4 = Standby / Power Off)
                                    opSucceeded = SetVCPFeature(phys[i].hPhysicalMonitor, 0xD6, targetValue);
                                } else if (currentOp == MonitorOp.SetBrightness) {
                                    // VESA VCP Code 0x10: Luminance / Brightness
                                    opSucceeded = SetVCPFeature(phys[i].hPhysicalMonitor, 0x10, targetValue);
                                } else if (currentOp == MonitorOp.GetBrightness) {
                                    uint codeType, curVal, maxVal;
                                    if (GetVCPFeatureAndVCPFeatureReply(phys[i].hPhysicalMonitor, 0x10, out codeType, out curVal, out maxVal)) {
                                        outCurrentValue = curVal;
                                        outMaxValue = maxVal;
                                        opSucceeded = true;
                                    }
                                }
                            } catch { }
                        }
                        DestroyPhysicalMonitors(count, phys);
                    }
                }
            }
        } catch { }
        return true;
    }

    /// <summary>
    /// Initializes Per-Monitor V2 DPI awareness to avoid virtualized coordinate mismatch.
    /// </summary>
    public static void InitializeDpiAwareness() {
        try {
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        } catch { }
    }

    /// <summary>
    /// Sends hardware DDC/CI power state command to the display at given desktop coordinates.
    /// </summary>
    public static bool SetMonitorPower(Rectangle bounds, uint powerState) {
        try {
            targetLeft = bounds.Left;
            targetTop = bounds.Top;
            currentOp = MonitorOp.SetPower;
            targetValue = powerState;
            opSucceeded = false;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, enumCallbackDelegate, IntPtr.Zero);
            return opSucceeded;
        } catch {
            return false;
        }
    }

    /// <summary>
    /// Sets display hardware luminance via VESA DDC/CI VCP Code 0x10.
    /// </summary>
    public static bool SetMonitorBrightness(Rectangle bounds, uint brightness) {
        try {
            targetLeft = bounds.Left;
            targetTop = bounds.Top;
            currentOp = MonitorOp.SetBrightness;
            targetValue = brightness;
            opSucceeded = false;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, enumCallbackDelegate, IntPtr.Zero);
            return opSucceeded;
        } catch {
            return false;
        }
    }

    /// <summary>
    /// Retrieves current and maximum hardware luminance via VESA DDC/CI VCP Code 0x10.
    /// </summary>
    public static bool GetMonitorBrightness(Rectangle bounds, out uint currentBrightness, out uint maxBrightness) {
        currentBrightness = 100;
        maxBrightness = 100;
        try {
            targetLeft = bounds.Left;
            targetTop = bounds.Top;
            currentOp = MonitorOp.GetBrightness;
            opSucceeded = false;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, enumCallbackDelegate, IntPtr.Zero);
            if (opSucceeded) {
                currentBrightness = outCurrentValue;
                maxBrightness = outMaxValue;
                return true;
            }
        } catch { }
        return false;
    }

    /// <summary>
    /// Determines whether the active foreground window covers the entire target monitor.
    /// </summary>
    public static bool IsWindowFullscreenOnScreen(Rectangle screenBounds) {
        try {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == GetDesktopWindow() || fg == GetShellWindow()) {
                return false;
            }
            RECT wRect;
            if (GetWindowRect(fg, out wRect)) {
                return (wRect.left <= screenBounds.Left && wRect.top <= screenBounds.Top &&
                        wRect.right >= screenBounds.Right && wRect.bottom >= screenBounds.Bottom);
            }
        } catch { }
        return false;
    }
}

