using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The kind of workstation a role is seated at in the building.</summary>
    public enum Workstation
    {
        None = 0,       // none: the person is on the mod's own payroll, not seated in the building
        Desk = 1,       // a desk computer (the game's desktop, gaming computer, computer or laptop) with its chair
        Register = 2    // a cash register: for a firm that sells over a counter, which no tech firm does
    }

    /// <summary>
    /// Which workstation each role is seated at in a tech firm, and which the firm can furnish. The game seats an employee at a
    /// workstation whose item lists one of the employee's skills (Item.suitableSkills), among the items the business can use
    /// (BuildingRegistration.GetAssignableItems: its type's primary skills and the building's). The game's desk computers list
    /// customer service for nobody and its cash register is sold to shops, not offices, so a sales, support or customer service
    /// hire at an office had no seat: the desks take the skill (<see cref="ModDesk"/>, put on the game's items by SeatGame) and
    /// every role has a desk. Game-free; EconomyCheck holds that every role of every kind of firm has a seat.
    /// Cost: a few comparisons when asked, on events; nothing per frame.
    /// </summary>
    public static class SeatRules
    {
        /// <summary>
        /// The skills of the mod's roles that the game's desk computers list as it ships (build 3682, read from the items: computer, desktop computer,
        /// laptop, gaming computer). They list others too (purchasing, logistics, pricing, travel and events), which no role of the mod's is hired under.
        /// </summary>
        public static readonly string[] GameDesk =
        {
            TalentCatalogue.Lawyer, TalentCatalogue.Programmer, TalentCatalogue.HrManager, TalentCatalogue.GraphicDesigner, TalentCatalogue.Headhunter
        };

        /// <summary>The skills the mod adds to the game's desk computers: the customer service, sales and support roles of the tech firms sit there too.</summary>
        public static readonly string[] ModDesk = { TalentCatalogue.CustomerService };

        // The game's skills a firm of each kind is hired under (its business type's employeePrimarySkills), in FirmKind's order.
        private static readonly string[][] Primary =
        {
            new[] { TalentCatalogue.Programmer, TalentCatalogue.GraphicDesigner, TalentCatalogue.CustomerService },
            new[] { TalentCatalogue.Programmer, TalentCatalogue.GraphicDesigner, TalentCatalogue.CustomerService },
            new[] { TalentCatalogue.Programmer, TalentCatalogue.CustomerService, TalentCatalogue.GraphicDesigner },
            new[] { TalentCatalogue.Programmer, TalentCatalogue.CustomerService },
            new[] { TalentCatalogue.Programmer, TalentCatalogue.CustomerService },
            new[] { TalentCatalogue.Programmer },          // the game's web development agency
            new[] { TalentCatalogue.GraphicDesigner }      // the game's graphic designer
        };

        /// <summary>The game skills a firm of a kind (<see cref="FirmKind"/>) is hired under. Empty for a kind this does not know.</summary>
        public static string[] PrimaryOf(int kind) => kind >= 0 && kind < Primary.Length ? Primary[kind] : Array.Empty<string>();

        /// <summary>
        /// True if the hour's seating gives a hire a desk and shifts: the business is a tech firm (<paramref name="kind"/> is not -1) and the hire
        /// holds a skill it is hired under. A lawyer or an HR manager at a software studio, an account manager at the game's web development agency,
        /// and anyone at a law firm, a headquarters or a bank are left to the player. The one question the start note, the hire form's line and the
        /// pass itself all ask, so what they promise is what the pass does.
        /// </summary>
        public static bool Seats(int kind, string skill) => !string.IsNullOrEmpty(skill) && Array.IndexOf(PrimaryOf(kind), skill) >= 0;

        /// <summary>The workstation a role is seated at: none for a role on the mod's payroll (no job of the game's), a desk for every other.</summary>
        public static Workstation Needs(RoleDef role) => role == null || !role.InGame ? Workstation.None : Workstation.Desk;

        /// <summary>True if a desk lists a skill: the game's own list and the mod's addition.</summary>
        public static bool DeskSeats(string skill)
        {
            return !string.IsNullOrEmpty(skill) && (Array.IndexOf(GameDesk, skill) >= 0 || Array.IndexOf(ModDesk, skill) >= 0);
        }

        /// <summary>A desk's skill list with the mod's desk skills added, or null if it lists them all already (nothing to change). The list given is not changed.</summary>
        public static string[] WithModDesk(string[] listed)
        {
            if (listed == null)
            {
                return null;
            }
            int missing = 0;
            for (int i = 0; i < ModDesk.Length; i++)
            {
                missing += Array.IndexOf(listed, ModDesk[i]) < 0 ? 1 : 0;
            }
            if (missing == 0)
            {
                return null;
            }
            var all = new string[listed.Length + missing];
            Array.Copy(listed, all, listed.Length);
            int at = listed.Length;
            for (int i = 0; i < ModDesk.Length; i++)
            {
                if (Array.IndexOf(listed, ModDesk[i]) < 0)
                {
                    all[at++] = ModDesk[i];
                }
            }
            return all;
        }

        /// <summary>
        /// Whether a firm of a kind can furnish a workstation: a desk when any skill it is hired under is one a desk lists (so the game offers the
        /// desk for its schedule); a register never, since a tech firm sells nothing over a counter and the game sells registers to shops.
        /// </summary>
        public static bool Furnishes(int kind, Workstation station)
        {
            switch (station)
            {
                case Workstation.None:
                    return true;
                case Workstation.Desk:
                    string[] hired = PrimaryOf(kind);
                    for (int i = 0; i < hired.Length; i++)
                    {
                        if (DeskSeats(hired[i]))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>True if a role hired at a firm of a kind has a seat there: no seat needed, or a workstation the firm furnishes that lists the role's skill.</summary>
        public static bool Seated(int kind, RoleDef role)
        {
            Workstation need = Needs(role);
            return need == Workstation.None || (Furnishes(kind, need) && (need != Workstation.Desk || DeskSeats(role.GameSkill)));
        }

        /// <summary>The first role (by id) with no seat at a firm of a kind, or -1 if every role has one.</summary>
        public static int FirstUnseated(int kind)
        {
            for (int i = 0; i < TalentCatalogue.Roles.Length; i++)
            {
                if (!Seated(kind, TalentCatalogue.Roles[i]))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
