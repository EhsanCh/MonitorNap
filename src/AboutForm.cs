using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/// <summary>/// Represents an individual project contributor with GitHub handle and profile link.
/// </summary>
public class Contributor {
    public string Name;
    public string Handle;
    public string Url;
}

/// <summary>
/// Standard About dialog displaying application metadata, repository link, and contributors.
/// </summary>
public class AboutForm : Form {
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

    // Extensible registry for active and future contributors
    private static readonly Contributor[] Contributors = new Contributor[] {
        new Contributor {            Name = "Ehsan Chavoshi",
            Handle = "@EhsanCh",
            Url = "https://github.com/EhsanCh"
        }
    };

    public AboutForm() {
        this.Text = "About MonitorNap";
        this.Icon = Program.AppIcon;
        this.Size = new Size(450, 440);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ShowInTaskbar = true;

        // App Icon Display
        PictureBox picIcon = new PictureBox {
            Location = new Point(20, 20),
            Size = new Size(48, 48)
        };
        picIcon.Paint += (s, e) => {
            if (Program.AppIcon != null) {
                IntPtr hdc = e.Graphics.GetHdc();
                try {
                    // DI_NORMAL (0x0003) renders the icon with native Windows alpha blending at 48x48
                    DrawIconEx(hdc, 0, 0, Program.AppIcon.Handle, picIcon.Width, picIcon.Height, 0, IntPtr.Zero, 3);
                } finally {
                    e.Graphics.ReleaseHdc(hdc);
                }
            }
        };
        // Header Labels
        Label lblTitle = new Label {            Text = "MonitorNap",
            Location = new Point(80, 20),
            AutoSize = true,
            Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold)
        };

        Label lblVersion = new Label {
            Text = "Version 1.0.0 (64-bit / 32-bit)",
            Location = new Point(82, 48),
            AutoSize = true,
            ForeColor = Color.DimGray
        };

        // Summary Description
        Label lblDesc = new Label {
            Text = "Intelligent hardware-level auto-standby power management for secondary monitors on Windows via VESA DDC/CI. Prevents desktop layout resetting while saving energy.",
            Location = new Point(20, 85),
            Size = new Size(395, 45),
            ForeColor = Color.FromArgb(50, 50, 50)
        };

        // Project Repository Link
        LinkLabel linkRepo = new LinkLabel {
            Text = "GitHub Repository: https://github.com/EhsanCh/MonitorNap",
            Location = new Point(20, 138),
            AutoSize = true,
            LinkColor = Color.FromArgb(0, 102, 204),
            ActiveLinkColor = Color.Blue,
            LinkBehavior = LinkBehavior.HoverUnderline
        };
        string repoUrl = "https://github.com/EhsanCh/MonitorNap";
        int repoStart = linkRepo.Text.IndexOf(repoUrl);
        if (repoStart >= 0) {
            linkRepo.Links.Clear();
            linkRepo.Links.Add(repoStart, repoUrl.Length, repoUrl);
        }
        linkRepo.LinkClicked += (s, e) => {
            try {
                if (e.Link != null && e.Link.LinkData != null) {
                    Process.Start(e.Link.LinkData.ToString());
                }
            } catch { }
        };

        // Contributors GroupBox
        GroupBox gbContributors = new GroupBox {
            Text = "Contributors & Maintainers",
            Location = new Point(20, 175),
            Size = new Size(395, 150)
        };

        Panel pnlContributors = new Panel {
            Location = new Point(10, 22),
            Size = new Size(375, 118),
            AutoScroll = true
        };

        int curY = 4;
        for (int i = 0; i < Contributors.Length; i++) {
            Contributor c = Contributors[i];
            string fullText = string.Format("{0} ({1})", c.Name, c.Handle);
            LinkLabel link = new LinkLabel {
                Text = fullText,
                Location = new Point(5, curY),
                AutoSize = true,
                LinkColor = Color.FromArgb(0, 102, 204),
                ActiveLinkColor = Color.Blue,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            int handleStart = fullText.IndexOf(c.Handle);
            if (handleStart >= 0) {
                link.Links.Clear();
                link.Links.Add(handleStart, c.Handle.Length, c.Url);
            }
            link.LinkClicked += (s, e) => {
                try {
                    if (e.Link != null && e.Link.LinkData != null) {
                        Process.Start(e.Link.LinkData.ToString());
                    }
                } catch { }
            };
            pnlContributors.Controls.Add(link);
            curY += 24;
        }
        gbContributors.Controls.Add(pnlContributors);

        // License Note
        Label lblLicense = new Label {
            Text = "Released under the MIT License.",
            Location = new Point(20, 350),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        // Close Button
        Button btnClose = new Button {
            Text = "Close",
            Location = new Point(325, 342),
            Size = new Size(90, 30)
        };
        btnClose.Click += (s, e) => this.Close();

        this.Controls.AddRange(new Control[] {
            picIcon, lblTitle, lblVersion, lblDesc, linkRepo, gbContributors, lblLicense, btnClose
        });
    }
}

