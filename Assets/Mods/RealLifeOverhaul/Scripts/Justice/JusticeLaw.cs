using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    // Game-free: nothing in this file knows the game or the engine. Every figure a player might
    // want to tune is here, and every locale key is written out in full so the check can find it.

    public enum ChargeClass
    {
        Misdemeanour,
        Felony,
        Grave
    }

    /// <summary>One thing a person can be charged with, and what a conviction for it carries before anything is weighed.</summary>
    public sealed class ChargeDef
    {
        public string Id;
        public string NameKey;
        public ChargeClass Class;
        public int Fine;               // nominal; bounded against cash when imposed
        public int CommunityHours;
        public int ProbationDays;
        public int SuspensionDays;     // driving licence
        public int JailHours;          // station custody; never more than the custody cap in all
        public int PrisonDays;         // at the default cap of 7 days; scaled with the cap option
        public string LesserId;        // what a plea deal can reduce it to; null if nothing
    }

    /// <summary>What a court hands down. Parts that do not apply are 0.</summary>
    public struct JusticeSentence
    {
        public int Fine;               // after the bound against cash, before what was paid at the roadside is taken off
        public int FineNominal;        // what the table said, for the explanation
        public int CommunityHours;
        public int ProbationDays;
        public int SuspensionDays;
        public int JailHours;
        public int PrisonDays;
        public bool Bounded;           // the fine was cut to what the player can bear
        public bool PrisonCommuted;    // prison was called for and the "no prison" option turned it into custody and a fine
        public bool FirstOffence;      // custody was turned into community service
    }

    public static class JusticeLaw
    {
        // Ids used in code. Other features pass these to Justice.Arrest / Justice.Summon.
        public const string Evading = "evading";
        public const string Reckless = "reckless";
        public const string HitRun = "hit_run";
        public const string HitRunInjury = "hit_run_injury";
        public const string HitRunDeath = "hit_run_death";
        public const string InjuryDriving = "injury_driving";
        public const string Manslaughter = "manslaughter";
        public const string Unlicensed = "unlicensed_driving";
        public const string Assault = "assault";
        public const string Theft = "theft";
        public const string Robbery = "robbery";
        public const string Vandalism = "vandalism";
        public const string Resisting = "resisting";
        public const string NoShow = "failure_to_appear";
        public const string Violation = "probation_violation";
        public const string TaxEvasion = "tax_evasion";    // tax kept back and found by an audit (Justice/TaxCases.cs)
        public const string MarketAbuse = "market_abuse";  // shares sold soon after talking them up on social media (Justice/TaxCases.cs, FileMarketAbuse)
        public const string Bribery = "bribery";            // a bribe paid to an inspector that came out (Justice/TaxCases.cs, FileBribery)
        public const string DataProtection = "data_protection";   // a firm off the data protection register, or breached again, after a breach (Justice/TaxCases.cs, FileDataProtection)

        // Caps. Nothing a court does goes past these, whatever is added up.
        public const int CustodyCapHours = 48;         // the station's own limit (Law bridge): never held longer without a prison sentence
        public const string HoldRemand = "reallife_justice_hold_remand";      // why somebody is in the station's cell, for the Law bridge: until the court sits
        public const string HoldSentence = "reallife_justice_hold_sentence";  // ...or serving hours of custody
        public const int PrisonCapDefault = 7;         // in-game days; the option runs 3 to 14
        public const int PrisonCapMin = 3;
        public const int PrisonCapMax = 14;
        public const int CommunityCapHours = 24;
        public const int ProbationCapDays = 21;
        public const int SuspensionCapDays = 30;
        public static int FineCap => Prices.At(60000);              // absolute
        public static int FineFloor => Prices.At(250);              // a fine is never bounded below this
        public const float FineShareOfCash = 0.35f;    // a court's fines in one case: at most this share of the cash in hand
        public const float FeeShareOfCash = 0.30f;     // a lawyer's fee likewise
        public const float BailShareOfCash = 0.20f;
        public const float CivilShareOfCash = 0.20f;
        public const float PayNowShare = 0.15f;        // a fine up to this share of cash is paid at once; above it, a payment plan
        public const int FinePlanDays = 7;
        public const int FeePlanDays = 6;
        public const int CivilPlanDays = 10;
        public const int MissedInstalmentsMost = 5;    // after this many days it could not be paid, a debt is written off (fines: into community hours)
        public static int CashFloor => Prices.At(50);               // an instalment is never taken if it would leave less than this
        public static int WriteOffPerHour => Prices.At(150);        // an unpaid fine becomes one hour of community service per this much...
        public const int WriteOffHoursMost = 12;       // ...and never more than this many hours
        public const int CommunityShiftHours = 4;      // one played shift
        public const int CommunityShiftPieces = 8;
        public const int CommunityDeadlineDays = 7;
        public const int WarrantSelfArrestDays = 3;    // a bench warrant nobody has acted on is served at the player's door after this long
        public const int AppealWindowDays = 3;
        public const int SealAfterCleanDays = 30;
        public static int CompensationPerHour => Prices.At(150);    // wrongful arrest: per hour held
        public static int CompensationPerPrisonDay => Prices.At(600);
        public static int CompensationFlat => Prices.At(400);
        public static int PrisonWorkPay => Prices.At(40);           // a day on the work detail
        public const float DismissBelow = 0.34f;       // evidence weaker than this does not go to trial
        public const float CourtOpens = 9f;
        public const float CourtCloses = 17f;

        public static readonly ChargeDef[] All =
        {
            new ChargeDef { Id = Evading, NameKey = "reallife_justice_charge_evading", Class = ChargeClass.Misdemeanour,
                Fine = 1500, CommunityHours = 8, ProbationDays = 5, SuspensionDays = 3, JailHours = 12 },
            new ChargeDef { Id = Reckless, NameKey = "reallife_justice_charge_reckless", Class = ChargeClass.Misdemeanour,
                Fine = 1200, CommunityHours = 6, ProbationDays = 4, SuspensionDays = 3 },
            new ChargeDef { Id = HitRun, NameKey = "reallife_justice_charge_hit_run", Class = ChargeClass.Misdemeanour,
                Fine = 2500, CommunityHours = 8, ProbationDays = 6, SuspensionDays = 5, JailHours = 24 },
            new ChargeDef { Id = HitRunInjury, NameKey = "reallife_justice_charge_hit_run_injury", Class = ChargeClass.Felony,
                Fine = 8000, CommunityHours = 16, ProbationDays = 10, SuspensionDays = 10, JailHours = 48, PrisonDays = 3, LesserId = HitRun },
            new ChargeDef { Id = HitRunDeath, NameKey = "reallife_justice_charge_hit_run_death", Class = ChargeClass.Grave,
                Fine = 25000, ProbationDays = 14, SuspensionDays = 21, JailHours = 48, PrisonDays = 7, LesserId = Manslaughter },
            new ChargeDef { Id = InjuryDriving, NameKey = "reallife_justice_charge_injury_driving", Class = ChargeClass.Felony,
                Fine = 4000, CommunityHours = 12, ProbationDays = 7, SuspensionDays = 7, JailHours = 24, LesserId = Reckless },
            new ChargeDef { Id = Manslaughter, NameKey = "reallife_justice_charge_manslaughter", Class = ChargeClass.Grave,
                Fine = 15000, ProbationDays = 14, SuspensionDays = 14, JailHours = 48, PrisonDays = 5, LesserId = InjuryDriving },
            new ChargeDef { Id = Unlicensed, NameKey = "reallife_justice_charge_unlicensed_driving", Class = ChargeClass.Misdemeanour,
                Fine = 1500, CommunityHours = 8, ProbationDays = 5, SuspensionDays = 5 },
            new ChargeDef { Id = Assault, NameKey = "reallife_justice_charge_assault", Class = ChargeClass.Felony,
                Fine = 3000, CommunityHours = 12, ProbationDays = 7, JailHours = 24, PrisonDays = 2, LesserId = Resisting },
            new ChargeDef { Id = Theft, NameKey = "reallife_justice_charge_theft", Class = ChargeClass.Misdemeanour,
                Fine = 1000, CommunityHours = 8, ProbationDays = 5 },
            new ChargeDef { Id = Robbery, NameKey = "reallife_justice_charge_robbery", Class = ChargeClass.Felony,
                Fine = 8000, ProbationDays = 10, JailHours = 48, PrisonDays = 4, LesserId = Theft },
            new ChargeDef { Id = Vandalism, NameKey = "reallife_justice_charge_vandalism", Class = ChargeClass.Misdemeanour,
                Fine = 800, CommunityHours = 6, ProbationDays = 3 },
            new ChargeDef { Id = Resisting, NameKey = "reallife_justice_charge_resisting", Class = ChargeClass.Misdemeanour,
                Fine = 1200, CommunityHours = 6, ProbationDays = 4, JailHours = 12 },
            new ChargeDef { Id = NoShow, NameKey = "reallife_justice_charge_failure_to_appear", Class = ChargeClass.Misdemeanour,
                Fine = 1000, CommunityHours = 4, ProbationDays = 3, JailHours = 12 },
            new ChargeDef { Id = Violation, NameKey = "reallife_justice_charge_probation_violation", Class = ChargeClass.Misdemeanour,
                Fine = 1500, ProbationDays = 5, JailHours = 24 },
            new ChargeDef { Id = TaxEvasion, NameKey = "reallife_justice_charge_tax_evasion", Class = ChargeClass.Misdemeanour,
                Fine = 3000, CommunityHours = 12, ProbationDays = 7, JailHours = 24 },
            new ChargeDef { Id = MarketAbuse, NameKey = "reallife_justice_charge_market_abuse", Class = ChargeClass.Misdemeanour,
                Fine = 4000, CommunityHours = 8, ProbationDays = 0, JailHours = 0 },
            new ChargeDef { Id = Bribery, NameKey = "reallife_justice_charge_bribery", Class = ChargeClass.Misdemeanour,
                Fine = 5000, CommunityHours = 12, ProbationDays = 7, JailHours = 0 },
            new ChargeDef { Id = DataProtection, NameKey = "reallife_justice_charge_data_protection", Class = ChargeClass.Misdemeanour,
                Fine = 6000, CommunityHours = 8, ProbationDays = 0, JailHours = 0 }
        };

        public static ChargeDef Find(string id)
        {
            if (id != null)
            {
                for (int i = 0; i < All.Length; i++)
                {
                    if (All[i].Id == id)
                    {
                        return All[i];
                    }
                }
            }
            return null;
        }

        public static int ClampCap(int days)
        {
            return days < PrisonCapMin ? PrisonCapMin : days > PrisonCapMax ? PrisonCapMax : days;
        }

        // ---------------------------------------------------------------- evidence

        /// <summary>
        /// How strong the case is, 0.05 to 0.97, from what the reporting feature saw: nothing but an
        /// accusation is 0.30; each witness adds 0.12 (three at most), a camera 0.22, being caught at
        /// it 0.25, having run 0.06. <paramref name="nudge"/> moves one charge a little from the others.
        /// </summary>
        public static float Strength(int witnesses, bool cameras, bool inTheAct, bool fled, float nudge = 0f)
        {
            float strength = 0.30f + 0.12f * Math.Min(Math.Max(witnesses, 0), 3) + (cameras ? 0.22f : 0f) + (inTheAct ? 0.25f : 0f)
                             + (fled ? 0.06f : 0f) + nudge;
            return Clamp(strength, 0.05f, 0.97f);
        }

        /// <summary>How much a lawyer of this quality (0 to 1) takes off the case against their client, as a fraction.</summary>
        public static float CounselEffect(float quality)
        {
            return (Clamp(quality, 0f, 1f) - 0.2f) * 0.30f;
        }

        /// <summary>What prior convictions add, as a fraction: 0.04 each, 0.12 at most.</summary>
        public static float RecordEffect(int priors)
        {
            return 0.04f * Math.Min(Math.Max(priors, 0), 3);
        }

        /// <summary>
        /// The case against the accused on one charge, in thousandths: evidence, plus the record, less counsel,
        /// plus whatever the accused's own choices in court did (thousandths, either sign). Never below 30 or above 970.
        /// </summary>
        public static int CaseAgainst(float evidence, int priors, float quality, int choices)
        {
            int total = (int)Math.Round((evidence + RecordEffect(priors) - CounselEffect(quality)) * 1000f) + choices;
            return total < 30 ? 30 : total > 970 ? 970 : total;
        }

        // ---------------------------------------------------------------- lawyers

        public const float PublicDefenderQuality = 0.25f;
        public const float NoCounselQuality = 0.10f;

        /// <summary>A firm's quality from its stars (1 to 5): 0.35 to 0.83.</summary>
        public static float FirmQuality(int stars)
        {
            return 0.35f + 0.12f * (Math.Min(Math.Max(stars, 1), 5) - 1);
        }

        /// <summary>Representing yourself: 0.10 with no training in law, 0.60 at the game's top lawyer skill (100).</summary>
        public static float SelfQuality(float lawyerSkill)
        {
            return 0.10f + 0.5f * Clamp(lawyerSkill / 100f, 0f, 1f);
        }

        /// <summary>A firm's fee for a case of this weight, before the bound against cash: 600 / 1800 / 3500 times 0.8 (one star) to 2.0 (five).</summary>
        public static int FirmFee(ChargeClass weight, int stars)
        {
            int basis = weight == ChargeClass.Grave ? 3500 : weight == ChargeClass.Felony ? 1800 : 600;   // at the start's prices
            return Round10(Prices.At(basis * (0.5f + 0.3f * Math.Min(Math.Max(stars, 1), 5))));
        }

        /// <summary>An amount bounded to a share of cash, never below a floor (so that it is still something) and never above itself.</summary>
        public static int Bound(int nominal, float cash, float share, int floor)
        {
            int most = Math.Max(floor, (int)(Math.Max(cash, 0f) * share));
            return Round10(Math.Min(nominal, most));
        }

        // ---------------------------------------------------------------- bail

        public enum BailKind
        {
            OwnRecognisance,   // released on a promise to appear
            Bail,              // an amount, refunded on appearing
            Remand             // held until the hearing, which is brought forward
        }

        public static BailKind BailFor(ChargeClass weight, int priors, bool fled, bool onWarrant)
        {
            if (onWarrant || (weight == ChargeClass.Grave && (fled || priors >= 2)))
            {
                return BailKind.Remand;
            }
            return weight == ChargeClass.Misdemeanour && priors == 0 && !fled ? BailKind.OwnRecognisance : BailKind.Bail;
        }

        /// <summary>A quarter of the charges' fines (at today's prices), at least 200, at most a fifth of the cash in hand (but never under 200); the 200 at today's prices too.</summary>
        public static int BailAmount(int finesOfCharges, float cash)
        {
            int least = Prices.At(200);
            return Bound(Math.Max(least, finesOfCharges / 4), cash, BailShareOfCash, least);
        }

        /// <summary>Days from the arrest to the hearing.</summary>
        public static int DaysToCourt(ChargeClass weight)
        {
            return weight == ChargeClass.Grave ? 4 : weight == ChargeClass.Felony ? 3 : 2;
        }

        // ---------------------------------------------------------------- sentencing

        public const float GuiltyPleaFactor = 0.75f;
        public const float DealFactor = 0.60f;

        /// <summary>
        /// The sentence for a set of convictions. <paramref name="factor"/> is 1 after a trial, 0.75 for a guilty
        /// plea, 0.6 for a deal; <paramref name="mitigation"/> is taken off on top (0.15 for having complied with the
        /// police, 0.10 for remorse the court believed).
        /// </summary>
        public static JusticeSentence Sentence(IList<ChargeDef> convicted, float factor, float mitigation, int priors, bool onProbation,
            int prisonCap, bool noPrison, float cash)
        {
            var sentence = new JusticeSentence();
            if (convicted == null || convicted.Count == 0)
            {
                return sentence;
            }
            prisonCap = ClampCap(prisonCap);
            int priorsCounted = Math.Min(Math.Max(priors, 0), 3);
            float weight = factor * (1f - Clamp(mitigation, 0f, 0.3f));
            ChargeClass worst = ChargeClass.Misdemeanour;
            int fines = 0, hours = 0, probation = 0, suspension = 0, jail = 0, prison = 0, felonies = 0;
            for (int i = 0; i < convicted.Count; i++)
            {
                ChargeDef charge = convicted[i];
                fines += charge.Fine;
                hours += charge.CommunityHours;
                probation = Math.Max(probation, charge.ProbationDays);
                suspension = Math.Max(suspension, charge.SuspensionDays);
                jail = Math.Max(jail, charge.JailHours);
                prison = Math.Max(prison, charge.PrisonDays);
                if (charge.Class > worst)
                {
                    worst = charge.Class;
                }
                if (charge.Class != ChargeClass.Misdemeanour)
                {
                    felonies++;
                }
            }

            // Prison: the gravest charge's term, a day for each further felony, more for a record; a repeat offender
            // (two convictions already) goes to prison for any felony. All of it scaled to the cap and held under it.
            if (prison > 0 && felonies > 1)
            {
                prison += felonies - 1;
            }
            if (prison == 0 && felonies > 0 && priors >= 2)
            {
                prison = 2;
            }
            if (prison > 0)
            {
                float days = prison * (prisonCap / (float)PrisonCapDefault) * (1f + 0.25f * priorsCounted) * weight;
                prison = Math.Min(prisonCap, Math.Max(2, (int)Math.Round(days)));
                if (factor <= DealFactor && worst != ChargeClass.Grave)
                {
                    jail = CustodyCapHours;   // a deal on anything short of the gravest keeps the accused out of prison
                    prison = 0;
                }
            }

            float fine = fines * weight * (1f + 0.25f * priorsCounted) * (onProbation ? 1.5f : 1f);
            if (prison > 0 && noPrison)
            {
                sentence.PrisonCommuted = true;
                fine += 500f * prison;
                jail = CustodyCapHours;
                prison = 0;
            }

            fine *= Prices.Level / 1000f;   // the charges' fines are the start's: the sentence is at today's prices
            sentence.FineNominal = Round10(Math.Min(fine, FineCap));
            sentence.Fine = Bound(sentence.FineNominal, cash, FineShareOfCash, FineFloor);
            sentence.Bounded = sentence.Fine < sentence.FineNominal;
            sentence.PrisonDays = prison;
            if (prison > 0)
            {
                sentence.ProbationDays = Math.Min(ProbationCapDays, Math.Max(5, probation + 2 * priorsCounted));
            }
            else
            {
                jail = Math.Min(CustodyCapHours, (int)Math.Round(jail * weight) + (jail > 0 ? 12 * priorsCounted : 0));
                hours = (int)Math.Round(hours * weight);
                if (jail > 0 && priors == 0 && worst == ChargeClass.Misdemeanour)
                {
                    sentence.FirstOffence = true;   // a first minor conviction: the cell becomes hours of work
                    hours += jail / 3;
                    jail = 0;
                }
                sentence.JailHours = jail;
                sentence.CommunityHours = Math.Min(CommunityCapHours, hours);
                sentence.ProbationDays = Math.Min(ProbationCapDays, probation + 2 * priorsCounted);
            }
            sentence.SuspensionDays = Math.Min(SuspensionCapDays, suspension + (suspension > 0 ? suspension * priorsCounted / 2 : 0));
            return sentence;
        }

        // ---------------------------------------------------------------- aftermath

        /// <summary>The chance an appeal succeeds, 0.05 to 0.60: better the thinner the evidence was and the better the lawyer.</summary>
        public static float AppealChance(float evidence, float quality)
        {
            return Clamp(0.15f + (0.6f - evidence) * 0.5f + Clamp(quality, 0f, 1f) * 0.2f, 0.05f, 0.60f);
        }

        /// <summary>A tenth of the fine, at least 300 (at today's prices), bounded to 15 % of cash (but never under the least).</summary>
        public static int AppealFee(int fine, float cash)
        {
            int least = Prices.At(300);
            return Bound(Math.Max(least, fine / 10), cash, 0.15f, least);
        }

        /// <summary>What a victim sues for (the damages at today's prices, as the crash worked them out): bounded to a fifth of the cash in hand, never under 300 at today's prices.</summary>
        public static int CivilClaim(int damages, int injured, int killed, float cash)
        {
            int least = Prices.At(300);
            int nominal = damages / 2 + injured * Prices.At(3000) + killed * Prices.At(12000);
            return Bound(Math.Max(least, nominal), cash, CivilShareOfCash, least);
        }

        /// <summary>Good behaviour: one day off for every four points, never more than a third of the term.</summary>
        public static int Remission(int behaviour, int term)
        {
            return Math.Max(0, Math.Min(behaviour / 4, term / 3));
        }

        /// <summary>The day of a term (1-based) on whose morning the parole board sits: after two thirds are served. 0 for terms too short to have one.</summary>
        public static int ParoleDay(int term)
        {
            if (term < 3)
            {
                return 0;
            }
            int day = (term * 2 + 2) / 3 + 1;   // ceil(2/3 term) days served, then the next morning
            return day <= term ? day : 0;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A stable hash (FNV-1a): the same text gives the same number in every session, unlike string.GetHashCode.</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                if (text != null)
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        hash = (hash ^ text[i]) * 16777619;
                    }
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        /// <summary>A number from 0 to 999 that depends only on a seed and what it is for: a decision that reloading does not change.</summary>
        public static int Roll(int seed, int purpose)
        {
            unchecked
            {
                uint x = (uint)(seed * 73856093) ^ (uint)(purpose * 19349663) ^ 0x9e3779b9;
                x ^= x >> 15;
                x *= 0x2c1b3c6d;
                x ^= x >> 12;
                x *= 0x297a2d39;
                x ^= x >> 15;
                return (int)(x % 1000);
            }
        }

        public static int Round10(float amount)
        {
            return (int)Math.Round(amount / 10f) * 10;
        }

        public static float Clamp(float value, float least, float most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
