using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Editeur
{
    partial class FenetrePrincipale : Form
    {
        string file; double dur, curT; int vw, vh;
        Image baseImg;
        List<Item> items = new List<Item>();
        bool busy;
        Process cur;
        PictureBox pic = new PictureBox();
        TrackBar trk = new TrackBar();
        TabControl tabs = new TabControl();
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

            tabs.Dock = DockStyle.Bottom; tabs.Height = 250;
            TabPage t1 = new TabPage("Coupe et compression");
            TabPage t2 = new TabPage("Titres et sous-titres");
            TabPage t3 = new TabPage("Dessin");
            tabs.TabPages.Add(t1); tabs.TabPages.Add(t2); tabs.TabPages.Add(t3);

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
                Item it = SelText(); if (it != null) SeekTo(it.Start);
            };
            lv.DoubleClick += Modifier;
            Panel rp = new Panel(); rp.Dock = DockStyle.Right; rp.Width = 150;
            Btn(rp, "Ajouter…", 10, 8, 130, 30, Ajouter);
            Btn(rp, "Modifier…", 10, 44, 130, 30, Modifier);
            Btn(rp, "Supprimer", 10, 80, 130, 30, Supprimer);
            Btn(rp, "Importer .srt…", 10, 116, 130, 30, Importer);
            Btn(rp, "Tout supprimer", 10, 152, 130, 30, delegate
            {
                bool any = false;
                foreach (Item z in items) if (z is TextItem) any = true;
                if (any && MessageBox.Show("Supprimer tous les textes ?", "Confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes)
                { items.RemoveAll(delegate(Item q) { return q is TextItem; }); Refill(null); }
            });
            t2.Controls.Add(lv);
            t2.Controls.Add(rp);

            BuildDrawTab(t3);
            BuildMergeTab();
            BuildMuxTab();

            Controls.Add(pic);
            Controls.Add(seek);
            Controls.Add(tabs);
            Controls.Add(top);

            FormClosing += delegate
            {
                try { if (cur != null && !cur.HasExited) cur.Kill(); } catch { }
            };
        }
    }
}
