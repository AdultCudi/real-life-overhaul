using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Whose list the garage strip draws: the repair bay's workshop, the custom shop, "My vehicles" or a stand's rack.</summary>
    public enum UiGarageKind : byte
    {
        Workshop,
        Shop,
        Vehicles,
        Stand
    }

    /// <summary>One entry or note of a Garage list as the list holds it: its words, its button's locale key, whether it can be picked, a colour for its tile.</summary>
    public struct UiGarageRow
    {
        public int Id;
        public string Name;
        public string Detail;
        public string ButtonKey;
        public bool Enabled;
        public bool Note;
        public int Colour;      // 0xRRGGBB for a tile beside the name, or -1 for none
    }

    /// <summary>
    /// What the garage strip makes of a Garage list's rows: which row is the shop's one main action (Pay, or Done when the
    /// bill is nothing), which opens a page, which is the way back, which is the option the cart holds, and which is a plain
    /// button; and the lines of the headline's band. The words are the list's own, the buttons' the keys the list gave them,
    /// so the strip says what the old window said. Game-free. Cost: a card a row, once a change.
    /// </summary>
    public static class GarageCardRules
    {
        public const string PayKey = "reallife_garage_list_pay";
        public const string DoneKey = "reallife_customshop_done";
        public const string BackKey = "reallife_garage_list_back";
        public const string ChosenKey = "reallife_garage_list_chosen";
        public const string ChooseKey = "reallife_garage_list_choose";
        public const string ReadKey = "reallife_tuning_list_read";
        public const int MostInfo = 8;

        /// <summary>The strip's card for a row. <paramref name="main"/>: the custom shop's first page, where Choose and Read open a page; on its other pages Choose picks an option.</summary>
        public static UiGarageCard Card(UiGarageRow row, UiGarageKind kind, bool main, Func<string, string> words)
        {
            var card = new UiGarageCard { Name = row.Name ?? string.Empty, Detail = row.Detail, Enabled = row.Enabled };
            if (row.Note)
            {
                card.Note = true;
                card.Enabled = false;
                return card;
            }
            if (row.Colour >= 0)
            {
                card.Swatch = UiDraw.SwatchKey(row.Colour);
            }
            string key = row.ButtonKey;
            if (kind == UiGarageKind.Shop && key != null)
            {
                if (key == PayKey || key == DoneKey)
                {
                    card.Main = true;
                }
                else if (key == BackKey)
                {
                    card.Back = true;
                    return card;   // its glyph stands for the button's word
                }
                else if (key == ChosenKey)
                {
                    card.Chosen = true;
                }
                else if (main && (key == ChooseKey || key == ReadKey))
                {
                    card.Opens = true;
                    return card;   // a chevron: the whole row opens the page
                }
            }
            card.Button = string.IsNullOrEmpty(key) ? null : words(key);
            return card;
        }

        /// <summary>The headline's lines from the list's own words, one to a line break, empty ones left out; how many, at most <see cref="MostInfo"/>.</summary>
        public static int Lines(string plain, string[] into)
        {
            int n = 0;
            if (string.IsNullOrEmpty(plain))
            {
                return 0;
            }
            string[] parts = plain.Split('\n');
            for (int i = 0; i < parts.Length && n < into.Length && n < MostInfo; i++)
            {
                string line = parts[i].Trim();
                if (line.Length > 0)
                {
                    into[n++] = line;
                }
            }
            return n;
        }

        /// <summary>The glyph in the strip's title for whose list it is.</summary>
        public static UiIcon IconFor(UiGarageKind kind) => kind == UiGarageKind.Vehicles ? UiIcon.Car : kind == UiGarageKind.Stand ? UiIcon.Store : UiIcon.Wrench;
    }
}
