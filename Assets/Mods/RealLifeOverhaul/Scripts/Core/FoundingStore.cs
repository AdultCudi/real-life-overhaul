using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The start days kept for the player's businesses (<see cref="FoundingRules"/>) in the save: one line of the save's modData under
    /// <see cref="Key"/>, written the way the tech sector writes its lines (Tech/TechCodec). Game-free.
    ///
    ///     v=1~r:place|name|start day|day last seen|estimate;...
    ///
    /// A save without the line starts with none; a later version's extra fields at the end of a record are read as nothing.
    /// </summary>
    public static class FoundingStore
    {
        public const string Key = "reallife:founding";
        public const int Version = 1;

        public static FoundingFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(FoundingFile f)
        {
            var w = new TechWriter(Version);
            w.Part('r');
            for (int i = 0; i < f.Records.Count && i < FoundingRules.RecordsMost; i++)
            {
                FoundingRecord r = f.Records[i];
                w.Record().Place(r.Place).Add(r.Name).Add(r.Day).Add(r.Seen).Add(r.Guess ? 1 : 0);
            }
            return w.ToString();
        }

        public static FoundingFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new FoundingFile();
            foreach (Fields x in r.Records('r'))
            {
                var m = new FoundingRecord { Place = x.Place(), Name = x.Text(), Day = x.Int(), Seen = x.Int(), Guess = x.Bool() };
                if (m.Day > 0 && m.Place.Length > 0 && f.Records.Count < FoundingRules.RecordsMost)
                {
                    f.Records.Add(m);
                }
            }
            return f;
        }
    }
}
