// StandUpReminder - a modern, Gen-Z styled "stand up" reminder for Windows.
// Compiled to a native .exe with the .NET Framework C# compiler (csc.exe).
// Features: system tray, configurable interval, rotating messages,
// animated sit-to-stand figure, gradient full-screen flash, and notifications.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace StandUpReminder
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Preview the flash reminder directly (used for testing/screenshots).
            if (args.Length > 0 && args[0] == "--flash")
            {
                var s = Settings.Load();
                Application.Run(new FlashForm(Prompts.Pick(), s));
                return;
            }

            bool createdNew;
            using (var mutex = new Mutex(true, "StandUpReminder_SingleInstance_v1", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Stand Up Reminder is already running (check the system tray).",
                        "Stand Up Reminder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.Run(new TrayApp());
            }
        }
    }

    // ------------------------------------------------------------------
    // Settings, persisted to %APPDATA%\StandUpReminder\settings.ini
    // ------------------------------------------------------------------
    class Settings
    {
        public int IntervalMinutes = 30;
        public string Mode = "Both";      // Notification | Flash | Both
        public int FlashSeconds = 15;
        public bool PlaySound = true;
        public int PaletteIndex = -1;     // -1 = random each time, else index into Palettes.All

        public static string Dir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StandUpReminder");
            }
        }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq).Trim();
                        string v = line.Substring(eq + 1).Trim();
                        switch (k)
                        {
                            case "IntervalMinutes": int.TryParse(v, out s.IntervalMinutes); break;
                            case "Mode": s.Mode = v; break;
                            case "FlashSeconds": int.TryParse(v, out s.FlashSeconds); break;
                            case "PlaySound": bool.TryParse(v, out s.PlaySound); break;
                            case "PaletteIndex": { int pi; if (int.TryParse(v, out pi)) s.PaletteIndex = pi; break; }
                        }
                    }
                }
            }
            catch { }
            if (s.IntervalMinutes < 1) s.IntervalMinutes = 30;
            if (s.FlashSeconds < 3) s.FlashSeconds = 15;
            if (s.Mode != "Notification" && s.Mode != "Flash" && s.Mode != "Both") s.Mode = "Both";
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new[]
                {
                    "IntervalMinutes=" + IntervalMinutes,
                    "Mode=" + Mode,
                    "FlashSeconds=" + FlashSeconds,
                    "PlaySound=" + PlaySound,
                    "PaletteIndex=" + PaletteIndex
                });
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------
    // A single reminder prompt (exercise kind + Gen-Z copy)
    // ------------------------------------------------------------------
    class Prompt
    {
        public string Kind;    // chip label, e.g. "STRETCH"
        public string Title;
        public string Sub;
        public Prompt(string kind, string title, string sub) { Kind = kind; Title = title; Sub = sub; }
    }

    // Rotating, categorized Gen-Z prompts: stand / stretch / walk / hydrate / eyes.
    static class Prompts
    {
        static readonly Random rng = new Random();

        class Cat
        {
            public string Kind;
            public string[] Titles;
            public string[] Subs;
        }

        static readonly Cat[] Cats =
        {
            new Cat {
                Kind = "STAND UP",
                Titles = new[] { "STAND UP bestie", "up you go", "no cap, get up", "little stand up era" },
                Subs = new[] {
                    "your back said 'we need space' - stand up rn",
                    "it's giving... sitting too long. up you go",
                    "30 seconds of standing = big W energy",
                    "stand up king / queen, shoulders back" }
            },
            new Cat {
                Kind = "STRETCH",
                Titles = new[] { "stretch break", "reach for it", "loosen up", "spine check" },
                Subs = new[] {
                    "reach for the sky, then side to side. slay",
                    "roll those shoulders, unclench that jaw",
                    "big stretch = big relief. you got this",
                    "arms up, deep breath, feel that reset" }
            },
            new Cat {
                Kind = "WALK IT OUT",
                Titles = new[] { "walk it out", "touch grass o'clock", "quick lap", "main character stroll" },
                Subs = new[] {
                    "brb touching grass - you should too",
                    "take a lap, refill the water, vibe",
                    "60 seconds of walking hits different",
                    "move those legs, reset the brain" }
            },
            new Cat {
                Kind = "HYDRATE",
                Titles = new[] { "hydrate bestie", "water check", "sip sip", "stay juicy" },
                Subs = new[] {
                    "when's the last time you drank water? go",
                    "hydration station: population you. drink up",
                    "a sip a day keeps the crash away",
                    "your brain is 75% water. top it up" }
            },
            new Cat {
                Kind = "EYE BREAK",
                Titles = new[] { "20-20-20 rule", "eyes need a break", "look away", "blink era" },
                Subs = new[] {
                    "look 20 ft away for 20 seconds. eyes = saved",
                    "screen's been staring back too long. look off",
                    "unfocus, blink, breathe. give the eyes a sec",
                    "distance gaze incoming - relax those eyes" }
            }
        };

        public static Prompt Pick()
        {
            var c = Cats[rng.Next(Cats.Length)];
            return new Prompt(c.Kind, c.Titles[rng.Next(c.Titles.Length)], c.Subs[rng.Next(c.Subs.Length)]);
        }
    }

    // ------------------------------------------------------------------
    // Modern gradient palettes (start, end)
    // ------------------------------------------------------------------
    static class Palettes
    {
        static readonly Random rng = new Random();
        public static readonly string[] Names =
        {
            "Violet Pink", "Ocean", "Sunset", "Indigo Sky", "Fuchsia Dream", "Emerald Lime",
            "Cotton Candy", "Peachy", "Aurora", "Mango", "Grape Soda", "Mint Breeze"
        };
        public static readonly Color[][] All =
        {
            new[]{ Color.FromArgb(124,58,237), Color.FromArgb(236,72,153) },   // violet -> pink
            new[]{ Color.FromArgb(59,130,246), Color.FromArgb(16,185,129) },   // blue -> emerald
            new[]{ Color.FromArgb(244,63,94),  Color.FromArgb(251,146,60) },   // rose -> orange
            new[]{ Color.FromArgb(99,102,241), Color.FromArgb(14,165,233) },   // indigo -> sky
            new[]{ Color.FromArgb(217,70,239), Color.FromArgb(99,102,241) },   // fuchsia -> indigo
            new[]{ Color.FromArgb(16,185,129), Color.FromArgb(132,204,22) },   // emerald -> lime
            new[]{ Color.FromArgb(236,72,153), Color.FromArgb(129,140,248) },  // pink -> periwinkle
            new[]{ Color.FromArgb(251,113,133), Color.FromArgb(253,186,116) }, // coral -> peach
            new[]{ Color.FromArgb(34,211,238), Color.FromArgb(167,139,250) },  // cyan -> lilac
            new[]{ Color.FromArgb(249,115,22), Color.FromArgb(234,179,8) },    // orange -> amber
            new[]{ Color.FromArgb(147,51,234), Color.FromArgb(59,7,100) },     // purple -> deep grape
            new[]{ Color.FromArgb(45,212,191), Color.FromArgb(96,165,250) },   // teal -> blue
        };
        public static Color[] Pick() { return All[rng.Next(All.Length)]; }
        public static Color[] Get(int i)
        {
            if (i < 0 || i >= All.Length) return Pick();
            return All[i];
        }
    }

    // ------------------------------------------------------------------
    // Tray application
    // ------------------------------------------------------------------
    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer timer;
        readonly Icon appIcon;
        Settings settings;
        bool paused;
        ToolStripMenuItem pauseItem;
        ToolStripMenuItem statusItem;

        public TrayApp()
        {
            settings = Settings.Load();

            appIcon = LoadAppIcon();
            tray = new NotifyIcon();
            tray.Icon = appIcon;
            tray.Visible = true;
            tray.Text = "Stand Up Reminder";
            tray.DoubleClick += (s, e) => ShowSettings();

            var menu = new ContextMenuStrip();
            statusItem = new ToolStripMenuItem("Reminders: ON") { Enabled = false };
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());
            var test = new ToolStripMenuItem("Test reminder now");
            test.Click += (s, e) => Fire();
            menu.Items.Add(test);
            pauseItem = new ToolStripMenuItem("Pause");
            pauseItem.Click += (s, e) => TogglePause();
            menu.Items.Add(pauseItem);
            var settingsItem = new ToolStripMenuItem("Settings...");
            settingsItem.Click += (s, e) => ShowSettings();
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            var exit = new ToolStripMenuItem("Exit");
            exit.Click += (s, e) => ExitApp();
            menu.Items.Add(exit);
            tray.ContextMenuStrip = menu;

            timer = new System.Windows.Forms.Timer();
            timer.Tick += (s, e) => Fire();
            RestartTimer();

            tray.ShowBalloonTip(4000, "Stand Up Reminder",
                "Running in the tray. First stretch in " + settings.IntervalMinutes + " min. Let's go!",
                ToolTipIcon.Info);
        }

        static Icon LoadAppIcon()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return SystemIcons.Application; }
        }

        void RestartTimer()
        {
            timer.Stop();
            timer.Interval = settings.IntervalMinutes * 60 * 1000;
            if (!paused) timer.Start();
        }

        void TogglePause()
        {
            paused = !paused;
            if (paused) { timer.Stop(); pauseItem.Text = "Resume"; statusItem.Text = "Reminders: PAUSED"; }
            else { RestartTimer(); pauseItem.Text = "Pause"; statusItem.Text = "Reminders: ON"; }
        }

        void Fire()
        {
            var p = Prompts.Pick();
            if (settings.Mode == "Notification" || settings.Mode == "Both")
            {
                tray.BalloonTipTitle = p.Title;
                tray.BalloonTipText = p.Sub;
                tray.BalloonTipIcon = ToolTipIcon.Info;
                tray.ShowBalloonTip(8000);
                if (settings.PlaySound) System.Media.SystemSounds.Asterisk.Play();
            }
            if (settings.Mode == "Flash" || settings.Mode == "Both")
            {
                var f = new FlashForm(p, settings);
                f.Show();
            }
        }

        void ShowSettings()
        {
            using (var dlg = new SettingsForm(settings))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    settings = dlg.Result;
                    settings.Save();
                    RestartTimer();
                    tray.Text = "Stand Up Reminder - every " + settings.IntervalMinutes + " min";
                }
            }
        }

        void ExitApp()
        {
            timer.Stop();
            timer.Dispose();
            tray.Visible = false;
            tray.Dispose();
            if (appIcon != null) appIcon.Dispose();
            ExitThread();
        }
    }

    // ------------------------------------------------------------------
    // Full-screen animated flash
    // ------------------------------------------------------------------
    class FlashForm : Form
    {
        readonly string title;
        readonly string sub;
        readonly string kind;
        readonly Settings settings;
        readonly Color[] palette;
        readonly System.Windows.Forms.Timer anim;
        static readonly Random arng = new Random();
        readonly int animKind;   // 0 = sit-to-stand, 1 = walking (drawn fallback)
        float phase;          // 0..1 animation loop for the figure
        float bgShift;        // gradient animation
        int elapsedMs;
        readonly int totalMs;
        Rectangle dismissRect;

        // Animated GIF character (drawn manually so it composites with transparency)
        Image gifImage;
        FrameDimension gifDim;
        int gifFrames;
        bool useGif;

        // Intro (fade + scale in) / outro (fade out) animation state
        float introT;         // 0..1, grows on open, shrinks on close
        bool closing;

        // Cached text resources (constant per flash - built once, not per frame)
        readonly string titleUpper;
        readonly string kindUpper;
        Font fTitle, fSub, fChip, fPill;
        SolidBrush brWhite, brSoft, brShadow;
        readonly StringFormat centerFmt =
            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        static string FindGif()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                foreach (var rel in new[] { "standup.gif", "assets\\standup.gif" })
                {
                    string p = System.IO.Path.Combine(dir, rel);
                    if (System.IO.File.Exists(p)) return p;
                }
            }
            catch { }
            return null;
        }

        public FlashForm(Prompt p, Settings settings)
        {
            this.title = p.Title;
            this.sub = p.Sub;
            this.kind = p.Kind;
            this.titleUpper = p.Title.ToUpperInvariant();
            this.kindUpper = p.Kind.ToUpperInvariant();
            this.settings = settings;
            this.palette = Palettes.Get(settings.PaletteIndex);
            this.animKind = arng.Next(2);
            this.totalMs = Math.Max(3, settings.FlashSeconds) * 1000;

            int w = Screen.PrimaryScreen.Bounds.Width;
            fTitle = new Font("Segoe UI", Math.Max(26f, w * 0.030f), FontStyle.Bold);
            fSub = new Font("Segoe UI Semibold", Math.Max(14f, w * 0.013f), FontStyle.Regular);
            fChip = new Font("Segoe UI Semibold", Math.Max(11f, w * 0.0095f), FontStyle.Bold);
            fPill = new Font("Segoe UI Semibold", Math.Max(11f, w * 0.010f), FontStyle.Bold);
            brWhite = new SolidBrush(Color.White);
            brSoft = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
            brShadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0));

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Opacity = 0; // fade in from transparent
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;

            KeyPreview = true;
            KeyDown += (s, e) => Dismiss();
            MouseClick += (s, e) => Dismiss();

            anim = new System.Windows.Forms.Timer();
            anim.Interval = 33; // ~30 fps
            anim.Tick += OnTick;

            string gif = FindGif();
            if (gif != null)
            {
                try
                {
                    gifImage = Image.FromFile(gif);
                    gifDim = new FrameDimension(gifImage.FrameDimensionsList[0]);
                    gifFrames = gifImage.GetFrameCount(gifDim);
                    useGif = gifFrames > 0;
                }
                catch { useGif = false; }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Rounded screen corners
            using (var rp = Rounded(new Rectangle(0, 0, Width, Height), 28))
                Region = new Region(rp);
            Activate();
            anim.Start();
            if (settings.PlaySound) System.Media.SystemSounds.Exclamation.Play();
        }

        void OnTick(object s, EventArgs e)
        {
            elapsedMs += anim.Interval;
            phase += anim.Interval / 1400f;
            if (phase >= 1f) phase -= 1f;
            bgShift += anim.Interval / 6000f;
            if (bgShift >= 1f) bgShift -= 1f;

            bool animatingOpacity = closing || introT < 1f;
            if (closing)
            {
                introT -= anim.Interval / 180f;
                if (introT <= 0f) { anim.Stop(); Close(); return; }
            }
            else
            {
                if (introT < 1f) { introT += anim.Interval / 260f; if (introT > 1f) introT = 1f; }
                if (elapsedMs >= totalMs) { Dismiss(); }
            }

            // Only touch Opacity while fading in/out (each set forces a layered-window redraw).
            if (animatingOpacity) Opacity = 0.98 * Ease(Clamp01(introT));
            Invalidate();
        }

        void Dismiss()
        {
            closing = true;   // OnTick fades out, then closes
        }

        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (anim != null) { anim.Stop(); anim.Dispose(); }
            if (gifImage != null) { gifImage.Dispose(); gifImage = null; }
            if (fTitle != null) fTitle.Dispose();
            if (fSub != null) fSub.Dispose();
            if (fChip != null) fChip.Dispose();
            if (fPill != null) fPill.Dispose();
            if (brWhite != null) brWhite.Dispose();
            if (brSoft != null) brSoft.Dispose();
            if (brShadow != null) brShadow.Dispose();
            if (centerFmt != null) centerFmt.Dispose();
        }

        static float Ease(float t)
        {
            // smootherstep
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        static PointF Lerp(PointF a, PointF b, float t)
        {
            return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Animated diagonal gradient background
            float sway = (float)(0.5 + 0.5 * Math.Sin(bgShift * 2 * Math.PI));
            Color c1 = Mix(palette[0], palette[1], sway * 0.25f);
            Color c2 = Mix(palette[1], palette[0], sway * 0.25f);
            using (var bg = new LinearGradientBrush(ClientRectangle, c1, c2, 45f))
                g.FillRectangle(bg, ClientRectangle);

            int cx = Width / 2;

            // Soft radial glow behind the character for depth (modern mesh-gradient feel)
            using (var glowPath = new GraphicsPath())
            {
                int gr = (int)(Math.Min(Width, Height) * 0.55f);
                var glowRect = new Rectangle(cx - gr, (int)(Height * 0.34f) - gr, gr * 2, gr * 2);
                glowPath.AddEllipse(glowRect);
                using (var glow = new PathGradientBrush(glowPath))
                {
                    glow.CenterColor = Color.FromArgb(70, 255, 255, 255);
                    glow.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) };
                    g.FillPath(glow, glowPath);
                }
            }

            // Smooth vertical scrim: fully clear at top, easing to dark at the
            // bottom so the text stays readable WITHOUT a hard seam.
            using (var scrim = new LinearGradientBrush(new Rectangle(0, 0, Width, Height),
                       Color.Black, Color.Black, 90f))
            {
                var blend = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(0, 0, 0, 0),
                        Color.FromArgb(0, 0, 0, 0),
                        Color.FromArgb(110, 0, 0, 0)
                    },
                    Positions = new[] { 0f, 0.55f, 1f }
                };
                scrim.InterpolationColors = blend;
                g.FillRectangle(scrim, ClientRectangle);
            }

            // Character: glassmorphism card + GIF, or drawn fallback. Scale-in on open.
            float cardScale = 0.86f + 0.14f * Ease(Clamp01(introT));
            if (useGif)
            {
                int baseSz = (int)(Math.Min(Width, Height) * 0.42f);
                int sz = (int)(baseSz * cardScale);
                int ccy = (int)(Height * 0.33f);
                Rectangle cardRect = new Rectangle(cx - sz / 2, ccy - sz / 2, sz, sz);
                DrawGlassCard(g, cardRect);

                Rectangle inner = Rectangle.Inflate(cardRect, -(int)(sz * 0.09f), -(int)(sz * 0.09f));
                int idx = (int)((elapsedMs / 33) % gifFrames);
                try { gifImage.SelectActiveFrame(gifDim, idx); } catch { }
                g.DrawImage(gifImage, inner);
            }
            else
            {
                float figSize = Math.Min(Width, Height) * 0.34f * cardScale;
                var box = new RectangleF(cx - figSize / 2f, Height * 0.30f, figSize, figSize);
                if (animKind == 1)
                    DrawWalker(g, box, phase);
                else
                    DrawFigure(g, box, Ease((float)(0.5 - 0.5 * Math.Cos(phase * 2 * Math.PI))));
            }

            // Exercise-kind chip above the title
            SizeF cs = g.MeasureString(kindUpper, fChip);
            int cw = (int)cs.Width + 44, ch = (int)cs.Height + 16;
            var chipR = new Rectangle(cx - cw / 2, (int)(Height * 0.585f), cw, ch);
            using (var path = Rounded(chipR, ch / 2))
            using (var cb = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
            using (var cpen = new Pen(Color.FromArgb(150, 255, 255, 255), 1.5f))
            {
                g.FillPath(cb, path);
                g.DrawPath(cpen, path);
            }
            g.DrawString(kindUpper, fChip, brWhite, chipR, centerFmt);

            // Title + subtitle (with a subtle drop shadow)
            var titleRect = new RectangleF(0, Height * 0.66f, Width, Height * 0.10f);
            g.DrawString(titleUpper, fTitle, brShadow, new RectangleF(3, Height * 0.66f + 3, Width, Height * 0.10f), centerFmt);
            g.DrawString(titleUpper, fTitle, brWhite, titleRect, centerFmt);
            g.DrawString(sub, fSub, brSoft, new RectangleF(0, Height * 0.77f, Width, Height * 0.06f), centerFmt);

            // Dismiss pill + countdown
            int remain = Math.Max(0, (totalMs - elapsedMs + 999) / 1000);
            string pill = "I'm up   -   tap anywhere (" + remain + "s)";
            SizeF ps = g.MeasureString(pill, fPill);
            int pw = (int)ps.Width + 56, ph = (int)ps.Height + 26;
            dismissRect = new Rectangle(cx - pw / 2, (int)(Height * 0.86f), pw, ph);
            using (var path = Rounded(dismissRect, ph / 2))
            using (var pb = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
            using (var pen = new Pen(Color.FromArgb(160, 255, 255, 255), 2f))
            {
                g.FillPath(pb, path);
                g.DrawPath(pen, path);
            }
            g.DrawString(pill, fPill, brWhite, dismissRect, centerFmt);
        }

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Frosted translucent "glass" panel: soft shadow, glassy fill, top sheen, light border.
        void DrawGlassCard(Graphics g, Rectangle card)
        {
            int radius = Math.Max(18, card.Width / 7);

            using (var shPath = Rounded(new Rectangle(card.X, card.Y + 16, card.Width, card.Height), radius))
            using (var sh = new PathGradientBrush(shPath))
            {
                sh.CenterColor = Color.FromArgb(85, 0, 0, 0);
                sh.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                g.FillPath(sh, shPath);
            }

            using (var path = Rounded(card, radius))
            {
                using (var glass = new LinearGradientBrush(card,
                           Color.FromArgb(95, 255, 255, 255), Color.FromArgb(32, 255, 255, 255), 90f))
                    g.FillPath(glass, path);

                var st = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                var sheenRect = new Rectangle(card.X, card.Y, card.Width, card.Height / 2);
                using (var sheen = new LinearGradientBrush(sheenRect,
                           Color.FromArgb(85, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                    g.FillRectangle(sheen, sheenRect);
                g.Restore(st);

                using (var pen = new Pen(Color.FromArgb(140, 255, 255, 255), 1.5f))
                    g.DrawPath(pen, path);
            }
        }

        // Draws a stick figure interpolating from sitting (s=0) to standing + arms raised (s=1)
        void DrawFigure(Graphics g, RectangleF box, float s)
        {
            Func<float, float, PointF> P = (nx, ny) =>
                new PointF(box.Left + nx * box.Width, box.Top + ny * box.Height);

            // sitting pose               // standing + celebrate pose
            PointF head = Lerp(P(0.40f, 0.33f), P(0.50f, 0.17f), s);
            PointF shoulder = Lerp(P(0.42f, 0.45f), P(0.50f, 0.30f), s);
            PointF hip = Lerp(P(0.40f, 0.63f), P(0.50f, 0.57f), s);
            PointF knee = Lerp(P(0.62f, 0.63f), P(0.50f, 0.77f), s);
            PointF foot = Lerp(P(0.62f, 0.90f), P(0.50f, 0.96f), s);
            PointF handL = Lerp(P(0.26f, 0.58f), P(0.30f, 0.10f), s);
            PointF handR = Lerp(P(0.58f, 0.58f), P(0.70f, 0.10f), s);
            float headR = box.Height * (0.075f + 0.008f * s);

            Color accent = Color.White;
            float pw = box.Height * 0.055f;

            // bob up a little as they stand
            float bob = -box.Height * 0.02f * s;
            Action<PointF, PointF> line = (a, b) =>
            {
                using (var pen = new Pen(accent, pw) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLine(pen, a.X, a.Y + bob, b.X, b.Y + bob);
            };

            // shadow on ground
            using (var sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            {
                float sw = box.Width * (0.34f - 0.08f * s);
                g.FillEllipse(sh, box.Left + box.Width / 2 - sw / 2, box.Top + box.Height * 0.965f, sw, box.Height * 0.03f);
            }

            // legs, torso, arms
            line(hip, knee); line(knee, foot);
            line(shoulder, hip);
            line(shoulder, handL); line(shoulder, handR);

            // head
            using (var hb = new SolidBrush(accent))
                g.FillEllipse(hb, head.X - headR, head.Y - headR + bob, headR * 2, headR * 2);

            // motion sparkles when standing
            if (s > 0.55f)
            {
                int a = (int)(255 * (s - 0.55f) / 0.45f);
                using (var sp = new SolidBrush(Color.FromArgb(Math.Min(255, a), 255, 255, 255)))
                {
                    DrawSpark(g, sp, handL, box.Height * 0.05f);
                    DrawSpark(g, sp, handR, box.Height * 0.05f);
                }
            }
        }

        static void DrawSpark(Graphics g, Brush b, PointF c, float r)
        {
            g.FillEllipse(b, c.X - r * 0.15f, c.Y - r, r * 0.3f, r * 2);
            g.FillEllipse(b, c.X - r, c.Y - r * 0.15f, r * 2, r * 0.3f);
        }

        // Draws an outlined "ink" polyline (dark outline + light fill) matching
        // the clean line-art look, then the caller layers head on top.
        static void DrawInk(Graphics g, PointF[] pts, float w, Color ink, Color fill)
        {
            using (var op = new Pen(ink, w * 1.55f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(op, pts);
            using (var ip = new Pen(fill, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(ip, pts);
        }

        static Color Darken(Color c, float f)
        {
            return Color.FromArgb((int)(c.R * f), (int)(c.G * f), (int)(c.B * f));
        }

        // A looping walk cycle, styled like the reference pedestrian icon.
        void DrawWalker(Graphics g, RectangleF box, float ph)
        {
            float t = ph * 2f * (float)Math.PI;
            Func<float, float, PointF> P = (nx, ny) =>
                new PointF(box.Left + nx * box.Width, box.Top + ny * box.Height);

            float bob = -box.Height * 0.02f * (float)Math.Abs(Math.Sin(t));
            PointF hip = P(0.50f, 0.60f); hip.Y += bob;
            PointF shoulder = P(0.545f, 0.34f); shoulder.Y += bob;   // slight forward lean
            PointF head = P(0.575f, 0.21f); head.Y += bob;
            float headR = box.Height * 0.085f;

            float thigh = box.Height * 0.20f, shin = box.Height * 0.21f;
            float upper = box.Height * 0.15f, fore = box.Height * 0.15f;

            Color ink = Darken(palette[0], 0.45f);   // palette-tinted dark outline
            Color fill = Color.White;
            float w = box.Height * 0.055f;

            // A two-bone limb via forward kinematics (angles from vertical, y-down).
            Func<PointF, float, float, float, float, PointF[]> limb =
                (root, l1, l2, a1, bend) =>
                {
                    PointF j = new PointF(root.X + l1 * (float)Math.Sin(a1), root.Y + l1 * (float)Math.Cos(a1));
                    float a2 = a1 - bend;
                    PointF tip = new PointF(j.X + l2 * (float)Math.Sin(a2), j.Y + l2 * (float)Math.Cos(a2));
                    return new[] { root, j, tip };
                };

            // Legs (offset by PI). Knee flexes during the forward swing.
            float legA_back = 0.55f * (float)Math.Sin(t + (float)Math.PI);
            float legA_front = 0.55f * (float)Math.Sin(t);
            float bendBack = 0.75f * Math.Max(0f, (float)Math.Sin(t + (float)Math.PI));
            float bendFront = 0.75f * Math.Max(0f, (float)Math.Sin(t));

            // Arms (opposite to legs), elbows flex slightly.
            float armA_back = -0.5f * (float)Math.Sin(t);
            float armA_front = -0.5f * (float)Math.Sin(t + (float)Math.PI);
            float ebBack = 0.5f * Math.Max(0f, -(float)Math.Sin(t));
            float ebFront = 0.5f * Math.Max(0f, -(float)Math.Sin(t + (float)Math.PI));

            // Ground shadow
            using (var sh = new SolidBrush(Color.FromArgb(55, 0, 0, 0)))
                g.FillEllipse(sh, box.Left + box.Width * 0.30f, box.Top + box.Height * 0.955f, box.Width * 0.40f, box.Height * 0.03f);

            // Draw far limbs first, then torso, then near limbs, then head.
            DrawInk(g, limb(shoulder, upper, fore, armA_back, ebBack), w, ink, fill);  // back arm
            DrawInk(g, limb(hip, thigh, shin, legA_back, bendBack), w, ink, fill);     // back leg
            DrawInk(g, new[] { shoulder, hip }, w, ink, fill);                          // torso
            DrawInk(g, limb(hip, thigh, shin, legA_front, bendFront), w, ink, fill);   // front leg
            DrawInk(g, limb(shoulder, upper, fore, armA_front, ebFront), w, ink, fill);// front arm

            // Head (outlined circle to match line-art)
            using (var op = new Pen(ink, w * 1.55f))
                g.DrawEllipse(op, head.X - headR, head.Y - headR, headR * 2, headR * 2);
            using (var fb = new SolidBrush(fill))
                g.FillEllipse(fb, head.X - headR + w * 0.2f, head.Y - headR + w * 0.2f, headR * 2 - w * 0.4f, headR * 2 - w * 0.4f);
        }
    }

    // ------------------------------------------------------------------
    // Settings dialog (modern dark styling)
    // ------------------------------------------------------------------
    class SettingsForm : Form
    {
        public Settings Result { get; private set; }
        readonly NumericUpDown numInt;
        readonly ComboBox cmbMode;
        readonly ComboBox cmbTheme;
        readonly NumericUpDown numFlash;
        readonly CheckBox chkSound;

        static readonly Color Bg = Color.FromArgb(24, 24, 37);
        static readonly Color Card = Color.FromArgb(36, 36, 56);
        static readonly Color Accent = Color.FromArgb(139, 92, 246);
        static readonly Color Fg = Color.FromArgb(230, 230, 240);

        public SettingsForm(Settings current)
        {
            Result = new Settings
            {
                IntervalMinutes = current.IntervalMinutes,
                Mode = current.Mode,
                FlashSeconds = current.FlashSeconds,
                PlaySound = current.PlaySound,
                PaletteIndex = current.PaletteIndex
            };

            Text = "Stand Up Reminder - Settings";
            Size = new Size(420, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Segoe UI", 10f);

            var header = new Label
            {
                Text = "Stand Up Reminder",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(24, 18)
            };
            Controls.Add(header);
            var tagline = new Label
            {
                Text = "move more. feel better. big W energy.",
                ForeColor = Color.FromArgb(150, 150, 170),
                AutoSize = true,
                Location = new Point(26, 50)
            };
            Controls.Add(tagline);

            AddLabel("Remind me every (minutes)", 90);
            numInt = MakeNum(1, 480, current.IntervalMinutes, 90);
            Controls.Add(numInt);

            AddLabel("Reminder style", 130);
            cmbMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(250, 128),
                Width = 140,
                FlatStyle = FlatStyle.Flat,
                BackColor = Card,
                ForeColor = Fg
            };
            cmbMode.Items.AddRange(new object[] { "Notification", "Flash", "Both" });
            cmbMode.SelectedItem = current.Mode;
            Controls.Add(cmbMode);

            AddLabel("Color theme", 170);
            cmbTheme = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(250, 168),
                Width = 140,
                FlatStyle = FlatStyle.Flat,
                BackColor = Card,
                ForeColor = Fg
            };
            cmbTheme.Items.Add("Random each time");
            cmbTheme.Items.AddRange(Palettes.Names);
            cmbTheme.SelectedIndex = (current.PaletteIndex < 0 || current.PaletteIndex >= Palettes.Names.Length)
                ? 0 : current.PaletteIndex + 1;
            Controls.Add(cmbTheme);

            AddLabel("Flash duration (seconds)", 210);
            numFlash = MakeNum(3, 120, current.FlashSeconds, 210);
            Controls.Add(numFlash);

            chkSound = new CheckBox
            {
                Text = "Play a sound",
                Checked = current.PlaySound,
                ForeColor = Fg,
                AutoSize = true,
                Location = new Point(26, 250)
            };
            Controls.Add(chkSound);

            var save = MakeButton("Save", 210, 300, Accent, Color.White);
            save.Click += (s, e) =>
            {
                Result.IntervalMinutes = (int)numInt.Value;
                Result.Mode = (string)cmbMode.SelectedItem;
                Result.FlashSeconds = (int)numFlash.Value;
                Result.PlaySound = chkSound.Checked;
                Result.PaletteIndex = cmbTheme.SelectedIndex <= 0 ? -1 : cmbTheme.SelectedIndex - 1;
                DialogResult = DialogResult.OK;
            };
            Controls.Add(save);

            var cancel = MakeButton("Cancel", 306, 300, Card, Fg);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            Controls.Add(cancel);

            AcceptButton = save;
            CancelButton = cancel;
        }

        void AddLabel(string text, int y)
        {
            Controls.Add(new Label { Text = text, ForeColor = Fg, AutoSize = true, Location = new Point(26, y + 2) });
        }

        NumericUpDown MakeNum(int min, int max, int val, int y)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Min(max, Math.Max(min, val)),
                Location = new Point(250, y),
                Width = 140,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Card,
                ForeColor = Fg
            };
        }

        Button MakeButton(string text, int x, int y, Color back, Color fore)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(84, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }
}
