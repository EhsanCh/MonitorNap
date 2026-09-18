# MonitorNap

[![Platform][badge-platform]][repo-url]
[![Language][badge-language]][repo-url]
[![Protocol][badge-protocol]][wiki-ddcci]
[![License][badge-license]][license-url]

[badge-platform]: https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows11&logoColor=white
[badge-language]: https://img.shields.io/badge/language-C%23-239120?style=flat-square&logo=csharp&logoColor=white
[badge-protocol]: https://img.shields.io/badge/protocol-VESA%20DDC%2FCI-informational?style=flat-square
[badge-license]: https://img.shields.io/badge/license-MIT-blue?style=flat-square
[repo-url]: https://github.com/EhsanCh/MonitorNap
[wiki-ddcci]: https://en.wikipedia.org/wiki/Display_Data_Channel#DDC/CI
[license-url]: https://github.com/EhsanCh/MonitorNap/blob/main/LICENSE

Intelligent, lightweight multi-monitor power management utility for Windows using VESA DDC/CI hardware control.

## Overview

MonitorNap is an intelligent, lightweight Windows utility that automates secondary monitor power management based on mouse cursor position. Utilizing direct VESA DDC/CI hardware control via DisplayPort, HDMI, or DVI, it automatically puts secondary displays into low-power standby when the cursor leaves the screen, and instantly wakes them up the moment the cursor re-enters. 

Unlike native Windows display settings which force desktop reconfiguration and rearrange open windows when a monitor is disabled, MonitorNap keeps Windows believing the display is active while commanding the physical panel hardware to enter low-power standby or dim.

## Features

- **Cursor-Based Auto Standby & Wake:** Automatically places the secondary monitor into low-power standby when the mouse cursor is not present on or moves away from that display, and instantly restores power and original brightness as soon as the cursor re-enters the monitor's screen bounds.
- **Direct Hardware Control:** Interacts with display microcontrollers via native Win32 and DXVA2 APIs (`dxva2.dll`) without third-party drivers or runtime dependencies.
- **Configurable Standby Modes:**
  - **Dim then Sleep (Default):** Reduces panel luminance to 0% as a visual warning 60 seconds before putting the panel into standby.
  - **Dim Only:** Drops hardware brightness to minimum without sending full standby commands.
  - **Black Curtain:** Displays a non-activating, borderless black overlay for displays or adapters lacking DDC/CI standby support.
- **Customizable Global Hotkey:** Toggle secondary monitor states instantly using configurable system-wide key combinations (Ctrl, Alt, Shift + Key).
- **Smart Standby Suppression:**
  - **Fullscreen Detection:** Automatically blocks standby if games, media players, or presentations are running fullscreen on the target display.
  - **Window Title Filtering:** User-configurable keywords (e.g., `YouTube`, `VLC`, `PotPlayer`) to inhibit standby during windowed media playback.
- **Per-Monitor Targeting:** Select specific secondary screens to manage in multi-display setups.
- **Minimal Footprint:** Native C# implementation consuming ~3-4 MB of RAM.

## Requirements

- **OS:** Windows 10 or Windows 11 (64-bit).
- **Display:** Monitor with DDC/CI support enabled in its OSD settings (enabled by default on most modern displays).
- **Connection:** Standard DisplayPort, HDMI, USB-C, or DVI cable.

## Installation

Download the latest standalone executable from the [Releases](https://github.com/EhsanCh/MonitorNap/releases) page. MonitorNap is portable and requires no installation.

## Building

The project can be compiled directly using the standard C# compiler (`csc.exe`) bundled with the .NET Framework on Windows.

1. Clone this repository:
   ```cmd
   git clone https://github.com/EhsanCh/MonitorNap.git
   cd MonitorNap
   ```
2. Build using the provided script:
   ```cmd
   build.bat
   ```
   Alternatively, invoke `csc.exe` manually:
   ```cmd
   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /win32icon:"assets\app.ico" /out:"dist\MonitorNap.exe" src\*.cs
   ```
3. The compiled binary will be placed in `dist\MonitorNap.exe`.

## Contributing

Contributions, issue reports, and pull requests are welcome. Feel free to review the repository issues or submit enhancements.

## Author

**Ehsan Chavoshi** ([@EhsanCh](https://github.com/EhsanCh))

## License

This project is licensed under the [MIT License](LICENSE).