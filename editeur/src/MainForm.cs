using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Editeur
{
    class FenetrePrincipale : Form
    {
        string file; double dur, curT; int vw, vh;
        Image baseImg;
        List<Item> items = new List<Item>();
        bool busy;
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
        ListView lv = new ListView();

        static void Put(Control p, Control c, int x, int y, int w, int h)
        { c.SetBounds(x, y, w, h); p.Controls.Add(c); }
        static Label Lb(Control p, string t, int x, int y)
        {
            Label l = new Label(); l.Text = t; l.AutoSize = true; l.Location = new Point(x, y + 4);
            p.Controls.Add(l); return l;
        }
        static Button Btn(Control p, string t, int x, int y, int w, int h, EventHandler ev)
        {
            Button b = new Button(); b.Text = t; b.Click += ev;
            Put(p, b, x, y, w, h); return b;
        }

        public FenetrePrincipale()
        {
            Text = "Éditeur vidéo";
            ClientSize = new Size(900, 720);
            MinimumSize = new Size(700, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9);

            Panel top = new Panel(); top.Dock = DockStyle.Top; top.Height = 44;
            Btn(top, "Ouvrir une vidéo…", 10, 8, 160, 28, Ouvrir);
            lblInfo.AutoEllipsis = true;
            lblInfo.SetBounds(180, 14, 700, 20);
            lblInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            top.Controls.Add(lblInfo);

            pic.BackColor = Color.Black; pic.SizeMode = PictureBoxSizeMode.Zoom; pic.Dock = DockStyle.Fill;

            Panel seek = new Panel(); seek.Dock = DockStyle.Bottom; seek.Height = 70;
            trk.Minimum = 0; trk.Maximum = 1000; trk.TickFrequency = 50;
            trk.Dock = DockStyle.Top; trk.Height = 45;
            trk.Scroll += delegate { curT = trk.Value / 1000.0 * dur; lblPos.Text = "Position : " + FF.Fmt(curT); };
            trk.MouseUp += delegate { if (file != null) ShowFrame(curT); };
            trk.KeyUp += delegate { if (file != null) ShowFrame(curT); };
            lblPos.AutoSize = true; lblPos.Text = "Position : 00:00:00.0"; lblPos.Location = new Point(10, 48);
            seek.Controls.Add(lblPos);
            seek.Controls.Add(trk);

            TabControl tabs = new TabControl(); tabs.Dock = DockStyle.Bottom; tabs.Height = 240;
            TabPage t1 = new TabPage("Coupe et compression");
            TabPage t2 = new TabPage("Titres et sous-titres");
            tabs.TabPages.Add(t1); tabs.TabPages.Add(t2);

            Lb(t1, "Début", 10, 10);
            Put(t1, txtStart, 60, 10, 90, 24);
            Btn(t1, "Début = position", 160, 8, 120, 28, delegate { if (file != null) txtStart.Text = FF.Fmt(curT); });
            Lb(t1, "Fin", 300, 10);
            Put(t1, txtEnd, 330, 10, 90, 24);
            Btn(t1, "Fin = position", 430, 8, 120, 28, delegate { if (file != null) txtEnd.Text = FF.Fmt(curT); });

            Lb(t1, "Résolution", 10, 46);
            cmbRes.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbRes.Items.AddRange(new object[] { "Originale", "1080p", "720p", "480p", "360p" });
            cmbRes.SelectedIndex = 0;
            Put(t1, cmbRes, 90, 46, 150, 24);

            rbQual.Text = "Qualité"; rbQual.Checked = true;
            Put(t1, rbQual, 10, 80, 80, 24);
            cmbQual.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbQual.Items.AddRange(new object[] { "Haute (gros fichier)", "Moyenne (conseillée)", "Basse (petit fichier)" });
            cmbQual.SelectedIndex = 1;
            Put(t1, cmbQual, 95, 80, 180, 24);
            rbSize.Text = "Poids visé (Mo)";
            Put(t1, rbSize, 300, 80, 130, 24);
            numSize.Minimum = 1; numSize.Maximum = 20000; numSize.Value = 50;
            Put(t1, numSize, 435, 80, 80, 24);

            btnGo = Btn(t1, "Démarrer", 10, 118, 160, 30, Go);
            bar.SetBounds(180, 122, 690, 22); bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            t1.Controls.Add(bar);
            lblStatus.AutoEllipsis = true;
            lblStatus.SetBounds(10, 156, 860, 20); lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            t1.Controls.Add(lblStatus);

            lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false;
            lv.Columns.Add("Type", 90); lv.Columns.Add("Début", 90); lv.Columns.Add("Fin", 90); lv.Columns.Add("Texte", 420);
            lv.Dock = DockStyle.Fill;
            lv.SelectedIndexChanged += delegate
            {
                if (busy || file == null) return;
                int i = Sel(); if (i >= 0) SeekTo(items[i].Start);
            };
            lv.DoubleClick += Modifier;
            Panel rp = new Panel(); rp.Dock = DockStyle.Right; rp.Width = 150;
            Btn(rp, "Ajouter…", 10, 8, 130, 30, Ajouter);
            Btn(rp, "Modifier…", 10, 44, 130, 30, Modifier);
            Btn(rp, "Supprimer", 10, 80, 130, 30, Supprimer);
            Btn(rp, "Importer .srt…", 10, 116, 130, 30, Importer);
            Btn(rp, "Tout supprimer", 10, 152, 130, 30, delegate
            {
                if (items.Count > 0 && MessageBox.Show("Supprimer tous les textes ?", "Confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes)
                { items.Clear(); Refill(null); }
            });
            t2.Controls.Add(lv);
            t2.Controls.Add(rp);

            Controls.Add(pic);
            Controls.Add(seek);
            Controls.Add(tabs);
            Controls.Add(top);

            FormClosing += delegate
            {
                try { if (cur != null && !cur.HasExited) cur.Kill(); } catch { }
            };
        }

        int Sel() { return lv.SelectedIndices.Count > 0 ? lv.SelectedIndices[0] : -1; }

        void Refill(Item sel)
        {
            items.Sort(delegate(Item x, Item y) { return x.Start.CompareTo(y.Start); });
            busy = true;
            lv.Items.Clear();
            foreach (Item it in items)
            {
                ListViewItem li = new ListViewItem(it.Kind);
                li.SubItems.Add(FF.Fmt(it.Start));
                li.SubItems.Add(FF.Fmt(it.End));
                li.SubItems.Add(it.Summary);
                lv.Items.Add(li);
                if (it == sel) { li.Selected = true; li.EnsureVisible(); }
            }
            busy = false;
            if (sel != null && file != null) SeekTo(sel.Start); else Compose();
        }

        void SeekTo(double t)
        {
            if (t < 0) t = 0; if (t > dur) t = dur;
            if (dur > 0) trk.Value = (int)Math.Min(1000.0, Math.Max(0.0, t / dur * 1000));
            ShowFrame(t);
        }

        void ShowFrame(double t)
        {
            curT = t;
            try
            {
                Image img = FF.Frame(file, t);
                if (img != null) { if (baseImg != null) baseImg.Dispose(); baseImg = img; }
            }
            catch { }
            Compose();
        }

        void Compose()
        {
            lblPos.Text = "Position : " + FF.Fmt(curT);
            if (baseImg == null) return;
            int w = baseImg.Width, h = baseImg.Height;
            Bitmap bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(baseImg, 0, 0, w, h);
                foreach (Item it in items)
                    if (curT >= it.Start && curT <= it.End) it.Draw(g, w, h);
            }
            Image old = pic.Image; pic.Image = bmp;
            if (old != null) old.Dispose();
        }

        void Ouvrir(object s, EventArgs e)
        {
            if (!File.Exists(FF.Exe)) { MessageBox.Show("ffmpeg.exe introuvable à côté du programme."); return; }
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "Vidéos|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.flv;*.m4v;*.mpg;*.ts|Tous les fichiers|*.*";
            if (d.ShowDialog() != DialogResult.OK) return;
            double du; int w, h;
            if (!FF.Probe(d.FileName, out du, out w, out h)) { MessageBox.Show("Impossible de lire cette vidéo."); return; }
            file = d.FileName; dur = du; vw = w; vh = h;
            items.Clear();
            txtStart.Text = FF.Fmt(0); txtEnd.Text = FF.Fmt(dur);
            trk.Value = 0;
            ShowFrame(0);
            if (baseImg != null) { vw = baseImg.Width; vh = baseImg.Height; }
            lblInfo.Text = Path.GetFileName(file) + "  -  " + vw + "x" + vh + "  -  " + FF.Fmt(dur);
            Refill(null);
        }

        void Ajouter(object s, EventArgs e)
        {
            if (file == null) { MessageBox.Show("Ouvre d'abord une vidéo."); return; }
            ItemDialog d = new ItemDialog(null, curT, dur);
            if (d.ShowDialog(this) != DialogResult.OK) return;
            items.Add(d.Result); Refill(d.Result);
        }

        void Modifier(object s, EventArgs e)
        {
            int i = Sel(); if (i < 0 || file == null) return;
            TextItem t = items[i] as TextItem; if (t == null) return;
            ItemDialog d = new ItemDialog(t, curT, dur);
            if (d.ShowDialog(this) != DialogResult.OK) return;
            items[i] = d.Result; Refill(d.Result);
        }

        void Supprimer(object s, EventArgs e)
        {
            int i = Sel(); if (i < 0) return;
            items.RemoveAt(i); Refill(null);
        }

        void Importer(object s, EventArgs e)
        {
            if (file == null) { MessageBox.Show("Ouvre d'abord une vidéo."); return; }
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "Sous-titres (*.srt)|*.srt|Tous les fichiers|*.*";
            if (d.ShowDialog() != DialogResult.OK) return;
            string txt = File.ReadAllText(d.FileName, new UTF8Encoding(false));
            if (txt.IndexOf('\uFFFD') >= 0) txt = File.ReadAllText(d.FileName, Encoding.Default);
            string[] lines = txt.Replace("\r", "").Split('\n');
            int i = 0, n = 0;
            while (i < lines.Length)
            {
                int k = lines[i].IndexOf("-->");
                if (k < 0) { i++; continue; }
                double s0 = 0, e0 = 0;
                string right = lines[i].Substring(k + 3).Trim().Split(' ')[0];
                bool ok = FF.ParseTime(lines[i].Substring(0, k), out s0) && FF.ParseTime(right, out e0);
                i++;
                StringBuilder sb = new StringBuilder();
                while (i < lines.Length && lines[i].Trim().Length > 0)
                {
                    if (sb.Length > 0) sb.Append("\r\n");
                    sb.Append(Regex.Replace(lines[i], "<[^>]+>", ""));
                    i++;
                }
                if (ok && sb.Length > 0 && e0 > s0)
                {
                    TextItem t = new TextItem();
                    t.Type = 1; t.PosV = 2; t.Size = 1;
                    t.Text = sb.ToString(); t.Start = s0; t.End = e0;
                    items.Add(t); n++;
                }
            }
            Refill(null);
            MessageBox.Show(n + " sous-titres importés.");
        }

        // Une seule image-séquence transparente contenant tous les textes, minutée.
        string BuildOverlay(double a, double b, string dir)
        {
            double len = b - a;
            List<double> cuts = new List<double>();
            cuts.Add(0); cuts.Add(len);
            bool any = false;
            foreach (Item it in items)
            {
                if (it.End <= a || it.Start >= b) continue;
                any = true;
                cuts.Add(Math.Max(0, it.Start - a));
                cuts.Add(Math.Min(len, it.End - a));
            }
            if (!any) return null;
            cuts.Sort();
            RenderPng(new List<Item>(), Path.Combine(dir, "e.png"));
            StringBuilder sb = new StringBuilder("ffconcat version 1.0\n");
            string lastName = null;
            for (int i = 0; i < cuts.Count - 1; i++)
            {
                double s = cuts[i], e = cuts[i + 1];
                if (e - s < 0.002) continue;
                double mid = a + (s + e) / 2;
                List<Item> on = new List<Item>();
                foreach (Item it in items) if (mid >= it.Start && mid < it.End) on.Add(it);
                string name = "e.png";
                if (on.Count > 0) { name = "p" + i + ".png"; RenderPng(on, Path.Combine(dir, name)); }
                sb.Append("file '" + name + "'\nduration " + FF.Sec(e - s) + "\n");
                lastName = name;
            }
            if (lastName == null) return null;
            sb.Append("file '" + lastName + "'\n");
            string list = Path.Combine(dir, "list.txt");
            File.WriteAllText(list, sb.ToString());
            return list;
        }

        void RenderPng(List<Item> on, string path)
        {
            using (Bitmap b = new Bitmap(vw, vh, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.Clear(Color.Transparent);
                    foreach (Item it in on) it.Draw(g, vw, vh);
                }
                b.Save(path, ImageFormat.Png);
            }
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

            string tmpDir = Path.Combine(Path.GetTempPath(), "edi_" + Guid.NewGuid().ToString("N"));
            string list = null;
            try
            {
                Directory.CreateDirectory(tmpDir);
                list = BuildOverlay(a, b, tmpDir);
            }
            catch (Exception ex) { MessageBox.Show("Erreur : " + ex.Message); return; }

            int[] hs = { 0, 1080, 720, 480, 360 };
            int H = hs[cmbRes.SelectedIndex];
            string sc = (H > 0 && (vh == 0 || H < vh)) ? "scale=-2:" + H : "scale=trunc(iw/2)*2:trunc(ih/2)*2";

            string args = "-hide_banner -n -ss " + FF.Sec(a) + " -i " + FF.Q(file);
            string fc;
            if (list != null)
            {
                args += " -f concat -i " + FF.Q(list);
                fc = "[0:v][1:v]overlay=0:0[v1];[v1]" + sc + "[vout]";
            }
            else fc = "[0:v]" + sc + "[vout]";
            args += " -t " + FF.Sec(len) + " -filter_complex " + FF.Q(fc) + " -map " + FF.Q("[vout]") + " -map 0:a?";
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
            Thread th = new Thread(delegate() { Run(args, o, len, tmpDir); });
            th.IsBackground = true;
            th.Start();
        }

        void Run(string args, string o, double total, string tmpDir)
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
            try { Directory.Delete(tmpDir, true); } catch { }
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
