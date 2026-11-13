using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where another mod's apps are found and where the kit shows them, game-free so it is checked offline (EconomyCheck,
    /// UiForeignChecks). The game has no call for a phone app, so a mod copies a button of the game's: onto the HUD phone's
    /// grid, onto the full menu's own row beside the game's apps, or onto the phone itself. The kit hides that row and
    /// covers the phone, so every such button is an app of the phone's model (PhoneItems), pressed through itself: a tile
    /// of the kit phone and a line of All apps for each, and a tile of the status bar's app row for the first that stood on
    /// the full menu's row, where the player met them before. Also the one line Player.log gives of what was found. Cost:
    /// a few comparisons when the apps are read; nothing a frame.
    /// </summary>
    public static class UiForeignApps
    {
        /// <summary>The apps other mods put on the full menu's own row that the status bar's row carries after the phone's first page; the rest wait in All apps.</summary>
        public const int RowMost = 2;

        /// <summary>The names the line gives of a place before it only counts the rest: a log line stays a line.</summary>
        public const int NamesMost = 12;

        private const string Template = "AppButtonTemplate";   // the game's own button every app's is a copy of

        /// <summary>True for a button's name that is another mod's: not one of the game's apps, not the template, not the mod's own.</summary>
        public static bool Others(string name, bool gameApp)
        {
            return !string.IsNullOrEmpty(name) && !gameApp && name != Template && !name.StartsWith(UiHostRules.ModPrefix, StringComparison.Ordinal) && PagerRules.ModRank(name) < 0;
        }

        /// <summary>
        /// One tile an app: true when the phone lists this button's app already, by its name or by the words under another
        /// mod's button (a mod that puts one app on the grid and on the full menu's row names the two apart and words them alike).
        /// <paramref name="titles"/> holds words for other mods' buttons alone, null for the rest.
        /// </summary>
        public static bool Listed(string name, string title, IList<string> names, IList<string> titles, int count) => Listed(name, title, names, titles, count, null, null);

        /// <summary>
        /// As above, where two buttons worded alike are one app only if one mod owns both: another mod's app with the same words as
        /// a different mod's is its own app and keeps its tile. An owner not known (null on either side) counts as the same.
        /// </summary>
        public static bool Listed(string name, string title, IList<string> names, IList<string> titles, int count, string owner, IList<string> owners)
        {
            string words = string.IsNullOrEmpty(title) ? null : title.Trim();
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                {
                    return true;
                }
                if (words != null && titles[i] != null && string.Equals(titles[i].Trim(), words, StringComparison.OrdinalIgnoreCase)
                    && (owner == null || owners == null || owners[i] == null || string.Equals(owners[i], owner, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A name no app of the phone has: the button's own, or with a number after it for another button of the same name.</summary>
        public static string Unique(string name, IList<string> names, int count)
        {
            string wanted = string.IsNullOrEmpty(name) ? "?" : name;
            string tried = wanted;
            for (int n = 2; Has(tried, names, count) && n < 100; n++)
            {
                tried = wanted + " " + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return tried;
        }

        private static bool Has(string name, IList<string> names, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether an app past the phone's first page still stands on the status bar's row: another mod's that stood on the
        /// full menu's own row, while fewer than <see cref="RowMost"/> are carried.
        /// </summary>
        public static bool OnRow(UiShellKind kind, PhoneItemFrom from, int carried)
        {
            return kind == UiShellKind.Other && from == PhoneItemFrom.Row && carried < RowMost;
        }

        /// <summary>
        /// What the kit holds of other mods' entries, by name: the apps of the phone (the grid's and the full menu's row's),
        /// what stands on the HUD (a button hung on the phone, a piece of the top bar, a button of the building's panel) and
        /// what was met in BizMan (a tab, a button). Fills the three lists; a fault is no entry.
        /// </summary>
        public static void Census(IList<UiShellApp> apps, IReadOnlyList<UiForeign.Piece> pieces, List<string> phone, List<string> hud, List<string> bizman)
        {
            phone.Clear();
            hud.Clear();
            bizman.Clear();
            for (int i = 0; apps != null && i < apps.Count; i++)
            {
                if (apps[i].Kind == UiShellKind.Other)
                {
                    (apps[i].From == PhoneItemFrom.Frame ? hud : phone).Add(apps[i].Title ?? apps[i].Id);
                }
            }
            for (int i = 0; pieces != null && i < pieces.Count; i++)
            {
                UiForeign.Piece p = pieces[i];
                if (p.Kind == UiPieceKind.Fault || p.Kind == UiPieceKind.App)
                {
                    continue;   // an app is counted from the phone's own list above
                }
                if (p.Screen == "hud")
                {
                    hud.Add(Named(p));
                }
                else if (p.Screen == "bizman")
                {
                    bizman.Add(Named(p));
                }
            }
        }

        /// <summary>A piece's name for the line: its mod and its object ("Empire Motors: ManagementButton"), the object alone when its mod is not known.</summary>
        public static string Named(UiForeign.Piece p)
        {
            string where = p.Where ?? string.Empty;
            int cut = where.LastIndexOf(": ", StringComparison.Ordinal);
            string thing = cut >= 0 ? where.Substring(cut + 2) : where;
            return string.IsNullOrEmpty(p.Owner) || p.Owner == thing ? thing : p.Owner + ": " + thing;
        }

        /// <summary>The self-check's line: how many entries of other mods the kit holds in each place, each by its name.</summary>
        public static string Line(IList<string> phone, IList<string> hud, IList<string> bizman)
        {
            var sb = new StringBuilder(160);
            sb.Append("Other mods' apps: ");
            Part(sb, phone, " on the phone");
            sb.Append(", ");
            Part(sb, hud, " on the HUD");
            sb.Append(", ");
            Part(sb, bizman, " in BizMan");
            return sb.Append('.').ToString();
        }

        /// <summary>A number that moves when what the line says would: the line is written again only then.</summary>
        public static int Signature(IList<string> phone, IList<string> hud, IList<string> bizman)
        {
            return unchecked((Fold(phone, 17) * 31 + Fold(hud, 19)) * 31 + Fold(bizman, 23));
        }

        private static int Fold(IList<string> names, int seed)
        {
            int hash = seed;
            for (int i = 0; names != null && i < names.Count; i++)
            {
                hash = unchecked(hash * 31 + (names[i] != null ? names[i].GetHashCode() : 0));
            }
            return hash;
        }

        private static void Part(StringBuilder sb, IList<string> names, string where)
        {
            int n = names != null ? names.Count : 0;
            sb.Append(n.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(where);
            if (n == 0)
            {
                return;
            }
            sb.Append(" (");
            for (int i = 0; i < n && i < NamesMost; i++)
            {
                sb.Append(i > 0 ? ", " : string.Empty).Append(names[i]);
            }
            if (n > NamesMost)
            {
                sb.Append(" and ").Append((n - NamesMost).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(" more");
            }
            sb.Append(')');
        }
    }
}
