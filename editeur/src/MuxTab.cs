using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Editeur
{
    partial class FenetrePrincipale
    {
        TextBox mxVid = new TextBox(), mxAud = new TextBox();
        Label mxInfo = new Label(), mxMsg = new Label();
        RadioButton mxRepl = new RadioButton(), mxMix = new RadioButton();
        NumericUpDown mxVol = new NumericUpDown(), mxOff = new NumericUpDown();
        CheckBox mxLoop = new CheckBox();
        Button mxGo;
        ProgressBar mxBar = new ProgressBar();
        double mxDur;
        bool mxHasAud;
        string mxLast = "";

        void BuildMuxTab()
        {
            TabPage t5 = new TabPage("Vidéo + son");
            tabs.TabPages.Add(t5);

            Lb(t5, "Vidéo", 10, 10);
            mxVid.ReadOnly = true;
            Put(t5, mxVid, 70, 10, 500, 24);
            Btn(t5, "Choisir…", 580, 8, 100, 28, MxPickVideo);

            Lb(t5, "Son", 10, 44);
            mxAud.ReadOnly = true;
            Put(t5, mxAud, 70, 44, 500, 24);
            Btn(t5, "Choisir…", 580, 42, 100, 28, MxPickAudio);

            mxInfo.AutoEllipsis = true;
            mxInfo.Text = "Choisis une vidéo (avec ou sans son) et un fichier son.";
            Put(t5, mxInfo, 10, 80, 670, 20);

            mxRepl.Text = "Remplacer le son de la vidéo"; mxRepl.Checked = true;
            Put(t5, mxRepl, 10, 106, 260, 24);
            mxMix.Text = "Mélanger avec le son d'origine";
            Put(t5, mxMix, 280, 106, 260, 24);

            Lb(t5, "Volume du son %", 10, 140);
            mxVol.Minimum = 0; mxVol.Maximum = 300; mxVol.Value = 100;
            Put(t5, mxVol, 125, 140, 60, 24);
            Lb(t5, "Début du son (s)", 210, 140);
            mxOff.Minimum = 0; mxOff.Maximum = 36000; mxOff.DecimalPlaces = 1; mxOff.Increment = 0.5M;
            Put(t5, mxOff, 330, 140, 70, 24);

            mxLoop.Text = "Répéter le son s'il est plus court que la vidéo";
            Put(t5, mxLoop, 10, 172, 400, 24);

            mxGo = Btn(t5, "Créer la vidéo", 10, 206, 150, 30, MxGo);
            mxBar.SetBounds(170, 211, 400, 22);
            t5.Controls.Add(mxBar);
            mxMsg.AutoEllipsis = true;
            Put(t5, mxMsg, 10, 244, 670, 20);
        }

        void MxPickVideo(object s, EventArgs e)
        {
            if (!File.Exists(FF.Exe)) { MessageBox.Show("ffmpeg.exe introuvable à côté du programme."); return; }
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "Vidéos|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.flv;*.m4v;*.mpg;*.ts|Tous les fichiers|*.*";
            if (d.ShowDialog() != DialogResult.OK) return;
            string err = Err(d.FileName);
            double du = Dure(err);
            if (du <= 0 || err.IndexOf(" Video:") < 0) { MessageBox.Show("Impossible de lire cette vidéo."); return; }
            mxVid.Text = d.FileName;
            mxDur = du;
            mxHasAud = err.IndexOf(" Audio:") >= 0;
            mxInfo.Text = "Durée " + FF.Fmt(du) + "  -  " + (mxHasAud ? "cette vidéo a déjà un son." : "cette vidéo n'a pas de son.");
            mxMix.Enabled = mxHasAud;
            if (!mxHasAud) mxRepl.Checked = true;
        }

        void MxPickAudio(object s, EventArgs e)
        {
            if (!File.Exists(FF.Exe)) { MessageBox.Show("ffmpeg.exe introuvable à côté du programme."); return; }
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "Sons|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac;*.wma;*.opus|Tous les fichiers (le son d'une vidéo marche aussi)|*.*";
            if (d.ShowDialog() != DialogResult.OK) return;
            string err = Err(d.FileName);
            if (err.IndexOf(" Audio:") < 0 || Dure(err) <= 0) { MessageBox.Show("Ce fichier ne contient pas de son."); return; }
            mxAud.Text = d.FileName;
        }

        void MxGo(object s, EventArgs e)
        {
            if (mxVid.Text.Length == 0 || mxAud.Text.Length == 0) { MessageBox.Show("Choisis une vidéo et un son."); return; }
            double off = (double)mxOff.Value;
            if (off >= mxDur) { MessageBox.Show("Le début du son est après la fin de la vidéo."); return; }

            string dir = Path.GetDirectoryName(mxVid.Text);
            string bn = Path.GetFileNameWithoutExtension(mxVid.Text) + "_son";
            string o = Path.Combine(dir, bn + ".mp4");
            int n = 2;
            while (File.Exists(o)) { o = Path.Combine(dir, bn + n + ".mp4"); n++; }

            int ms = (int)(off * 1000);
            string vol = (((double)mxVol.Value) / 100.0).ToString("0.###", CultureInfo.InvariantCulture);
            string norm = "aresample=44100,aformat=sample_fmts=fltp:channel_layouts=stereo";
            string nw = "[1:a]" + norm + ",volume=" + vol + ",adelay=" + ms + "|" + ms + ",apad";
            string fc;
            if (mxMix.Checked && mxHasAud)
                fc = "[0:a]" + norm + ",apad[o];" + nw + "[n];[o][n]amix=inputs=2:duration=first:dropout_transition=0,volume=2[aout]";
            else
                fc = nw + "[aout]";

            string inp = "-hide_banner -n -i " + FF.Q(mxVid.Text) + (mxLoop.Checked ? " -stream_loop -1" : "") + " -i " + FF.Q(mxAud.Text);
            string tail = " -filter_complex " + FF.Q(fc) + " -map 0:v:0 -map " + FF.Q("[aout]") + " -t " + FF.Sec(mxDur)
                + " -c:a aac -b:a 160k -movflags +faststart " + FF.Q(o);
            string a1 = inp + " -c:v copy" + tail;
            string a2 = inp + " -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p" + tail;

            mxGo.Enabled = false; mxBar.Value = 0;
            mxMsg.Text = "Traitement en cours…";
            double total = mxDur;
            Thread th = new Thread(delegate() { RunMux(a1, a2, o, total); });
            th.IsBackground = true;
            th.Start();
        }

        bool MxExec(string args, string o, double total)
        {
            mxLast = "";
            Process p = FF.Start(args);
            cur = p;
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                if (e.Data.Trim().Length > 0) mxLast = e.Data;
                Match m = Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+\.?\d*)");
                if (m.Success)
                {
                    double t = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60
                        + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                    int pct = (int)Math.Min(100.0, t / total * 100);
                    try { BeginInvoke(new MethodInvoker(delegate { mxBar.Value = pct; })); } catch { }
                }
            };
            p.BeginErrorReadLine();
            p.WaitForExit();
            bool ok = p.ExitCode == 0 && File.Exists(o);
            if (!ok) { try { if (File.Exists(o)) File.Delete(o); } catch { } }
            return ok;
        }

        void RunMux(string a1, string a2, string o, double total)
        {
            bool ok = MxExec(a1, o, total);
            if (!ok && !IsDisposed)
            {
                try { BeginInvoke(new MethodInvoker(delegate { mxMsg.Text = "Nouvel essai avec recompression de la vidéo…"; mxBar.Value = 0; })); } catch { }
                ok = MxExec(a2, o, total);
            }
            string msg = ok
                ? "Terminé : " + o + "  (" + (new FileInfo(o).Length / 1048576.0).ToString("0.0") + " Mo)"
                : "Échec : " + mxLast;
            try
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    mxGo.Enabled = true;
                    mxBar.Value = ok ? 100 : 0;
                    mxMsg.Text = msg;
                    MessageBox.Show(msg);
                }));
            }
            catch { }
        }
    }
}
