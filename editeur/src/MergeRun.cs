using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Editeur
{
    partial class FenetrePrincipale
    {
        void Fusion(object sender, EventArgs ev)
        {
            if (clips.Count == 0) { MessageBox.Show("Ajoute au moins une vidéo ou une image."); return; }
            int W, H, ix = cmbOut.SelectedIndex;
            if (ix == 1) { W = 1920; H = 1080; }
            else if (ix == 2) { W = 1280; H = 720; }
            else if (ix == 3) { W = 854; H = 480; }
            else
            {
                Clip r = clips[0];
                foreach (Clip c0 in clips) if (!c0.Image) { r = c0; break; }
                W = r.W; H = r.H;
            }
            W -= W % 2; H -= H % 2;
            if (W < 16 || H < 16) { MessageBox.Show("Taille de sortie invalide."); return; }

            string dir = Path.GetDirectoryName(clips[0].Fichier);
            string o = Path.Combine(dir, "assemblage.mp4");
            int n = 2;
            while (File.Exists(o)) { o = Path.Combine(dir, "assemblage" + n + ".mp4"); n++; }

            double total = 0;
            StringBuilder a = new StringBuilder("-hide_banner -n");
            foreach (Clip c1 in clips)
            {
                if (c1.Image) a.Append(" -loop 1 -framerate 30 -t " + FF.Sec(c1.Dur));
                a.Append(" -i " + FF.Q(c1.Fichier));
                total += c1.Dur;
            }
            foreach (Snd s1 in snds) a.Append(" -i " + FF.Q(s1.Fichier));

            StringBuilder f = new StringBuilder();
            for (int i = 0; i < clips.Count; i++)
            {
                Clip c = clips[i];
                string d = FF.Sec(c.Dur);
                f.Append("[" + i + ":v]trim=duration=" + d + ",setpts=PTS-STARTPTS,scale=" + W + ":" + H
                    + ":force_original_aspect_ratio=decrease,pad=" + W + ":" + H
                    + ":(ow-iw)/2:(oh-ih)/2,setsar=1,fps=30,format=yuv420p[v" + i + "];");
                if (c.Audio && !c.Image)
                    f.Append("[" + i + ":a]aresample=44100,aformat=sample_fmts=fltp:channel_layouts=stereo,asetpts=PTS-STARTPTS,apad,atrim=duration="
                        + d + "[a" + i + "];");
                else
                    f.Append("anullsrc=r=44100:cl=stereo,aformat=sample_fmts=fltp,atrim=duration=" + d + "[a" + i + "];");
            }
            for (int i = 0; i < clips.Count; i++) f.Append("[v" + i + "][a" + i + "]");
            f.Append("concat=n=" + clips.Count + ":v=1:a=1[vc][ac];");

            double keep = chkKeep.Checked ? (double)numKeep.Value / 100.0 : 0.0;
            string kv = keep.ToString("0.###", CultureInfo.InvariantCulture);
            if (snds.Count == 0) f.Append("[ac]volume=" + kv + "[aout]");
            else
            {
                f.Append("[ac]volume=" + kv + "[am0];");
                for (int j = 0; j < snds.Count; j++)
                {
                    Snd sn = snds[j];
                    int ms = (int)(sn.Start * 1000);
                    string sv = (sn.Vol / 100.0).ToString("0.###", CultureInfo.InvariantCulture);
                    f.Append("[" + (clips.Count + j) + ":a]aresample=44100,aformat=sample_fmts=fltp:channel_layouts=stereo,volume="
                        + sv + ",adelay=" + ms + "|" + ms + ",apad[as" + j + "];");
                }
                f.Append("[am0]");
                for (int j = 0; j < snds.Count; j++) f.Append("[as" + j + "]");
                int k = snds.Count + 1;
                f.Append("amix=inputs=" + k + ":duration=first:dropout_transition=0,volume=" + k + "[aout]");
            }

            string fc = f.ToString();
            if (fc.Length > 28000) { MessageBox.Show("Trop d'éléments : assemble-les en deux fois."); return; }
            a.Append(" -filter_complex " + FF.Q(fc) + " -map " + FF.Q("[vc]") + " -map " + FF.Q("[aout]"));
            a.Append(" -t " + FF.Sec(total) + " -c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p");
            a.Append(" -c:a aac -b:a 160k -movflags +faststart " + FF.Q(o));

            btnMerge.Enabled = false; bar2.Value = 0;
            lblMerge.Text = "Assemblage en cours…";
            string args = a.ToString();
            double tt = total;
            Thread th = new Thread(delegate() { RunMerge(args, o, tt); });
            th.IsBackground = true;
            th.Start();
        }

        void RunMerge(string args, string o, double total)
        {
            string l1 = "", l2 = "";
            Process p = FF.Start(args);
            cur = p;
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                if (e.Data.Trim().Length > 0) { l1 = l2; l2 = e.Data; }
                Match m = Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+\.?\d*)");
                if (m.Success)
                {
                    double t = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60
                        + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                    int pct = (int)Math.Min(100.0, t / total * 100);
                    try { BeginInvoke(new MethodInvoker(delegate { bar2.Value = pct; })); } catch { }
                }
            };
            p.BeginErrorReadLine();
            p.WaitForExit();
            bool ok = p.ExitCode == 0 && File.Exists(o);
            if (!ok) { try { if (File.Exists(o)) File.Delete(o); } catch { } }
            string msg = ok
                ? "Terminé : " + o + "  (" + (new FileInfo(o).Length / 1048576.0).ToString("0.0") + " Mo)"
                : "Échec : " + l1 + " " + l2;
            try
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    btnMerge.Enabled = true;
                    bar2.Value = ok ? 100 : 0;
                    lblMerge.Text = msg;
                    MessageBox.Show(msg);
                }));
            }
            catch { }
        }
    }
}
