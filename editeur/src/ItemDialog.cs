using System;
using System.Drawing;
using System.Windows.Forms;

namespace Editeur
{
    class ItemDialog : Form
    {
        public TextItem Result;
        ComboBox cType = new ComboBox(), cPos = new ComboBox(), cSize = new ComboBox();
        TextBox tx = new TextBox(), ts = new TextBox(), te = new TextBox();
        Button bCol = new Button();
        Color col = Color.White;
        bool isNew;
        double now;

        Label Lb(string t, int x, int y)
        {
            Label l = new Label(); l.Text = t; l.AutoSize = true; l.Location = new Point(x, y + 4);
            Controls.Add(l); return l;
        }
        Button Bt(string t, int x, int y, int w, int h, EventHandler ev)
        {
            Button b = new Button(); b.Text = t; b.SetBounds(x, y, w, h); b.Click += ev;
            Controls.Add(b); return b;
        }

        public ItemDialog(TextItem it, double nowPos, double dur)
        {
            now = nowPos; isNew = (it == null);
            Text = isNew ? "Ajouter un texte" : "Modifier le texte";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 400);
            Font = new Font("Segoe UI", 9);

            Lb("Type", 10, 12);
            cType.DropDownStyle = ComboBoxStyle.DropDownList;
            cType.Items.AddRange(new object[] { "Titre", "Sous-titre" });
            cType.SetBounds(100, 12, 150, 24); Controls.Add(cType);

            Lb("Texte", 10, 44);
            tx.Multiline = true; tx.AcceptsReturn = true;
            tx.SetBounds(100, 44, 350, 90); Controls.Add(tx);

            Lb("Début", 10, 146);
            ts.SetBounds(100, 146, 90, 24); Controls.Add(ts);
            Bt("= position", 200, 144, 110, 28, delegate { ts.Text = FF.Fmt(now); });
            Lb("Fin", 10, 180);
            te.SetBounds(100, 180, 90, 24); Controls.Add(te);
            Bt("= position", 200, 178, 110, 28, delegate { te.Text = FF.Fmt(now); });

            Lb("Position", 10, 216);
            cPos.DropDownStyle = ComboBoxStyle.DropDownList;
            cPos.Items.AddRange(new object[] { "Haut", "Milieu", "Bas" });
            cPos.SetBounds(100, 216, 150, 24); Controls.Add(cPos);

            Lb("Taille", 10, 250);
            cSize.DropDownStyle = ComboBoxStyle.DropDownList;
            cSize.Items.AddRange(new object[] { "Petite", "Moyenne", "Grande" });
            cSize.SetBounds(100, 250, 150, 24); Controls.Add(cSize);

            Lb("Couleur", 10, 284);
            bCol.SetBounds(100, 284, 90, 26); bCol.FlatStyle = FlatStyle.Standard;
            bCol.Click += delegate
            {
                ColorDialog cd = new ColorDialog(); cd.Color = col;
                if (cd.ShowDialog() == DialogResult.OK) { col = cd.Color; bCol.BackColor = col; }
            };
            Controls.Add(bCol);

            cType.SelectedIndexChanged += delegate
            {
                if (!isNew) return;
                cPos.SelectedIndex = cType.SelectedIndex == 0 ? 1 : 2;
                cSize.SelectedIndex = cType.SelectedIndex == 0 ? 2 : 1;
            };

            if (isNew)
            {
                ts.Text = FF.Fmt(now);
                te.Text = FF.Fmt(Math.Min(dur, now + 4));
                cType.SelectedIndex = 1;
            }
            else
            {
                cType.SelectedIndex = it.Type;
                tx.Text = it.Text;
                ts.Text = FF.Fmt(it.Start); te.Text = FF.Fmt(it.End);
                cPos.SelectedIndex = it.PosV; cSize.SelectedIndex = it.Size;
                col = it.Col;
            }
            bCol.BackColor = col;

            Button ok = Bt("OK", 260, 350, 90, 30, OkClick);
            Button an = Bt("Annuler", 360, 350, 90, 30, null);
            an.DialogResult = DialogResult.Cancel;
            AcceptButton = ok; CancelButton = an;
        }

        void OkClick(object s, EventArgs e)
        {
            double a, b;
            if (!FF.ParseTime(ts.Text, out a) || !FF.ParseTime(te.Text, out b))
            { MessageBox.Show("Temps invalide (exemples : 00:01:30 ou 90)."); return; }
            if (b <= a) { MessageBox.Show("La fin doit être après le début."); return; }
            if (tx.Text.Trim().Length == 0) { MessageBox.Show("Écris un texte."); return; }
            TextItem r = new TextItem();
            r.Type = cType.SelectedIndex; r.Text = tx.Text;
            r.Start = a; r.End = b;
            r.PosV = cPos.SelectedIndex; r.Size = cSize.SelectedIndex; r.Col = col;
            Result = r;
            DialogResult = DialogResult.OK;
        }
    }
}
