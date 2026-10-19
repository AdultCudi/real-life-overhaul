using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using Localizor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>What there is to talk about now, from what really happened: the news, the player's shops and shifts, the weather, their day.</summary>
    public static class SocialTalk
    {
        private static readonly List<SocialTopic> Fitting = new List<SocialTopic>(20);
        private static readonly List<float> Weights = new List<float>(20);

        /// <summary>A topic that fits, never the one this person talked about last, with what its lines need filled in.</summary>
        public static SocialTopic Pick(SocialPerson person, out Dictionary<string, string> data)
        {
            Fitting.Clear();
            Weights.Clear();
            float total = 0f;
            foreach (SocialTopic topic in SocialCatalogue.Topics)
            {
                if (topic.Id == person.LastTopic)
                {
                    continue;
                }
                float weight = Weight(topic.Id, person);
                if (weight <= 0f)
                {
                    continue;
                }
                Fitting.Add(topic);
                Weights.Add(weight);
                total += weight;
            }
            SocialTopic picked = SocialCatalogue.Topics[SocialCatalogue.TopicQuiet];
            float roll = Random.value * total;
            for (int i = 0; i < Fitting.Count; i++)
            {
                roll -= Weights[i];
                if (roll <= 0f)
                {
                    picked = Fitting[i];
                    break;
                }
            }
            person.LastTopic = picked.Id;
            data = SocialLife.Data(person);
            Fill(picked.Id, person, data);
            return picked;
        }

        // Zero if nothing of the kind has happened.
        private static float Weight(int id, SocialPerson person)
        {
            switch (id)
            {
                case SocialCatalogue.TopicCrash: return News.Recent(NewsKind.PlayerCrash, 2, out _, out _) ? 4f : 0f;
                case SocialCatalogue.TopicArrest: return News.Recent(NewsKind.PlayerArrested, 3, out _, out _) ? 5f : 0f;
                case SocialCatalogue.TopicAccident: return News.Recent(NewsKind.Accident, 1, out _, out _) ? 3f : 0f;
                case SocialCatalogue.TopicCrime: return News.Recent(NewsKind.Crime, 1, out _, out _) ? 3f : 0f;
                case SocialCatalogue.TopicShopGood: return Shop(out _, out _, out float profit) && profit >= 0f ? 3.5f : 0f;
                case SocialCatalogue.TopicShopBad: return Shop(out _, out _, out float loss) && loss < 0f ? 3.5f : 0f;
                case SocialCatalogue.TopicWork: return JobDirector.EarnedToday > 0 ? 3.5f : 0f;
                case SocialCatalogue.TopicGoodDay: return person.Mood >= 1 ? 3f : 0f;
                case SocialCatalogue.TopicBadDay: return person.Mood <= -1 ? 3.5f : 0f;
                case SocialCatalogue.TopicTheirJob: return person.Job == SocialCatalogue.Jobs.Length - 1 ? 0f : 1f;
                case SocialCatalogue.TopicRain: return RainHelper.isRaining ? 1.5f : 0f;
                case SocialCatalogue.TopicFine: return !RainHelper.isRaining && SocialLife.Hour >= 8 && SocialLife.Hour < 19 ? 1f : 0f;
                case SocialCatalogue.TopicMemory: return LastOuting(person) != MemoryCode.None ? 2.5f : 0f;
                case SocialCatalogue.TopicBirthday: return BirthdayIn(person) >= 1 && BirthdayIn(person) <= 5 ? 4f : 0f;
                case SocialCatalogue.TopicRich:
                    return SaveGameManager.Current != null && SaveGameManager.Current.Money >= 50000f && person.Stage >= SocialStage.Friend ? 1.5f : 0f;
                case SocialCatalogue.TopicSick: return ModState.IsSick ? 5f : 0f;
                case SocialCatalogue.TopicBreakIn: return News.Recent(NewsKind.BreakIn, 3, out _, out _) ? 4f : 0f;
                case SocialCatalogue.TopicKnocked: return News.Recent(NewsKind.PlayerKnocked, 2, out _, out _) ? 4f : 0f;
                default: return 1f;   // the district, and quiet
            }
        }

        private static void Fill(int id, SocialPerson person, Dictionary<string, string> data)
        {
            Vector3 where;
            string detail;
            switch (id)
            {
                case SocialCatalogue.TopicCrash:
                    if (News.Recent(NewsKind.PlayerCrash, 2, out where, out _))
                    {
                        data["street"] = SocialPlaces.StreetAt(where);
                    }
                    break;
                case SocialCatalogue.TopicAccident:
                    if (News.Recent(NewsKind.Accident, 1, out where, out _))
                    {
                        data["street"] = SocialPlaces.StreetAt(where);
                    }
                    break;
                case SocialCatalogue.TopicCrime:
                    if (News.Recent(NewsKind.Crime, 1, out where, out detail))
                    {
                        data["street"] = SocialPlaces.StreetAt(where);
                        data["what"] = SocialCatalogue.CrimeNoun(detail).GetLocalization();
                    }
                    break;
                case SocialCatalogue.TopicShopGood:
                case SocialCatalogue.TopicShopBad:
                    if (Shop(out string shop, out float sales, out float profit))
                    {
                        data["shop"] = shop;
                        data["amount"] = SocialLife.Money(id == SocialCatalogue.TopicShopGood ? sales : -profit);
                    }
                    break;
                case SocialCatalogue.TopicWork:
                    data["amount"] = SocialLife.Money(JobDirector.EarnedToday);
                    break;
                case SocialCatalogue.TopicMemory:
                    data["outing"] = SocialLife.T(SocialCatalogue.MemoryName(LastOuting(person)));
                    break;
                case SocialCatalogue.TopicBirthday:
                    data["days"] = BirthdayIn(person).ToString();
                    break;
                case SocialCatalogue.TopicBreakIn:
                    if (News.Recent(NewsKind.BreakIn, 3, out _, out detail))
                    {
                        data["shop"] = detail ?? string.Empty;
                    }
                    break;
            }
            // A line never shows a bare placeholder.
            if (!data.ContainsKey("street"))
            {
                data["street"] = "reallife_news_street_unknown".GetLocalization();
            }
            if (!data.ContainsKey("shop"))
            {
                data["shop"] = "reallife_social_your_shop".GetLocalization();
            }
            if (!data.ContainsKey("amount"))
            {
                data["amount"] = string.Empty;
            }
        }

        /// <summary>The player's best shop of the last day the game closed the books on. False if none of theirs is trading.</summary>
        public static bool Shop(out string name, out float sales, out float profit)
        {
            name = null;
            sales = 0f;
            profit = 0f;
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || save.financialSummaries == null || save.financialSummaries.Count == 0)
                {
                    return false;
                }
                FinancialSummary summary = save.financialSummaries[save.financialSummaries.Count - 1];
                if (summary == null || summary.businessIncomeStatements == null || summary.dayNumber < save.Day - 2)
                {
                    return false;
                }
                FinancialSummary.BusinessIncomeStatement best = null;
                foreach (FinancialSummary.BusinessIncomeStatement statement in summary.businessIncomeStatements)
                {
                    if (statement != null && statement.Address != null && (best == null || statement.TotalSales > best.TotalSales))
                    {
                        best = statement;
                    }
                }
                if (best == null)
                {
                    return false;
                }
                BuildingRegistration registration = BuildingHelper.GetBuildingRegistration(best.Address);
                if (registration == null || !GoingPublic.Mine(registration) || string.IsNullOrEmpty(registration.BusinessName))
                {
                    return false;
                }
                name = registration.BusinessName;
                sales = best.TotalSales;
                profit = best.TotalProfit;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static MemoryCode LastOuting(SocialPerson person)
        {
            for (int i = person.Memories.Count - 1; i >= 0; i--)
            {
                MemoryCode code = person.Memories[i].Code;
                if (code >= MemoryCode.Coffee && code <= MemoryCode.Business)
                {
                    return code;
                }
            }
            return MemoryCode.None;
        }

        /// <summary>Days until their next birthday; 0 if it is today.</summary>
        public static int BirthdayIn(SocialPerson person)
        {
            int perYear = SocialPlaces.DaysPerYear;
            int today = (Mathf.Max(1, SocialLife.Day) - 1) % perYear + 1;
            return ((person.Birthday - today) % perYear + perYear) % perYear;
        }

        /// <summary>Their age today: a year more at each birthday since the game's first day (SocialRules.AgeOn).</summary>
        public static int AgeOf(SocialPerson person)
        {
            return SocialRules.AgeOn(person.Age, person.Birthday, SocialLife.Day, SocialPlaces.DaysPerYear);
        }

        /// <summary>What the answer and the first exchange of the day are worth, more face to face. Returns their reply's key.</summary>
        public static string Answer(SocialPerson person, SocialTopic topic, bool warm, bool inPerson, bool firstOfTalk)
        {
            bool firstOfDay = FirstOfDay(person, inPerson, firstOfTalk);
            SocialWorth worth = Worth(person, topic, warm, inPerson, firstOfTalk);
            if (firstOfDay)
            {
                ModState.AddModifier(person.IsFamily ? ModState.Family : ModState.Social);
            }
            if (!warm && !topic.BothFine && person.Affection + worth.Affection < 15f)
            {
                SocialLife.Remember(person, MemoryCode.Rude);
            }
            SocialLife.Contacted(person);
            if (!inPerson)
            {
                person.LastCallDay = SocialLife.Day;
            }
            SocialLife.Change(person, worth.Familiarity, worth.Affection, worth.Trust);
            return warm ? topic.ReactA : topic.ReactB;
        }

        /// <summary>What <see cref="Answer"/> would be worth now, changing nothing (SocialRules.Chat with this person's day).</summary>
        public static SocialWorth Worth(SocialPerson person, SocialTopic topic, bool warm, bool inPerson, bool firstOfTalk)
        {
            bool textedLately = person.LastTextDay > 0 && person.LastTextDay >= SocialLife.Day - 1;
            return SocialRules.Chat(FirstOfDay(person, inPerson, firstOfTalk), inPerson, textedLately, warm, topic.BothFine);
        }

        // The talk's first exchange counts the talk itself: on a call once a day, face to face every time.
        private static bool FirstOfDay(SocialPerson person, bool inPerson, bool firstOfTalk) => firstOfTalk && (person.LastCallDay != SocialLife.Day || inPerson);
    }
}
