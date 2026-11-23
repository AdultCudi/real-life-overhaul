using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One professional of a firm as the morning's assignment sees them.</summary>
    public struct AssignPerson
    {
        public string Id;
        public int Role, Level;
        public int Points;          // their main skill, thousandths of a point: the better one is moved first
        public bool Pinned;         // the player put them in their role by hand: they stay
        public bool Away;           // on notice, on a course or otherwise not to be moved
        public bool Support, Sales; // their role is a support one, a selling one (only people of the customer service skill, who can be either)
    }

    /// <summary>A move the assignment made: who, to which role, and why.</summary>
    public struct AssignMove
    {
        public string Id;
        public int To;
        public int Why;             // AssignRules.WhySupport or WhySales
    }

    /// <summary>
    /// A firm's manager (or an executive) assigning its professionals to roles each morning, by the firm's needs: who answers its customers' tickets and who sells. Only people of the
    /// customer service skill can be either, and only within it does a role change (TechDirector.SetRole's own rule); a person the player put in their role by hand is never moved.
    /// Deterministic from the people and the needs: no dice. Game-free.
    /// </summary>
    public static class AssignRules
    {
        public const int MovesMost = 2;                 // moves in a morning: a team is not reshuffled in a day
        public const int WhySupport = 0, WhySales = 1;

        /// <summary>
        /// The moves for one firm. <paramref name="supportNeeded"/> is the support people its customers' tickets need, <paramref name="sellersWanted"/> the sellers its work wants.
        /// With <paramref name="contractsFirst"/> (the firm takes contracts and offers wait) a missing seller is filled before a missing support person. Somebody is moved only from
        /// the side that has more than its need, so a move never opens a gap of its own.
        /// </summary>
        public static int Plan(IList<AssignPerson> people, int supportNeeded, int sellersWanted, bool contractsFirst, List<AssignMove> into)
        {
            int support = 0, sales = 0;
            for (int i = 0; i < people.Count; i++)
            {
                support += people[i].Support ? 1 : 0;
                sales += people[i].Sales ? 1 : 0;
            }
            bool[] used = new bool[people.Count];
            int made = 0;
            for (int turn = 0; turn < 2 && made < MovesMost; turn++)
            {
                bool sellFirst = turn == 0 ? contractsFirst : !contractsFirst;
                while (made < MovesMost)
                {
                    bool toSales = sellFirst && sales < sellersWanted && support > supportNeeded;
                    bool toSupport = !sellFirst && support < supportNeeded && sales > sellersWanted;
                    if (!toSales && !toSupport)
                    {
                        break;
                    }
                    int pick = Best(people, used, toSales);
                    if (pick < 0)
                    {
                        break;
                    }
                    used[pick] = true;
                    into.Add(new AssignMove { Id = people[pick].Id, To = toSales ? TalentCatalogue.SalesExecutive : SupportRole(people[pick].Level), Why = toSales ? WhySales : WhySupport });
                    support += toSales ? -1 : 1;
                    sales += toSales ? 1 : -1;
                    made++;
                }
            }
            return made;
        }

        // The best of those who may be moved out of the side with more than its need: the highest skill, the id breaking a tie.
        private static int Best(IList<AssignPerson> people, bool[] used, bool fromSupport)
        {
            int best = -1;
            for (int i = 0; i < people.Count; i++)
            {
                AssignPerson p = people[i];
                if (used[i] || p.Pinned || p.Away || !(fromSupport ? p.Support : p.Sales))
                {
                    continue;
                }
                if (best < 0 || p.Points > people[best].Points || p.Points == people[best].Points && string.CompareOrdinal(p.Id, people[best].Id) < 0)
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The support role a level can hold: IT support to senior, customer success above.</summary>
        public static int SupportRole(int level) => level <= TalentCatalogue.Senior ? TalentCatalogue.ItSupport : TalentCatalogue.CustomerSuccess;
    }
}
