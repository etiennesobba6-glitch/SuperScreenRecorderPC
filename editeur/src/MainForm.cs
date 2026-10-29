using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Editeur
{
    class FenetrePrincipale : Form
    {
        string file; double dur; int vh;
        Process cur;
        PictureBox pic = new PictureBox();
        TrackBar trk = new TrackBar();
        Label lblPos = new Label(), lblInfo = new Label(), lblStatus = new Label();
        TextBox txtStart = new TextBox(), txtEnd = new TextBox();
        ComboBox cmbRes = new ComboBox(), cmbQual = new ComboBox();
        RadioButton rbQual = new RadioButton(), rbSize = new RadioButton();
        NumericUpDown numSize = new NumericUpDown();
        Button btnGo;
        ProgressBar bar = new ProgressBar();

        const AnchorStyles BL = AnchorStyles.Bottom | AnchorStyles.Left;

        void Add(Control c, int x, int y, int w, int h, AnchorStyles a)
        {
            c.SetBounds(x, y, w, h); c.Anchor = a; Controls.Add(c);
        }
        Label L(string t, int x, int y)
        {
            Label l = new Label(); l.Text = t; l.AutoSize = true;
            Add(l, x, y + 4, 60, 20, BL); return l;
        }
        Button B(string t, int x, int y, int w, int h, EventHandler ev)
        {
            Button b = new Button(); b.Text = t; b.Click += ev;
            Add(b, x, y, w, h, BL); return b;
        }

        public FenetrePrincipale()
        {
            Text = "Éditeur vidéo";
            ClientSize = new Size(880, 680);
            MinimumSize = new Size(900, 720);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9);

            Button open = new Button(); open.Text = "Ouvrir une vidéo…"; open.Click += Ouvrir;
            Add(open, 10, 10, 160, 28, AnchorStyles.Top | AnchorStyles.Left);
            lblInfo.AutoEllipsis = true;
            Add(lblInfo, 180, 16, 690, 20, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);

            pic.BackColor = Color.Black; pic.SizeMode = PictureBoxSizeMode.Zoom;
            Add(pic, 10, 45, 860, 380, AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right);

            trk.Minimum = 0; trk.Maximum = 1000; trk.TickFrequency = 50;
            Add(trk, 10, 428, 860, 45, BL | AnchorStyles.Right);
            trk.Scroll += delegate { lblPos.Text = "Position : " + FF.Fmt(Pos()); };
            trk.MouseUp += delegate { Preview(); };
            trk.KeyUp += delegate { Preview(); };
            lblPos.AutoSize = true; lblPos.Text = "Position : 00:00:00.0";
            Add(lblPos, 10, 476, 300, 20, BL);

            L("Début", 10, 504);
            Add(txtStart, 60, 504, 90, 24, BL);
            B("Début = position", 160, 502, 120, 28, delegate { if (file != null) txtStart.Text = FF.Fmt(Pos()); });
            L("Fin", 300, 504);
            Add(txtEnd, 330, 504, 90, 24, BL);
            B("Fin = position", 430, 502, 120, 28, delegate { if (file != null) txtEnd.Text = FF.Fmt(Pos()); });

            L("Résolution", 10, 540);
            cmbRes.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbRes.Items.AddRange(new object[] { "Originale", "1080p", "720p", "480p", "360p" });
            cmbRes.SelectedIndex = 0;
            Add(cmbRes, 90, 540, 150, 24, BL);

            rbQual.Text = "Qualité"; rbQual.Checked = true;
            Add(rbQual, 10, 572, 80, 24, BL);
            cmbQual.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbQual.Items.AddRange(new object[] { "Haute (gros fichier)", "Moyenne (conseillée)", "Basse (petit fichier)" });
            cmbQual.SelectedIndex = 1;
            Add(cmbQual, 95, 572, 180, 24, BL);
            rbSize.Text = "Poids visé (Mo)";
            Add(rbSize, 300, 572, 130, 24, BL);
            numSize.Minimum = 1; numSize.Maximum = 20000; numSize.Value = 50;
            Add(numSize, 435, 572, 80, 24, BL);

            btnGo = B("Démarrer", 10, 610, 160, 30, Go);
            Add(bar, 180, 614, 690, 22, BL | AnchorStyles.Right);
            lblStatus.AutoEllipsis = true;
            Add(lblStatus, 10, 648, 860, 20, BL | AnchorStyles.Right);

            FormClosing += delegate
            {
                try { if (cur != null && !cur.HasExited) cur.Kill(); } catch { }
            };
        }

        double Pos() { return trk.Value / 1000.0 * dur; }

        void Preview() { if (file != null) ShowFrame(Pos()); }

        void ShowFrame(double t)
        {
            try
            {
                Image img = FF.Frame(file, t);
                if (pic.Image != null) pic.Image.Dispose();
                pic.Image = img;
                lblPos.Text = "Position : " + FF.Fmt(t);
            }
            catch { }
        }

        void Ouvrir(object s, EventArgs e)
        {
            if (!File.Exists(FF.Exe)) { MessageBox.Show("ffmpeg.exe introuvable à côté du programme."); return; }
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "Vidéos|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.flv;*.m4v;*.mpg;*.ts|Tous les fichiers|*.*";
            if (d.ShowDialog() != DialogResult.OK) return;
            double du; int w, h;
            if (!FF.Probe(d.FileName, out du, out w, out h)) { MessageBox.Show("Impossible de lire cette vidéo."); return; }
            file = d.FileName; dur = du; vh = h;
            txtStart.Text = FF.Fmt(0); txtEnd.Text = FF.Fmt(dur);
            trk.Value = 0;
            lblInfo.Text = Path.GetFileName(file) + "  -  " + w + "x" + h + "  -  " + FF.Fmt(dur);
            ShowFrame(0);
        }

        void Go(object s, EventArgs e)
        {
            if (file == null) { MessageBox.Show("Ouvre d'abord une vidéo."); return; }
            double a, b;
            if (!FF.ParseTime(txtStart.Text, out a) || !FF.ParseTime(txtEnd.Text, out b))
            { MessageBox.Show("Temps invalide (exemples : 00:01:30 ou 90)."); return; }
            if (b > dur) b = dur;
            if (b - a < 0.1) { MessageBox.Show("La fin doit être après le début."); return; }
            double len = b - a;

            string dir = Path.GetDirectoryName(file);
            string baseName = Path.GetFileNameWithoutExtension(file) + "_edit";
            string o = Path.Combine(dir, baseName + ".mp4");
            int n = 2;
            while (File.Exists(o)) { o = Path.Combine(dir, baseName + n + ".mp4"); n++; }

            string args = "-hide_banner -n -ss " + FF.Sec(a) + " -i " + FF.Q(file) + " -t " + FF.Sec(len);
            int[] hs = { 0, 1080, 720, 480, 360 };
            int H = hs[cmbRes.SelectedIndex];
            if (H > 0 && (vh == 0 || H < vh)) args += " -vf scale=-2:" + H;
            else args += " -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\"";
            args += " -c:v libx264 -preset medium -pix_fmt yuv420p";
            if (rbQual.Checked)
            {
                int[] crf = { 20, 24, 28 };
                args += " -crf " + crf[cmbQual.SelectedIndex];
            }
            else
            {
                int kb = (int)(((double)numSize.Value * 8388.0 * 0.95) / len) - 128;
                if (kb < 80) { MessageBox.Show("Poids visé trop petit pour cette durée. Augmente-le."); return; }
                args += " -b:v " + kb + "k -maxrate " + (kb * 3 / 2) + "k -bufsize " + (kb * 2) + "k";
            }
            args += " -c:a aac -b:a 128k -movflags +faststart " + FF.Q(o);

            btnGo.Enabled = false; bar.Value = 0;
            lblStatus.Text = "Traitement en cours…";
            Thread th = new Thread(delegate() { Run(args, o, len); });
            th.IsBackground = true;
            th.Start();
        }

        void Run(string args, string o, double total)
        {
            string last = "";
            Process p = FF.Start(args);
            cur = p;
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                last = e.Data;
                Match m = Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+\.?\d*)");
                if (m.Success)
                {
                    double t = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60
                        + double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    int pct = (int)Math.Min(100.0, t / total * 100);
                    try { BeginInvoke(new MethodInvoker(delegate { bar.Value = pct; })); } catch { }
                }
            };
            p.BeginErrorReadLine();
            p.WaitForExit();
            bool ok = p.ExitCode == 0 && File.Exists(o);
            string msg = ok
                ? "Terminé : " + o + "  (" + (new FileInfo(o).Length / 1048576.0).ToString("0.0") + " Mo)"
                : "Échec : " + last;
            try
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    btnGo.Enabled = true;
                    bar.Value = ok ? 100 : 0;
                    lblStatus.Text = msg;
                    MessageBox.Show(msg);
                }));
            }
            catch { }
        }
    }
}
