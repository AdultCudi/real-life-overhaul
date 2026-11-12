using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's help pages read for the kit's reader, game-free: a page's markdown (paragraphs, bold names, bulleted lists, links
    /// to other pages written [words](page)) turned into the reader's blocks, the links it holds in the order the reader's
    /// anchors are numbered, and the glyph of each category of the game's list. The game's own pages, its list, its search and its
    /// history stay the game's. Cost: a pass over a page's words once a page is shown (the longest is under three thousand letters).
    /// </summary>
    public static class HelpRules
    {
        /// <summary>The page link that stands for a picture or a video the kit does not draw: it opens the game's own view of the page.</summary>
        public const string MediaLink = "reallife:media";

        /// <summary>The game's help link to a place on the city map: it closes the help and moves the camera, as the game does.</summary>
        public const string AddressPrefix = "address:";

        /// <summary>
        /// A page's markdown as the reader's blocks. A line that is only a link (a list of pages) is a link of its own; a link
        /// inside words stands out in bold there and has a link block of its own after the block that holds it. Each page the page
        /// links to goes into <paramref name="slugs"/> once, and a block's <see cref="UiReaderBlock.Target"/> is its place there.
        /// <paramref name="media"/> is true when the page holds a picture or a video (the game's own view shows them).
        /// </summary>
        public static UiReaderBlock[] Parse(string markdown, List<string> slugs, out bool media)
        {
            media = false;
            slugs.Clear();
            var blocks = new List<UiReaderBlock>(64);
            if (string.IsNullOrEmpty(markdown))
            {
                return blocks.ToArray();
            }
            string[] lines = markdown.Replace("\r", string.Empty).Split('\n');
            var paragraph = new StringBuilder(256);
            var found = new List<KeyValuePair<string, string>>(4);   // the links inside the block being read: words, page
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    Flush(blocks, slugs, paragraph, found, UiReaderKind.Paragraph, 0);
                    continue;
                }
                if (line.StartsWith("![", StringComparison.Ordinal) || line.IndexOf("<video", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.StartsWith("[image,", StringComparison.OrdinalIgnoreCase) || line.StartsWith("[video,", StringComparison.OrdinalIgnoreCase))
                {
                    media = true;
                    continue;
                }
                string text;
                if (Mark(line, "## ", out text) || Mark(line, "### ", out text))
                {
                    Flush(blocks, slugs, paragraph, found, UiReaderKind.Paragraph, 0);
                    blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Subheading, Text = Inline(text, found) });
                    found.Clear();
                    continue;
                }
                if (Mark(line, "# ", out text))
                {
                    Flush(blocks, slugs, paragraph, found, UiReaderKind.Paragraph, 0);
                    blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Heading, Text = Inline(text, found) });
                    found.Clear();
                    continue;
                }
                if (Mark(line, "* ", out text) || Mark(line, "- ", out text))
                {
                    Flush(blocks, slugs, paragraph, found, UiReaderKind.Paragraph, 0);
                    text = text.Trim();
                    if (Whole(text, out string words, out string page))
                    {
                        blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Link, Text = words, Target = Slot(slugs, page) });
                        continue;
                    }
                    paragraph.Append(text);
                    Flush(blocks, slugs, paragraph, found, UiReaderKind.Bullet, 0);
                    continue;
                }
                if (Mark(line, "> ", out text))
                {
                    Flush(blocks, slugs, paragraph, found, UiReaderKind.Paragraph, 0);
                    paragraph.Append(text);
                    Flush(blocks, slugs, paragraph, found, UiReaderKind.Note, 0);
                    continue;
                }
                int dot = line.IndexOf(". ", StringComparison.Ordinal);
                if (dot > 0 && dot <= 3 && int.TryParse(line.Substring(0, dot), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number))
                {
                    Flush(blocks, slugs, paragraph, found, UiReaderKind.Paragraph, 0);
                    paragraph.Append(line.Substring(dot + 2));
                    Flush(blocks, slugs, paragraph, found, UiReaderKind.Numbered, number);
                    continue;
                }
                if (paragraph.Length > 0)
                {
                    paragraph.Append(' ');
                }
                paragraph.Append(line);
            }
            Flush(blocks, slugs, paragraph, found, UiReaderKind.Paragraph, 0);
            return blocks.ToArray();
        }

        // The words of a line after a mark that opens it (a list's bullet, a heading's hashes).
        private static bool Mark(string line, string mark, out string text)
        {
            bool is_ = line.StartsWith(mark, StringComparison.Ordinal);
            text = is_ ? line.Substring(mark.Length) : null;
            return is_;
        }

        // A line that is one link and nothing else: "[Gift (Cheap)](products-cheapgift)".
        private static bool Whole(string text, out string words, out string page)
        {
            words = page = null;
            if (text.Length < 5 || text[0] != '[' || text[text.Length - 1] != ')')
            {
                return false;
            }
            int close = text.IndexOf("](", StringComparison.Ordinal);
            if (close < 2 || text.IndexOf(']') != close || text.IndexOf(')', close + 2) != text.Length - 1)
            {
                return false;
            }
            words = text.Substring(1, close - 1).Replace("**", string.Empty).Trim();
            page = text.Substring(close + 2, text.Length - close - 3).Trim();
            return words.Length > 0 && page.Length > 0;
        }

        // The block that has been read, with its links standing out in bold and a link block after it for each page it names.
        private static void Flush(List<UiReaderBlock> blocks, List<string> slugs, StringBuilder text, List<KeyValuePair<string, string>> found, UiReaderKind kind, int number)
        {
            if (text.Length == 0)
            {
                found.Clear();
                return;
            }
            string words = Inline(text.ToString(), found);
            text.Length = 0;
            blocks.Add(new UiReaderBlock { Kind = kind, Text = words, Number = number });
            var seen = new List<string>(found.Count);
            for (int i = 0; i < found.Count; i++)
            {
                if (seen.Contains(found[i].Value))
                {
                    continue;
                }
                seen.Add(found[i].Value);
                blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Link, Text = found[i].Key, Target = Slot(slugs, found[i].Value) });
            }
            found.Clear();
        }

        // The place of a page in the list of links, added when it is new.
        private static int Slot(List<string> slugs, string page)
        {
            int at = slugs.IndexOf(page);
            if (at >= 0)
            {
                return at;
            }
            slugs.Add(page);
            return slugs.Count - 1;
        }

        /// <summary>
        /// A line's words with its marks turned into the reader's: **bold** between the bold tags, [words](page) as the words in bold
        /// (the page is noted in <paramref name="found"/>), and any other mark left out, so none reaches the screen.
        /// </summary>
        public static string Inline(string text, List<KeyValuePair<string, string>> found)
        {
            var sb = new StringBuilder(text.Length + 16);
            bool bold = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    sb.Append(bold ? "</b>" : "<b>");
                    bold = !bold;
                    i++;
                    continue;
                }
                if (c == '[')
                {
                    int close = text.IndexOf("](", i + 1, StringComparison.Ordinal);
                    int end = close > i ? text.IndexOf(')', close + 2) : -1;
                    if (close > i && end > close && text.IndexOf(']', i + 1) == close)
                    {
                        string words = text.Substring(i + 1, close - i - 1).Replace("**", string.Empty);
                        string page = text.Substring(close + 2, end - close - 2).Trim();
                        found.Add(new KeyValuePair<string, string>(words, page));
                        sb.Append(bold ? words : "<b>" + words + "</b>");
                        i = end;
                        continue;
                    }
                }
                if (c == '<')
                {
                    sb.Append('‹');   // a stray angle bracket would read as one of the reader's tags
                    continue;
                }
                sb.Append(c);
            }
            if (bold)
            {
                sb.Append("</b>");
            }
            return sb.ToString();
        }

        /// <summary>The glyph of a category of the game's list (and of the mod's two for staff and roles), by the key of its name.</summary>
        public static UiIcon IconOf(string categoryKey)
        {
            switch (categoryKey)
            {
                case "help_general": return UiIcon.Info;
                case "common_finance": return UiIcon.Coin;
                case "building_title": return UiIcon.Building;
                case "common_business_types": return UiIcon.Store;
                case "employee_types": return UiIcon.People;
                case "employee_management": return UiIcon.IdBadge;
                case "common_sellable_products": return UiIcon.Package;
                case "help_importers": return UiIcon.Truck;
                case "common_furniture": return UiIcon.Sofa;
                case "rivals_title": return UiIcon.Trophy;
                case "common_factoryrecipes": return UiIcon.Receipt;
                case "common_factorymachines": return UiIcon.Factory;
                case "common_factory_ingredients": return UiIcon.Cube;
                case "vehicles_title": return UiIcon.Car;
                case "reallife_staff_cat_places": return UiIcon.Briefcase;
                case "reallife_staff_cat_roles": return UiIcon.IdBadge;
                default: return UiIcon.Info;
            }
        }

        /// <summary>True for a link that moves the camera on the city map rather than opening a page (the game closes the help then).</summary>
        public static bool IsPlace(string slug) => slug != null && slug.StartsWith(AddressPrefix, StringComparison.Ordinal);
    }
}
