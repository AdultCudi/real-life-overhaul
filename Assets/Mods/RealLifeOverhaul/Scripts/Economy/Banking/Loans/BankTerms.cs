using System;
using System.Collections.Generic;
using System.Text;
using Buildings;
using Helpers;
using SpecialServices.Bank;
using Streets;
using Tutorial.SideQuests;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One of the city's banks as a lender: where it is, what the game calls it, and its settings object.</summary>
    public sealed class Lender
    {
        public Address Address;
        public string Street;       // as the credit file writes it
        public int Number;
        public string Name;
        public BankSettings Settings;
        public int Rank;            // the bank's place by what it lends on its own terms, the largest 0 (BankRules.Rank); branches of one name share it
    }

    /// <summary>
    /// The city's banks, and their loan terms moved by the rating. A bank's dialogue reads its BankSettings afresh each time
    /// (the rate in LoanHelper, the cap in BankDialog.LoanRequest), so writing the two fields is enough: a cap of 0 makes the
    /// game itself refuse. The first values seen are kept for the session, so <see cref="Restore"/> is exact; nothing is saved.
    /// </summary>
    public static class BankTerms
    {
        internal const string TutorialLoanObjective = "tutorial_quest_establish_first_business_objective_1";   // the game's own
        internal const string BankruptcyQuest = "SQBankruptcy";   // the game's own side quest: SideQuestHelper.BankruptcyQuestId

        private struct Own
        {
            public float Cap;
            public int Rate;
        }

        // Kept across city loads on purpose: an object written to in one city is the same object in the next.
        private static readonly Dictionary<BankSettings, Own> Originals = new Dictionary<BankSettings, Own>();
        private static readonly List<Lender> List = new List<Lender>(8);
        private static readonly List<BankSettings> Unique = new List<BankSettings>(4);
        private static readonly List<Lender> Firsts = new List<Lender>(4);
        private static readonly List<string> RankNames = new List<string>(8);
        private static readonly List<float> RankCaps = new List<float>(8);
        private static readonly List<int> RankOrder = new List<int>(8);
        private static int[] _ranks = new int[8];

        public static IReadOnlyList<Lender> Lenders => List;

        /// <summary>One branch of each bank, by rank (the largest lender first): the banks the bank apps are for.</summary>
        public static IReadOnlyList<Lender> Banks => Firsts;

        /// <summary>True while the terms in memory are the rating's, not the banks' own.</summary>
        public static bool Applied { get; private set; }

        /// <summary>What every bank together would let the player owe on its own terms.</summary>
        public static float OwnCapTotal { get; private set; }

        /// <summary>The bank that holds the mod's accounts: the largest cap of its own, the first by name among equals. Null without a bank.</summary>
        public static Lender House { get; private set; }

        /// <summary>What is owed on business loans at the banks with these settings (the desk's): a bank's cap rises by it, so they do not use up unsecured lending.</summary>
        public static Func<BankSettings, float> Secured;

        /// <summary>Finds the city's banks (one look through the game's special buildings) and notes each one's own terms. Returns how many.</summary>
        public static int Find()
        {
            List.Clear();
            Unique.Clear();
            OwnCapTotal = 0f;
            foreach (KeyValuePair<Address, Building> pair in BuildingHelper.SpecialServiceBuildings)
            {
                Building building = pair.Value;
                var settings = building != null && building.SpecialService != null ? building.SpecialService.settings as BankSettings : null;
                if (settings == null || pair.Key == null)
                {
                    continue;
                }
                BuildingRegistration registration = BuildingHelper.GetBuildingRegistration(pair.Key);
                if (registration == null)
                {
                    continue;   // the game's daily run reads the registration's name: a bank without one is no lender
                }
                if (!Originals.ContainsKey(settings))
                {
                    Originals[settings] = new Own { Cap = settings.maxTotalLoanAmount, Rate = settings.annualInterestRate };
                }
                if (!Unique.Contains(settings))
                {
                    Unique.Add(settings);
                }
                List.Add(new Lender
                {
                    Address = pair.Key,
                    Street = CreditStore.Clean(pair.Key.streetName),
                    Number = pair.Key.streetNumber,
                    Name = string.IsNullOrEmpty(registration.BusinessName) ? pair.Key.ToFormattedString() : registration.BusinessName,
                    Settings = settings
                });
                OwnCapTotal += Mathf.Max(0f, Originals[settings].Cap);
            }
            List.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            RankBanks();
            House = null;
            for (int i = 0; i < List.Count; i++)
            {
                if (House == null || OwnCap(List[i].Settings) > OwnCap(House.Settings))
                {
                    House = List[i];
                }
            }
            return List.Count;
        }

        // Each branch's rank, and one branch of each bank in rank order, by the rule the offline check holds (BankRules.Rank).
        private static void RankBanks()
        {
            RankNames.Clear();
            RankCaps.Clear();
            Firsts.Clear();
            for (int i = 0; i < List.Count; i++)
            {
                RankNames.Add(List[i].Name);
                RankCaps.Add(OwnCap(List[i].Settings));
            }
            if (_ranks.Length < List.Count)
            {
                _ranks = new int[List.Count];
            }
            int banks = BankRules.Rank(RankNames, RankCaps, _ranks, RankOrder);
            for (int i = 0; i < List.Count; i++)
            {
                List[i].Rank = _ranks[i];
            }
            for (int r = 0; r < banks; r++)
            {
                for (int i = 0; i < List.Count; i++)
                {
                    if (List[i].Rank == r)
                    {
                        Firsts.Add(List[i]);
                        break;
                    }
                }
            }
        }

        /// <summary>The bank a branch at this address belongs to, as its first branch in <see cref="Banks"/>; null if the city has no bank there.</summary>
        public static Lender BankOf(string street, int number)
        {
            Lender branch = At(street, number);
            return branch != null && branch.Rank >= 0 && branch.Rank < Firsts.Count ? Firsts[branch.Rank] : null;
        }

        public static bool IsBank(Lender bank, string street, int number)
        {
            Lender branch = At(street, number);
            return bank != null && branch != null && branch.Rank == bank.Rank;
        }

        public static float OwnCap(BankSettings settings)
        {
            Own own;
            return settings != null && Originals.TryGetValue(settings, out own) ? own.Cap : 0f;
        }

        public static int OwnRate(BankSettings settings)
        {
            Own own;
            return settings != null && Originals.TryGetValue(settings, out own) ? own.Rate : 0;
        }

        /// <summary>One of the game's quest loans is open at this bank, so it keeps its own terms: the tutorial's at every bank, the bankruptcy quest's where offered.</summary>
        public static bool QuestHold(BankSettings settings)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || settings == null)
            {
                return false;
            }
            if (TutorialHelper.IsTutorialEnabled() && !save.CompletedQuestEntries.Contains(TutorialLoanObjective))
            {
                return true;
            }
            return settings.allowSideQuestEmergencyLoan && SideQuestHelper.IsSideQuestActive(BankruptcyQuest);
        }

        public static LenderTerms Wanted(BankSettings settings, int score)
        {
            Own own = Originals[settings];
            if (QuestHold(settings))
            {
                return new LenderTerms { Lends = true, Cap = own.Cap, AnnualRate = own.Rate };
            }
            LenderTerms terms = LendingTerms.For(score, own.Cap, own.Rate);
            if (terms.Lends && Secured != null)
            {
                terms.Cap += Mathf.Max(0f, Secured(settings));
            }
            return terms;
        }

        /// <summary>The lender at an address as the credit file writes one; null if the city has no bank there.</summary>
        public static Lender At(string street, int number)
        {
            for (int i = 0; i < List.Count; i++)
            {
                if (List[i].Number == number && string.Equals(List[i].Street, street, StringComparison.Ordinal))
                {
                    return List[i];
                }
            }
            return null;
        }

        /// <summary>Writes the terms for this rating on every bank, only where a value differs. Returns how many banks were changed.</summary>
        public static int Apply(int score)
        {
            int changed = 0;
            for (int i = 0; i < Unique.Count; i++)
            {
                BankSettings settings = Unique[i];
                if (settings == null)
                {
                    continue;   // the game has unloaded it
                }
                LenderTerms want = Wanted(settings, score);
                if (settings.maxTotalLoanAmount != want.Cap || settings.annualInterestRate != want.AnnualRate)
                {
                    settings.maxTotalLoanAmount = want.Cap;
                    settings.annualInterestRate = want.AnnualRate;
                    changed++;
                }
            }
            Applied = Unique.Count > 0;
            return changed;
        }

        /// <summary>Puts every bank's own terms back exactly, those of this city and any other object ever written to. Safe at any time.</summary>
        public static int Restore()
        {
            int restored = 0;
            List<BankSettings> dead = null;
            foreach (KeyValuePair<BankSettings, Own> pair in Originals)
            {
                BankSettings settings = pair.Key;
                if (settings == null)
                {
                    (dead ?? (dead = new List<BankSettings>())).Add(settings);
                    continue;
                }
                if (settings.maxTotalLoanAmount != pair.Value.Cap || settings.annualInterestRate != pair.Value.Rate)
                {
                    settings.maxTotalLoanAmount = pair.Value.Cap;
                    settings.annualInterestRate = pair.Value.Rate;
                    restored++;
                }
            }
            if (dead != null)
            {
                for (int i = 0; i < dead.Count; i++)
                {
                    Originals.Remove(dead[i]);   // unloaded by the game: loaded again, it has its own values
                }
            }
            Applied = false;
            return restored;
        }

        /// <summary>Every bank's terms in memory are what this rating says (or, with <paramref name="own"/>, the banks' own).</summary>
        public static bool Check(int score, bool own, out string detail)
        {
            bool ok = true;
            var text = new StringBuilder(160);
            for (int i = 0; i < Unique.Count; i++)
            {
                BankSettings settings = Unique[i];
                if (settings == null)
                {
                    continue;
                }
                Own mine = Originals[settings];
                LenderTerms want = own ? new LenderTerms { Cap = mine.Cap, AnnualRate = mine.Rate } : Wanted(settings, score);
                bool match = settings.maxTotalLoanAmount == want.Cap && settings.annualInterestRate == want.AnnualRate;
                ok &= match;
                text.Append(i > 0 ? "; " : string.Empty).Append(NameOf(settings)).Append(": own ").Append(mine.Cap.ToString("0")).Append(" at ").Append(mine.Rate)
                    .Append("%, in memory ").Append(settings.maxTotalLoanAmount.ToString("0")).Append(" at ").Append(settings.annualInterestRate).Append('%')
                    .Append(!own && QuestHold(settings) ? " (a quest loan is open: left alone)" : string.Empty)
                    .Append(match ? string.Empty : ", expected " + want.Cap.ToString("0") + " at " + want.AnnualRate + "%");
            }
            detail = Unique.Count == 0 ? "no bank found" : text.ToString();
            return ok;
        }

        private static string NameOf(BankSettings settings)
        {
            for (int i = 0; i < List.Count; i++)
            {
                if (List[i].Settings == settings)
                {
                    return List[i].Name;
                }
            }
            return settings.name;
        }

        /// <summary>The city has gone: its banks are forgotten, their own terms kept.</summary>
        public static void Forget()
        {
            List.Clear();
            Unique.Clear();
            Firsts.Clear();
            OwnCapTotal = 0f;
            House = null;
            Secured = null;
        }
    }
}
