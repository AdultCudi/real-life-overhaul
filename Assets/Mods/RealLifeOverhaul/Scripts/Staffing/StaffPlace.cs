using System;

namespace RealLifeOverhaul.Staffing
{
    /// <summary>How a number in a staffing sentence is written. Money is kept in cents at the start's prices and shown through the reader's own money format.</summary>
    public enum FigureKind
    {
        Count = 0,       // a whole number
        Permille = 1,    // thousandths, written as a per cent: 150 is "15%", 5 is "0.5%"
        Money = 2,       // cents at the start's prices
        Times = 3,       // thousandths of a factor, written as "4" for 4000 and "1.5" for 1500
        Tenths = 4,      // tenths of a whole, written "7.7" for 77
        Word = 5         // an index into StaffTable.Words: a name or a phrase of the table's own, looked up in the reader's language
    }

    /// <summary>One number a sentence names: {name} in its text is replaced by it. Every one is read from the mod's own catalogues when the table is made.</summary>
    public readonly struct StaffFigure
    {
        public readonly string Name;
        public readonly long Value;
        public readonly FigureKind Kind;

        public StaffFigure(string name, long value, FigureKind kind)
        {
            Name = name;
            Value = value;
            Kind = kind;
        }
    }

    /// <summary>A sentence of the reference: the key of its text and the figures it names. The text holds no digit of its own.</summary>
    public sealed class StaffNote
    {
        public readonly string Key;
        public readonly StaffFigure[] Figures;
        public readonly string[] Links;       // role ids named in the sentence as {links}, in order; empty for none

        public StaffNote(string key, StaffFigure[] figures, string[] links = null)
        {
            Key = key;
            Figures = figures ?? Array.Empty<StaffFigure>();
            Links = links ?? Array.Empty<string>();
        }
    }

    /// <summary>Whether a line of a place's staffing has to be met to open or run it, or only adds to it.</summary>
    public enum StaffNeed
    {
        Required = 0,
        Optional = 1
    }

    /// <summary>
    /// One thing a place asks of its staff: the roles that can meet it (any one of them does), how many and per what, and
    /// what happens without. <see cref="Does"/> is the sentence of the rule, <see cref="Without"/> the sentence of the loss.
    /// </summary>
    public sealed class StaffLine
    {
        public readonly string[] Roles;       // role ids, StaffTable.Roles
        public readonly StaffNeed Need;
        public readonly StaffNote Does;       // what the role does here and how their number and skill change the result
        public readonly StaffNote Counts;     // the least, a sensible number and where more stops helping; null when the line is one fixed number
        public readonly StaffNote Without;    // what happens with none; null for a line whose absence costs nothing but what it would add
        public readonly StaffRate Rate;       // how many are needed, where the rule is a count per something; Rate.Per is 0 for none
        public readonly string Unit;          // text key of the thing counted ("racks"), empty for none

        public StaffLine(string[] roles, StaffNeed need, StaffNote does, StaffNote counts, StaffNote without, StaffRate rate = default, string unit = "")
        {
            Roles = roles;
            Need = need;
            Does = does;
            Counts = counts;
            Without = without;
            Rate = rate;
            Unit = unit ?? string.Empty;
        }
    }

    /// <summary>
    /// A count of people per so many of something: none for none, otherwise at least <see cref="Least"/>, and one more for
    /// every <see cref="Per"/> beyond. The one rule the data centre's technicians, and every table of the reference, are counted by.
    /// </summary>
    public readonly struct StaffRate
    {
        public readonly long Per;
        public readonly int Least;

        public StaffRate(long per, int least)
        {
            Per = per;
            Least = least;
        }

        public bool Counts => Per > 0;

        /// <summary>People needed for so many units: none for none or fewer, else the least, or one for every Per (rounded up) if that is more.</summary>
        public int Need(long units)
        {
            if (units <= 0 || Per <= 0)
            {
                return 0;
            }
            long each = (units + Per - 1) / Per;
            return (int)Math.Min(int.MaxValue / 4, Math.Max(Least, each));
        }
    }

    /// <summary>A business or workplace the reference describes: its name, what it is, and every line of its staffing.</summary>
    public sealed class StaffPlace
    {
        public readonly string Id;
        public readonly StaffLine[] Lines;
        public readonly StaffNote[] Also;     // other facts of its staffing that are no role's: who hires, a limit, how crews come

        public StaffPlace(string id, StaffLine[] lines, StaffNote[] also)
        {
            Id = id;
            Lines = lines;
            Also = also ?? Array.Empty<StaffNote>();
        }

        public string NameKey => "reallife" + "_staff_place_" + Id;

        public string AboutKey => "reallife" + "_staff_about_" + Id;
    }

    /// <summary>
    /// A role the reference describes. A professional of the talent catalogue has <see cref="Talent"/> set and is described
    /// from its catalogue entry; any other role lists its own ways to be had in <see cref="Gets"/> and to be trained in <see cref="Trains"/>.
    /// </summary>
    public sealed class StaffRole
    {
        public readonly string Id;
        public readonly string NameKey;
        public readonly int Talent;           // the talent catalogue's role id, -1 for a role of the table's own
        public readonly StaffNote[] About;    // what the role is, in a sentence or two
        public readonly StaffNote[] Gets;     // ways to have one: the game's hiring, a search firm, taken on by itself
        public readonly StaffNote[] Trains;   // ways to train an employee into it

        public StaffRole(string id, string nameKey, int talent, StaffNote[] about, StaffNote[] gets, StaffNote[] trains)
        {
            Id = id;
            NameKey = nameKey;
            Talent = talent;
            About = about ?? Array.Empty<StaffNote>();
            Gets = gets ?? Array.Empty<StaffNote>();
            Trains = trains ?? Array.Empty<StaffNote>();
        }
    }
}
