using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Staffing
{
    /// <summary>What a block of a page is.</summary>
    public enum StaffBlockKind
    {
        Heading = 0,
        Text = 1,
        Bullet = 2,
        Link = 3        // a button to another entry (Slug)
    }

    /// <summary>One block of a page, in order.</summary>
    public readonly struct StaffBlock
    {
        public readonly StaffBlockKind Kind;
        public readonly string Text;
        public readonly string Slug;

        public StaffBlock(StaffBlockKind kind, string text, string slug = null)
        {
            Kind = kind;
            Text = text;
            Slug = slug;
        }
    }

    /// <summary>How a page is written for the one reading it: its language, its money, and how a name that links to another entry looks.</summary>
    public sealed class StaffStyle
    {
        public Func<string, string> Text;               // a text key's words in the reader's language (the key itself when there are none)
        public Func<long, string> Money;                // cents at the start's prices, as the reader's money is written
        public Func<string, string> Bold = s => s;      // a name picked out
        public Func<string, string, string> Link;       // slug and name: a name that opens the entry; null leaves the name bold
        public bool ButtonsAtEnd;                       // a button to each entry named, at the foot of the page (where the name cannot be a link)
    }

    /// <summary>
    /// Builds the reference's pages from the table: a business's, with its roles and what each does, how many, and what
    /// happens without; a role's, with what it does at each business, how to get one and how to train one. Game-free, so the
    /// Guide, the game's help, the Discord guide and the checks all read the same words. The text of a sentence is a key; its
    /// figures are filled in from the table and are never in the text.
    /// </summary>
    public static class StaffPages
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>One figure as the reader sees it.</summary>
        public static string Show(StaffFigure f, StaffStyle style)
        {
            switch (f.Kind)
            {
                case FigureKind.Permille:
                    return (f.Value % 10 == 0 ? (f.Value / 10).ToString("#,##0", Inv) : (f.Value / 10.0).ToString("0.#", Inv)) + "%";
                case FigureKind.Money:
                    return style.Money(f.Value);
                case FigureKind.Times:
                    return f.Value % 1000 == 0 ? (f.Value / 1000).ToString("#,##0", Inv) : (f.Value / 1000.0).ToString("0.#", Inv);
                case FigureKind.Tenths:
                    return f.Value % 10 == 0 ? (f.Value / 10).ToString("#,##0", Inv) : (f.Value / 10.0).ToString("0.0", Inv);
                case FigureKind.Word:
                    IReadOnlyList<string> words = StaffTable.Words;
                    return f.Value >= 0 && f.Value < words.Count ? style.Text(words[(int)f.Value]) : "?";
                default:
                    return f.Value.ToString("#,##0", Inv);
            }
        }

        /// <summary>A sentence with its figures and its role names put in.</summary>
        public static string Say(StaffNote note, StaffStyle style, List<string> named)
        {
            string text = style.Text(note.Key);
            if (text.IndexOf('{') < 0)
            {
                return text;
            }
            var result = new StringBuilder(text.Length + 48);
            int at = 0;
            while (at < text.Length)
            {
                int open = text.IndexOf('{', at);
                int close = open < 0 ? -1 : text.IndexOf('}', open);
                if (open < 0 || close < 0)
                {
                    result.Append(text, at, text.Length - at);
                    break;
                }
                result.Append(text, at, open - at);
                string name = text.Substring(open + 1, close - open - 1);
                if (name == "links")
                {
                    result.Append(NameList(note.Links, style, named));
                }
                else
                {
                    bool found = false;
                    for (int i = 0; i < note.Figures.Length && !found; i++)
                    {
                        if (note.Figures[i].Name == name)
                        {
                            result.Append(Show(note.Figures[i], style));
                            found = true;
                        }
                    }
                    if (!found)
                    {
                        result.Append(text, open, close - open + 1);
                    }
                }
                at = close + 1;
            }
            return result.ToString();
        }

        /// <summary>The names of roles, joined with commas, each a link where the style makes one. A role's id gives its entry.</summary>
        public static string NameList(IList<string> roleIds, StaffStyle style, List<string> named)
        {
            var list = new StringBuilder(96);
            for (int i = 0; i < roleIds.Count; i++)
            {
                StaffRole role = StaffIndex.RoleOf(roleIds[i]);
                string name = role != null ? style.Text(role.NameKey) : roleIds[i];
                if (list.Length > 0)
                {
                    list.Append(", ");
                }
                list.Append(style.Link != null ? style.Link(role != null ? role.NameKey : roleIds[i], name) : style.Bold(name));
                if (named != null && role != null && !named.Contains(role.NameKey))
                {
                    named.Add(role.NameKey);
                }
            }
            return list.ToString();
        }

        /// <summary>The blocks of an entry's page.</summary>
        public static List<StaffBlock> Page(StaffEntry entry, StaffStyle style)
        {
            var blocks = new List<StaffBlock>(48);
            var named = new List<string>(16);
            if (entry.IsPlace)
            {
                PlaceBlocks(entry.Place, style, blocks, named);
            }
            else
            {
                RoleBlocks(entry.Role, style, blocks, named);
            }
            if (style.ButtonsAtEnd && named.Count > 0)
            {
                blocks.Add(new StaffBlock(StaffBlockKind.Heading, style.Text("reallife_staff_h_open")));
                for (int i = 0; i < named.Count; i++)
                {
                    StaffEntry target = StaffIndex.FindSlug(named[i]);
                    if (target != null)
                    {
                        blocks.Add(new StaffBlock(StaffBlockKind.Link, style.Text(target.NameKey), target.NameKey));
                    }
                }
            }
            return blocks;
        }

        private static void PlaceBlocks(StaffPlace place, StaffStyle style, List<StaffBlock> blocks, List<string> named)
        {
            blocks.Add(new StaffBlock(StaffBlockKind.Text, style.Text(place.AboutKey)));
            for (int pass = 0; pass < 2; pass++)
            {
                StaffNeed need = pass == 0 ? StaffNeed.Required : StaffNeed.Optional;
                bool any = false;
                for (int l = 0; l < place.Lines.Length; l++)
                {
                    StaffLine line = place.Lines[l];
                    if (line.Need != need)
                    {
                        continue;
                    }
                    if (!any)
                    {
                        any = true;
                        blocks.Add(new StaffBlock(StaffBlockKind.Heading, style.Text(pass == 0 ? "reallife_staff_h_required" : "reallife_staff_h_optional")));
                    }
                    string who = NameList(line.Roles, style, named);
                    blocks.Add(new StaffBlock(StaffBlockKind.Bullet, (line.Roles.Length > 1 ? style.Text("reallife_staff_any") + " " : string.Empty) + who + ": " + Say(line.Does, style, named)));
                    if (line.Counts != null)
                    {
                        blocks.Add(new StaffBlock(StaffBlockKind.Text, style.Bold(style.Text("reallife_staff_how_many")) + " " + Say(line.Counts, style, named)));
                    }
                    if (line.Without != null)
                    {
                        blocks.Add(new StaffBlock(StaffBlockKind.Text, style.Bold(style.Text("reallife_staff_without")) + " " + Say(line.Without, style, named)));
                    }
                }
            }
            if (place.Also.Length > 0)
            {
                blocks.Add(new StaffBlock(StaffBlockKind.Heading, style.Text("reallife_staff_h_also")));
                for (int i = 0; i < place.Also.Length; i++)
                {
                    blocks.Add(new StaffBlock(StaffBlockKind.Bullet, Say(place.Also[i], style, named)));
                }
            }
        }

        private static void RoleBlocks(StaffRole role, StaffStyle style, List<StaffBlock> blocks, List<string> named)
        {
            for (int i = 0; i < role.About.Length; i++)
            {
                blocks.Add(new StaffBlock(StaffBlockKind.Text, Say(role.About[i], style, named)));
            }
            var uses = new List<KeyValuePair<StaffPlace, int>>(16);
            StaffIndex.UsesOf(role.Id, uses);
            blocks.Add(new StaffBlock(StaffBlockKind.Heading, style.Text("reallife_staff_h_does")));
            if (uses.Count == 0)
            {
                blocks.Add(new StaffBlock(StaffBlockKind.Text, style.Text("reallife_staff_no_use")));
            }
            var seen = new Dictionary<string, string>(16);   // a sentence already said on this page, and the business it was said for
            for (int u = 0; u < uses.Count; u++)
            {
                StaffPlace place = uses[u].Key;
                StaffLine line = place.Lines[uses[u].Value];
                string placeName = style.Text(place.NameKey);
                string where = style.Link != null ? style.Link(place.NameKey, placeName) : style.Bold(placeName);
                if (!named.Contains(place.NameKey))
                {
                    named.Add(place.NameKey);
                }
                string need = style.Text(line.Need == StaffNeed.Required ? "reallife_staff_need_required" : "reallife_staff_need_optional");
                string does = Say(line.Does, style, named);
                string said = seen.TryGetValue(does, out string first) ? style.Text("reallife_staff_same").Replace("{place}", first) : does;
                seen[does] = seen.ContainsKey(does) ? seen[does] : placeName;
                blocks.Add(new StaffBlock(StaffBlockKind.Bullet, where + " (" + need + "): " + said));
                if (line.Counts != null)
                {
                    string counts = Say(line.Counts, style, named);
                    if (!seen.ContainsKey(counts))
                    {
                        seen[counts] = placeName;
                        blocks.Add(new StaffBlock(StaffBlockKind.Text, style.Bold(style.Text("reallife_staff_how_many")) + " " + counts));
                    }
                }
                if (line.Without != null)
                {
                    string without = Say(line.Without, style, named);
                    if (!seen.ContainsKey(without))
                    {
                        seen[without] = placeName;
                        blocks.Add(new StaffBlock(StaffBlockKind.Text, style.Bold(style.Text("reallife_staff_without")) + " " + without));
                    }
                }
            }
            blocks.Add(new StaffBlock(StaffBlockKind.Heading, style.Text("reallife_staff_h_gets")));
            for (int i = 0; i < role.Gets.Length; i++)
            {
                blocks.Add(new StaffBlock(StaffBlockKind.Bullet, Say(role.Gets[i], style, named)));
            }
            blocks.Add(new StaffBlock(StaffBlockKind.Heading, style.Text("reallife_staff_h_trains")));
            for (int i = 0; i < role.Trains.Length; i++)
            {
                blocks.Add(new StaffBlock(StaffBlockKind.Bullet, Say(role.Trains[i], style, named)));
            }
        }

        // ---------------------------------------------------------------- the same page as text

        /// <summary>A page as the Guide writes one: "# " a heading, "- " a bullet, a plain line.</summary>
        public static string AsGuideText(List<StaffBlock> blocks)
        {
            var text = new StringBuilder(2048);
            for (int i = 0; i < blocks.Count; i++)
            {
                StaffBlock b = blocks[i];
                switch (b.Kind)
                {
                    case StaffBlockKind.Heading:
                        text.Append("# ").Append(b.Text).Append('\n');
                        break;
                    case StaffBlockKind.Bullet:
                        text.Append("- ").Append(b.Text).Append('\n');
                        break;
                    case StaffBlockKind.Link:
                        break;
                    default:
                        text.Append(b.Text).Append('\n');
                        break;
                }
            }
            return text.ToString().TrimEnd('\n');
        }

        /// <summary>A page as the game's help writes one (mark-down): a link is [name](slug).</summary>
        public static string AsMarkdown(List<StaffBlock> blocks)
        {
            var text = new StringBuilder(2048);
            for (int i = 0; i < blocks.Count; i++)
            {
                StaffBlock b = blocks[i];
                switch (b.Kind)
                {
                    case StaffBlockKind.Heading:
                        text.Append("\n### ").Append(b.Text).Append("\n\n");
                        break;
                    case StaffBlockKind.Bullet:
                        text.Append("- ").Append(b.Text).Append('\n');
                        break;
                    case StaffBlockKind.Link:
                        text.Append("- [").Append(b.Text).Append("](").Append(b.Slug).Append(")\n");
                        break;
                    default:
                        text.Append('\n').Append(b.Text).Append("\n\n");
                        break;
                }
            }
            return text.ToString().Trim();
        }

        /// <summary>The style the game's help reads: names that link are [name](slug), bold is **name**.</summary>
        public static StaffStyle MarkdownStyle(Func<string, string> text, Func<long, string> money)
        {
            return new StaffStyle { Text = text, Money = money, Bold = s => "**" + s + "**", Link = (slug, name) => "[" + name + "](" + slug + ")" };
        }

        /// <summary>The style the Guide, the Discord guide and the checks read: the game's text tags, links as bold names with buttons at the foot where the page has them.</summary>
        public static StaffStyle GuideStyle(Func<string, string> text, Func<long, string> money, bool buttons)
        {
            return new StaffStyle { Text = text, Money = money, Bold = s => "<b>" + s + "</b>", ButtonsAtEnd = buttons };
        }
    }
}
