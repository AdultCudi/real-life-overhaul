using System.IO;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The tape file written whole without a moment in which neither tape is on disk: the new one is written beside it
    /// (".new"), the old moved aside (".old"), the new moved in, the old deleted. A crash at any step leaves a whole tape a
    /// load finds (<see cref="Settle"/>). Game-free; file calls only, on the tape's writer thread and before a read.
    /// </summary>
    public static class MarketTapeSwap
    {
        public static string NewOf(string path) => path + ".new";

        public static string OldOf(string path) => path + ".old";

        /// <summary>Writes <paramref name="text"/> as the whole of the file at <paramref name="path"/>.</summary>
        public static void Write(string path, string text)
        {
            string fresh = NewOf(path), old = OldOf(path);
            File.WriteAllText(fresh, text);
            if (File.Exists(old))
            {
                File.Delete(old);
            }
            if (File.Exists(path))
            {
                File.Move(path, old);
            }
            File.Move(fresh, path);
            File.Delete(old);
        }

        /// <summary>As <see cref="Write(string, string)"/>, the text given as its lines in order (each with its own end), never joined in memory.</summary>
        public static void Write(string path, string[] lines)
        {
            string fresh = NewOf(path), old = OldOf(path);
            using (var writer = new StreamWriter(fresh, false, NoMark))
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    writer.Write(lines[i]);
                }
            }
            if (File.Exists(old))
            {
                File.Delete(old);
            }
            if (File.Exists(path))
            {
                File.Move(path, old);
            }
            File.Move(fresh, path);
            File.Delete(old);
        }

        private static readonly Encoding NoMark = new UTF8Encoding(false);   // as File.WriteAllText writes: UTF-8, no byte order mark

        /// <summary>
        /// After a crash mid-write, puts the whole tape back at <paramref name="path"/>. With the tape there, a ".new" beside
        /// it may be half written and goes. Without it, the ".new" was written whole before the old was moved aside (or, from
        /// an older writer, deleted), so it is the tape; with no ".new", the ".old" is.
        /// </summary>
        public static void Settle(string path)
        {
            string fresh = NewOf(path), old = OldOf(path);
            if (File.Exists(path))
            {
                if (File.Exists(fresh))
                {
                    File.Delete(fresh);
                }
                if (File.Exists(old))
                {
                    File.Delete(old);
                }
                return;
            }
            if (File.Exists(fresh))
            {
                File.Move(fresh, path);
            }
            else if (File.Exists(old))
            {
                File.Move(old, path);
            }
            if (File.Exists(old))
            {
                File.Delete(old);
            }
        }
    }
}
