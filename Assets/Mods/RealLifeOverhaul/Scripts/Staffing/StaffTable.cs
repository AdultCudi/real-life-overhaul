using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Staffing
{
    /// <summary>
    /// The reference of staffing: every business and workplace the mod adds or changes, the roles that run it and how each is
    /// had. Game-free. Every number in it is read from the mod's own catalogues and rules when it is made (nothing is written
    /// in a text), so the Guide, the game's help, the Discord guide and Uncle Fred all say what the game checks. The parts:
    /// StaffTable.Tech (data centres, the cloud and the tech firms), StaffTable.Work (the other businesses) and
    /// StaffTable.Roles (the roles and how each is had). Built once, on first use: a few thousand steps of arithmetic.
    /// </summary>
    public static partial class StaffTable
    {
        private static readonly List<string> WordKeys = new List<string>(64);
        private static StaffPlace[] _places;
        private static StaffRole[] _roles;

        /// <summary>The table's own names and phrases, by index (<see cref="FigureKind.Word"/>): text keys looked up in the reader's language.</summary>
        public static IReadOnlyList<string> Words
        {
            get
            {
                Ensure();
                return WordKeys;
            }
        }

        /// <summary>Every business and workplace, in the order they were written (the reference sorts them for the reader).</summary>
        public static StaffPlace[] Places
        {
            get
            {
                Ensure();
                return _places;
            }
        }

        /// <summary>Every role, professional or not.</summary>
        public static StaffRole[] Roles
        {
            get
            {
                Ensure();
                return _roles;
            }
        }

        private static void Ensure()
        {
            if (_places != null)
            {
                return;
            }
            var places = new List<StaffPlace>(40);
            TechPlaces(places);
            WorkPlaces(places);
            _roles = MakeRoles();
            _places = places.ToArray();
        }

        // ---------------------------------------------------------------- the builders every part writes with

        private static StaffFigure N(string name, long value) => new StaffFigure(name, value, FigureKind.Count);

        private static StaffFigure P(string name, long permille) => new StaffFigure(name, permille, FigureKind.Permille);

        private static StaffFigure M(string name, long cents) => new StaffFigure(name, cents, FigureKind.Money);

        private static StaffFigure X(string name, long thousandths) => new StaffFigure(name, thousandths, FigureKind.Times);

        private static StaffFigure T(string name, long tenths) => new StaffFigure(name, tenths, FigureKind.Tenths);

        private static StaffFigure Wd(string name, string key)
        {
            int at = WordKeys.IndexOf(key);
            if (at < 0)
            {
                at = WordKeys.Count;
                WordKeys.Add(key);
            }
            return new StaffFigure(name, at, FigureKind.Word);
        }

        private static StaffNote Note(string key, params StaffFigure[] figures) => new StaffNote(key, figures);

        private static StaffNote Links(string key, string[] roles, params StaffFigure[] figures) => new StaffNote(key, figures, roles);

        private static StaffLine Line(string[] roles, StaffNeed need, StaffNote does, StaffNote counts, StaffNote without, StaffRate rate = default, string unit = "")
        {
            return new StaffLine(roles, need, does, counts, without, rate, unit);
        }

        private static string[] Ids(params string[] ids) => ids;

        /// <summary>The ids of the talent catalogue's roles a test accepts.</summary>
        private static string[] RolesWhere(Func<RoleDef, bool> test)
        {
            var found = new List<string>(16);
            for (int i = 0; i < TalentCatalogue.RoleCount; i++)
            {
                RoleDef r = TalentCatalogue.Role(i);
                if (test(r))
                {
                    found.Add(r.Key);
                }
            }
            return found.ToArray();
        }

        private static string[] InFamily(Tech.Family family) => RolesWhere(r => r.Family == family);

        private static string KeyOf(int talentRole) => TalentCatalogue.Role(talentRole).Key;

        /// <summary>The level words, junior to principal, as Word figures' keys.</summary>
        private static string LevelWord(int level) => "reallife" + "_talent_level_" + TalentCatalogue.LevelKeys[TalentCatalogue.Clamp(level, 0, TalentCatalogue.Levels - 1)];
    }
}
