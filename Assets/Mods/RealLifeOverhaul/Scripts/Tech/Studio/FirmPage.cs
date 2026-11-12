using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A figure at the head of a firm's page: its label's key, its value as written, how it moved against the week before, and its days.</summary>
    public sealed class ViewFigure
    {
        public string LabelKey;
        public string Value;
        public string Change;            // written with its sign, or null
        public bool Rising, Good;        // the change's way, and whether that way is good for the player
        public float[] Trend;            // the days it came from, oldest first, or null
        public bool Wide;                // stands in two of the row's places, its days drawn beside the number
    }

    /// <summary>A table of a firm's page: its columns' keys (the first says what the rows are), the columns that hold figures, and its rows as written.</summary>
    public sealed class ViewTable
    {
        public string[] ColumnKeys = new string[0];
        public bool[] Figures = new bool[0];   // a column of figures stands to the right
        public readonly List<string[]> Rows = new List<string[]>(8);
        public string EmptyKey;                // the words a table without a row shows
    }

    /// <summary>A key and its value on a firm's page.</summary>
    public sealed class ViewPair
    {
        public string LabelKey;
        public string Value;
    }

    /// <summary>The groups a firm's people are counted in on its page, by their role.</summary>
    public enum TeamGroup
    {
        Engineers = 0, Testers = 1, Data = 2, Security = 3, Infrastructure = 4, Designers = 5, Product = 6, Support = 7, Sales = 8, Business = 9, Count = 10
    }

    /// <summary>The people of one group on a firm's page: how many, their mean skill out of 100, and whether the firm lacks a group its work needs.</summary>
    public sealed class ViewRole
    {
        public TeamGroup Group;
        public int Count;
        public int Skill;
        public bool Needed;
    }

    /// <summary>
    /// What a firm's page reads from the game beyond the files, filled by the director when the page is drawn: the takings
    /// of its last days and its people by group. One a page, made when the page opens or after a press, never a frame.
    /// </summary>
    public sealed class FirmReading
    {
        public readonly long[] Takings = new long[FirmsCatalogue.MonthDays];   // the days' takings, oldest first, cents at the start's prices
        public readonly int[] Count = new int[(int)TeamGroup.Count];
        public readonly long[] Skill = new long[(int)TeamGroup.Count];         // the group's skill in points, summed

        /// <summary>The group a role counts in: testers apart from the engineers they sit with, the professions every company has at the end.</summary>
        public static TeamGroup GroupOf(RoleDef role)
        {
            if (role.Id == TalentCatalogue.QaTester)
            {
                return TeamGroup.Testers;
            }
            switch (role.Family)
            {
                case Family.Engineering: return TeamGroup.Engineers;
                case Family.Data: return TeamGroup.Data;
                case Family.Security: return TeamGroup.Security;
                case Family.Infrastructure: return TeamGroup.Infrastructure;
                case Family.Design: return TeamGroup.Designers;
                case Family.Product: return TeamGroup.Product;
                case Family.Support: return TeamGroup.Support;
                case Family.Commercial: return TeamGroup.Sales;
                default: return TeamGroup.Business;
            }
        }

        /// <summary>Counts one person of a role with their skill in points.</summary>
        public void Add(RoleDef role, int skill)
        {
            int g = (int)GroupOf(role);
            Count[g]++;
            Skill[g] += skill;
        }
    }

    /// <summary>
    /// A tech firm's whole page as data, for the mod's design system (Scripts/Ui) to draw: its screen's title, what to do next
    /// and its actions, three figures with the last 30 days' takings against the week before, its main table (a studio's
    /// games, a firm's products), a second (the games market's newest, a firm's markets), its team by group with the groups
    /// it lacks, and its facts. Game-free: EconomyCheck builds every page offline.
    /// </summary>
    public sealed class FirmPage
    {
        public StudioScreen Screen;      // the title, what to do next and the actions
        public readonly List<ViewFigure> Figures = new List<ViewFigure>(3);
        public readonly ViewTable Main = new ViewTable();
        public readonly ViewTable More = new ViewTable();
        public readonly List<ViewRole> Team = new List<ViewRole>(8);
        public string TeamKey;           // the team's heading
        public string FactsKey;          // the facts' heading
        public readonly List<ViewPair> Facts = new List<ViewPair>(8);
    }
}
