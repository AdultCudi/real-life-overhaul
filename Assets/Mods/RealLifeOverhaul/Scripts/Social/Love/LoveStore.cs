using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The love life's part of the save: <see cref="LoveFile"/> as one string under the key reallife:love, a header line of fields
    /// and then a line for each outing (O), night (H) and child (B). A save made before it has no such key and loads as no
    /// second person, no nights and no children. New fields are appended at the end of their line, so a version that reads fewer
    /// leaves them and one that reads more finds a shorter line and defaults them; a line with a tag nobody here knows is kept
    /// as it was read. Game-free (EconomyCheck, LoveChecks.cs). COST: read once on a load, written on a save.
    /// </summary>
    public static class LoveStore
    {
        public const string Key = "reallife:love";
        public const int Version = 1;

        private const char Field = '|';
        private const char Record = '\n';
        private const int HeaderFields = 26;   // the header's fields this version writes: a shorter line from an older one reads the rest as none

        /// <summary>The save's text for the file; empty for one that holds nothing, so a save with no love life writes no key.</summary>
        public static string Write(LoveFile f)
        {
            if (f == null || !f.Has)
            {
                return string.Empty;
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            var text = new StringBuilder(256);
            text.Append(Version).Append(Field).Append(f.Second).Append(Field).Append(f.SecondSince)
                .Append(Field).Append(f.Suspicion[0]).Append(Field).Append(f.Suspicion[1])
                .Append(Field).Append(f.SignDay[0]).Append(Field).Append(f.SignDay[1])
                .Append(Field).Append(f.DeniedDay[0]).Append(Field).Append(f.DeniedDay[1])
                .Append(Field).Append(f.Knows[0]).Append(Field).Append(f.Knows[1])
                .Append(Field).Append(f.TagDay[0]).Append(Field).Append(f.TagDay[1])
                .Append(Field).Append(f.Forgiven).Append(Field).Append(f.ForgivenWho).Append(Field).Append(f.AroundUntil)
                .Append(Field).Append(f.ConfrontWho).Append(Field).Append(f.ConfrontOf).Append(Field).Append(f.ConfrontDay).Append(Field).Append((int)f.ConfrontHow)
                .Append(Field).Append(f.DateOfferWho).Append(Field).Append(f.DateOfferDay)
                .Append(Field).Append(f.LastOfferDay).Append(Field).Append(f.LastScandalDay).Append(Field).Append(f.Nights).Append(Field).Append(f.LastSupportDay);
            for (int i = 0; i < f.Outings.Count; i++)
            {
                LoveOuting o = f.Outings[i];
                text.Append(Record).Append('O').Append(Field).Append(o.Person).Append(Field).Append(o.Day).Append(Field).Append(o.Minute).Append(Field).Append(o.District);
            }
            for (int i = 0; i < f.Hooks.Count; i++)
            {
                LoveHook h = f.Hooks[i];
                text.Append(Record).Append('H').Append(Field).Append(h.Person).Append(Field).Append(h.Day).Append(Field).Append(h.Hour).Append(Field).Append((int)h.Outcome)
                    .Append(Field).Append(h.Drinks).Append(Field).Append(h.Taken ? 1 : 0).Append(Field).Append(h.Mine ? 1 : 0).Append(Field).Append(h.TextDay)
                    .Append(Field).Append(h.QuietUntil).Append(Field).Append(h.Around ? 1 : 0);
            }
            for (int i = 0; i < f.Babies.Count; i++)
            {
                LoveBaby b = f.Babies[i];
                text.Append(Record).Append('B').Append(Field).Append(b.Person).Append(Field).Append(b.Day).Append(Field).Append(b.NewsDay).Append(Field).Append(b.DueDay)
                    .Append(Field).Append(b.Carrier).Append(Field).Append(b.Told ? 1 : 0).Append(Field).Append((int)b.Choice).Append(Field).Append(b.ChoiceDay)
                    .Append(Field).Append(b.Born ? 1 : 0).Append(Field).Append(b.Girl ? 1 : 0).Append(Field).Append(Clean(b.Name)).Append(Field).Append(b.Support)
                    .Append(Field).Append(b.PaidDay).Append(Field).Append(b.Behind).Append(Field).Append(b.Partner ? 1 : 0);
            }
            for (int i = 0; i < f.Kept.Count; i++)
            {
                text.Append(Record).Append(f.Kept[i]);
            }
            return text.ToString();
        }

        /// <summary>
        /// The file a save's text holds; null for text no one here can read (another version, a damaged record), which the caller keeps as it
        /// is. An empty or missing text is an empty file.
        /// </summary>
        public static LoveFile Read(string text)
        {
            var f = new LoveFile();
            if (string.IsNullOrEmpty(text))
            {
                return f;
            }
            try
            {
                string[] lines = text.Split(Record);
                string[] h = lines[0].Split(Field);
                if (h.Length < 3 || Int(h, 0) != Version)
                {
                    return null;
                }
                f.Second = Clamp(Int(h, 1), -1, 399);
                f.SecondSince = Math.Max(0, Int(h, 2));
                for (int i = 0; i < 2; i++)
                {
                    f.Suspicion[i] = Clamp(Int(h, 3 + i), 0, 100);
                    f.SignDay[i] = Math.Max(0, Int(h, 5 + i));
                    f.DeniedDay[i] = Math.Max(0, Int(h, 7 + i));
                    f.Knows[i] = Int(h, 9 + i) != 0 ? 1 : 0;
                    f.TagDay[i] = Math.Max(0, Int(h, 11 + i));
                }
                f.Forgiven = Math.Max(0, Int(h, 13));
                f.ForgivenWho = h.Length > 14 ? Clamp(Int(h, 14), -1, 399) : -1;
                f.AroundUntil = Math.Max(0, Int(h, 15));
                f.ConfrontWho = h.Length > 16 ? Clamp(Int(h, 16), -1, 399) : -1;
                f.ConfrontOf = h.Length > 17 ? Clamp(Int(h, 17), -1, 399) : -1;
                f.ConfrontDay = Math.Max(0, Int(h, 18));
                int how = Int(h, 19);
                f.ConfrontHow = how >= (int)CatchHow.Seen && how <= (int)CatchHow.Child ? (CatchHow)how : CatchHow.Seen;
                f.DateOfferWho = h.Length > 20 ? Clamp(Int(h, 20), -1, 399) : -1;
                f.DateOfferDay = Math.Max(0, Int(h, 21));
                f.LastOfferDay = Math.Max(0, Int(h, 22));
                f.LastScandalDay = Math.Max(0, Int(h, 23));
                f.Nights = Math.Max(0, Int(h, 24));
                f.LastSupportDay = Math.Max(0, Int(h, 25));
                for (int i = 1; i < lines.Length; i++)
                {
                    ReadLine(f, lines[i]);
                }
            }
            catch (Exception)
            {
                return null;
            }
            return f;
        }

        private static void ReadLine(LoveFile f, string line)
        {
            if (line.Length == 0)
            {
                return;
            }
            string[] c = line.Split(Field);
            switch (c[0])
            {
                case "O":
                    if (c.Length >= 5 && f.Outings.Count < LoveFile.OutingsMost)
                    {
                        f.Outings.Add(new LoveOuting { Person = Int(c, 1), Day = Int(c, 2), Minute = Int(c, 3), District = Int(c, 4) });
                    }
                    break;
                case "H":
                    if (c.Length >= 9 && f.Hooks.Count < LoveFile.HooksMost)
                    {
                        int outcome = Int(c, 4);
                        f.Hooks.Add(new LoveHook
                        {
                            Person = Int(c, 1), Day = Int(c, 2), Hour = Int(c, 3),
                            Outcome = outcome >= (int)NightOutcome.Warm && outcome <= (int)NightOutcome.Mishap ? (NightOutcome)outcome : NightOutcome.Warm,
                            Drinks = Math.Max(0, Int(c, 5)), Taken = Int(c, 6) != 0, Mine = Int(c, 7) != 0, TextDay = Math.Max(0, Int(c, 8)),
                            QuietUntil = c.Length > 9 ? Math.Max(0, Int(c, 9)) : 0, Around = c.Length > 10 && Int(c, 10) != 0
                        });
                    }
                    break;
                case "B":
                    if (c.Length >= 10 && f.Babies.Count < LoveFile.BabiesMost)
                    {
                        int choice = Int(c, 7);
                        f.Babies.Add(new LoveBaby
                        {
                            Person = Int(c, 1), Day = Int(c, 2), NewsDay = Int(c, 3), DueDay = Int(c, 4), Carrier = Int(c, 5) == 1 ? 1 : 0, Told = Int(c, 6) != 0,
                            Choice = choice >= (int)LoveChoice.None && choice <= (int)LoveChoice.StayOut ? (LoveChoice)choice : LoveChoice.None,
                            ChoiceDay = Math.Max(0, Int(c, 8)), Born = Int(c, 9) != 0, Girl = c.Length > 10 && Int(c, 10) != 0,
                            Name = c.Length > 11 && c[11].Length > 0 ? c[11] : null, Support = c.Length > 12 ? Math.Max(0, Int(c, 12)) : 0,
                            PaidDay = c.Length > 13 ? Math.Max(0, Int(c, 13)) : 0, Behind = c.Length > 14 ? Math.Max(0, Int(c, 14)) : 0, Partner = c.Length > 15 && Int(c, 15) != 0
                        });
                    }
                    break;
                default:
                    f.Kept.Add(line);   // a later version's line: written back as it was read
                    break;
            }
        }

        private static int Int(string[] c, int i)
        {
            return i < c.Length && int.TryParse(c[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        private static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        // Nothing a record is split on may be inside a field.
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            if (text.Length > 40)
            {
                text = text.Substring(0, 40);
            }
            return text.IndexOf(Field) < 0 && text.IndexOf(Record) < 0 && text.IndexOf('\r') < 0
                ? text
                : text.Replace(Field, '/').Replace(Record, ' ').Replace('\r', ' ');
        }
    }
}
