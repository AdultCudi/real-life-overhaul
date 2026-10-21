using System.Globalization;
using Entities;
using Helpers;
using UI.Notification;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The household chores, done for real (ChoreRules): the partner asks when something is really so (the fridge nearly
    /// empty, the home a tip, the player unwashed, a filthy car) and it counts once it has really changed (shopping in the
    /// fridge, the home tidied, a shower, a car wash). Both at home, they come over and ask face to face (HomeLife.ComeOver,
    /// Opening): yes or not now; left unanswered a few hours, or asked while apart, it comes as a text, as agreed. Done in
    /// time: thanks and a warmer home; not done by its day: a text and a cooler one. One at a time, some days apart; with
    /// "Friction" off, none. COST: once a game hour, a few readings through the Home bridge.
    /// </summary>
    internal static class Chores
    {
        private static readonly string[] Thanks = { "reallife_family_chore_thanks_1", "reallife_family_chore_thanks_2", "reallife_family_chore_thanks_3" };
        private static readonly string[] Sighs = { "reallife_family_chore_sigh_1", "reallife_family_chore_sigh_2", "reallife_family_chore_sigh_3" };
        private static int _lastThanks = -1, _lastSigh = -1;

        /// <summary>True while a chore raised at home waits to be talked over: the partner comes over to the player.</summary>
        public static bool Pending => FamilyState.S.ChoreKind != 0 && FamilyState.S.ChoreAskStamp > 0;

        public static void Hourly(int day, int hour)
        {
            FamilySave s = FamilyState.S;
            if (!s.LivesIn || !FamilyState.Together || !Settings.FamilyFriction)
            {
                s.ChoreKind = 0;
                s.ChoreAskStamp = 0;
                return;
            }
            IHomeBridge home = FamilyBridges.Home;
            int food = home != null ? home.FoodUnits : -1;
            float tidy = home != null ? home.Tidiness : -1f;
            float hygiene = home != null ? home.Hygiene : -1f;
            float car = home != null ? home.CarDirt : -1f;
            var kind = (ChoreKind)s.ChoreKind;
            int stamp = day * 24 + hour;
            if (kind != ChoreKind.None)
            {
                bool agreed = s.ChoreAskStamp == 0;
                if (ChoreRules.Done(kind, food, tidy, hygiene, car))
                {
                    Settle(day);
                    if (agreed)
                    {
                        FamilyState.Harmony(ChoreRules.HarmonyDone);
                        s.LastSharedDay = day;
                        Partner.Tell(FamilyEvent.SharedTime, 1.5f, 1f);
                        FamilyText.Toast(NotificationType.Success, FamilyText.Pick(Thanks, ref _lastThanks), Args.Us, 8f);
                    }
                    return;
                }
                if (day > s.ChoreDue)
                {
                    Settle(day);
                    if (agreed)
                    {
                        FamilyState.Harmony(ChoreRules.HarmonyMissed);
                        FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_chore_missed", Args.Us);
                    }
                    return;
                }
                if (!agreed && stamp - s.ChoreAskStamp >= ChoreRules.AskHours)
                {
                    if (ChoreRules.Textable(kind) && hour >= 9 && hour <= 21)
                    {
                        s.ChoreAskStamp = 0;   // never talked over at home: asked by text, as agreed
                        FamilyText.Message(FamilyPhone.PartnerContact, ChoreRules.AskKey(kind), Args.Us);
                    }
                    else if (!ChoreRules.Textable(kind))
                    {
                        Settle(day);   // a shower is not asked by text
                    }
                }
                return;
            }
            if (hour < 9 || hour > 21 || day - s.ChoreDay < ChoreRules.GapDays || FamilyDirector.Instance == null || FamilyDirector.Instance.HasQuestions)
            {
                return;
            }
            bool together = HomeLife.Present;
            kind = ChoreRules.Wanted(food, tidy, hygiene, car, together);
            if (kind == ChoreKind.None)
            {
                return;
            }
            s.ChoreKind = (int)kind;
            s.ChoreDue = ChoreRules.Due(kind, day);
            if (together)
            {
                s.ChoreAskStamp = stamp;   // they come over to say it (HomeLife.ComeOver)
            }
            else
            {
                s.ChoreAskStamp = 0;
                FamilyText.Message(FamilyPhone.PartnerContact, ChoreRules.AskKey(kind), Args.Us);
            }
            FamilyDirector.Logger?.Info("Family: a chore asked (" + kind + ", by day " + s.ChoreDue + (together ? ", face to face" : ", by text") + "): food "
                                        + food + ", tidiness " + tidy.ToString("F0", CultureInfo.InvariantCulture) + ", hygiene " + hygiene.ToString("F0", CultureInfo.InvariantCulture) + ", car dirt " + car.ToString("F2", CultureInfo.InvariantCulture) + ".");
        }

        /// <summary>The chore that waits to be talked over, first in a conversation at home; null if none waits.</summary>
        public static DialogEntry Opening()
        {
            if (!Pending)
            {
                return null;
            }
            string ask = ChoreRules.AskKey((ChoreKind)FamilyState.S.ChoreKind);
            return FamilyTalk.Either(FamilyText.Say("reallife_family_face_line", Args.Us.With(FamilyText.T(ask))),
                "reallife_family_chore_yes", Agreed, "reallife_family_chore_no", Refused);
        }

        private static DialogEntry Agreed()
        {
            FamilySave s = FamilyState.S;
            s.ChoreAskStamp = 0;
            s.LastTalkDay = TimeHelper.CurrentDay;
            return FamilyTalk.Close("reallife_family_chore_agreed", Args.Us);
        }

        private static DialogEntry Refused()
        {
            Settle(TimeHelper.CurrentDay);
            FamilyState.S.LastTalkDay = TimeHelper.CurrentDay;
            FamilyState.Harmony(ChoreRules.HarmonyRefused);
            return FamilyTalk.Close(FamilyText.Pick(Sighs, ref _lastSigh), Args.Us);
        }

        private static void Settle(int day)
        {
            FamilySave s = FamilyState.S;
            s.ChoreKind = 0;
            s.ChoreAskStamp = 0;
            s.ChoreDay = day;
        }
    }
}
