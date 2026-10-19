using Dialogs;
using Entities;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The partner's work as the player's fortune grows (PartnerWorkRules): once a day, whether they would like to cut back to
    /// half days, said by a text and talked over in a family conversation; a yes shortens their working day at home and
    /// halves their pay and so their share of the rent. With the option off nothing is asked and their days are full, as
    /// before. Cost: nothing per frame; a few comparisons once a game day.
    /// </summary>
    internal static class PartnerWork
    {
        /// <summary>Their working day as the option and their choice make it: full days with the option off.</summary>
        public static int Work => Settings.FamilyWork ? FamilyState.S.PWork : PartnerWorkRules.FullTime;

        /// <summary>The cut back is waiting to be talked over in a family conversation.</summary>
        public static bool CanTalk => Settings.FamilyWork && FamilyState.Together && FamilyState.S.LivesIn && FamilyState.S.PWorkAsk;

        /// <summary>They are on half days and could go back to full ones.</summary>
        public static bool CanReturn => Settings.FamilyWork && FamilyState.Together && FamilyState.S.LivesIn && FamilyState.S.PWork == PartnerWorkRules.PartTime;

        /// <summary>Once a day: whether they ask, by a text; an ask left unanswered stands until it is talked over.</summary>
        public static void Daily(int day)
        {
            FamilySave s = FamilyState.S;
            float cash = SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;
            if (!PartnerWorkRules.Asks(Settings.FamilyWork, FamilyState.Together && s.LivesIn, s.PWork, s.PWorkAsk, cash, Prices.At(PartnerWorkRules.AskAtCash), day, s.PWorkAskDay))
            {
                return;
            }
            s.PWorkAsk = true;
            s.PWorkAskDay = day;
            FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_work_text", Args.Us.With(FamilyText.T(Partner.JobKey)));
            FamilyDirector.Logger?.Info("Family: " + Partner.Name + " would like to cut back to half days at work.");
        }

        /// <summary>The talk: half days, or not yet.</summary>
        public static DialogEntry Talk()
        {
            return FamilyTalk.Either(FamilyText.Say("reallife_family_work_ask", Args.Us.With(FamilyText.T(Partner.JobKey))), "reallife_family_work_yes", () =>
            {
                FamilySave s = FamilyState.S;
                s.PWorkAsk = false;
                s.PWork = PartnerWorkRules.PartTime;
                FamilyState.Harmony(PartnerWorkRules.HarmonyYes);
                Partner.Tell(FamilyEvent.SharedTime, PartnerWorkRules.AffectionYes);
                FamilyDirector.Logger?.Info("Family: " + Partner.Name + " works half days from now on.");
                return FamilyTalk.Close("reallife_family_work_yes_done");
            }, "reallife_family_work_no", () =>
            {
                FamilySave s = FamilyState.S;
                s.PWorkAsk = false;
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
        }
    }
}
