using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>The hard times, each optional: arguments, counselling, time apart, a way back, and divorce, settled from cash in hand only.</summary>
    internal static class HardTimes
    {
        private struct Row
        {
            public string Body, Yes, No, YesLine, NoLine;
            public float YesGain, NoGain;
            public Row(string body, string yes, string no, float yesGain, float noGain, string yesLine, string noLine)
            {
                Body = body; Yes = yes; No = no; YesGain = yesGain; NoGain = noGain; YesLine = yesLine; NoLine = noLine;
            }
        }

        private const string Sorry = "reallife_family_argue_say_sorry";
        private const string Stand = "reallife_family_argue_say_stand";
        private const string Listen = "reallife_family_argue_say_listen";
        private const string Shrug = "reallife_family_argue_say_shrug";
        private const string Joke = "reallife_family_argue_say_joke";
        private const string Explain = "reallife_family_argue_say_explain";

        // What is said first, the two answers, what each does to the home, and how it lands. The better answer is not always the same one.
        private static readonly Row[] Arguments =
        {
            new Row("reallife_family_argue_1", Sorry, Stand, 6f, -8f, "reallife_family_argue_1_a", "reallife_family_argue_1_b"),
            new Row("reallife_family_argue_2", Listen, Shrug, 6f, -9f, "reallife_family_argue_2_a", "reallife_family_argue_2_b"),
            new Row("reallife_family_argue_3", Joke, Explain, -5f, 5f, "reallife_family_argue_3_a", "reallife_family_argue_3_b"),
            new Row("reallife_family_argue_4", Sorry, Explain, 4f, 3f, "reallife_family_argue_4_a", "reallife_family_argue_4_b"),
            new Row("reallife_family_argue_5", Listen, Stand, 6f, -6f, "reallife_family_argue_5_a", "reallife_family_argue_5_b"),
            new Row("reallife_family_argue_6", Joke, Shrug, 5f, -7f, "reallife_family_argue_6_a", "reallife_family_argue_6_b"),
            new Row("reallife_family_argue_7", Sorry, Stand, -2f, 4f, "reallife_family_argue_7_a", "reallife_family_argue_7_b"),
            new Row("reallife_family_argue_8", Listen, Explain, 6f, 1f, "reallife_family_argue_8_a", "reallife_family_argue_8_b"),
            new Row("reallife_family_argue_9", Joke, Stand, 3f, -8f, "reallife_family_argue_9_a", "reallife_family_argue_9_b"),
            new Row("reallife_family_argue_10", Sorry, Shrug, 7f, -9f, "reallife_family_argue_10_a", "reallife_family_argue_10_b")
        };

        private static readonly string[] ArrestLines = { "reallife_family_arrest_1", "reallife_family_arrest_2", "reallife_family_arrest_3" };
        private static readonly string[] SessionLines = { "reallife_family_counselling_done_1", "reallife_family_counselling_done_2", "reallife_family_counselling_done_3" };
        private static readonly string[] ApartCalls = { "reallife_family_apart_call_1", "reallife_family_apart_call_2", "reallife_family_apart_call_3", "reallife_family_apart_call_4" };
        private static int _lastSession = -1, _lastCall = -1;

        public static void Daily(int day)
        {
            FamilySave s = FamilyState.S;
            if (FamilyState.Bond == Bond.Separated)
            {
                // Long silence, and they draw their own conclusion.
                if (day - s.SeparatedDay >= FamilyBalance.PartnerFilesAfterDays && day - s.LastTalkDay >= FamilyBalance.PartnerFilesAfterDays)
                {
                    string name = Partner.Name;
                    End(true);
                    FamilyText.Toast(NotificationType.Warning, "reallife_family_divorce_theirs", new Args { Name = name }, 12f);
                }
                return;
            }
            if (!FamilyState.Together || !Settings.FamilyFriction)
            {
                s.LowDays = 0;
                return;
            }
            s.LowDays = s.Harmony < FamilyBalance.HarmonyBreak ? s.LowDays + 1 : 0;
            if (s.LivesIn && s.LowDays >= FamilyBalance.BreakDays)
            {
                Separate(true);
                return;
            }
            if (FamilyBridges.Law != null && FamilyBridges.Law.InCustody)
            {
                return;   // nobody argues through the bars
            }
            HardTimesWatch.Daily(day);   // what is going on: a moment comes of it at its hour (HardTimesWatch.Hourly)
        }

        /// <summary>
        /// A difficult moment: the one HardTimesWatch chose from what is going on (HardTimesRules), asked as a question
        /// with two answers.
        /// </summary>
        internal static void Argue(int day, int pick)
        {
            FamilySave s = FamilyState.S;
            pick = Mathf.Clamp(pick, 0, Arguments.Length - 1);
            // Those had lately are passed over while another fits as well (HardTimesRules.Choose); the round starts
            // again once all have come.
            if (s.SeenArguments.Count >= Arguments.Length)
            {
                s.SeenArguments.Clear();
            }
            s.SeenArguments.Add(pick);
            s.LastArgument = pick;
            s.LastArgueDay = day;
            Row row = Arguments[pick];
            FamilyDirector.Instance.Ask("reallife_family_argue_header", row.Body, Args.Us, row.Yes, row.No,
                () => Answered(row.YesGain, row.YesLine), () => Answered(row.NoGain, row.NoLine));
        }

        private static void Answered(float gain, string line)
        {
            HardTimesWatch.Answered(SaidSorry(line));
            FamilyState.Harmony(gain);
            FamilyState.S.LastTalkDay = TimeHelper.CurrentDay;
            if (gain >= 0f)
            {
                Partner.Tell(FamilyEvent.MadeUp, gain * 0.4f, gain * 0.4f);
                FamilyText.Toast(NotificationType.Success, line, Args.Us, 10f);
            }
            else
            {
                Partner.Tell(FamilyEvent.Argued, gain * 0.5f, gain * 0.3f);
                FamilyText.Toast(NotificationType.Warning, line, Args.Us, 10f);
            }
        }

        // Whether an answer's line is a "Say sorry": a run of them is what one moment is about (HardTimesWatch).
        private static bool SaidSorry(string line)
        {
            for (int i = 0; i < Arguments.Length; i++)
            {
                Row r = Arguments[i];
                if ((r.Yes == Sorry && r.YesLine == line) || (r.No == Sorry && r.NoLine == line))
                {
                    return true;
                }
            }
            return false;
        }

        public static void Arrest(int severity)
        {
            if (!FamilyState.HasPartner)
            {
                Relatives.MomSays("reallife_family_mom_arrest");
                return;
            }
            severity = Mathf.Clamp(severity, 1, 3);
            FamilyState.Harmony(FamilyBalance.HarmonyArrest * (0.6f + 0.4f * severity));
            FamilyState.S.BrokenPromises++;
            Partner.Tell(FamilyEvent.BrokenPromise, -6f * severity, -10f * severity);
            FamilyText.Message(FamilyPhone.PartnerContact, ArrestLines[severity - 1], Args.Us);
            Relatives.MomSays("reallife_family_mom_arrest");
        }

        public static bool CanCounsel => FamilyState.HasPartner && (FamilyState.S.Harmony < FamilyBalance.HarmonyCounsel || FamilyState.Bond == Bond.Separated)
                                         && FamilyState.FindErrand(Errands.Counselling) == null && TimeHelper.CurrentDay - FamilyState.S.LastCounselDay >= 3;

        public static DialogEntry Book()
        {
            int day = TimeHelper.CurrentDay + 1;
            Errands.Add(Errands.Counselling, day, 10, 19);
            string where = FamilyPlaces.Nearest(FamilyPlaces.Hospital, PlayerHelper.GetCityPosition(), out Venue venue) ? venue.Name : FamilyText.T("reallife_family_place_unknown");
            return FamilyTalk.Close("reallife_family_counselling_booked", Args.Us.At(where).On(FamilyText.When(day)).Cost(FamilyBalance.CounsellingCost));
        }

        public static void Session(Errand errand)
        {
            FamilySave s = FamilyState.S;
            FamilyDirector director = FamilyDirector.Instance;
            if (!FamilyTalk.Pay(FamilyBalance.CounsellingCost, "reallife_transaction_family_counselling"))
            {
                return;
            }
            FamilyState.MoneyOut(FamilyBalance.CounsellingCost, 4);
            s.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            director.Pass("reallife_family_passage_counselling", 1, null, () =>
            {
                float gain = Mathf.Max(6f, FamilyBalance.HarmonyCounselling - 3f * Mathf.Min(3, s.Counselling));   // the first sessions do the most
                s.Counselling++;
                s.LastCounselDay = TimeHelper.CurrentDay;
                s.LastTalkDay = TimeHelper.CurrentDay;
                s.LowDays = 0;
                FamilyState.Harmony(gain);
                Partner.Tell(FamilyEvent.Counselling, 4f, 6f);
                FamilyText.Good(FamilyText.Pick(SessionLines, ref _lastSession), Args.Us);
            });
        }

        public static void SessionMissed(Errand errand)
        {
            FamilyState.S.BrokenPromises++;
            FamilyState.Harmony(-6f);
            Partner.Tell(FamilyEvent.BrokenPromise, -3f, -5f);
            FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_counselling_missed", Args.Us);
        }

        public static DialogEntry AskApart()
        {
            return FamilyTalk.Either(FamilyText.Say("reallife_family_apart_ask"), "reallife_family_apart_yes", () =>
            {
                Separate(false);
                return FamilyTalk.Close("reallife_family_apart_done");
            }, "reallife_family_apart_no", () => FamilyTalk.Close("reallife_family_apart_stayed"));
        }

        public static void Separate(bool theirs)
        {
            FamilySave s = FamilyState.S;
            if (!FamilyState.Together)
            {
                return;
            }
            string name = Partner.Name;
            s.BondBefore = s.Bond;
            WeddingPlanner.Cancel(false);
            FamilyState.Bond = Bond.Separated;
            s.SeparatedDay = TimeHelper.CurrentDay;
            s.LastTalkDay = TimeHelper.CurrentDay;
            s.LowDays = 0;
            s.Harmony = Mathf.Max(s.Harmony, 20f);
            HomeLife.MoveOut();
            FamilyState.DropErrand(Errands.Dinner);
            FamilyModifiers.Remove(FamilyModifiers.Married);
            FamilyModifiers.Remove(FamilyModifiers.Engaged);
            FamilyModifiers.Add(FamilyModifiers.Heartache);
            FamilyState.Remember("reallife_family_memory_separated", name);
            Partner.Tell(FamilyEvent.Separated, -10f, -6f);
            Relatives.MomSays("reallife_family_mom_separated");
            if (theirs)
            {
                FamilyText.Toast(NotificationType.Warning, "reallife_family_apart_theirs", Args.Us, 12f);
                FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_apart_theirs_text", Args.Us);
            }
            FamilyDirector.Logger?.Info("Family: separated (" + (theirs ? "their" : "the player's") + " decision).");
        }

        public static bool CanCallApart => FamilyState.Bond == Bond.Separated && FamilyState.S.LastTalkDay != TimeHelper.CurrentDay;

        public static DialogEntry CallApart()
        {
            FamilyState.S.LastTalkDay = TimeHelper.CurrentDay;
            FamilyState.Harmony(3f);
            Partner.Tell(FamilyEvent.MadeUp, 2f, 2f);
            return FamilyTalk.Close(FamilyText.Pick(ApartCalls, ref _lastCall));
        }

        public static bool CanComeHome => FamilyState.Bond == Bond.Separated && FamilyState.S.Harmony >= FamilyBalance.HarmonyReconcile;

        public static DialogEntry ComeHome()
        {
            FamilySave s = FamilyState.S;
            var before = (Bond)s.BondBefore;
            FamilyState.Bond = before == Bond.Married || before == Bond.Engaged ? before : Bond.Dating;
            s.SinceDay = TimeHelper.CurrentDay;
            s.LastSharedDay = TimeHelper.CurrentDay;
            s.LastTalkDay = TimeHelper.CurrentDay;
            BuildingRegistration home = FamilyPlaces.Home();
            bool movedBack = home != null && FamilyPlaces.HasRoom(home, 0);
            if (movedBack)
            {
                s.LivesIn = true;
                FamilyPlaces.MakeHome(home);
            }
            FamilyModifiers.Remove(FamilyModifiers.Heartache);
            FamilyModifiers.Add(FamilyModifiers.Joy, 24);
            FamilyState.Remember("reallife_family_memory_reconciled", Partner.Name);
            Partner.Tell(FamilyEvent.Reconciled, 8f, 6f);
            Relatives.MomSays("reallife_family_mom_reconciled");
            return FamilyTalk.Close(movedBack ? "reallife_family_reconciled_home" : "reallife_family_reconciled");
        }

        public static bool CanEnd => FamilyState.Bond == Bond.Separated && TimeHelper.CurrentDay - FamilyState.S.SeparatedDay >= FamilyBalance.DivorceAfterDays;

        public static int Settlement()
        {
            if ((Bond)FamilyState.S.BondBefore != Bond.Married)
            {
                return 0;
            }
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, SaveGameManager.Current.Money) * FamilyBalance.SettlementShare / 10f) * 10, 0, FamilyBalance.SettlementCap);
        }

        public static DialogEntry EndAsk()
        {
            bool married = (Bond)FamilyState.S.BondBefore == Bond.Married;
            return FamilyTalk.Either(FamilyText.Say(married ? "reallife_family_divorce_ask" : "reallife_family_part_ask", Args.Us.Cost(Settlement())),
                married ? "reallife_family_divorce_yes" : "reallife_family_part_yes", () =>
                {
                    string name = Partner.Name;
                    End(false);
                    return FamilyTalk.Close(married ? "reallife_family_divorce_done" : "reallife_family_part_done", new Args { Name = name });
                }, "reallife_family_apart_no", () => FamilyTalk.Close("reallife_family_apart_stayed"));
        }

        /// <summary>A divorce (if they were married) or a parting. The settlement is a share of cash in hand, capped; businesses are never touched.</summary>
        public static void End(bool theirs)
        {
            FamilySave s = FamilyState.S;
            bool married = (Bond)s.BondBefore == Bond.Married;
            string name = Partner.Name;
            int settlement = Settlement();
            if (settlement > 0)
            {
                FamilyTalk.Move(-settlement, "reallife_transaction_family_settlement");
                FamilyState.MoneyOut(settlement, 4);
            }
            Partner.Tell(FamilyEvent.Divorced, -20f, -10f);
            FamilyState.Remember(married ? "reallife_family_memory_divorced" : "reallife_family_memory_parted", name);
            FamilyState.Bond = married ? Bond.Divorced : Bond.Single;
            FamilyState.DropErrand(Errands.Counselling);
            FamilyState.DropErrand(Errands.Dinner);
            Partner.Part();
            s.ExName = name;
            if (!string.IsNullOrEmpty(s.NextOfKin) && s.NextOfKin == name)
            {
                s.NextOfKin = null;
            }
            FamilyModifiers.Add(FamilyModifiers.Heartache);
            Relatives.MomSays("reallife_family_mom_divorced");
            FamilyDirector.Logger?.Info($"Family: {(married ? "divorced" : "parted")} ({(theirs ? "their" : "the player's")} decision), settlement {settlement}.");
            if (married && FamilyState.AtHome() > 0)
            {
                s.Custody = 1;   // the children live with the player; how often they visit the other parent is asked
                FamilyDirector.Instance.Ask("reallife_family_custody_header", "reallife_family_custody_body", new Args { Name = name },
                    "reallife_family_custody_alternate", "reallife_family_custody_every", () => s.Custody = 1, () => s.Custody = 2);
            }
        }
    }
}
