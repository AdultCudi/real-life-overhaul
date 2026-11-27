using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>An evening's offer: who, by what facts, what the player chose and what they said back.</summary>
    public sealed class LoveOffer
    {
        public SocialPerson Person;
        public NightKind Kind;
        public NightFacts Facts;
        public int Chosen;          // 0 not answered, 1 go back to theirs, 2 come back to mine, 3 not tonight
        public bool Accepted;       // they said yes to the player's "come back to mine"
        public bool Refused;        // they said no to it
        public int Variant;         // which of the pool's lines it opened with
        public LoveHook Hook;       // what the night came to, once it has been

        /// <summary>The evening goes on: they asked and the player went, or the player asked and they said yes.</summary>
        public bool Night => Chosen == 1 || (Chosen == 2 && Accepted);
    }

    /// <summary>
    /// A night, in the game: who is asked, what drink the player has had, the offer's roll, the outcome's roll and what a night does to
    /// the two of them. The rules are LoveRules; every roll is seeded from the cast so the same evening gives the same answer on every
    /// machine. COST: a few reads and a roll when an evening ends, a night's settlement once.
    /// </summary>
    internal static class LoveNight
    {
        /// <summary>Drinks in the blood now (Leisure's own state), 0 when there is none or Leisure is off.</summary>
        public static float DrinksNow()
        {
            try
            {
                return Leisure.LeisureDirector.Drinks;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>Somebody an evening could be offered by: met, a friend or more, not family, not the partner, open to a romance with the player, adults both.</summary>
        public static bool Eligible(SocialPerson p)
        {
            if (p == null || !p.Met || p.IsFamily || p.Drifted || p.Together || p.Stage < SocialStage.Friend || !SocialLife.Romantic(p))
            {
                return false;
            }
            int player = SocialLife.PlayerYears();
            return SocialTalk.AgeOf(p) >= RomanceRules.AgeLeast && (player == 0 || player >= RomanceRules.AgeLeast);
        }

        public static NightFacts FactsOf(SocialPerson p, NightKind kind, bool wentWell, float drinks)
        {
            SocialPerson partner = SocialState.Partner;
            return new NightFacts
            {
                Kind = kind, WentWell = wentWell, Affection = p.Affection, Trust = p.Trust, Stage = p.Stage, Mood = p.Mood, Hour = SocialLife.Hour, Drinks = drinks,
                Taken = partner != null && partner != p, NightOwl = p.Has(SocialTrait.NightOwl), Flaky = p.Has(SocialTrait.Flaky), Careful = p.Has(SocialTrait.Careful),
                Reliable = p.Has(SocialTrait.Reliable), Homebody = p.Has(SocialTrait.Homebody), Supportive = p.Has(SocialTrait.Supportive), Envious = p.Has(SocialTrait.Envious),
                Generous = p.Has(SocialTrait.Generous), Together = p.Together
            };
        }

        /// <summary>
        /// Whether somebody offers to go on, after an evening that went well: the chance of LoveRules.OfferChance, a roll of the cast's own
        /// seed, once a day at the most and not again with the same person for a week. Null for no offer.
        /// </summary>
        public static LoveOffer Offer(SocialPerson p, NightKind kind, bool wentWell, float minDrinks)
        {
            if (!LoveHooks.HookupsOn || !LoveState.Loaded || !Eligible(p))
            {
                return null;
            }
            LoveFile f = LoveState.File;
            int day = SocialLife.Day, hour = SocialLife.Hour;
            if (f.LastOfferDay == day || (hour >= 5 && hour < 17))
            {
                return null;
            }
            LoveHook last = f.LastHook(p.Index);
            if (last != null && day - last.Day < LoveRules.OfferGapDays)
            {
                return null;
            }
            float drinks = Math.Max(DrinksNow(), minDrinks);
            NightFacts nf = FactsOf(p, kind, wentWell, drinks);
            int chance = LoveRules.OfferChance(nf);
            int roll = LoveRules.Roll(LoveState.Seed, day, p.Index * 31 + hour, LoveRules.SaltOffer);
            SocialDirector.Logger?.Info("Love check (offer): " + p.Name + " after " + kind + ": chance " + chance + " per mille (affection " + p.Affection.ToString("F0", CultureInfo.InvariantCulture)
                                        + ", drinks " + drinks.ToString("F1", CultureInfo.InvariantCulture) + ", hour " + hour + "), roll " + roll
                                                + (roll < chance ? ": offered" : ": no offer") + ": PASS");
            if (roll >= chance)
            {
                return null;
            }
            return new LoveOffer { Person = p, Kind = kind, Facts = nf, Variant = LoveRules.Variant(LoveState.Seed, day, p.Index, 6) };
        }

        /// <summary>The player's "come back to mine": whether they say yes, without a second thought the more they have had. Settled once, kept on the offer.</summary>
        public static void AskedBack(LoveOffer o)
        {
            int roll = LoveRules.Roll(LoveState.Seed, SocialLife.Day, o.Person.Index, LoveRules.SaltYes);
            o.Accepted = roll < LoveRules.YesChance(o.Facts);
            o.Refused = !o.Accepted;
        }

        /// <summary>
        /// The night itself, under a black screen: the outcome by a seeded roll weighed by the person, what is between them and the drink; the
        /// figures between them, the player's mood, a small cost for a mishap, a night that got around, an offer to date, and the chance of a child.
        /// </summary>
        public static LoveHook Settle(LoveOffer o)
        {
            SocialPerson p = o.Person;
            LoveFile f = LoveState.File;
            NightFacts nf = o.Facts;
            int day = SocialLife.Day, hour = SocialLife.Hour;
            var weights = new int[6];
            LoveRules.Weights(nf, weights);
            int roll = LoveRules.Roll(LoveState.Seed, day, p.Index * 31 + hour, LoveRules.SaltOutcome);
            NightOutcome outcome = LoveRules.Pick(roll, weights);
            var hook = new LoveHook
            {
                Person = p.Index, Day = day, Hour = hour, Outcome = outcome, Drinks = (int)(nf.Drinks * 10f), Taken = nf.Taken, Mine = o.Chosen == 2,
                TextDay = outcome == NightOutcome.Awkward || outcome == NightOutcome.Regret ? 0 : day + 1,
                QuietUntil = outcome == NightOutcome.Awkward ? day + LoveRules.QuietDays : 0, Around = outcome == NightOutcome.GotAround
            };
            f.RememberHook(hook);
            f.Nights++;
            f.LastOfferDay = day;
            LoveRules.Figures(outcome, out float fam, out float aff, out float trust);
            SocialLife.Change(p, fam, aff, trust);
            p.Shared++;
            SocialLife.Contacted(p);
            SocialLife.Remember(p, outcome == NightOutcome.Awkward ? MemoryCode.NightAwkward : outcome == NightOutcome.Regret ? MemoryCode.NightRegret : MemoryCode.Night);
            if (LoveRules.MoodLifts(outcome))
            {
                ModState.AddModifier(ModState.MoodUpBig);
            }
            else if (LoveRules.MoodDips(outcome, nf.Taken))
            {
                ModState.AddModifier(ModState.MoodDown);
            }
            if (outcome == NightOutcome.Mishap)
            {
                SocialLife.Pay(Prices.At(LoveRules.MishapCost(p.Index + day)), "reallife_transaction_love_mishap");
            }
            if (outcome == NightOutcome.GotAround)
            {
                f.AroundUntil = day + LoveRules.AroundDays;
                if (nf.Taken)
                {
                    f.Suspicion[0] = Math.Min(100, f.Suspicion[0] + LoveRules.SuspicionPerSign);
                }
            }
            if (outcome == NightOutcome.WantsMore)
            {
                f.DateOfferWho = p.Index;
                f.DateOfferDay = day;
            }
            LoveBabies.Conceived(p, nf, outcome, day);
            o.Hook = hook;
            SocialDirector.Logger?.Info("Love check (night): with " + p.Name + " at " + (hook.Mine ? "the player's" : "their") + " place: " + outcome + " (weights " + string.Join("/", weights)
                                        + ", roll " + roll + "), figures " + fam.ToString("F0", CultureInfo.InvariantCulture) + "/" + aff.ToString("F0", CultureInfo.InvariantCulture) + "/"
                                        + trust.ToString("F0", CultureInfo.InvariantCulture) + ": PASS");
            return hook;
        }

        /// <summary>A drinks outing counts as drinks: two, so a night after it is judged by them and Leisure's own tipsy and drink-driving rules see them too.</summary>
        public const float OutingDrinks = 2f;

        public static void Drink(float units)
        {
            try
            {
                if (Leisure.LeisureState.Loaded)
                {
                    Leisure.LeisureState.AddDrink(TimeHelper.NowInMinutes(), units);
                    if (Leisure.LeisureState.DrinksAt(TimeHelper.NowInMinutes()) >= Leisure.BarDrinks.TipsyFrom)
                    {
                        ModState.AddModifier(ModState.Tipsy);
                    }
                }
            }
            catch (Exception e)
            {
                SocialDirector.Logger?.Error(e);
            }
        }
    }
}
