using Dialogs;
using Entities;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The partner's work as the player's fortune grows (PartnerWorkRules): whether they would like to cut back to half days,
    /// raised by a text when the partner's voice allows (PartnerVoice) and talked over face to face at home within the week; a yes shortens their working day at home and
    /// halves their pay and so their share of the rent. With the option off nothing is asked and their days are full, as
    /// before. Cost: nothing per frame; a few comparisons once a game day.
    /// </summary>
    internal static class PartnerWork
    {
        /// <summary>Their working day as the option and their choice make it: full days with the option off.</summary>
        public static int Work => !Settings.FamilyWork ? PartnerWorkRules.FullTime
            : PartnerWishRules.Studying(FamilyState.S.PRetrainUntil, TimeHelper.CurrentDay) ? PartnerWorkRules.Studying : FamilyState.S.PWork;

        /// <summary>The cut back is waiting to be talked over in a family conversation.</summary>
        public static bool CanTalk => Settings.FamilyWork && FamilyState.Together && FamilyState.S.LivesIn && FamilyState.S.PWorkAsk && PartnerVoice.Asking(PartnerVoice.Work);

        /// <summary>They are on half days and could go back to full ones.</summary>
        public static bool CanReturn => Settings.FamilyWork && FamilyState.Together && FamilyState.S.LivesIn && FamilyState.S.PWork == PartnerWorkRules.PartTime;

        /// <summary>Once a game hour in the day: whether they ask, by a text; an ask left unanswered stands until it is talked over.</summary>
        public static void Hourly(int day)
        {
            FamilySave s = FamilyState.S;
            if (s.PWorkAsk && !PartnerVoice.Asking(PartnerVoice.Work))
            {
                s.PWorkAsk = false;   // the week went by without the talk: as a no
                s.PWorkAskDay = day;
                FamilyState.Harmony(PartnerWorkRules.HarmonyNo);
                return;
            }
            float cash = SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;
            if (!PartnerWorkRules.Asks(Settings.FamilyWork, FamilyState.Together && s.LivesIn && !Partner.Retired && !s.PWishAsk, s.PWork, s.PWorkAsk, cash, Prices.At(PartnerWorkRules.AskAtCash), day, s.PWorkAskDay,
                    FamilyState.IncomeHeld, Prices.At(PartnerWorkRules.AskAtIncome)))
            {
                return;
            }
            if (!PartnerVoice.Free)
            {
                return;   // (asked on a later day: one thing at a time, PartnerVoice)
            }
            s.PWorkAsk = true;
            s.PWorkAskDay = day;
            PartnerVoice.Ask(PartnerVoice.Work, "reallife_family_work_text", Args.Us.With(FamilyText.T(Partner.JobKey)));
            FamilyDirector.Logger?.Info("Family: " + Partner.Name + " would like to cut back to half days at work.");
        }

        /// <summary>The talk: half days, or not yet.</summary>
        public static DialogEntry Talk()
        {
            return FamilyTalk.Either(FamilyText.Say("reallife_family_work_ask", Args.Us.With(FamilyText.T(Partner.JobKey))), "reallife_family_work_yes", () =>
            {
                FamilySave s = FamilyState.S;
                s.PWorkAsk = false;
                PartnerVoice.Answered(PartnerVoice.Work);
                s.PWork = PartnerWorkRules.PartTime;
                FamilyState.Harmony(PartnerWorkRules.HarmonyYes);
                Partner.Tell(FamilyEvent.SharedTime, PartnerWorkRules.AffectionYes);
                FamilyDirector.Logger?.Info("Family: " + Partner.Name + " works half days from now on.");
                PartnerGlad.Start(PartnerGlad.Work);
                return FamilyTalk.Close("reallife_family_work_yes_done");
            }, "reallife_family_work_no", () =>
            {
                FamilySave s = FamilyState.S;
                s.PWorkAsk = false;
                PartnerVoice.Answered(PartnerVoice.Work);
                s.PWorkAskDay = TimeHelper.CurrentDay;
                FamilyState.Harmony(PartnerWorkRules.HarmonyNo);
                return FamilyTalk.Close("reallife_family_work_no_done");
            });
        }

        /// <summary>Back to full days, from a family conversation.</summary>
        public static DialogEntry Return()
        {
            FamilySave s = FamilyState.S;
            s.PWork = PartnerWorkRules.FullTime;
            s.PWorkAskDay = TimeHelper.CurrentDay;
            FamilyDirector.Logger?.Info("Family: " + Partner.Name + " goes back to full days at work.");
            return FamilyTalk.Close("reallife_family_work_return_done");
        }

        /// <summary>Somebody new, or nobody: their work starts as full days with nothing asked.</summary>
        public static void Reset(FamilySave s)
        {
            s.PWork = PartnerWorkRules.FullTime;
            s.PWorkAsk = false;
            s.PWorkAskDay = 0;
            s.PGlad = null;
        }
    }
}
