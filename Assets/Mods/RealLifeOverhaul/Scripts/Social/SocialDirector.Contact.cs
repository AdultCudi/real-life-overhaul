using System.Collections.Generic;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The social director's part for who gets in touch, each hour, and the round of the day.</summary>
    public sealed partial class SocialDirector
    {
        private static readonly float[] WealthTiers = { 10000f, 50000f, 250000f, 1000000f, 5000000f };   // money they react to

        private bool _reactArrest;
        private int _reactedCrashDay, _reactedBreakInDay;

        internal void OnArrested()
        {
            if (_broken || !SocialState.Loaded || !Settings.Social || SocialState.LastArrestDay == SocialLife.Day)
            {
                return;
            }
            SocialState.LastArrestDay = SocialLife.Day;
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson person = SocialState.Get(i);
                if (person == null || !person.Met)
                {
                    continue;
                }
                // Word gets round. Family worries more than it judges; a supportive friend hardly minds.
                float trust = person.IsFamily ? -2f : person.Has(SocialTrait.Supportive) ? -1f : SocialRules.ArrestTrust;
                SocialLife.Remember(person, MemoryCode.Arrest);
                SocialLife.Change(person, 0f, 0f, trust);
            }
            _reactArrest = true;
        }

        private void Hourly(float real, int day, int hour, bool skipping)
        {
            News.NoteWeather();
            if (skipping || !Settings.SocialContact || SocialState.MetCount == 0 || !SocialLife.MayText())
            {
                return;
            }
            // Something that happened to the player comes first.
            if (_reactArrest)
            {
                _reactArrest = false;
                SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
                SocialPerson who = mom != null && Random.value < 0.5f ? mom : Contactor(hour);
                if (who != null)
                {
                    SocialLife.SendText(who, SocialLines.Pick(who.IsFamily ? SocialCatalogue.TextArrestMom
                        : who.Has(SocialTrait.Supportive) || who.Together ? SocialCatalogue.TextArrestKind : SocialCatalogue.TextArrestStern), SocialLife.Data(who));
                }
                return;
            }
            if (_reactedCrashDay != day && News.Recent(NewsKind.PlayerCrash, 0, out Vector3 where, out _))
            {
                _reactedCrashDay = day;
                SocialPerson who = Contactor(hour);
                if (who != null && who.Stage >= SocialStage.Friend)
                {
                    SocialLife.SendText(who, SocialLines.Pick(SocialCatalogue.TextCrash), SocialLife.Data(who, "street", SocialPlaces.StreetAt(where)));
                    return;
                }
            }
            if (_reactedBreakInDay != day && News.Recent(NewsKind.BreakIn, 0, out _, out string shop))
            {
                _reactedBreakInDay = day;
                SocialPerson who = Contactor(hour);
                if (who != null && who.Stage >= SocialStage.Friend)
                {
                    SocialLife.SendText(who, SocialLines.Pick(SocialCatalogue.TextBreakIn), SocialLife.Data(who, "shop", shop ?? SocialLife.T("reallife_social_your_shop")));
                    return;
                }
            }
            if (Random.value > 0.09f + 0.02f * SocialState.MetCount)
            {
                return;
            }
            SocialPerson person = Contactor(hour);
            if (person == null)
            {
                return;
            }
            bool free = !SocialState.Offer.Active && !SocialState.Plan.Active;
            float roll = Random.value;
            if (free && Settings.SocialOutings && roll < 0.38f)
            {
                Invite(person, real);
            }
            else if (free && roll < 0.52f && person.Trust >= 20f)
            {
                AskFavour(person, real, -1);
            }
            else if (roll < 0.58f && person.Stage >= SocialStage.Friend && Settings.SocialMeetings && day - SocialState.LastMeetDay >= 2 && NextStranger() != null
                     && _introStranger == null)
            {
                SocialPerson stranger = NextStranger();
                if (SocialLife.SendText(person, SocialLines.Pick(SocialCatalogue.TextIntro), SocialLife.Data(person, "other", stranger.Name)))
                {
                    QueueIntroduction(person, stranger);
                }
            }
            else
            {
                string[] pool = person.MovedIn ? SocialCatalogue.TextHome : person.Together ? SocialCatalogue.TextLove
                    : person.Mood <= -1 ? SocialCatalogue.TextBadDay : SocialCatalogue.TextNews;
                SocialLife.SendText(person, SocialLines.Pick(pool), SocialLife.Data(person));
            }
        }

        // Somebody awake who has not texted today; the closer, the likelier.
        private static SocialPerson Contactor(int hour)
        {
            float total = 0f;
            int day = SocialLife.Day;
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && p.Met && !p.Drifted && !p.Asleep(hour) && p.LastTextDay != day)
                {
                    total += Keenness(p);
                }
            }
            if (total <= 0f)
            {
                return null;
            }
            float roll = Random.value * total;
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && p.Met && !p.Drifted && !p.Asleep(hour) && p.LastTextDay != day)
                {
                    roll -= Keenness(p);
                    if (roll <= 0f)
                    {
                        return p;
                    }
                }
            }
            return null;
        }

        private static float Keenness(SocialPerson p)
        {
            return 1f + p.Closeness / 25f + (p.Together ? 3f : 0f);
        }

        // Only what they have a free hour for in the days ahead (the call that answers it looks again, PersonDialog.Asked).
        private void Invite(SocialPerson person, float real)
        {
            PlanKind kind = person.Wants;
            OutingDef def = SocialCatalogue.Outing(kind);
            if (person.Stage < def.Needs && !person.IsFamily)
            {
                kind = PlanKind.Coffee;
                def = SocialCatalogue.Outing(kind);
            }
            if (SocialLife.NextSlot(person, kind, SocialLife.Now + SocialRules.PlanLead) < 0f)
            {
                return;
            }
            SocialOffer offer = SocialState.Offer;
            offer.Person = person.Index;
            offer.Kind = (int)kind;
            offer.Amount = 0;
            offer.Expires = SocialLife.Now + 36f * 60f;
            // Somebody close may ring; a text is answered by calling them.
            if (person.Closeness >= 40 && Random.value < 0.3f && StartRing(person, real))
            {
                return;
            }
            if (!SocialLife.SendText(person, def.Invite, SocialLife.Data(person)))
            {
                offer.Clear();
            }
        }

        // Money or help moving only of a friend. which: 0 a lift, 1 a loan, 2 a move, -1 at random.
        private void AskFavour(SocialPerson person, float real, int which)
        {
            bool friend = person.Stage >= SocialStage.Friend;
            bool canCarry = ClipLibrary.Has("carry_box_hold") && PropLibrary.Ready;
            int kind = which >= 0 ? which : Random.Range(0, friend ? 3 : 1);
            if (kind == 2 && !canCarry)
            {
                kind = 0;
            }
            SocialOffer offer = SocialState.Offer;
            offer.Person = person.Index;
            offer.Expires = SocialLife.Now + 6f * 60f;
            offer.Amount = 0;
            string[] text;
            if (kind == 1)
            {
                float money = SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;
                offer.Kind = SocialOffer.Loan;
                offer.Amount = Mathf.Clamp(Mathf.RoundToInt(money * 0.02f / 10f) * 10, Prices.At(40), Prices.At(400));
                offer.Expires = SocialLife.Now + 24f * 60f;
                text = SocialCatalogue.TextLoanAsk;
            }
            else if (kind == 2)
            {
                offer.Kind = (int)PlanKind.Moving;
                offer.Expires = SocialLife.Now + 24f * 60f;
                text = SocialCatalogue.TextMovingAsk;
            }
            else
            {
                offer.Kind = (int)PlanKind.Lift;
                text = SocialCatalogue.TextLiftAsk;
            }
            if (Random.value < 0.4f && StartRing(person, real))
            {
                return;
            }
            Dictionary<string, string> data = SocialLife.Data(person, "amount", SocialLife.Money(offer.Amount));
            if (!SocialLife.SendText(person, SocialLines.Pick(text), data, which >= 0))
            {
                offer.Clear();
            }
        }

        private void Daily(int day)
        {
            int perYear = SocialPlaces.DaysPerYear;
            int today = (Mathf.Max(1, day) - 1) % perYear + 1;
            int yesterday = (Mathf.Max(2, day) - 2) % perYear + 1;
            SocialState.Day = day;
            SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
            bool reminded = false;
            ISocialLaw law = SocialLinks.Law;
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p == null || !p.Met)
                {
                    continue;
                }
                // Mostly fine, now and then rough or grand; a birthday is a good day.
                float roll = Random.value;
                p.Mood = roll < 0.08f ? -2 : roll < 0.25f ? -1 : roll < 0.65f ? 0 : roll < 0.9f ? 1 : 2;
                if (p.Birthday == today)
                {
                    p.Mood = 2;
                }
                if (day - p.WantsDay >= 3)
                {
                    PlanKind wants = p.Wants;
                    float best = 0f;
                    for (int k = 0; k <= (int)PlanKind.Business; k++)
                    {
                        var kind = (PlanKind)k;
                        if (kind == p.Wants || p.Stage < SocialCatalogue.Outing(kind).Needs)
                        {
                            continue;
                        }
                        float score = SocialRules.Liking(p, kind) * Random.Range(0.6f, 1.4f);
                        if (score > best)
                        {
                            best = score;
                            wants = kind;
                        }
                    }
                    p.Wants = wants;
                    p.WantsDay = day;
                }

                if (Settings.SocialRelationships)
                {
                    if (SocialRules.Decay(p, day))
                    {
                        SocialLife.Restage(p);
                    }
                    if (!p.Drifted && !p.IsFamily && p.Stage == SocialStage.Acquaintance && p.Familiarity < SocialRules.DriftFamiliarity && day - p.LastContactDay > 10)
                    {
                        p.Drifted = true;
                        SocialLife.Toast(NotificationType.Info, "reallife_social_drifted", SocialLife.Data(p), 9f);
                        Logger?.Info("Social: " + p.Name + " has drifted away.");
                    }
                    else if (!p.Drifted && p.Stage >= SocialStage.Friend && day - p.LastContactDay == SocialRules.Grace(p) + 2)
                    {
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextDrift), SocialLife.Data(p));
                    }
                    if (p.Birthday == yesterday && day > 1 && p.MetDay < day - 1 && p.Stage >= SocialStage.Friend && p.BirthdayYear != (day - 2) / perYear)
                    {
                        p.BirthdayYear = (day - 2) / perYear;
                        SocialLife.Change(p, 0f, SocialRules.BirthdayMissed, 0f);
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextBirthdayMissed), SocialLife.Data(p));
                    }
                    if (p.Debt > 0 && day - p.DebtDay > SocialRules.RepayDays)
                    {
                        p.DebtDay = day;
                        SocialLife.Remember(p, MemoryCode.NotRepaid);
                        SocialLife.Change(p, 0f, -2f, SocialRules.NotRepaidTrust);
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextDebt), SocialLife.Data(p, "amount", SocialLife.Money(p.Debt)), true);
                    }
                    // People who like things done properly think less of somebody with a record, until it is lived down.
                    if (law != null && law.HasRecord && law.Reputation < 60 && (p.Has(SocialTrait.Punctual) || p.Has(SocialTrait.Careful) || p.Has(SocialTrait.Reliable)))
                    {
                        SocialLife.Change(p, 0f, 0f, -0.3f);
                    }
                }

                LoanStep loanStep = SocialRules.Loan(p.Loan, p.LoanDue, day);
                if (loanStep == LoanStep.Due)
                {
                    bool repays = p.Has(SocialTrait.Reliable) || p.Remembers(MemoryCode.NotRepaid) == false && Random.value < (p.Has(SocialTrait.Flaky) ? 0.55f : 0.9f);
                    if (repays)
                    {
                        Dictionary<string, string> data = SocialLife.Data(p, "amount", SocialLife.Money(p.Loan));
                        SocialLife.Give(p.Loan, "reallife_transaction_social_loan_back");
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextLoanBack), data, true);
                        p.Loan = 0;
                    }
                    else
                    {
                        // An excuse and a few more days; at their end they pay or it is written off (below).
                        Dictionary<string, string> data = SocialLife.Data(p, "amount", SocialLife.Money(p.Loan));
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextLoanExcuse), data, true);
                        p.LoanDue = SocialRules.GraceUntil(day);
                    }
                }
                else if (loanStep == LoanStep.GraceOver)
                {
                    bool repays = !p.Has(SocialTrait.Flaky) || Random.value < 0.5f;
                    Dictionary<string, string> data = SocialLife.Data(p, "amount", SocialLife.Money(p.Loan));
                    if (repays)
                    {
                        SocialLife.Give(p.Loan, "reallife_transaction_social_loan_back");
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextLoanBack), data, true);
                    }
                    else
                    {
                        SocialLife.Remember(p, MemoryCode.NotRepaid);
                        SocialLife.Toast(NotificationType.Warning, "reallife_social_loan_written_off", data, 9f);
                    }
                    p.Loan = 0;
                }

                // Their birthday today (they say so), or a friend's tomorrow (Mom says so).
                if (Settings.SocialContact && p.Birthday == today && p.BirthdayYear != (day - 1) / perYear)
                {
                    SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextBirthdayOwn), SocialLife.Data(p), true);
                }
                else if (Settings.SocialContact && !reminded && mom != null && p != mom && p.Stage >= SocialStage.Friend && p.Birthday == today % perYear + 1)
                {
                    reminded = true;
                    SocialLife.SendText(mom, SocialLines.Pick(SocialCatalogue.TextBirthdayMom), SocialLife.Data(p));
                }
                if (p.MovedIn)
                {
                    ModState.AddModifier(ModState.Family, 24);
                }
            }

            float money = SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;
            int tier = 0;
            while (tier < WealthTiers.Length && money >= WealthTiers[tier])
            {
                tier++;
            }
            if (tier > SocialState.WealthTier && Settings.SocialContact)
            {
                SocialPerson who = Contactor(SocialLife.Hour);
                if (who != null && who.Stage >= SocialStage.Friend)
                {
                    bool sour = who.Has(SocialTrait.Envious);
                    if (SocialLife.SendText(who, SocialLines.Pick(sour ? SocialCatalogue.TextRichSour : SocialCatalogue.TextRichKind), SocialLife.Data(who)))
                    {
                        SocialLife.Change(who, 0f, sour ? -1f : 1f, 0f);
                    }
                }
            }
            SocialState.WealthTier = tier;
        }
    }
}
