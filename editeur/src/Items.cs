using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Editeur
{
    abstract class Item
    {
        public double Start, End;
        public abstract string Kind { get; }
        public abstract string Summary { get; }
        public abstract void Draw(Graphics g, int w, int h);
    }

    class TextItem : Item
    {
        public int Type;   // 0 = titre, 1 = sous-titre
        public string Text = "";
        public int PosV;   // 0 haut, 1 milieu, 2 bas
        public int Size;   // 0 petite, 1 moyenne, 2 grande
        public Color Col = Color.White;

        public override string Kind { get { return Type == 0 ? "Titre" : "Sous-titre"; } }
        public override string Summary { get { return Text.Replace("\r\n", " / ").Replace("\n", " / "); } }

        public override void Draw(Graphics g, int w, int h)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            float[] k = { 0.045f, 0.065f, 0.11f };
            float fs = h * k[Size];
            using (Font f = new Font("Segoe UI", fs, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                float lw = w * 0.9f, lx = w * 0.05f;
                SizeF sz = g.MeasureString(Text, f, (int)lw, sf);
                float y = PosV == 0 ? h * 0.05f : (PosV == 1 ? (h - sz.Height) / 2 : h - sz.Height - h * 0.06f);
                RectangleF r = new RectangleF(lx, y, lw, sz.Height);
                if (Type == 1)
                {
                    float bw = sz.Width + fs * 0.8f;
                    using (Brush bb = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                        g.FillRectangle(bb, (w - bw) / 2, y - fs * 0.15f, bw, sz.Height + fs * 0.3f);
                }
                float d = Math.Max(1f, fs / 22f);
                using (Brush sh = new SolidBrush(Color.FromArgb(220, 0, 0, 0)))
                {
                    g.DrawString(Text, f, sh, new RectangleF(r.X - d, r.Y, r.Width, r.Height), sf);
                    g.DrawString(Text, f, sh, new RectangleF(r.X + d, r.Y, r.Width, r.Height), sf);
                    g.DrawString(Text, f, sh, new RectangleF(r.X, r.Y - d, r.Width, r.Height), sf);
                    g.DrawString(Text, f, sh, new RectangleF(r.X, r.Y + d, r.Width, r.Height), sf);
                }
                using (Brush tb = new SolidBrush(Col))
                    g.DrawString(Text, f, tb, r, sf);
            }
        }
    }
}
