using System;
using System.Collections.Generic;
using BigAmbitions.Characters;
using Entities;
using Helpers;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the names read from or do to the game: the names its hired staff and waiting candidates have now, a new name no one
    /// has (<see cref="StaffNameRules"/>), and a second bearer of a name renamed. Event-time only (a hire, a day, a load).
    /// </summary>
    internal static class StaffNamesGame
    {
        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The names of every hired employee in the save and of every candidate waiting, the other players' too in a Going Public session.</summary>
        public static void Names(HashSet<string> into)
        {
            GameInstance save = Save;
            AddAll(into, save != null ? save.EmployeeInstances : null);
            AddAll(into, save != null ? save.CandidateEmployeeInstances : null);
        }

        private static void AddAll(HashSet<string> into, List<EmployeeInstance> all)
        {
            for (int i = 0; all != null && i < all.Count; i++)
            {
                string name = all[i] != null && all[i].characterData != null ? all[i].characterData.name : null;
                if (StaffNameRules.IsName(name))
                {
                    into.Add(name.Trim());
                }
            }
        }

        /// <summary>
        /// A name and a sex made the game's own way and checked against <paramref name="taken"/>, which then holds it. The game
        /// remembers every name it rolls, so the ones passed over are not made again.
        /// </summary>
        public static string Fresh(HashSet<string> taken, out bool female)
        {
            Gender gender = Gender.Male;
            string name = StaffNameRules.Fresh(() =>
            {
                (Gender g, string n) = RecruitmentHelper.GetRandomGenderAndName();
                gender = g;
                return n;
            }, taken);
            female = gender == Gender.Female;
            return name;
        }

        /// <summary>A new name for somebody who keeps their sex.</summary>
        public static string FreshFor(HashSet<string> taken, bool female)
        {
            Gender gender = female ? Gender.Female : Gender.Male;
            return StaffNameRules.Fresh(() => CharacterNames.GetRandomNameForGender(gender), taken);
        }

        /// <summary>The player's hired employees in the order the save lists them (hiring order), and whether each is the player's to rename.</summary>
        public static void Hired(List<EmployeeInstance> into, List<bool> mine)
        {
            into.Clear();
            mine.Clear();
            List<EmployeeInstance> all = Save != null ? Save.EmployeeInstances : null;
            bool session = GoingPublic.InSession;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (e == null || e.characterData == null)
                {
                    continue;
                }
                into.Add(e);
                mine.Add(!session || !GoingPublic.Foreign(TalentGame.Business(e)));
            }
        }

        /// <summary>The candidates waiting in the MyEmployees app; none in a Going Public session, where they are not the mod's to rename.</summary>
        public static void Candidates(List<EmployeeInstance> into)
        {
            into.Clear();
            List<EmployeeInstance> all = Save != null ? Save.CandidateEmployeeInstances : null;
            for (int i = 0; !GoingPublic.InSession && all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].characterData != null)
                {
                    into.Add(all[i]);
                }
            }
        }

        public static bool Female(EmployeeInstance e) => e.characterData.gender == Gender.Female;

        public static string Name(EmployeeInstance e) => e.characterData.name;

        public static void Rename(EmployeeInstance e, string name)
        {
            e.characterData.name = name;
        }
    }
}
