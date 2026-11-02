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
    partial class FenetrePrincipale
    {
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
