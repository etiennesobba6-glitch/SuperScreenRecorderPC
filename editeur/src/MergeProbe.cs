using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Editeur
{
    partial class FenetrePrincipale
    {
        string Err(string path)
        {
            Process p = FF.Start("-hide_banner -i " + FF.Q(path));
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return err;
        }

        static double Dure(string err)
        {
            Match m = Regex.Match(err, @"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)");
            if (!m.Success) return -1;
            return int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60
                + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        }

        Clip ProbeClip(string f, bool img, double d)
        {
            string err = Err(f);
            Match v = Regex.Match(err, @"Video:.*?, (\d{2,5})x(\d{2,5})");
            if (!v.Success) return null;
            Clip c = new Clip();
            c.Fichier = f; c.Image = img;
            c.W = int.Parse(v.Groups[1].Value); c.H = int.Parse(v.Groups[2].Value);
            Match r = Regex.Match(err, @"(?:rotate\s*:|rotation of)\s*(-?\d+)");
            if (r.Success && Math.Abs(int.Parse(r.Groups[1].Value)) % 180 == 90)
            { int t = c.W; c.W = c.H; c.H = t; }
            if (img) c.Dur = d;
            else
            {
                c.Dur = Dure(err);
                if (c.Dur <= 0) return null;
                c.Audio = err.IndexOf(" Audio:") >= 0;
            }
            return c;
        }

        void AddFiles(string filter, bool img)
        {
            if (!File.Exists(FF.Exe)) { MessageBox.Show("ffmpeg.exe introuvable à côté du programme."); return; }
            OpenFileDialog d = new OpenFileDialog();
            d.Multiselect = true; d.Filter = filter;
            if (d.ShowDialog() != DialogResult.OK) return;
            StringBuilder bad = new StringBuilder();
            Cursor = Cursors.WaitCursor;
            try
            {
                foreach (string f in d.FileNames)
                {
                    Clip c = ProbeClip(f, img, (double)numImg.Value);
                    if (c == null) bad.Append(Path.GetFileName(f) + "\n"); else clips.Add(c);
                }
            }
            finally { Cursor = Cursors.Default; }
            if (bad.Length > 0) MessageBox.Show("Fichiers illisibles, ignorés :\n" + bad);
            RefillClips(clips.Count - 1);
        }

        void AddVideos(object s, EventArgs e)
        {
            AddFiles("Vidéos|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.flv;*.m4v;*.mpg;*.ts|Tous les fichiers|*.*", false);
        }

        void AddImages(object s, EventArgs e)
        {
            AddFiles("Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp|Tous les fichiers|*.*", true);
        }

        void AddSnd(object s, EventArgs e)
        {
            if (!File.Exists(FF.Exe)) { MessageBox.Show("ffmpeg.exe introuvable à côté du programme."); return; }
            OpenFileDialog d = new OpenFileDialog();
            d.Multiselect = true;
            d.Filter = "Sons|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac;*.wma;*.opus|Tous les fichiers (le son d'une vidéo marche aussi)|*.*";
            if (d.ShowDialog() != DialogResult.OK) return;
            StringBuilder bad = new StringBuilder();
            Cursor = Cursors.WaitCursor;
            try
            {
                foreach (string f in d.FileNames)
                {
                    string err = Err(f);
                    double du = Dure(err);
                    if (err.IndexOf(" Audio:") < 0 || du <= 0) { bad.Append(Path.GetFileName(f) + "\n"); continue; }
                    Snd sn = new Snd(); sn.Fichier = f; sn.Dur = du;
                    snds.Add(sn);
                }
            }
            finally { Cursor = Cursors.Default; }
            if (bad.Length > 0) MessageBox.Show("Fichiers sans son, ignorés :\n" + bad);
            RefillSnds(snds.Count - 1);
        }

        void DelSnd(object s, EventArgs e)
        {
            Snd sn = SelSnd(); if (sn == null) return;
            snds.Remove(sn); RefillSnds(-1);
        }
    }
}
