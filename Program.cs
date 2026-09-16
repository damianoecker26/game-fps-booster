using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

// Game FPS Booster — free FPS booster / game optimizer for low-end PCs.
// Single-file C# / WinForms / .NET Framework 4.8, compiled by the legacy csc (C# 5).
// Visual style: "Afterburner" — dark navy, cyan / electric-blue neon, turbine gauge.
// No network, no telemetry, no bundles. Default asInvoker (no forced UAC).
//
// BOOST = free RAM (trim working sets of background apps)
//       + quiet background apps (lower their priority => CPU headroom for the game)
//       + switch Windows to the High Performance power plan.
// Never touches the active game (foreground window) or any anti-cheat / DRM helper.

namespace GameFpsBooster
{
    static class Program
    {
        [STAThread]
        static void Main(string[] argv)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string shot = null;
            bool selftest = false;
            for (int i = 0; i < argv.Length; i++)
            {
                string a = argv[i].ToLowerInvariant();
                if (a == "--shot" && i + 1 < argv.Length) { shot = argv[i + 1]; i++; }
                else if (a == "--selftest") selftest = true;
            }

            // Headless engine smoke: run the whole boost once, write the result to a file.
            if (selftest)
            {
                BoostResult r = Booster.BoostAll();
                try
                {
                    System.IO.File.WriteAllText("fpsboost_selftest.txt",
                        "freed=" + r.FreedBytes + " optimized=" + r.Optimized + " highperf=" + r.HighPerf);
                }
                catch { }
                return;
            }

            // Headless UI smoke: render the window off-screen to a PNG.
            if (shot != null)
            {
                var f = new MainForm();
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-2000, -2000);
                f.ShowInTaskbar = false;
                f.Show();
                Application.DoEvents();
                f.RenderOnce();
                Application.DoEvents();
                var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height);
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.ClientSize.Width, f.ClientSize.Height));
                bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
                f.Close();
                return;
            }

            Application.Run(new MainForm());
        }
    }

    // ---- Palette (Afterburner) ----
    static class Theme
    {
        public static readonly Color Bg      = Color.FromArgb(10, 13, 20);    // dark navy base
        public static readonly Color Card    = Color.FromArgb(18, 23, 33);
        public static readonly Color Txt     = Color.FromArgb(238, 244, 252);
        public static readonly Color Mut     = Color.FromArgb(140, 152, 172);
        public static readonly Color MutB    = Color.FromArgb(86, 96, 114);
        public static readonly Color Accent  = Color.FromArgb(34, 211, 238);  // cyan
        public static readonly Color Accent2 = Color.FromArgb(56, 140, 255);  // electric blue
        public static readonly Color Border  = Color.FromArgb(30, 38, 52);
        public static readonly Color Track   = Color.FromArgb(28, 36, 50);
        public static readonly Color Blade   = Color.FromArgb(40, 54, 74);
        public static readonly Color HubFill = Color.FromArgb(12, 16, 24);
    }

    // ---- Native interop ----
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        // Trim a process working set (moves pages to standby => counted as available RAM).
        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        // Lower a background process priority so the game gets more CPU. Benign, no admin.
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        // Foreground window -> PID, so we never touch the app the user is actively using
        // (their game). Documented, benign user32 calls.
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public const uint PROCESS_QUERY_INFORMATION = 0x0400;
        public const uint PROCESS_SET_QUOTA         = 0x0100;
        public const uint PROCESS_SET_INFORMATION   = 0x0200;
        public const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
    }

    class BoostResult
    {
        public long FreedBytes;
        public int Optimized;
        public bool HighPerf;
    }

    // ---- Boost engine ----
    static class Booster
    {
        // Anti-cheat / DRM helper processes we must NEVER open, trim or reprioritise. A
        // kernel-mode anti-cheat treats an external, unsigned process opening a handle to it
        // as tampering — which can get the user game-banned. Never remove this list.
        static readonly string[] Excluded = {
            "easyanticheat", "eac_launcher", "eaanticheat",
            "beservice", "bedaisy", "battleye",
            "vgc", "vgtray", "vanguard",
            "faceit", "faceitservice",
            "pnkbstr", "punkbuster",
            "gameguard", "gamemon", "npggnt",
            "xigncode", "xhunter",
            "ricochet", "anticheat"
        };

        public static bool Read(out ulong total, out ulong avail, out uint load)
        {
            var m = new Native.MEMORYSTATUSEX();
            m.dwLength = (uint)Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX));
            if (Native.GlobalMemoryStatusEx(ref m))
            {
                total = m.ullTotalPhys; avail = m.ullAvailPhys; load = m.dwMemoryLoad;
                return true;
            }
            total = 0; avail = 0; load = 0;
            return false;
        }

        static bool IsExcludedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            for (int i = 0; i < Excluded.Length; i++)
                if (n.IndexOf(Excluded[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        static uint ForegroundPid()
        {
            try
            {
                IntPtr hwnd = Native.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return 0;
                uint pid;
                Native.GetWindowThreadProcessId(hwnd, out pid);
                return pid;
            }
            catch { return 0; }
        }

        // Switch to a performance power plan. Tries High Performance, then Ultimate. Benign,
        // documented (powercfg). Degrades gracefully if the plan is unavailable / restricted.
        static bool SetHighPerformance()
        {
            string[] guids = {
                "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // High performance (SCHEME_MIN)
                "e9a42b02-d5df-448d-aa00-03f14749eb61"  // Ultimate performance
            };
            for (int i = 0; i < guids.Length; i++)
            {
                try
                {
                    var psi = new ProcessStartInfo("powercfg", "/setactive " + guids[i]);
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    var p = Process.Start(psi);
                    if (p == null) continue;
                    p.WaitForExit(4000);
                    if (p.HasExited && p.ExitCode == 0) return true;
                }
                catch { }
            }
            return false;
        }

        // One boost pass. For every BACKGROUND process we are allowed to open:
        //   - EmptyWorkingSet  (free RAM)
        //   - SetPriorityClass(BelowNormal)  (give the game more CPU)
        // Never touches: Idle/System, anti-cheat/DRM helpers, this app, or the foreground
        // window's process (the user's active game). Then flips the power plan.
        public static BoostResult BoostAll()
        {
            var res = new BoostResult();
            try { Native.EmptyWorkingSet(Native.GetCurrentProcess()); } catch { }

            uint fg = ForegroundPid();
            int self = 0;
            try { self = Process.GetCurrentProcess().Id; } catch { }

            ulong tBefore, aBefore; uint lBefore;
            Read(out tBefore, out aBefore, out lBefore);

            Process[] procs;
            try { procs = Process.GetProcesses(); } catch { return res; }

            foreach (var pr in procs)
            {
                int pid;
                string name;
                try { pid = pr.Id; name = pr.ProcessName; }
                catch { try { pr.Dispose(); } catch { } continue; }

                bool skip = pid <= 4
                    || pid == self
                    || (fg != 0 && (uint)pid == fg)
                    || IsExcludedName(name);
                if (skip) { try { pr.Dispose(); } catch { } continue; }

                IntPtr h = Native.OpenProcess(
                    Native.PROCESS_QUERY_INFORMATION | Native.PROCESS_SET_QUOTA | Native.PROCESS_SET_INFORMATION,
                    false, pid);
                if (h != IntPtr.Zero)
                {
                    bool did = false;
                    try { if (Native.EmptyWorkingSet(h)) did = true; } catch { }
                    try { Native.SetPriorityClass(h, Native.BELOW_NORMAL_PRIORITY_CLASS); } catch { }
                    if (did) res.Optimized++;
                    Native.CloseHandle(h);
                }
                try { pr.Dispose(); } catch { }
            }

            ulong tAfter, aAfter; uint lAfter;
            Read(out tAfter, out aAfter, out lAfter);
            long freed = (long)aAfter - (long)aBefore;
            if (freed < 0) freed = 0;
            res.FreedBytes = freed;

            res.HighPerf = SetHighPerformance();
            return res;
        }
    }

    // ---- Turbine logo mark ----
    class LogoPanel : Panel
    {
        public LogoPanel()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float w = Width, h = Height;
            if (w < 8 || h < 8) return;
            float cx = w / 2f, cy = h / 2f;
            float R = Math.Min(w, h) / 2f - 2f;

            using (var pen = new Pen(Theme.Accent, Math.Max(2f, R * 0.16f)))
                g.DrawEllipse(pen, cx - R, cy - R, R * 2, R * 2);

            int blades = 6;
            using (var bp = new Pen(Theme.Accent2, Math.Max(1.6f, R * 0.13f)))
            {
                bp.StartCap = LineCap.Round; bp.EndCap = LineCap.Round;
                for (int i = 0; i < blades; i++)
                {
                    double a = Math.PI * 2 * i / blades;
                    double a2 = a + 0.6; // tangential offset => turbine blade look
                    float r1 = R * 0.26f, r2 = R * 0.70f;
                    g.DrawLine(bp,
                        cx + r1 * (float)Math.Cos(a), cy + r1 * (float)Math.Sin(a),
                        cx + r2 * (float)Math.Cos(a2), cy + r2 * (float)Math.Sin(a2));
                }
            }

            using (var hub = new SolidBrush(Theme.Accent))
                g.FillEllipse(hub, cx - R * 0.17f, cy - R * 0.17f, R * 0.34f, R * 0.34f);
        }
    }

    // ---- Turbine load gauge ----
    class GaugePanel : Panel
    {
        public double Load = 0;         // 0..100
        public string CenterText = "—";
        public string SubText = "";

        public GaugePanel()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int pad = 10;
            int size = Math.Min(Width, Height) - pad * 2;
            if (size <= 40) return;
            float cx = Width / 2f, cy = Height / 2f;
            float R = size / 2f;
            double frac = Math.Max(0, Math.Min(100, Load)) / 100.0;

            // turbine blades around a full circle (lit up to current load)
            int blades = 48;
            for (int i = 0; i < blades; i++)
            {
                double a = Math.PI * 2 * i / blades - Math.PI / 2; // start at top
                double a2 = a + 0.14;                              // tangential blade offset
                float rOut = R;
                float rIn = R - size * 0.085f;
                bool lit = ((double)i / blades) <= frac;
                Color c = lit ? Theme.Accent : Theme.Blade;
                using (var tp = new Pen(c, 2.3f))
                {
                    tp.StartCap = LineCap.Round; tp.EndCap = LineCap.Round;
                    g.DrawLine(tp,
                        cx + rOut * (float)Math.Cos(a), cy + rOut * (float)Math.Sin(a),
                        cx + rIn * (float)Math.Cos(a2), cy + rIn * (float)Math.Sin(a2));
                }
            }

            // ring track + value
            float arcR = R - size * 0.16f;
            var arcRect = new RectangleF(cx - arcR, cy - arcR, arcR * 2, arcR * 2);
            float thick = Math.Max(6f, size * 0.045f);
            using (var tp = new Pen(Theme.Track, thick))
            { tp.StartCap = LineCap.Round; tp.EndCap = LineCap.Round; g.DrawArc(tp, arcRect, -90, 360); }
            float sweep = 360f * (float)frac;
            if (sweep > 0.5f)
                using (var vp = new Pen(Theme.Accent, thick))
                { vp.StartCap = LineCap.Round; vp.EndCap = LineCap.Round; g.DrawArc(vp, arcRect, -90, sweep); }

            // inner hub
            float hubR = arcR - thick * 1.25f;
            if (hubR > 4f)
            {
                using (var hb = new SolidBrush(Theme.HubFill))
                    g.FillEllipse(hb, cx - hubR, cy - hubR, hubR * 2, hubR * 2);
                using (var hp = new Pen(Color.FromArgb(70, Theme.Accent), 2f))
                    g.DrawEllipse(hp, cx - hubR, cy - hubR, hubR * 2, hubR * 2);
            }

            // center readout
            var sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            using (var big = new Font("Segoe UI", size * 0.15f, FontStyle.Bold | FontStyle.Italic))
            using (var sub = new Font("Segoe UI", 9.5f))
            using (var bc = new SolidBrush(Theme.Txt))
            using (var mc = new SolidBrush(Theme.Mut))
            {
                g.DrawString(CenterText, big, bc, new RectangleF(cx - R, cy - size * 0.085f, R * 2, size * 0.20f), sf);
                g.DrawString(SubText, sub, mc, new RectangleF(cx - R, cy + size * 0.135f, R * 2, size * 0.12f), sf);
            }
        }
    }

    // ---- Main window ----
    class MainForm : Form
    {
        const string Brand = "Game FPS Booster";
        const int AutoThresholdPct = 80;

        GaugePanel _gauge;
        Button _btnBoost;
        Label _lblResult, _lblHint;
        CheckBox _chkAuto;
        NotifyIcon _tray;
        Timer _tmrUi, _tmrAuto, _tmrPulse;
        double _pulse;
        bool _busy;
        bool _trayHintShown;

        public MainForm()
        {
            DoubleBuffered = true;
            BuildUi();
            BuildTray();
            Load += (s, e) => { UpdateGauge(); _tmrUi.Start(); _tmrPulse.Start(); };
            Resize += OnResize;
            FormClosing += (s, e) => { try { if (_tray != null) _tray.Visible = false; } catch { } };
        }

        // Used by the headless --shot path to populate the gauge once before capture.
        public void RenderOnce() { UpdateGauge(); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = ClientRectangle;
            using (var bg = new SolidBrush(Theme.Bg))
                g.FillRectangle(bg, r);

            // faint diagonal "speed" streaks
            using (var pl = new Pen(Color.FromArgb(9, 90, 200, 255)))
            {
                int step = 7;
                for (int x = -r.Height; x < r.Width; x += step)
                    g.DrawLine(pl, x, 0, x + r.Height, r.Height);
            }

            // soft cyan glow behind the gauge (gauge center ~ 220,211)
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int gcx = 220, gcy = 211;
            for (int i = 9; i >= 1; i--)
            {
                int rad = i * 22;
                using (var gb = new SolidBrush(Color.FromArgb(5, Theme.Accent)))
                    g.FillEllipse(gb, gcx - rad, gcy - rad, rad * 2, rad * 2);
            }

            // subtle accent inner frame
            using (var bp = new Pen(Color.FromArgb(55, Theme.Accent), 1f))
                g.DrawRectangle(bp, 0, 0, r.Width - 1, r.Height - 1);
        }

        void BuildUi()
        {
            Text = Brand;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(440, 600);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Theme.Bg;
            ForeColor = Theme.Txt;
            Font = new Font("Segoe UI", 10f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var logo = new LogoPanel();
            logo.SetBounds(18, 14, 42, 42);
            Controls.Add(logo);

            var name = new Label { Text = Brand.ToUpperInvariant(), ForeColor = Theme.Accent, Font = new Font("Segoe UI", 16f, FontStyle.Bold | FontStyle.Italic), AutoSize = true, Location = new Point(68, 15), BackColor = Color.Transparent };
            var tagline = new Label { Text = "Turbo your games — free FPS boost", ForeColor = Theme.Mut, Font = new Font("Segoe UI", 9.5f, FontStyle.Italic), AutoSize = true, Location = new Point(70, 44), BackColor = Color.Transparent };
            Controls.Add(name);
            Controls.Add(tagline);

            _gauge = new GaugePanel();
            _gauge.SetBounds(90, 80, 260, 262);
            Controls.Add(_gauge);

            _btnBoost = MakeButton("BOOST NOW");
            _btnBoost.SetBounds(40, 360, 360, 58);
            _btnBoost.Click += async (s, e) => await DoBoost();
            Controls.Add(_btnBoost);

            _lblResult = new Label { Text = "Ready. Hit Boost for more FPS.", ForeColor = Theme.Accent, Font = new Font("Segoe UI", 10f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
            _lblResult.SetBounds(30, 426, 380, 26);
            Controls.Add(_lblResult);

            _chkAuto = new CheckBox { Text = "  Game Mode — auto-boost when the system is loaded", ForeColor = Theme.Txt, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9.5f), FlatStyle = FlatStyle.Flat };
            _chkAuto.SetBounds(40, 462, 360, 26);
            _chkAuto.CheckedChanged += (s, e) => { if (_tmrAuto != null) _tmrAuto.Enabled = _chkAuto.Checked; };
            Controls.Add(_chkAuto);

            _lblHint = new Label { ForeColor = Theme.Mut, Font = new Font("Segoe UI", 9f), BackColor = Color.Transparent };
            _lblHint.SetBounds(40, 496, 360, 44);
            _lblHint.Text = "🛡  Frees RAM, quiets background apps and sets High Performance.\r\nNever touches your active game or anti-cheat.";
            Controls.Add(_lblHint);

            var footer = new Label { Text = "Free · no ads · no telemetry · no bundles", ForeColor = Theme.MutB, Font = new Font("Segoe UI", 8.5f), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
            footer.SetBounds(40, 566, 360, 22);
            Controls.Add(footer);

            _tmrUi = new Timer { Interval = 1500 };
            _tmrUi.Tick += (s, e) => UpdateGauge();

            _tmrAuto = new Timer { Interval = 20000, Enabled = false };
            _tmrAuto.Tick += async (s, e) => await AutoTick();

            _tmrPulse = new Timer { Interval = 45 };
            _tmrPulse.Tick += (s, e) =>
            {
                _pulse += 0.09;
                if (_btnBoost != null && _btnBoost.Enabled)
                {
                    double t = (Math.Sin(_pulse) + 1.0) / 2.0;
                    _btnBoost.BackColor = Lerp(Theme.Accent, Theme.Accent2, t);
                }
            };
        }

        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Boost now", null, async (s, e) => await DoBoost());
            menu.Items.Add("Open", null, (s, e) => RestoreFromTray());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => { try { _tray.Visible = false; } catch { } Application.Exit(); });

            Icon ico;
            try { ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { ico = SystemIcons.Application; }
            if (ico == null) ico = SystemIcons.Application;

            _tray = new NotifyIcon { Icon = ico, Visible = true, Text = Brand, ContextMenuStrip = menu };
            _tray.DoubleClick += (s, e) => RestoreFromTray();
        }

        void OnResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                if (!_trayHintShown && _tray != null)
                {
                    _trayHintShown = true;
                    try { _tray.ShowBalloonTip(2500, Brand, "Still running in the tray. Double-click to open.", ToolTipIcon.Info); }
                    catch { }
                }
            }
        }

        void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        Button MakeButton(string text)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(6, 14, 26),
                BackColor = Theme.Accent,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold | FontStyle.Italic),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Resize += (s, e) => Round(b, 10);
            return b;
        }

        static void Round(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(c.Width - d - 1, 0, d, d, 270, 90);
            path.AddArc(c.Width - d - 1, c.Height - d - 1, d, d, 0, 90);
            path.AddArc(0, c.Height - d - 1, d, d, 90, 90);
            path.CloseFigure();
            c.Region = new Region(path);
        }

        static Color Lerp(Color a, Color b, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            int r = (int)(a.R + (b.R - a.R) * t);
            int g = (int)(a.G + (b.G - a.G) * t);
            int bl = (int)(a.B + (b.B - a.B) * t);
            return Color.FromArgb(r, g, bl);
        }

        void UpdateGauge()
        {
            ulong total, avail; uint load;
            if (!Booster.Read(out total, out avail, out load)) return;
            ulong used = total > avail ? total - avail : 0;
            _gauge.Load = load;
            _gauge.CenterText = load + "%";
            _gauge.SubText = GB(used) + " / " + GB(total);
            _gauge.Invalidate();
            if (_tray != null)
            {
                string t = Brand + " — " + load + "%";
                if (t.Length > 60) t = t.Substring(0, 60);
                _tray.Text = t;
            }
        }

        async Task DoBoost()
        {
            if (_busy) return;
            _busy = true;
            string old = _btnBoost.Text;
            _btnBoost.Enabled = false;
            _btnBoost.BackColor = Theme.MutB;
            _btnBoost.Text = "BOOSTING…";
            _lblResult.ForeColor = Theme.Mut;
            _lblResult.Text = "Optimizing your system…";

            BoostResult res = null;
            await Task.Run(() => { res = Booster.BoostAll(); });
            await Task.Delay(300);
            if (res == null) res = new BoostResult();

            string tail = res.HighPerf ? "  ·  High Perf ON" : "";
            _lblResult.ForeColor = Theme.Accent;
            _lblResult.Text = "✓  Freed " + HumanFreed(res.FreedBytes) + "  ·  " + res.Optimized + " quieted" + tail;
            UpdateGauge();

            _btnBoost.Text = old;
            _btnBoost.Enabled = true;
            _busy = false;
        }

        async Task AutoTick()
        {
            if (_busy) return;
            ulong t, a; uint load;
            if (!Booster.Read(out t, out a, out load)) return;
            if (load < AutoThresholdPct) return;

            _busy = true;
            BoostResult res = null;
            await Task.Run(() => { res = Booster.BoostAll(); });
            if (res == null) res = new BoostResult();
            UpdateGauge();
            _lblResult.ForeColor = Theme.Accent;
            _lblResult.Text = "⚡  Auto-boost at " + load + "%  ·  " + res.Optimized + " quieted";
            _busy = false;
        }

        static string GB(ulong bytes)
        {
            double g = bytes / 1073741824.0;
            return g.ToString("0.0") + " GB";
        }

        static string HumanFreed(long bytes)
        {
            if (bytes < 0) bytes = 0;
            double mb = bytes / 1048576.0;
            if (mb >= 1024) return (mb / 1024.0).ToString("0.0") + " GB";
            return mb.ToString("0") + " MB";
        }
    }
}
