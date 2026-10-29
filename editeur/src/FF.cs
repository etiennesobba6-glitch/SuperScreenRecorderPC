using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Editeur
{
    static class FF
    {
        public static string Exe { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"); } }
        public static string Q(string s) { return "\"" + s + "\""; }
        public static string Sec(double s) { return s.ToString("0.###", CultureInfo.InvariantCulture); }

        public static Process Start(string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(Exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            return Process.Start(psi);
        }

        public static bool Probe(string file, out double duration, out int w, out int h)
        {
            duration = 0; w = 0; h = 0;
            Process p = Start("-hide_banner -i " + Q(file));
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            Match m = Regex.Match(err, @"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)");
            if (!m.Success) return false;
            duration = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60
                + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            Match v = Regex.Match(err, @"Video:.*?, (\d{2,5})x(\d{2,5})");
            if (v.Success) { w = int.Parse(v.Groups[1].Value); h = int.Parse(v.Groups[2].Value); }
            return true;
        }

        public static Image Frame(string file, double t)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "edi_prev.png");
            if (File.Exists(tmp)) File.Delete(tmp);
            Process p = Start("-hide_banner -loglevel error -ss " + Sec(t) + " -i " + Q(file) + " -frames:v 1 " + Q(tmp));
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (!File.Exists(tmp)) return null;
            return Image.FromStream(new MemoryStream(File.ReadAllBytes(tmp)));
        }

        public static bool ParseTime(string s, out double t)
        {
            t = 0;
            s = s.Trim().Replace(',', '.');
            if (s.Length == 0) return false;
            double tot = 0;
            foreach (string part in s.Split(':'))
            {
                double v;
                if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
                tot = tot * 60 + v;
            }
            t = tot;
            return true;
        }

        public static string Fmt(double t)
        {
            int h = (int)(t / 3600);
            int m = (int)((t % 3600) / 60);
            double s = t % 60;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00.0}", h, m, s);
        }
    }
}
