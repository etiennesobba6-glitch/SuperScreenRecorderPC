using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SuperScreenRecorder
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }

    class RegionSelector : Form
    {
        Point start;
        Rectangle sel;
        bool dragging;
        public Rectangle Result;

        public RegionSelector()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Opacity = 0.4;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
        }

        static Rectangle Norm(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X);
            int y = Math.Min(a.Y, b.Y);
            return new Rectangle(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            start = e.Location;
            sel = new Rectangle(e.Location, Size.Empty);
            dragging = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging) return;
            sel = Norm(start, e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            sel = Norm(start, e.Location);
            if (sel.Width >= 32 && sel.Height >= 32)
            {
                Result = sel;
                DialogResult = DialogResult.OK;
            }
            else
            {
                sel = Rectangle.Empty;
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var f = new Font("Arial", 16, FontStyle.Bold))
            {
                e.Graphics.DrawString("Dessinez la zone avec la souris (min 32 x 32). Echap pour annuler.",
                    f, Brushes.White, 20, 20);
                if (sel.Width > 0 && sel.Height > 0)
                {
                    using (var p = new Pen(Color.Red, 3))
                        e.Graphics.DrawRectangle(p, sel);
                    e.Graphics.DrawString(sel.Width + " x " + sel.Height, f, Brushes.Yellow,
                        sel.X + 6, sel.Y + 6);
                }
            }
        }
    }

    class MainForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr h, int id);

        Label status, regionLabel;
        ComboBox zone, res, fps, weight, pos, dur;
        TextBox titleBox, subBox;
        CheckBox frameBox;
        Button btn, zoneBtn;
        Timer timer = new Timer();
        Process ff;
        string outFile;
        DateTime startTime;
        StringBuilder errors = new StringBuilder();
        Rectangle region = Rectangle.Empty;

        readonly string[] zoneLabels = { "Tout l'ecran", "Zone a dessiner" };
        readonly string[] resLabels = { "Ecran natif", "1080p", "720p", "480p" };
        readonly int[] resValues = { 0, 1080, 720, 480 };
        readonly int[] fpsValues = { 30, 24, 15 };
        readonly string[] weightLabels = {
            "Ultra leger", "Tres leger", "Leger", "Equilibre", "Haute qualite" };
        readonly string[] crf = { "36", "32", "28", "25", "22" };
        readonly string[] posLabels = { "Haut", "Centre", "Bas" };
        readonly string[] durLabels = { "5 premieres secondes", "10 premieres secondes", "Toute la video" };
        readonly int[] durValues = { 5, 10, 0 };

        public MainForm()
        {
            Text = "Super Screen Recorder";
            Width = 380;
            Height = 560;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            status = new Label { Left = 20, Top = 12, Width = 330, Height = 36,
                Text = "Pret. Raccourci : F9 pour demarrer / arreter." };
            Controls.Add(status);

            zone = AddCombo("Zone a enregistrer", 20, 52, 200, zoneLabels, 0);
            zoneBtn = new Button { Left = 230, Top = 74, Width = 120, Height = 24,
                Text = "Choisir la zone", Enabled = false };
            zoneBtn.Click += (s, e) => ChooseRegion();
            Controls.Add(zoneBtn);
            regionLabel = new Label { Left = 20, Top = 102, Width = 330, Height = 18, Text = "" };
            Controls.Add(regionLabel);

            res = AddCombo("Resolution", 20, 125, 160, resLabels, 2);
            fps = AddCombo("Images/seconde", 190, 125, 160, Array.ConvertAll(fpsValues, v => v + " fps"), 0);
            weight = AddCombo("Poids de la video", 20, 180, 330, weightLabels, 2);

            titleBox = AddText("Titre (facultatif)", 235);
            subBox = AddText("Sous-titre (facultatif)", 290);
            pos = AddCombo("Position du texte", 20, 345, 150, posLabels, 2);
            dur = AddCombo("Duree d'affichage", 190, 345, 160, durLabels, 0);

            frameBox = new CheckBox { Left = 20, Top = 402, Width = 330,
                Text = "Afficher un cadre autour de la zone", Enabled = false };
            Controls.Add(frameBox);

            btn = new Button { Left = 20, Top = 435, Width = 330, Height = 45,
                Text = "Demarrer (F9)" };
            btn.Click += (s, e) => Toggle();
            Controls.Add(btn);

            zone.SelectedIndexChanged += (s, e) =>
            {
                bool custom = zone.SelectedIndex == 1;
                zoneBtn.Enabled = custom;
                frameBox.Enabled = custom;
                if (custom && region.IsEmpty && !ChooseRegion()) zone.SelectedIndex = 0;
                UpdateRegionLabel();
            };

            timer.Interval = 500;
            timer.Tick += (s, e) =>
            {
                TimeSpan t = DateTime.Now - startTime;
                status.Text = "Enregistrement en cours  " +
                    string.Format("{0:00}:{1:00}", (int)t.TotalMinutes, t.Seconds) +
                    "\nF9 pour arreter";
            };

            Load += (s, e) => RegisterHotKey(Handle, 1, 0, (uint)Keys.F9);
            FormClosing += (s, e) =>
            {
                if (ff != null) Stop();
                UnregisterHotKey(Handle, 1);
            };
        }

        ComboBox AddCombo(string title, int left, int top, int width, string[] items, int selected)
        {
            Controls.Add(new Label { Left = left, Top = top, Width = width, Text = title });
            var c = new ComboBox { Left = left, Top = top + 22, Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items);
            c.SelectedIndex = selected;
            Controls.Add(c);
            return c;
        }

        TextBox AddText(string title, int top)
        {
            Controls.Add(new Label { Left = 20, Top = top, Width = 330, Text = title });
            var t = new TextBox { Left = 20, Top = top + 22, Width = 330 };
            Controls.Add(t);
            return t;
        }

        void UpdateRegionLabel()
        {
            if (zone.SelectedIndex == 1 && !region.IsEmpty)
                regionLabel.Text = "Zone : " + region.Width + " x " + region.Height +
                    "  (a partir de " + region.X + ", " + region.Y + ")";
            else
                regionLabel.Text = "";
        }

        bool ChooseRegion()
        {
            bool ok = false;
            Hide();
            using (var sel = new RegionSelector())
            {
                if (sel.ShowDialog() == DialogResult.OK)
                {
                    region = sel.Result;
                    ok = true;
                }
            }
            Show();
            Activate();
            UpdateRegionLabel();
            return ok;
        }

        Rectangle ClampRegion()
        {
            Rectangle r = Rectangle.Intersect(region, Screen.PrimaryScreen.Bounds);
            r.Width = r.Width / 2 * 2;
            r.Height = r.Height / 2 * 2;
            return r;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312) Toggle();
            base.WndProc(ref m);
        }

        void Toggle()
        {
            if (ff == null) Start(); else Stop();
        }

        static string Esc(string p)
        {
            return p.Replace("\\", "/").Replace(":", "\\:");
        }

        string TextPiece(string text, string fileName, string font, int size, string y, string enable)
        {
            string file = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(file, text, new UTF8Encoding(false));
            return ",drawtext=fontfile='" + Esc(font) + "':textfile='" + Esc(file) +
                "':expansion=none:fontcolor=white:fontsize=" + size +
                ":box=1:boxcolor=black@0.6:boxborderw=" + (size / 3) +
                ":x=(w-text_w)/2:y=" + y + enable;
        }

        string BuildText(int outH)
        {
            string t = titleBox.Text.Trim();
            string s = subBox.Text.Trim();
            if (t.Length == 0 && s.Length == 0) return "";

            string win = Environment.GetEnvironmentVariable("WINDIR");
            if (string.IsNullOrEmpty(win)) win = "C:\\Windows";
            string font = Path.Combine(win, "Fonts\\arial.ttf");

            int tSize = Math.Max(18, outH / 16);
            int sSize = Math.Max(14, outH / 26);
            int margin = Math.Max(10, outH / 18);
            int d = durValues[dur.SelectedIndex];
            string enable = d > 0 ? ":enable='lt(t," + d + ")'" : "";
            bool hasT = t.Length > 0, hasS = s.Length > 0;

            string yT, yS;
            switch (pos.SelectedIndex)
            {
                case 0:
                    yT = margin.ToString();
                    yS = (margin + (hasT ? tSize * 2 : 0)).ToString();
                    break;
                case 1:
                    yT = "(h-text_h)/2-" + (hasS ? sSize : 0);
                    yS = "(h-text_h)/2+" + (hasT ? tSize : 0);
                    break;
                default:
                    yS = "h-text_h-" + margin;
                    yT = "h-text_h-" + (margin + (hasS ? sSize * 2 + 10 : 0));
                    break;
            }

            string f = "";
            if (hasT) f += TextPiece(t, "ssr_title.txt", font, tSize, yT, enable);
            if (hasS) f += TextPiece(s, "ssr_sub.txt", font, sSize, yS, enable);
            return f;
        }

        void Start()
        {
            string ffPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (!File.Exists(ffPath))
            {
                MessageBox.Show("ffmpeg.exe introuvable a cote du programme.");
                return;
            }

            Rectangle reg = Rectangle.Empty;
            if (zone.SelectedIndex == 1)
            {
                if (region.IsEmpty && !ChooseRegion()) return;
                reg = ClampRegion();
                if (reg.Width < 16 || reg.Height < 16)
                {
                    MessageBox.Show("Zone invalide, choisis-la de nouveau.");
                    region = Rectangle.Empty;
                    UpdateRegionLabel();
                    return;
                }
            }

            string dir = Path.Combine(
                Environment.GetEnvironmentVariable("USERPROFILE") + "\\Videos",
                "SuperScreenRecorder");
            Directory.CreateDirectory(dir);
            outFile = Path.Combine(dir, "rec_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4");

            int r = resValues[res.SelectedIndex];
            int baseH = reg.IsEmpty ? Screen.PrimaryScreen.Bounds.Height : reg.Height;
            int outH = r == 0 ? baseH : Math.Min(r, baseH);
            string vf = r == 0
                ? "scale=trunc(iw/2)*2:trunc(ih/2)*2"
                : "scale=-2:min(" + r + "\\,ih)";
            vf += BuildText(outH);

            int f = fpsValues[fps.SelectedIndex];
            string input = "-f gdigrab -framerate " + f + " -draw_mouse 1";
            if (!reg.IsEmpty)
            {
                input += " -offset_x " + reg.X + " -offset_y " + reg.Y +
                    " -video_size " + reg.Width + "x" + reg.Height;
                if (frameBox.Checked) input += " -show_region 1";
            }
            string args = "-y -loglevel error -nostats " + input + " -i desktop -vf \"" + vf +
                "\" -c:v libx264 -preset veryfast -crf " +
                crf[weight.SelectedIndex] + " -pix_fmt yuv420p -movflags +faststart \"" + outFile + "\"";

            var psi = new ProcessStartInfo(ffPath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };
            errors.Length = 0;
            ff = Process.Start(psi);
            ff.ErrorDataReceived += (s, e) => { if (e.Data != null) errors.AppendLine(e.Data); };
            ff.BeginErrorReadLine();

            startTime = DateTime.Now;
            btn.Text = "Arreter (F9)";
            timer.Start();
            WindowState = FormWindowState.Minimized;
        }

        void Stop()
        {
            timer.Stop();
            try
            {
                ff.StandardInput.WriteLine("q");
                ff.StandardInput.Flush();
                if (!ff.WaitForExit(10000)) ff.Kill();
            }
            catch { }
            ff = null;
            btn.Text = "Demarrer (F9)";
            WindowState = FormWindowState.Normal;
            Activate();
            if (File.Exists(outFile))
            {
                long mo = new FileInfo(outFile).Length / (1024 * 1024);
                status.Text = "Termine (" + mo + " Mo)\n" + outFile;
            }
            else
            {
                status.Text = "Erreur : " + errors.ToString();
            }
        }
    }
}
