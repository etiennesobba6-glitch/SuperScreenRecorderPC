using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Editeur
{
    class DrawItem : Item
    {
        public int Tool;
        public List<PointF> Pts = new List<PointF>();
        public Color Col = Color.Red;
        public int Thick = 1;

        static readonly string[] Names = { "Stylo", "Rectangle", "Soulignement", "Surlignage" };
        static readonly string[] ThickNames = { "fine", "moyenne", "forte" };

        public override string Kind { get { return Names[Tool]; } }
        public override string Summary { get { return "Épaisseur " + ThickNames[Thick]; } }

        static PointF P(PointF p, int w, int h) { return new PointF(p.X * w, p.Y * h); }

        public override void Draw(Graphics g, int w, int h)
        {
            if (Pts.Count < 2) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float pw = h * (Thick == 0 ? 0.004f : (Thick == 1 ? 0.008f : 0.015f));
            if (pw < 1.5f) pw = 1.5f;
            PointF a = P(Pts[0], w, h);
            PointF b = P(Pts[Pts.Count - 1], w, h);
            if (Tool == 0)
            {
                PointF[] arr = new PointF[Pts.Count];
                for (int i = 0; i < arr.Length; i++) arr[i] = P(Pts[i], w, h);
                using (Pen p = new Pen(Col, pw))
                {
                    p.LineJoin = LineJoin.Round;
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLines(p, arr);
                }
            }
            else if (Tool == 1)
            {
                using (Pen p = new Pen(Col, pw))
                    g.DrawRectangle(p, Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
            }
            else if (Tool == 2)
            {
                using (Pen p = new Pen(Col, pw))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLine(p, a.X, b.Y, b.X, b.Y);
                }
            }
            else
            {
                using (Brush br = new SolidBrush(Color.FromArgb(110, Col)))
                    g.FillRectangle(br, Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
            }
        }
    }
}
