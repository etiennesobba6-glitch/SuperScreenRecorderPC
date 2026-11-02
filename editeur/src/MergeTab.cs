using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Editeur
{
    class Clip
    {
        public string Fichier;
        public bool Image, Audio;
        public double Dur;
        public int W, H;
    }

    class Snd
    {
        public string Fichier;
        public double Dur, Start;
        public int Vol = 100;
    }

    partial class FenetrePrincipale
    {
        List<Clip> clips = new List<Clip>();
        List<Snd> snds = new List<Snd>();
        ListView lvC = new ListView(), lvS = new ListView();
        ComboBox cmbOut = new ComboBox();
        CheckBox chkKeep = new CheckBox();
        NumericUpDown numKeep = new NumericUpDown(), numImg = new NumericUpDown();
        NumericUpDown numVol = new NumericUpDown(), numOff = new NumericUpDown();
        Button btnMerge;
        ProgressBar bar2 = new ProgressBar();
        Label lblMerge = new Label();
        bool mBusy;

        static void Cfg(ListView l)
        {
            l.View = View.Details; l.FullRowSelect = true; l.HideSelection = false; l.MultiSelect = false;
        }

        static Label Title(string t)
        {
            Label l = new Label(); l.Text = t; l.Dock = DockStyle.Top; l.Height = 20;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            return l;
        }

        void BuildMergeTab()
        {
            tabs.Height = 330;
            TabPage t4 = new TabPage("Assembler");
            tabs.TabPages.Add(t4);

            Panel pr = new Panel(); pr.Dock = DockStyle.Fill;
            Cfg(lvS);
            lvS.Columns.Add("Son", 200); lvS.Columns.Add("Volume", 60); lvS.Columns.Add("Début", 70);
            lvS.Dock = DockStyle.Fill;
            lvS.SelectedIndexChanged += delegate
            {
                Snd sn = SelSnd(); if (sn == null) return;
                mBusy = true;
                numVol.Value = Math.Min(300, Math.Max(0, sn.Vol));
                numOff.Value = (decimal)Math.Min(3600.0, Math.Max(0.0, sn.Start));
                mBusy = false;
            };
            Panel sb = new Panel(); sb.Dock = DockStyle.Right; sb.Width = 130;
            Btn(sb, "Ajouter un son…", 5, 4, 120, 26, AddSnd);
            Btn(sb, "Retirer", 5, 34, 120, 26, DelSnd);
            Panel sc = new Panel(); sc.Dock = DockStyle.Bottom; sc.Height = 34;
            Lb(sc, "Volume %", 0, 4);
            numVol.Minimum = 0; numVol.Maximum = 300; numVol.Value = 100;
            Put(sc, numVol, 70, 4, 60, 24);
            Lb(sc, "Début (s)", 145, 4);
            numOff.Minimum = 0; numOff.Maximum = 3600; numOff.DecimalPlaces = 1; numOff.Increment = 0.5M;
            Put(sc, numOff, 210, 4, 70, 24);
            numVol.ValueChanged += delegate { SndChanged(); };
            numOff.ValueChanged += delegate { SndChanged(); };
            pr.Controls.Add(lvS);
            pr.Controls.Add(sb);
            pr.Controls.Add(sc);
            pr.Controls.Add(Title("Sons ajoutés (musique, voix…)"));

            Panel pl = new Panel(); pl.Dock = DockStyle.Left; pl.Width = 470;
            Cfg(lvC);
            lvC.Columns.Add("Nom", 150); lvC.Columns.Add("Type", 50); lvC.Columns.Add("Durée", 70); lvC.Columns.Add("Taille", 70);
            lvC.Dock = DockStyle.Fill;
            Panel cb = new Panel(); cb.Dock = DockStyle.Right; cb.Width = 130;
            Btn(cb, "Ajouter vidéos…", 5, 4, 120, 25, AddVideos);
            Btn(cb, "Ajouter images…", 5, 32, 120, 25, AddImages);
            Btn(cb, "Monter", 5, 60, 120, 25, delegate { MoveClip(-1); });
            Btn(cb, "Descendre", 5, 88, 120, 25, delegate { MoveClip(1); });
            Btn(cb, "Retirer", 5, 116, 120, 25, delegate
            {
                int i = SelClip(); if (i < 0) return;
                clips.RemoveAt(i); RefillClips(Math.Min(i, clips.Count - 1));
            });
            Btn(cb, "Tout retirer", 5, 144, 120, 25, delegate { clips.Clear(); RefillClips(-1); });
            pl.Controls.Add(lvC);
            pl.Controls.Add(cb);
            pl.Controls.Add(Title("Séquence : vidéos et images, dans l'ordre"));

            Panel pb = new Panel(); pb.Dock = DockStyle.Bottom; pb.Height = 100;
            Lb(pb, "Taille de sortie", 10, 6);
            cmbOut.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbOut.Items.AddRange(new object[] { "Comme la 1re vidéo", "1080p (1920x1080)", "720p (1280x720)", "480p (854x480)" });
            cmbOut.SelectedIndex = 0;
            Put(pb, cmbOut, 110, 6, 170, 24);
            chkKeep.Text = "Garder le son des vidéos"; chkKeep.Checked = true;
            Put(pb, chkKeep, 300, 6, 180, 24);
            Lb(pb, "Volume %", 490, 6);
            numKeep.Minimum = 0; numKeep.Maximum = 300; numKeep.Value = 100;
            Put(pb, numKeep, 555, 6, 55, 24);
            Lb(pb, "Durée d'une image (s)", 10, 36);
            numImg.Minimum = 1; numImg.Maximum = 600; numImg.DecimalPlaces = 1; numImg.Increment = 0.5M; numImg.Value = 5;
            Put(pb, numImg, 150, 36, 60, 24);
            Btn(pb, "Appliquer à l'image choisie", 220, 34, 190, 28, ApplyImg);
            btnMerge = Btn(pb, "Assembler", 10, 68, 130, 28, Fusion);
            bar2.SetBounds(150, 71, 300, 22); pb.Controls.Add(bar2);
            lblMerge.AutoEllipsis = true;
            lblMerge.SetBounds(460, 73, 400, 20);
            lblMerge.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pb.Controls.Add(lblMerge);

            t4.Controls.Add(pr);
            t4.Controls.Add(pl);
            t4.Controls.Add(pb);
        }

        int SelClip() { return lvC.SelectedIndices.Count > 0 ? lvC.SelectedIndices[0] : -1; }

        Snd SelSnd()
        {
            int i = lvS.SelectedIndices.Count > 0 ? lvS.SelectedIndices[0] : -1;
            return (i >= 0 && i < snds.Count) ? snds[i] : null;
        }

        void SndChanged()
        {
            if (mBusy) return;
            Snd sn = SelSnd(); if (sn == null) return;
            sn.Vol = (int)numVol.Value;
            sn.Start = (double)numOff.Value;
            ListViewItem li = lvS.SelectedItems[0];
            li.SubItems[1].Text = sn.Vol + " %";
            li.SubItems[2].Text = FF.Fmt(sn.Start);
        }

        double Total()
        {
            double t = 0;
            foreach (Clip c in clips) t += c.Dur;
            return t;
        }

        void RefillClips(int sel)
        {
            lvC.Items.Clear();
            foreach (Clip c in clips)
            {
                ListViewItem li = new ListViewItem(Path.GetFileName(c.Fichier));
                li.SubItems.Add(c.Image ? "Image" : "Vidéo");
                li.SubItems.Add(FF.Fmt(c.Dur));
                li.SubItems.Add(c.W + "x" + c.H);
                lvC.Items.Add(li);
            }
            if (sel >= 0 && sel < clips.Count) { lvC.Items[sel].Selected = true; lvC.Items[sel].EnsureVisible(); }
            lblMerge.Text = clips.Count == 0 ? "" : "Durée totale : " + FF.Fmt(Total());
        }

        void RefillSnds(int sel)
        {
            mBusy = true;
            lvS.Items.Clear();
            foreach (Snd sn in snds)
            {
                ListViewItem li = new ListViewItem(Path.GetFileName(sn.Fichier));
                li.SubItems.Add(sn.Vol + " %");
                li.SubItems.Add(FF.Fmt(sn.Start));
                lvS.Items.Add(li);
            }
            mBusy = false;
            if (sel >= 0 && sel < snds.Count) { lvS.Items[sel].Selected = true; lvS.Items[sel].EnsureVisible(); }
        }

        void MoveClip(int dir)
        {
            int i = SelClip(), j = i + dir;
            if (i < 0 || j < 0 || j >= clips.Count) return;
            Clip t = clips[i]; clips[i] = clips[j]; clips[j] = t;
            RefillClips(j);
        }

        void ApplyImg(object s, EventArgs e)
        {
            int i = SelClip();
            if (i < 0 || !clips[i].Image) { MessageBox.Show("Choisis d'abord une image dans la liste."); return; }
            clips[i].Dur = (double)numImg.Value;
            RefillClips(i);
        }
    }
}
