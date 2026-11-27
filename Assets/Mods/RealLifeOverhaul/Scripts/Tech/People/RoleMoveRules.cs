using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One professional as the bulk role change sees them: who, where, their role and the skills that tell how well they would fit another.</summary>
    public struct MoveFolk
    {
        public string Id, Place;
        public int Role, Level;
        public int A, B;            // the role's two specialisms, thousandths of a point
        public int Joined;          // the day they joined: of equal fit the newest goes first
        public bool Holds;          // the game's skill they are hired under (or their payroll's family) lets them hold the new role (TechDirector.CanHold)
        public bool Away;           // on notice or on a course: not to be moved
    }

    /// <summary>What a place needs kept: the builders and designers it has now, and what its work in hand asks of them.</summary>
    public sealed class PlaceFloor
    {
        public string Place = string.Empty, Name = string.Empty;
        public int Builders, Designers;   // engineers (FirmsRules.Builds) and designers at work there now
        public int Building;              // products and games being built there
        public int NeedBuilders, NeedDesigners;   // active contracts that need engineers, and those that need designers (branding)
    }

    /// <summary>What the player's own cloud needs kept across all their firms: the staff it was opened on.</summary>
    public struct CityFloor
    {
        public bool CloudOpen, Hyper;
        public int Reliability;   // site reliability and DevOps people (InfraCatalogue.CloudEngineersLeast are wanted while the cloud is open)
        public int Chiefs;        // chief technology officers
        public int StaffInfra;    // infrastructure people of staff level or above (a hyperscaler wants a chief and InfraCatalogue.HyperStaffInfra of them)
    }

    /// <summary>What a bulk role change comes to: who moves, and who was left out and why.</summary>
    public sealed class RoleMovePlan
    {
        public const int WhyProduct = 0, WhyContract = 1, WhyBranding = 2, WhyCloud = 3, WhyHyper = 4, Whys = 5;

        public int To = -1, Asked;                       // the role they move to, and how many were asked for (0: all who can)
        public readonly List<string> Ids = new List<string>(16);   // who moves, best suited first
        public int Able;                                 // those who could move: in another role, able to hold this one, not on notice or a course
        public int Already, Away, CannotHold;            // left out: already in the role, on notice or a course, hired under a skill that cannot hold it
        public int Refused;                              // left out because moving them would break something (<see cref="Why"/>)
        public readonly int[] Why = new int[Whys];       // ...by what it would break
        public readonly string[] WhyPlace = new string[Whys];   // the first place each reason stopped somebody at (its name), for the text that names it
        public int Moved => Ids.Count;
        public int Left => Able - Moved - Refused;       // able and not moved because the count asked for was reached

        public void Clear()
        {
            To = -1;
            Asked = Able = Already = Away = CannotHold = Refused = 0;
            Ids.Clear();
            for (int i = 0; i < Whys; i++)
            {
                Why[i] = 0;
                WhyPlace[i] = null;
            }
        }
    }

    /// <summary>
    /// Moving several professionals to another role at once (Team page, and the manager's balancing): who goes first, and what may not be moved. A move is the single
    /// change's own (TechDirector.SetRole: the role and a level kept inside the new role's, no fee and no days), so the bulk change gains nothing a hundred single ones would
    /// not: it only chooses the people. They are chosen by how well they fit the new role, by the skills the code knows (the scores of the specialisms the old and new
    /// roles share), of equal fit the newest first, the id last so every machine picks the same. A move is refused where it would newly break something running: the last
    /// builder of a product being built or of a contract being worked, the last designer of a branding contract, the site reliability staff of an open cloud, a hyperscaler's
    /// chief or staff. Game-free: EconomyCheck runs it. Cost: a sort of the candidates and a pass of them, when a page is written and once more when the move is made.
    /// </summary>
    public static class RoleMoveRules
    {
        // The places' and the cloud's needs are held as bits, so a move is refused only for what it would newly break: a place already short is not made a reason to refuse.
        private const int BitProduct = 1, BitContract = 2, BitBranding = 4, BitCloud = 8, BitHyper = 16;

        /// <summary>How well a professional of one role would fit another, by the skills the code knows: the scores they hold in the specialisms the two roles share (thousandths).</summary>
        public static int Fit(RoleDef from, int a, int b, RoleDef to)
        {
            if (from == null || to == null)
            {
                return 0;
            }
            int s = 0;
            s += to.SkillA == from.SkillA ? a : to.SkillA == from.SkillB ? b : 0;
            s += to.SkillB == from.SkillA ? a : to.SkillB == from.SkillB ? b : 0;
            return s;
        }

        /// <summary>The order people go in: best fit, then the newest, then the id (a total order, so the same on every machine).</summary>
        public static int Order(in MoveFolk x, int fitX, in MoveFolk y, int fitY)
        {
            if (fitX != fitY)
            {
                return fitY.CompareTo(fitX);
            }
            if (x.Joined != y.Joined)
            {
                return y.Joined.CompareTo(x.Joined);
            }
            return string.CompareOrdinal(x.Id, y.Id);
        }

        /// <summary>
        /// The people a move to <paramref name="to"/> takes: <paramref name="asked"/> of them (0: every one who can), best suited first, refusing a move that would newly break
        /// what a place or the cloud needs, as <paramref name="floors"/> and <paramref name="city"/> read it (they are copied: the caller's stay as they were). Fills
        /// <paramref name="plan"/>.
        /// </summary>
        public static void Plan(IList<MoveFolk> people, int to, int asked, IList<PlaceFloor> floors, CityFloor city, RoleMovePlan plan)
        {
            plan.Clear();
            plan.To = TalentCatalogue.Clamp(to, 0, TalentCatalogue.RoleCount - 1);
            plan.Asked = Math.Max(0, asked);
            RoleDef target = TalentCatalogue.Role(plan.To);
            var able = new List<int>(people.Count);
            for (int i = 0; i < people.Count; i++)
            {
                MoveFolk p = people[i];
                if (p.Role == plan.To)
                {
                    plan.Already++;
                }
                else if (p.Away)
                {
                    plan.Away++;
                }
                else if (!p.Holds)
                {
                    plan.CannotHold++;
                }
                else
                {
                    able.Add(i);
                }
            }
            plan.Able = able.Count;
            var fit = new int[people.Count];
            for (int k = 0; k < able.Count; k++)
            {
                MoveFolk p = people[able[k]];
                fit[able[k]] = Fit(TalentCatalogue.Role(p.Role), p.A, p.B, target);
            }
            able.Sort((x, y) => Order(people[x], fit[x], people[y], fit[y]));
            var places = new PlaceFloor[floors != null ? floors.Count : 0];
            for (int i = 0; i < places.Length; i++)
            {
                PlaceFloor f = floors[i];
                places[i] = new PlaceFloor { Place = f.Place, Name = f.Name, Builders = f.Builders, Designers = f.Designers, Building = f.Building, NeedBuilders = f.NeedBuilders,
                    NeedDesigners = f.NeedDesigners };
            }
            int limit = plan.Asked > 0 ? plan.Asked : int.MaxValue;
            for (int k = 0; k < able.Count && plan.Ids.Count < limit; k++)
            {
                MoveFolk p = people[able[k]];
                PlaceFloor at = null;
                for (int i = 0; i < places.Length && at == null; i++)
                {
                    at = places[i].Place == p.Place ? places[i] : null;
                }
                RoleDef from = TalentCatalogue.Role(p.Role);
                int levelAfter = TalentCatalogue.Clamp(p.Level, target.LeastLevel, target.MostLevel);
                CityFloor after = city;
                int builders = at != null ? at.Builders : 0, designers = at != null ? at.Designers : 0;
                Count(from, p.Level, -1, ref builders, ref designers, ref after);
                Count(target, levelAfter, 1, ref builders, ref designers, ref after);
                int before = Broken(at, at != null ? at.Builders : 0, at != null ? at.Designers : 0, city);
                int now = Broken(at, builders, designers, after);
                int broke = now & ~before;
                if (broke != 0)
                {
                    int why = (broke & BitProduct) != 0 ? RoleMovePlan.WhyProduct
                        : (broke & BitContract) != 0 ? RoleMovePlan.WhyContract : (broke & BitBranding) != 0 ? RoleMovePlan.WhyBranding
                        : (broke & BitCloud) != 0 ? RoleMovePlan.WhyCloud : RoleMovePlan.WhyHyper;
                    plan.Refused++;
                    plan.Why[why]++;
                    plan.WhyPlace[why] = plan.WhyPlace[why] ?? (at != null ? at.Name : string.Empty);
                    continue;
                }
                if (at != null)
                {
                    at.Builders = builders;
                    at.Designers = designers;
                }
                city = after;
                plan.Ids.Add(p.Id);
            }
        }

        // One person of a role leaving (-1) or joining (+1) what the place and the cloud count.
        private static void Count(RoleDef r, int level, int sign, ref int builders, ref int designers, ref CityFloor city)
        {
            builders += FirmsRules.Builds(r) ? sign : 0;
            designers += r.Family == Family.Design ? sign : 0;
            city.Reliability += r.Id == TalentCatalogue.Sre || r.Id == TalentCatalogue.DevOps ? sign : 0;
            city.Chiefs += r.Id == TalentCatalogue.Cto ? sign : 0;
            city.StaffInfra += r.Family == Family.Infrastructure && level >= InfraCatalogue.HyperStaffLevel ? sign : 0;
        }

        // What is short, as bits.
        private static int Broken(PlaceFloor at, int builders, int designers, in CityFloor city)
        {
            int bits = 0;
            if (at != null)
            {
                bits |= at.Building > 0 && builders < 1 ? BitProduct : 0;
                bits |= at.NeedBuilders > 0 && builders < 1 ? BitContract : 0;
                bits |= at.NeedDesigners > 0 && designers < 1 ? BitBranding : 0;
            }
            bits |= city.CloudOpen && city.Reliability < InfraCatalogue.CloudEngineersLeast ? BitCloud : 0;
            bits |= city.Hyper && !InfraRules.HasChiefs(city.Chiefs > 0, city.StaffInfra) ? BitHyper : 0;
            return bits;
        }

        /// <summary>The key of the text that names why a move was refused.</summary>
        public static string WhyKey(int why)
        {
            switch (why)
            {
                case RoleMovePlan.WhyProduct: return "reallife_roles_move_why_product";
                case RoleMovePlan.WhyContract: return "reallife_roles_move_why_contract";
                case RoleMovePlan.WhyBranding: return "reallife_roles_move_why_branding";
                case RoleMovePlan.WhyCloud: return "reallife_roles_move_why_cloud";
                default: return "reallife_roles_move_why_hyper";
            }
        }

        /// <summary>The count a stepper shows: its words are "All" and then 1 up to how many can move; the number asked for is that word's index, 0 meaning all.</summary>
        public static int Asked(int stepAt, int able) => stepAt <= 0 ? 0 : Math.Min(stepAt, Math.Max(0, able));
    }
}
