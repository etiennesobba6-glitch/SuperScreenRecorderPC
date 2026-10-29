using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Editeur
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new FenetrePrincipale());
        }
    }

    class FenetrePrincipale : Form
    {
        public FenetrePrincipale()
        {
            Text = "Éditeur vidéo";
            Width = 640;
            Height = 400;
            StartPosition = FormStartPosition.CenterScreen;
            string ff = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            Label l = new Label();
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleCenter;
            l.Font = new Font("Segoe UI", 12);
            l.Text = File.Exists(ff)
                ? "ffmpeg.exe trouvé.\nÉditeur prêt (étape 1)."
                : "ffmpeg.exe introuvable à côté du programme.";
            Controls.Add(l);
        }
    }
}
