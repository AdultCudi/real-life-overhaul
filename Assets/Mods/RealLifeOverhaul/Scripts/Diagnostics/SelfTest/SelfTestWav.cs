using System;
using System.IO;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>A short sine tone as a 16 bit mono PCM wav file, game-free so an offline check can read it back.</summary>
    public static class SelfTestWav
    {
        public const int Rate = 22050;   // samples a second

        public static byte[] Make(double seconds, double hz)
        {
            int samples = (int)Math.Max(1, Math.Min(10 * Rate, seconds * Rate));
            using (var ms = new MemoryStream(44 + samples * 2))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + samples * 2);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                {
                    double fade = Math.Min(1.0, Math.Min(i, samples - i) / (0.02 * Rate));   // 20 ms in and out: no click
                    w.Write((short)(Math.Sin(2 * Math.PI * hz * i / Rate) * 0.3 * short.MaxValue * fade));
                }
                return ms.ToArray();
            }
        }

        public static void Write(string path, double seconds, double hz)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Make(seconds, hz));
        }
    }
}
