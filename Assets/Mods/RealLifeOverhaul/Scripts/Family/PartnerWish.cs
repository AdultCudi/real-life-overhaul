using Dialogs;
using Entities;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The partner's bigger wish about work (PartnerWishRules), under the same switch as cutting back (PartnerWork): whether
    /// they ask to stop working or to retrain for the job they always wanted, said by a text and talked over face to face at
    /// home; the end of their studies told by a text. Stopping is undone, and chipping in for the rent asked for, face to
    /// face at home too. With the switch off nothing is asked or paid, and an ask left open is dropped. Cost: nothing per
    /// frame; a few comparisons once a game hour in the day, and once a day.
    /// </summary>
    internal static class PartnerWish
    {
        /// <summary>A wish waits to be talked over face to face at home.</summary>
        public static bool CanTalk => Settings.FamilyWork && FamilyState.Together && FamilyState.S.LivesIn && FamilyState.S.PWishAsk && PartnerVoice.Asking(PartnerVoice.Wish);

        /// <summary>They have stopped working and could go back to it.</summary>
        public static bool CanResume => Settings.FamilyWork && FamilyState.Together && FamilyState.S.LivesIn && FamilyState.S.PWork == PartnerWorkRules.Stopped;

        private static int Dream => PartnerWishRules.DreamOf(FamilyState.S.PSeed, FamilyState.S.PJob, Partner.TradeCount);

        /// <summary>They retrained for the job they wanted: it is theirs now, whatever People said they did before.</summary>
        public static bool OwnJob => FamilyState.S.PWishGranted && PartnerWishRules.WishOf(FamilyState.S.PSeed) == PartnerWishRules.Retrain;

        /// <summary>
        /// Once a game hour in the day: the end of their studies, else whether they ask (raised when the partner's voice
        /// allows, talked over face to face within the week: PartnerVoice).
        /// </summary>
        public static void Hourly(int day)
        {
            FamilySave s = FamilyState.S;
            if (!Settings.FamilyWork)
            {
                s.PWishAsk = false;   // switched off: nothing is asked, and an ask left open is dropped, not counted as a no (PartnerVoice.Hourly)
                return;
            }
            if (s.PRetrainUntil > 0 && day >= s.PRetrainUntil)
            {
                s.PRetrainUntil = 0;
                if (FamilyState.Together)
                {
                    PartnerVoice.Warm("reallife_family_wish_trained", Args.Us.With(FamilyText.T(Partner.JobKey)));
                }
                return;
            }
            if (s.PWishAsk && !PartnerVoice.Asking(PartnerVoice.Wish))
            {
                s.PWishAsk = false;   // the week went by without the talk: as a no
                s.PWishAskDay = day;
                FamilyState.Harmony(PartnerWishRules.HarmonyNo);
                return;
            }
            float cash = SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;
            if (!PartnerWishRules.Asks(Settings.FamilyWork, FamilyState.Together && s.LivesIn && !Partner.Retired, PartnerWork.Work, s.PWishGranted, s.PWishAsk, cash,
                    Prices.At(PartnerWishRules.AskAtCash), day, s.PWishAskDay,
                    FamilyState.IncomeHeld, Prices.At(PartnerWishRules.AskAtIncome)) || !PartnerVoice.Free)
            {
                return;
            }
            s.PWishAsk = true;
            s.PWishAskDay = day;
            s.PWorkAsk = false;   // the bigger wish takes the place of a cut back still to be talked over
            PartnerVoice.Ask(PartnerVoice.Wish, "reallife_family_wish_text", Args.Us);
            FamilyDirector.Logger?.Info("Family: " + Partner.Name + " has a wish about work to talk over.");
        }

        /// <summary>The talk: to stop working, or to retrain for the job they always wanted; yes or not yet.</summary>
        public static DialogEntry Talk()
        {
            bool stop = PartnerWishRules.WishOf(FamilyState.S.PSeed) == PartnerWishRules.Stop;
            int dream = Dream;
            string ask = stop ? "reallife_family_wish_stop_ask" : "reallife_family_wish_retrain_ask";
            Args fill = Args.Us.With(FamilyText.T(Partner.KeyOf(dream)));
            return FamilyTalk.Either(FamilyText.Say(ask, fill), "reallife_family_work_yes", () =>
            {
                FamilySave s = FamilyState.S;
                s.PWishAsk = false;
                PartnerVoice.Answered(PartnerVoice.Wish);
                s.PWishGranted = true;
                if (stop)
                {
                    s.PWork = PartnerWorkRules.Stopped;
                    PartnerGlad.Start(PartnerGlad.Stop);
                }
                else
                {
                    s.PJob = dream;
                    s.PJobKey = null;   // the new job is Family's trade now, not the one People gave them
                    PartnerGlad.Start(PartnerGlad.Retrain);
                    s.PIncome = Partner.IncomeOf(dream);
                    s.PWork = PartnerWorkRules.FullTime;
                    s.PRetrainUntil = TimeHelper.CurrentDay + PartnerWishRules.RetrainDays;
                }
                FamilyState.Harmony(PartnerWishRules.HarmonyYes);
                Partner.Tell(FamilyEvent.SharedTime, PartnerWishRules.AffectionYes);
                FamilyDirector.Logger?.Info("Family: " + Partner.Name + (stop ? " stops working." : " retrains for a new job, " + PartnerWishRules.RetrainDays + " days."));
                return FamilyTalk.Close(stop ? "reallife_family_wish_stop_done" : "reallife_family_wish_retrain_done", fill);
            }, "reallife_family_work_no", () =>
            {
                FamilySave s = FamilyState.S;
                s.PWishAsk = false;
                PartnerVoice.Answered(PartnerVoice.Wish);
                s.PWishAskDay = TimeHelper.CurrentDay;
                FamilyState.Harmony(PartnerWishRules.HarmonyNo);
                return FamilyTalk.Close("reallife_family_work_no_done");
            });
        }

        /// <summary>They earn nothing (stopped, or retired) and could be asked to chip in for the rent.</summary>
        public static bool CanAskChipIn => Settings.FamilyWork && FamilyState.Together && FamilyState.S.LivesIn
            && (FamilyState.S.PWork == PartnerWorkRules.Stopped || Partner.Retired)
            && PartnerWishRules.CanAskChipIn(TimeHelper.CurrentDay, FamilyState.S.PChipInUntil, FamilyState.S.PChipInAskDay);

        // A quarter of a week of the pay they had (People's job, when the social life keeps them), from their savings.
        private static int ChipInWeek => Prices.At(PartnerWishRules.ChipInWeek(FamilyState.S.PIncome > 0 ? FamilyState.S.PIncome : Partner.IncomeOf(FamilyState.S.PJob)));

        /// <summary>Asking them to chip in: yes in a happy enough home, a quarter of a week's pay a week for a month from their savings.</summary>
        public static DialogEntry AskChipIn()
        {
            FamilySave s = FamilyState.S;
            int day = TimeHelper.CurrentDay;
            s.PChipInAskDay = day;
            if (!PartnerWishRules.ChipsIn(s.Harmony))
            {
                FamilyState.Harmony(PartnerWishRules.ChipInNo);
                return FamilyTalk.Close("reallife_family_chip_no");
            }
            s.PChipInUntil = day + PartnerWishRules.ChipInWeeks * 7;
            s.PChipInDay = 0;
            FamilyDirector.Logger?.Info("Family: " + Partner.Name + " chips in " + ChipInWeek + " a week for " + PartnerWishRules.ChipInWeeks + " weeks.");
            return FamilyTalk.Close("reallife_family_chip_yes", new Args { Name = Partner.Name, Amount = FamilyText.Money(ChipInWeek) });
        }

        /// <summary>Once a day: a week's share towards the rent while they chip in.</summary>
        public static void Daily(int day)
        {
            FamilySave s = FamilyState.S;
            if (!Settings.FamilyWork || day >= s.PChipInUntil || !FamilyState.Together || (s.PChipInDay > 0 && day - s.PChipInDay < 7))
            {
                return;
            }
            s.PChipInDay = day;
            int week = ChipInWeek;
            FamilyTalk.Move(week, "reallife_transaction_family_chip_in");
            FamilyState.MoneyIn(week);
        }

        /// <summary>Back to work on full days after stopping, from a family conversation.</summary>
        public static DialogEntry Resume()
        {
            FamilyState.S.PWork = PartnerWorkRules.FullTime;
            FamilyState.S.PChipInUntil = 0;   // earning again: their share of the rent is their pay's, not their savings'
            FamilyDirector.Logger?.Info("Family: " + Partner.Name + " goes back to work.");
            return FamilyTalk.Close("reallife_family_wish_resume_done");
        }

        /// <summary>Somebody new, or nobody: no wish asked, granted or under way.</summary>
        public static void Reset(FamilySave s)
        {
            s.PWishAsk = false;
            s.PWishGranted = false;
            s.PWishAskDay = 0;
            s.PRetrainUntil = 0;
            s.PChipInUntil = 0;
            s.PChipInDay = 0;
            s.PChipInAskDay = 0;
        }
    }
}
