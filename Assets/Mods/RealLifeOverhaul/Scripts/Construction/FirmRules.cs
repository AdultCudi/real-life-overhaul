using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A crew the labour market offers this week: its kind, skill, foreman and day rate.</summary>
    public struct CrewOffer
    {
        public int Gang;
        public int Skill;
        public int Foreman;
        public int Rate;   // money a working day, at today's prices
    }

    /// <summary>
    /// The company: its reputation from what it hands over, the speciality's effect, the insurance's premium and what it
    /// pays, the crews the labour market offers and a rival's offer to one of them, the plant's resale and upkeep, and the
    /// role an employee of the office is given by their skill. Game-free.
    /// </summary>
    public static class FirmRules
    {
        /// <summary>A reputation moved by some points, kept from 0 to 100.</summary>
        public static int Moved(int reputation, int points)
        {
            return BuildCatalogue.Clamp(reputation + points, 0, 100);
        }

        /// <summary>
        /// What a handover does to the reputation: on time (or within a twentieth late) it rises, late it falls a point a
        /// tenth of the time over, a few points at most; very good work adds more.
        /// </summary>
        public static int HandoverPoints(int startDay, int dueDay, int handedDay, int quality)
        {
            int planned = Math.Max(1, dueDay - startDay);
            int late = handedDay - dueDay;
            int points;
            if (late <= 0)
            {
                points = BuildCatalogue.RepOnTime;
            }
            else if (late * 20 <= planned)
            {
                points = BuildCatalogue.RepSlightlyLate;
            }
            else
            {
                points = -Math.Min(BuildCatalogue.RepLateMost, BuildCatalogue.RepLateStep * (1 + late * 10 / planned));
            }
            if (quality >= BuildCatalogue.QualityRepFrom)
            {
                points += BuildCatalogue.RepQuality;
            }
            return points;
        }

        /// <summary>True when work of a kind is the company's speciality.</summary>
        public static bool Special(Speciality speciality, ProjectKind model, BuildUse use, int civicKind)
        {
            switch (speciality)
            {
                case Speciality.Homes:
                    return model == ProjectKind.Plot && use == BuildUse.Homes;
                case Speciality.Offices:
                    return model == ProjectKind.Plot && (use == BuildUse.Offices || use == BuildUse.Shops);
                case Speciality.Roads:
                    return model == ProjectKind.Contract && (civicKind == 0 || civicKind == 3);
                case Speciality.Renovation:
                    return model == ProjectKind.Rebuild || (model == ProjectKind.Contract && (civicKind == 1 || civicKind == 2));
                case Speciality.Demolition:
                    return model == ProjectKind.Rebuild;
                default:
                    return false;
            }
        }

        /// <summary>The speciality's lift to a stage's pace, in thousandths: on its own work, and demolition's on every first stage.</summary>
        public static int SpecialPace(Speciality speciality, ProjectKind model, BuildUse use, int civicKind, WorkStage stage)
        {
            if (speciality == Speciality.Demolition && stage == WorkStage.Ground)
            {
                return BuildCatalogue.SpecialPacePermille * 3;
            }
            return Special(speciality, model, use, civicKind) ? BuildCatalogue.SpecialPacePermille : 0;
        }

        /// <summary>A day's insurance: a share of the works under way, at least the floor, at today's prices; nothing without cover.</summary>
        public static long Premium(CoverLevel level, long underWay, int prices)
        {
            int l = BuildCatalogue.Clamp((int)level, 0, BuildCatalogue.CoverDayPerMillion.Length - 1);
            if (l == 0)
            {
                return 0L;
            }
            long day = Math.Max(0L, underWay) / 1000000L * BuildCatalogue.CoverDayPerMillion[l];
            return Math.Max((long)BuildCatalogue.CoverLeast * Math.Max(1, prices) / 1000L, day);
        }

        /// <summary>What the insurer pays of a loss: its share of the loss for a theft or a claim, the storm's only on full cover; less the excess.</summary>
        public static long Pays(CoverLevel level, long loss, bool claim, bool storm, int prices)
        {
            int l = BuildCatalogue.Clamp((int)level, 0, BuildCatalogue.CoverTheftPermille.Length - 1);
            long share = storm ? (BuildCatalogue.CoverStorm[l] ? 1000 : 0) : claim ? BuildCatalogue.CoverClaimPermille[l] : BuildCatalogue.CoverTheftPermille[l];
            long excess = (long)BuildCatalogue.CoverExcess * Math.Max(1, prices) / 1000L;
            return Math.Max(0L, Math.Max(0L, loss) * share / 1000L - excess);
        }

        /// <summary>One of the labour market's offers in a week: the same offer for the same save, week and slot.</summary>
        public static CrewOffer Offer(int seed, int week, int slot, int prices)
        {
            BuildRandom random = BuildRandom.Of(seed, week, 97 + slot * 7);
            int gang = random.Range(0, BuildCatalogue.GangCount);
            int skill = random.Range(BuildCatalogue.SkillLeast, BuildCatalogue.SkillMost + 1);
            int foreman = random.Range(0, BuildCatalogue.ForemanKeys.Length);
            return new CrewOffer { Gang = gang, Skill = skill, Foreman = foreman, Rate = PlansRules.CrewDay(gang, skill, prices) };
        }

        /// <summary>The week a day is in, from the save's first week.</summary>
        public static int Week(int day)
        {
            return Math.Max(0, day - 1) / 7;
        }

        /// <summary>
        /// Whether a rival offers a crew more this week: likelier for a skilled crew and a big rival; which rival, and how much
        /// more pay in thousandths.
        /// </summary>
        public static bool Poach(int seed, int crew, int week, int skill, out int rival, out int pay)
        {
            BuildRandom random = BuildRandom.Of(seed, crew, week * 11 + 101);
            rival = random.Range(0, BuildCatalogue.RivalCount);
            pay = random.Range(BuildCatalogue.PoachPayLeast, BuildCatalogue.PoachPayMost + 1);
            int chance = BuildCatalogue.PoachPermille * BuildCatalogue.Clamp(skill, 0, 100) / 60 * BuildCatalogue.RivalCrews[rival] / 5;
            return random.Chance(chance);
        }

        /// <summary>What a piece of plant fetches when sold: a share of its price, less for every month owned, never under the floor.</summary>
        public static long Resale(int price, int daysOwned)
        {
            int permille = Math.Max(BuildCatalogue.ResaleLeastPermille, BuildCatalogue.ResalePermille - Math.Max(0, daysOwned) / 30 * BuildCatalogue.ResaleLossPerMonth);
            return (long)Math.Max(0, price) * permille / 1000L;
        }

        /// <summary>A day's upkeep of a piece of plant owned.</summary>
        public static long Upkeep(int price)
        {
            return (long)Math.Max(0, price) * BuildCatalogue.UpkeepPermille / 10000L;
        }

        /// <summary>The role an employee of the office starts in, by their main skill in the game: designers draw, the client-facing run sites.</summary>
        public static StaffRole RoleFor(string skill)
        {
            switch (skill)
            {
                case "ba:skill_graphicdesigner":
                    return StaffRole.Architect;
                case "ba:skill_customerservice":
                    return StaffRole.SiteManager;
                default:
                    return StaffRole.None;
            }
        }
    }
}
