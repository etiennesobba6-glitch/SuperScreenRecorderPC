using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Editeur
{
    partial class FenetrePrincipale
    {
        ComboBox cmbTool = new ComboBox(), cmbThick = new ComboBox();
        Button btnColor = new Button();
        CheckBox chkDraw = new CheckBox();
        NumericUpDown numDur = new NumericUpDown();
        ListView lv2 = new ListView();
        Color drawCol = Color.Red;
        DrawItem live;
        Stack<Item> undo = new Stack<Item>();
        int lastTick;

        void BuildDrawTab(TabPage t3)
        {
            lv2.View = View.Details; lv2.FullRowSelect = true; lv2.HideSelection = false; lv2.MultiSelect = false;
            lv2.Columns.Add("Outil", 110); lv2.Columns.Add("Début", 90); lv2.Columns.Add("Fin", 90); lv2.Columns.Add("Détail", 200);
            lv2.Dock = DockStyle.Fill;
            lv2.SelectedIndexChanged += delegate
            {
                if (busy || file == null) return;
                DrawItem d = SelDraw(); if (d != null) SeekTo(d.Start);
            };

            Panel top = new Panel(); top.Dock = DockStyle.Top; top.Height = 76;
            Lb(top, "Outil", 10, 10);
            cmbTool.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTool.Items.AddRange(new object[] { "Stylo", "Rectangle", "Soulignement", "Surlignage" });
            cmbTool.SelectedIndex = 0;
            Put(top, cmbTool, 55, 10, 130, 24);
            cmbTool.SelectedIndexChanged += delegate
            {
                drawCol = cmbTool.SelectedIndex == 3 ? Color.Yellow : Color.Red;
                btnColor.BackColor = drawCol;
            };
            Lb(top, "Couleur", 200, 10);
            btnColor.BackColor = drawCol;
            btnColor.Click += delegate
            {
                ColorDialog cd = new ColorDialog(); cd.Color = drawCol;
                if (cd.ShowDialog() == DialogResult.OK) { drawCol = cd.Color; btnColor.BackColor = drawCol; }
            };
            Put(top, btnColor, 260, 9, 50, 26);
            Lb(top, "Épaisseur", 325, 10);
            cmbThick.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbThick.Items.AddRange(new object[] { "Fine", "Moyenne", "Forte" });
            cmbThick.SelectedIndex = 1;
            Put(top, cmbThick, 395, 10, 100, 24);

            chkDraw.Text = "Dessiner sur l'image";
            chkDraw.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            chkDraw.CheckedChanged += delegate { pic.Cursor = chkDraw.Checked ? Cursors.Cross : Cursors.Default; };
            Put(top, chkDraw, 10, 44, 200, 24);
            Lb(top, "Durée (s)", 230, 44);
            numDur.Minimum = 1; numDur.Maximum = 600; numDur.Value = 5;
            Put(top, numDur, 295, 44, 60, 24);
            Lb(top, "Place-toi sur l'instant voulu, coche la case, trace sur l'image.", 370, 44);

            Panel rp = new Panel(); rp.Dock = DockStyle.Right; rp.Width = 150;
            Btn(rp, "Début = position", 10, 8, 130, 30, delegate { SetDrawTime(true); });
            Btn(rp, "Fin = position", 10, 44, 130, 30, delegate { SetDrawTime(false); });
            Btn(rp, "Annuler le dernier", 10, 80, 130, 30, UndoLast);
            Btn(rp, "Supprimer", 10, 116, 130, 30, delegate
            {
                DrawItem d = SelDraw(); if (d == null) return;
                items.Remove(d); Refill(null);
            });
            Btn(rp, "Tout supprimer", 10, 152, 130, 30, delegate
            {
                if (HasDraw() && MessageBox.Show("Supprimer tous les dessins ?", "Confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes)
                { items.RemoveAll(delegate(Item q) { return q is DrawItem; }); Refill(null); }
            });

            t3.Controls.Add(lv2);
            t3.Controls.Add(top);
            t3.Controls.Add(rp);

            pic.MouseDown += PicDown;
            pic.MouseMove += PicMove;
            pic.MouseUp += PicUp;
            tabs.SelectedIndexChanged += delegate { if (tabs.SelectedTab != t3) chkDraw.Checked = false; };
        }

        bool HasDraw()
        {
            foreach (Item x in items) if (x is DrawItem) return true;
            return false;
        }

        DrawItem SelDraw()
        {
            return lv2.SelectedItems.Count > 0 ? lv2.SelectedItems[0].Tag as DrawItem : null;
        }

        void UndoLast(object s, EventArgs e)
        {
            while (undo.Count > 0)
            {
                Item it = undo.Pop();
                if (items.Remove(it)) { Refill(null); return; }
            }
        }

        void SetDrawTime(bool isStart)
        {
            DrawItem d = SelDraw();
            if (d == null) { MessageBox.Show("Choisis d'abord un dessin dans la liste."); return; }
            if (isStart)
            {
                if (curT >= d.End) { MessageBox.Show("Le début doit être avant la fin."); return; }
                d.Start = curT;
            }
            else
            {
                if (curT <= d.Start) { MessageBox.Show("La fin doit être après le début."); return; }
                d.End = curT;
            }
            double t = curT;
            Refill(d);
            if (!isStart) SeekTo(t);
        }

        bool ToNorm(int mx, int my, out PointF n)
        {
            n = PointF.Empty;
            if (baseImg == null) return false;
            float iw = baseImg.Width, ih = baseImg.Height;
            float cw = pic.ClientSize.Width, ch = pic.ClientSize.Height;
            float s = Math.Min(cw / iw, ch / ih);
            if (s <= 0) return false;
            float ox = (cw - iw * s) / 2, oy = (ch - ih * s) / 2;
            float x = (mx - ox) / (iw * s), y = (my - oy) / (ih * s);
            x = Math.Max(0f, Math.Min(1f, x));
            y = Math.Max(0f, Math.Min(1f, y));
            n = new PointF(x, y);
            return true;
        }

        void PicDown(object s, MouseEventArgs e)
        {
            if (!chkDraw.Checked || file == null || baseImg == null || e.Button != MouseButtons.Left) return;
            PointF n;
            if (!ToNorm(e.X, e.Y, out n)) return;
            live = new DrawItem();
            live.Tool = cmbTool.SelectedIndex;
            live.Col = drawCol;
            live.Thick = cmbThick.SelectedIndex;
            live.Pts.Add(n);
            live.Pts.Add(n);
        }

        void PicMove(object s, MouseEventArgs e)
        {
            if (live == null) return;
            PointF n;
            if (!ToNorm(e.X, e.Y, out n)) return;
            if (live.Tool == 0) live.Pts.Add(n); else live.Pts[1] = n;
            int t = Environment.TickCount;
            if (t - lastTick > 30) { lastTick = t; Compose(); }
        }

        void PicUp(object s, MouseEventArgs e)
        {
            if (live == null) return;
            DrawItem d = live; live = null;
            PointF a = d.Pts[0], b = d.Pts[d.Pts.Count - 1];
            float dist = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
            bool valid = d.Tool == 0 ? d.Pts.Count > 3 : dist > 0.01f;
            if (!valid) { Compose(); return; }
            d.Start = curT;
            d.End = Math.Min(dur, curT + (double)numDur.Value);
            if (d.End - d.Start < 0.1) d.Start = Math.Max(0, d.End - 0.5);
            items.Add(d);
            undo.Push(d);
            Refill(d, false);
        }
    }
}
