using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Editeur
{
    partial class FenetrePrincipale
    {
        Item SelText()
        {
            return lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as Item : null;
        }

        void Refill(Item sel) { Refill(sel, true); }

        void Refill(Item sel, bool seek)
        {
            items.Sort(delegate(Item x, Item y) { return x.Start.CompareTo(y.Start); });
            busy = true;
            lv.Items.Clear();
            lv2.Items.Clear();
            foreach (Item it in items)
            {
                ListViewItem li = new ListViewItem(it.Kind);
                li.SubItems.Add(FF.Fmt(it.Start));
                li.SubItems.Add(FF.Fmt(it.End));
                li.SubItems.Add(it.Summary);
                li.Tag = it;
                if (it is TextItem) lv.Items.Add(li); else lv2.Items.Add(li);
                if (it == sel) { li.Selected = true; li.EnsureVisible(); }
            }
            busy = false;
            if (sel != null && seek && file != null) SeekTo(sel.Start); else Compose();
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
                if (live != null) live.Draw(g, w, h);
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
            items.Clear(); undo.Clear(); live = null;
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
            if (file == null) return;
            TextItem t = SelText() as TextItem; if (t == null) return;
            ItemDialog d = new ItemDialog(t, curT, dur);
            if (d.ShowDialog(this) != DialogResult.OK) return;
            items.Remove(t);
            items.Add(d.Result); Refill(d.Result);
        }

        void Supprimer(object s, EventArgs e)
        {
            Item t = SelText(); if (t == null) return;
            items.Remove(t); Refill(null);
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
    }
}
