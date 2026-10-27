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

    class MainForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr h, int id);

        Label status;
        ComboBox res, fps, weight;
        Button btn;
        Timer timer = new Timer();
        Process ff;
        string outFile;
        DateTime startTime;
        StringBuilder errors = new StringBuilder();

        readonly string[] resLabels = { "Ecran natif", "1080p", "720p", "480p" };
        readonly int[] resValues = { 0, 1080, 720, 480 };
        readonly int[] fpsValues = { 30, 24, 15 };
        readonly string[] weightLabels = {
            "Ultra leger", "Tres leger", "Leger", "Equilibre", "Haute qualite" };
        readonly string[] crf = { "36", "32", "28", "25", "22" };

        public MainForm()
        {
            Text = "Super Screen Recorder";
            Width = 380;
            Height = 380;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            status = new Label { Left = 20, Top = 15, Width = 330, Height = 40,
                Text = "Pret. Raccourci : F9 pour demarrer / arreter." };
            Controls.Add(status);

            res = AddCombo("Resolution", 65, resLabels, 2);
            fps = AddCombo("Images par seconde", 125, Array.ConvertAll(fpsValues, v => v + " fps"), 0);
            weight = AddCombo("Poids de la video", 185, weightLabels, 2);

            btn = new Button { Left = 20, Top = 255, Width = 330, Height = 45,
                Text = "Demarrer (F9)" };
            btn.Click += (s, e) => Toggle();
            Controls.Add(btn);

            timer.Interval = 500;
            timer.Tick += (s, e) =>
            {
                TimeSpan t = DateTime.Now - startTime;
                status.Text = "Enregistrement en cours  " + t.ToString(@"mm\:ss") + "\nF9 pour arreter";
            };

            Load += (s, e) => RegisterHotKey(Handle, 1, 0, (uint)Keys.F9);
            FormClosing += (s, e) =>
            {
                if (ff != null) Stop();
                UnregisterHotKey(Handle, 1);
            };
        }

        ComboBox AddCombo(string title, int top, string[] items, int selected)
        {
            Controls.Add(new Label { Left = 20, Top = top, Width = 330, Text = title });
            var c = new ComboBox { Left = 20, Top = top + 22, Width = 330,
                DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items);
            c.SelectedIndex = selected;
            Controls.Add(c);
            return c;
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

        void Start()
        {
            string ffPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (!File.Exists(ffPath))
            {
                MessageBox.Show("ffmpeg.exe introuvable a cote du programme.");
                return;
            }
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "SuperScreenRecorder");
            Directory.CreateDirectory(dir);
            outFile = Path.Combine(dir, "rec_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4");

            int r = resValues[res.SelectedIndex];
            string vf = r == 0
                ? "scale=trunc(iw/2)*2:trunc(ih/2)*2"
                : "scale=-2:min(" + r + "\\,ih)";
            int f = fpsValues[fps.SelectedIndex];
            string args = "-y -loglevel error -nostats -f gdigrab -framerate " + f +
                " -draw_mouse 1 -i desktop -vf \"" + vf + "\" -c:v libx264 -preset veryfast -crf " +
                crf[weight.SelectedIndex] + " -pix_fmt yuv420p -movflags +faststart \"" + outFile + "\"";

            var psi = new ProcessStartInfo(ffPath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };
            errors.Clear();
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
