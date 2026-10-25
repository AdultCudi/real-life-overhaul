using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Rivals;
using Entities;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the hiring part reads from or does to the game: the recruitment office's campaigns and candidates, districts and rivals;
    /// candidates made and hired with the game's own calls; money as dated transactions at the business.
    /// </summary>
    internal static class HiringGame
    {
        public const string AdKey = "reallife_transaction_tech_recruitment_ad";
        public const string ChecksKey = "reallife_transaction_tech_recruitment_checks";
        public const string SearchKey = "reallife_transaction_tech_recruitment_search";
        private const string FullTime = "ba:jobdemand_fulltime";

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The districts the city's buildings stand in, as the game names them.</summary>
        public static void Districts(List<string> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                string n = GameRegistrations.District(all[i]);   // null for one whose building the city lacks: skipped, not a fault
                if (!string.IsNullOrEmpty(n) && !into.Contains(n) && n != "ba:neighborhood_global")
                {
                    into.Add(n);
                }
            }
        }

        public static string District(string district)
        {
            return string.IsNullOrEmpty(district) ? "-" : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        /// <summary>The companies that hire professionals: the game's own rivals by name, and the city's fictional tech firms.</summary>
        public static void Rivals(List<string> into)
        {
            into.Clear();
            try
            {
                if (RivalsHelper.IsFeatureEnabled)
                {
                    List<RivalData> all = RivalsHelper.GetAllRivalData();
                    for (int i = 0; all != null && i < all.Count && into.Count < 8; i++)
                    {
                        if (all[i] != null && !string.IsNullOrEmpty(all[i].rivalName))
                        {
                            into.Add(all[i].rivalName);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            into.AddRange(HiringCatalogue.RivalFirms);
        }

        /// <summary>The city's cycle as a number: 0 boom to 3 recession (the city's economy, steady while it is off).</summary>
        public static int Cycle => Economy.CityEconomy.Running ? (int)Economy.CityEconomy.State : 1;

        public static bool Summer(int day) => GameCalendar.SeasonOf(day) == Events.Season.Summer;

        /// <summary>The recruitment office's running campaigns for a professional skill at the player's businesses.</summary>
        public static void Campaigns(List<RecruitmentCampaign> into)
        {
            into.Clear();
            List<RecruitmentCampaign> all = Save != null ? Save.RecruitmentCampaigns : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                RecruitmentCampaign c = all[i];
                if (c != null && !c.finished && c.skillRequirement != null && TalentCatalogue.Professional(c.skillRequirement.skillName) && c.businessAddress != null
                    && c.agencyAddress != null && c.candidateFindTimes != null && c.candidateFindTimes.Count > 0)
                {
                    into.Add(c);
                }
            }
        }

        /// <summary>A campaign's key, the same while it runs.</summary>
        public static string KeyOf(RecruitmentCampaign c)
        {
            int first = int.MaxValue;
            for (int i = 0; i < c.candidateFindTimes.Count; i++)
            {
                first = Math.Min(first, c.candidateFindTimes[i].Day * 24 + c.candidateFindTimes[i].Hour);
            }
            return Place(c.agencyAddress) + "#" + Place(c.businessAddress) + "#" + c.skillRequirement.skillName + "#" + c.amountOfCandidates + "#" + first;
        }

        /// <summary>The first and last day a campaign finds candidates.</summary>
        public static void Days(RecruitmentCampaign c, out int from, out int to)
        {
            from = int.MaxValue;
            to = 0;
            for (int i = 0; i < c.candidateFindTimes.Count; i++)
            {
                from = Math.Min(from, c.candidateFindTimes[i].Day);
                to = Math.Max(to, c.candidateFindTimes[i].Day);
            }
        }

        public static string Place(Address a)
        {
            return a == null ? string.Empty : Uri.EscapeDataString(a.streetName ?? string.Empty) + "~" + a.streetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The candidates waiting in the game's MyEmployees app who came from a recruitment office, for a professional skill.</summary>
        public static void OfficeCandidates(List<EmployeeInstance> into)
        {
            into.Clear();
            List<EmployeeInstance> all = Save != null ? Save.CandidateEmployeeInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (e == null || e.candidateInfo == null || e.candidateInfo.fromJobBoard || e.candidateInfo.sourceAddress == null || e.characterData == null
                    || e.characterData.skills == null || e.characterData.skills.Count == 0 || !TalentCatalogue.Professional(e.characterData.skills[0].name))
                {
                    continue;
                }
                BuildingRegistration source = BuildingHelper.GetBuildingRegistration(e.candidateInfo.sourceAddress);
                if (source != null && source.businessTypeName == HiringCatalogue.AgencyType)
                {
                    into.Add(e);
                }
            }
        }

        /// <summary>A name and a sex for a new seeker, the game's own way (one no employee of the player's has).</summary>
        public static void Name(Seeker s)
        {
            try
            {
                (Gender gender, string name) = RecruitmentHelper.GetRandomGenderAndName();
                s.P.Name = name;
                s.P.Female = gender == Gender.Female;
            }
            catch (Exception)
            {
                s.P.Name = "?";
            }
        }

        /// <summary>A candidate in the game's MyEmployees app made from a seeker, as the office makes its own. Null if the game refused.</summary>
        public static EmployeeInstance Candidate(Seeker s, BuildingRegistration business, Address source)
        {
            try
            {
                RoleDef r = s.P.Def;
                if (!r.InGame || business == null)
                {
                    return null;
                }
                EmployeeInstance e = RecruitmentHelper.GenerateCandidate(r.GameSkill, Mathf.Clamp(s.P.Main / 1000f, 1f, 100f), business.Address, new List<string> { FullTime });
                if (e == null)
                {
                    return null;
                }
                e.candidateInfo.sourceAddress = source;
                if (!string.IsNullOrEmpty(s.P.Name) && s.P.Name != "?")
                {
                    e.characterData.name = s.P.Name;
                    e.characterData.gender = s.P.Female ? Gender.Female : Gender.Male;
                }
                if (s.P.Age >= 18 && Save != null && Save.gameVariables != null)
                {
                    e.characterData.ageInDays = Mathf.Min(s.P.Age, 60) * Mathf.Max(60, Save.gameVariables.daysPerYear);
                }
                return e;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        /// <summary>Hires a candidate the game's own way at the wage agreed, between the game's own for the skill and WageRules' cap.</summary>
        public static bool Hire(EmployeeInstance candidate, int cents)
        {
            try
            {
                EmployeeHelper.HireCandidate(candidate);
                candidate.hourlyWage = WageRules.UpTo(TalentGame.WageCents(candidate), cents, TalentGame.FormulaCents(candidate)) / 100f;
                candidate.satisfaction = Mathf.Max(candidate.satisfaction, 70f);
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        public static string Id(EmployeeInstance e) => e != null ? e.id : string.Empty;

        /// <summary>The player's headquarters, the first the game lists (the same on every machine), or null with none: it briefs a data centre's crew search.</summary>
        public static BuildingRegistration Headquarters()
        {
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && reg.RentedByPlayer && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.businessTypeName == HiringCatalogue.Headquarters && GameRegistrations.Found(reg))
                {
                    return reg;
                }
            }
            return null;
        }

        /// <summary>A recruiting cost of a business, cents: deductible, refused if the cash does not cover it.</summary>
        public static bool Pay(int cents, string key, BuildingRegistration at, string name)
        {
            return TalentGame.Cost(cents, key, at, name, false, false);
        }
    }
}
