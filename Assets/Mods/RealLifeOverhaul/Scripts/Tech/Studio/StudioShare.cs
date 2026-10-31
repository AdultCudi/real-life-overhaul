using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The rival studios' market in a Going Public session: one part of the shared city (Core/SharedCity, letter
    /// <see cref="Part"/>), written by the host and applied on the guests, so every player meets the same rival games, the
    /// same release days and the same hot genre. Only the rivals travel: each player's own games stay on their own machine
    /// (never another player's things). A message of another version of the mod is never applied (SharedCity), so an older
    /// or absent RLO never reads it. The rival records are the save's own (<see cref="StudioStore"/>). Game-free.
    /// </summary>
    public static class StudioShare
    {
        public const char Part = 'V';
        public const int Version = 1;

        /// <summary>The host's rival market as it stands, whole.</summary>
        public static string Write(StudioFile f)
        {
            if (f == null)
            {
                return null;
            }
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.NextRival).Add(f.TrendGenre).Add(f.TrendUntil);
            StudioStore.WriteRivalDays(w, f);
            StudioStore.WriteRivalGames(w, f);
            return w.ToString();
        }

        /// <summary>A guest takes the host's rival market in place of its own; false (nothing changed) for a line it cannot read.</summary>
        public static bool Apply(StudioFile f, string text)
        {
            if (f == null || string.IsNullOrEmpty(text))
            {
                return false;
            }
            try
            {
                var r = new TechReader(text);
                if (r.Version != Version)
                {
                    return false;
                }
                var read = new StudioFile();
                Fields h = r.One('h');
                read.NextRival = Math.Max(1, h.Int(1));
                read.TrendGenre = h.Int(-1);
                read.TrendUntil = h.Int();
                StudioStore.ReadRivals(r, read);
                f.NextRival = read.NextRival;
                f.TrendGenre = read.TrendGenre;
                f.TrendUntil = read.TrendUntil;
                Array.Copy(read.RivalNext, f.RivalNext, f.RivalNext.Length);
                f.Rivals.Clear();
                f.Rivals.AddRange(read.Rivals);
                return true;
            }
            catch (Exception)
            {
                return false;   // read whole first: a broken line changes nothing
            }
        }
    }
}
