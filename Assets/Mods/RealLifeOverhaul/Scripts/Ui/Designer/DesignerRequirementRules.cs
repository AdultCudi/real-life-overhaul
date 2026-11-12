using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A business's requirements as the designer counts them on its live list of placed things: the game's own two kinds, a count of certain items (by
    /// name, one piece a number of square metres) and a kind of thing (any item of a type), each met by the same test that counts it, so an item
    /// fills a requirement exactly when it is counted toward it. The game's building registration holds that list and the designer's own actions
    /// change it as things are placed, moved, sold and put back (HideCreatedItem, SellRevertibleAction), so a line flips before anything is
    /// applied. The game's own IsRequirementMet stays what says a line is met; this says how many, and which items would meet it. Game-free.
    /// </summary>
    public static class DesignerRequirementRules
    {
        /// <summary>One requirement as plain data: the names of the items that meet it, or the flags of the type that does, and how many it wants.</summary>
        public sealed class Rule
        {
            public string[] Names;   // the item names that count toward it; null or empty when it is a kind of thing
            public int Mask;         // the item type's flags: any one of them counts; 0 when only names do
            public int Need;         // how many pieces it wants; 0 for one of its kind
        }

        /// <summary>One thing placed in the business: its item's name and the flags of its type.</summary>
        public struct Placed
        {
            public string Name;
            public int Mask;
        }

        /// <summary>True when an item, by its name and its type's flags, counts toward the requirement.</summary>
        public static bool Meets(Rule rule, string name, int mask)
        {
            if (rule.Names != null)
            {
                for (int i = 0; i < rule.Names.Length; i++)
                {
                    if (string.Equals(rule.Names[i], name, System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return rule.Mask != 0 && (mask & rule.Mask) != 0;
        }

        /// <summary>How many of what is placed count toward a requirement, and whether that meets it (the game's own: its count, or one where it asks for none).</summary>
        public static void Read(Rule rule, List<Placed> placed, out int have, out bool met)
        {
            have = 0;
            for (int i = 0; i < placed.Count; i++)
            {
                if (Meets(rule, placed[i].Name, placed[i].Mask))
                {
                    have++;
                }
            }
            met = have >= (rule.Need > 0 ? rule.Need : 1);
        }

        /// <summary>How many of the requirements are met by what is placed, and how many there are.</summary>
        public static int CountMet(Rule[] rules, List<Placed> placed, out int of)
        {
            of = rules != null ? rules.Length : 0;
            int met = 0;
            for (int i = 0; i < of; i++)
            {
                Read(rules[i], placed, out _, out bool ok);
                met += ok ? 1 : 0;
            }
            return met;
        }
    }
}
