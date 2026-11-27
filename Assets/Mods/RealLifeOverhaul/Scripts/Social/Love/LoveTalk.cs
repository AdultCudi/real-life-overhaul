using System;
using System.Collections;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What is said in the panel about the other person, an evening's offer, the morning after, a child and an offer to date: the pages
    /// of each, on a call (PersonDialog), face to face (SceneDialog, Family's talk at home). The panel offers three answers, so a fourth
    /// is asked in the game's question box (never a hang-up that settles something). Every figure that moves is told after, as an outing
    /// tells it. COST: a page is built when it is shown.
    /// </summary>
    internal static class LoveTalk
    {
        private static string Pick(string[] pool, int a, int b) => LoveCatalogue.Pick(pool, LoveState.Seed, a, b);

        private static DialogEntry Say(SocialPerson p, string key, Dictionary<string, string> data)
        {
            SocialLife.Said(p, key, data);
            return new DialogEntry { messageData = key.Localize(data), OnCancel = PhoneContacts.HangUp };
        }

        private static DialogEntry Built(SocialPerson p, string text)
        {
            var data = new Dictionary<string, string> { { "text", text } };
            SocialLife.Said(p, SocialLinks.WordsKey, data);   // the person's last words are these, not a key nothing defines
            return new DialogEntry { messageData = SocialLinks.WordsKey.Localize(data), OnCancel = PhoneContacts.HangUp };
        }

        private static DialogEntry Last(SocialPerson p, string key, Dictionary<string, string> data)
        {
            SocialLife.Said(p, key, data);
            return PhoneContacts.Closing(key, data);
        }

        private static DialogEntry Done()
        {
            DialogController.current.FinishDialog();
            return null;
        }

        private static void Leave()
        {
            if (DialogController.current != null)
            {
                DialogController.current.CancelDialog();
            }
        }

        // ---------------------------------------------------------------- what a call to somebody opens on

        /// <summary>The call to this person opens on something of the love life: they found out, their child, an offer to date. Null for nothing.</summary>
        public static Func<DialogEntry> Calling(SocialPerson p)
        {
            if (!LoveState.Loaded || p == null)
            {
                return null;
            }
            LoveFile f = LoveState.File;
            if (f.ConfrontWho == p.Index)
            {
                return () => Confront(p);
            }
            LoveBaby b = f.BabyOf(p.Index);
            if (b != null && b.Told && !b.Born && b.Choice == LoveChoice.None)
            {
                return () => BabyChoice(p, b);
            }
            if (f.DateOfferWho == p.Index)
            {
                return () => DateOffer(p);
            }
            return null;
        }

        // ---------------------------------------------------------------- asking out while taken

        /// <summary>"You are with Sam. Ask Jo out anyway?" in the player's own terms: yes goes on to the asking, no leaves it.</summary>
        public static DialogEntry Warn(SocialPerson p, Func<DialogEntry> yes)
        {
            SocialPerson first = SocialState.Partner;
            Dictionary<string, string> data = SocialLife.Data(p, "partner", first != null ? SocialLife.First(first) : string.Empty);
            DialogEntry entry = Say(p, Pick(LoveCatalogue.Warn, SocialLife.Day, p.Index), data);
            entry.ConfirmTextOverride = "reallife_love_warn_yes".Localize();
            entry.OnConfirm = yes;
            entry.SecondOptionTextOverride = "reallife_love_warn_no";
            entry.OnSecondOption = () => Last(p, "reallife_love_warn_bye", data);
            return entry;
        }

        // ---------------------------------------------------------------- somebody found out

        /// <summary>Face to face at home: Family's talk with the partner opens on it while they wait.</summary>
        public static DialogEntry ConfrontHome()
        {
            if (!LoveState.Loaded || LoveState.File.ConfrontWho < 0)
            {
                return null;
            }
            SocialPerson w = SocialState.Get(LoveState.File.ConfrontWho);
            return w != null ? Confront(w) : null;
        }

        private static DialogEntry Confront(SocialPerson w)
        {
            LoveFile f = LoveState.File;
            SocialPerson other = SocialState.Get(f.ConfrontOf);
            Dictionary<string, string> data = SocialLife.Data(w, "other", other != null ? SocialLife.First(other) : string.Empty);
            DialogEntry entry = Say(w, Pick(LoveCatalogue.ConfrontPool(f.ConfrontHow), f.ConfrontDay, w.Index), data);
            entry.ConfirmTextOverride = "reallife_love_btn_confess".Localize();
            entry.OnConfirm = () => Answer(w, LoveAnswer.Confess);
            entry.SecondOptionTextOverride = "reallife_love_btn_deny";
            entry.OnSecondOption = () => Answer(w, LoveAnswer.Deny);
            entry.CancelTextOverride = "reallife_love_btn_over";
            entry.OnCancel = () => AskWhoEnds(w, data);
            return entry;
        }

        private static DialogEntry Answer(SocialPerson w, LoveAnswer answer)
        {
            LoveReport report = LoveLife.Resolve(w, answer);
            SocialDirector.Instance?.ShowCard(report.CardTitle, report.CardBody);
            SocialLife.Said(w, report.Line, report.Data);
            return PhoneContacts.Closing(report.Line, report.Data);
        }

        // "Say it is over": who it is over with is asked in the game's own question box, once the panel has closed. Closing the box answers nothing.
        private static void AskWhoEnds(SocialPerson w, Dictionary<string, string> data)
        {
            WhenFree(() =>
            {
                if (!LoveState.Loaded || LoveState.File.ConfrontWho != w.Index)
                {
                    return;
                }
                GameConfirm.Show("reallife_love_over_header".Localize(data), "reallife_love_over_body".Localize(data), () => AnswerLater(w, LoveAnswer.EndOther),
                    () => AnswerLater(w, LoveAnswer.EndThis), "reallife_love_over_other", "reallife_love_over_this", false, true);
            });
        }

        // The answer taken in the box: the talk is over, so their words come as a text and the figures on the card.
        private static void AnswerLater(SocialPerson w, LoveAnswer answer)
        {
            try
            {
                if (!LoveState.Loaded || LoveState.File.ConfrontWho != w.Index)
                {
                    return;
                }
                LoveReport report = LoveLife.Resolve(w, answer);
                SocialDirector.Instance?.ShowCard(report.CardTitle, report.CardBody);
                if (w.Met)
                {
                    SocialLife.SendText(w, report.Line, report.Data, true, true);
                }
            }
            catch (Exception e)
            {
                SocialDirector.Logger?.Error(e);
            }
        }

        /// <summary>Runs <paramref name="action"/> once no conversation or question box is open (a minute at most), on the social director.</summary>
        public static void WhenFree(Action action)
        {
            SocialDirector d = SocialDirector.Instance;
            if (d == null)
            {
                action();
                return;
            }
            d.StartCoroutine(Guard.Routine(Free(action)));
        }

        private static IEnumerator Free(Action action)
        {
            float waited = 0f;
            yield return null;
            while ((DialogController.current != null || HudConfirm.isOpen) && waited < 60f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            try
            {
                action();
            }
            catch (Exception e)
            {
                SocialDirector.Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- a child of a night

        private static DialogEntry BabyChoice(SocialPerson p, LoveBaby b)
        {
            Dictionary<string, string> data = SocialLife.Data(p, "amount", SocialLife.Money(Prices.At(LoveRules.SupportAmount(0f))));
            DialogEntry entry;
            if (b.Carrier == 0)
            {
                entry = Say(p, Pick(LoveCatalogue.BabyOpen, b.Day, p.Index), data);
                entry.ConfirmTextOverride = "reallife_love_btn_baby_involved".Localize();
                entry.OnConfirm = () => Chosen(p, b, LoveChoice.Involved, data);
                entry.SecondOptionTextOverride = "reallife_love_btn_baby_support";
                entry.OnSecondOption = () => Chosen(p, b, LoveChoice.Support, data);
                entry.Pays(DialogButton.Second, "reallife_ui_dialog_cost_month", "amount", SocialLife.Money(Prices.At(LoveRules.SupportAmount(0f))));
                entry.CancelTextOverride = "reallife_love_btn_baby_out";
                entry.OnCancel = () => AskStayOut(p, b, data);
                return entry;
            }
            entry = Say(p, "reallife_love_baby_open_me", data);
            entry.ConfirmTextOverride = "reallife_love_btn_baby_me_involved".Localize();
            entry.OnConfirm = () => Chosen(p, b, LoveChoice.Involved, data);
            entry.SecondOptionTextOverride = "reallife_love_btn_baby_me_alone";
            entry.OnSecondOption = () => Chosen(p, b, LoveChoice.StayOut, data);
            return entry;
        }

        private static DialogEntry Chosen(SocialPerson p, LoveBaby b, LoveChoice choice, Dictionary<string, string> data)
        {
            LoveBabies.Choose(b, p, choice, false);
            string[] pool = choice == LoveChoice.Involved ? LoveCatalogue.BabyInvolved : choice == LoveChoice.Support ? LoveCatalogue.BabySupport : LoveCatalogue.BabyOut;
            return Last(p, Pick(pool, SocialLife.Day, p.Index), data);
        }

        private static void AskStayOut(SocialPerson p, LoveBaby b, Dictionary<string, string> data)
        {
            WhenFree(() =>
            {
                if (b.Choice != LoveChoice.None || b.Born)
                {
                    return;
                }
                GameConfirm.Show("reallife_love_stayout_header".Localize(data), "reallife_love_stayout_body".Localize(data), () => LoveBabies.Choose(b, p, LoveChoice.StayOut, true), null,
                    "reallife_love_stayout_yes", "reallife_love_stayout_no", false, true);
            });
        }

        // ---------------------------------------------------------------- an offer to date, after a night

        private static DialogEntry DateOffer(SocialPerson p)
        {
            Dictionary<string, string> data = SocialLife.Data(p);
            DialogEntry entry = Say(p, Pick(LoveCatalogue.DateOfferOpen, SocialLife.Day, p.Index), data);
            entry.ConfirmTextOverride = "reallife_love_btn_dateyes".Localize();
            entry.OnConfirm = () => AcceptDate(p);
            entry.SecondOptionTextOverride = "reallife_love_btn_dateno";
            entry.OnSecondOption = () =>
            {
                LoveState.File.DateOfferWho = -1;
                SocialLife.Change(p, 0f, -2f, 0f);
                return Last(p, "reallife_love_dateno_line", data);
            };
            return entry;
        }

        private static DialogEntry AcceptDate(SocialPerson p)
        {
            Dictionary<string, string> data = SocialLife.Data(p);
            if (!LoveHooks.CanDate)
            {
                return Last(p, "reallife_love_dateoffer_blocked", data);
            }
            if (LoveHooks.NeedsWarning)
            {
                return Warn(p, () => BeginDating(p, data));
            }
            return BeginDating(p, data);
        }

        private static DialogEntry BeginDating(SocialPerson p, Dictionary<string, string> data)
        {
            LoveState.File.DateOfferWho = -1;
            if (LoveHooks.Taken)
            {
                LoveHooks.BeginSecond(p);
            }
            SocialLife.Remember(p, MemoryCode.AskedOut);
            SocialLife.SetStage(p, SocialStage.Dating);
            return Last(p, SocialLines.Pick(SocialCatalogue.AskOutYes), data);
        }

        // ---------------------------------------------------------------- an evening's offer, and the morning after

        /// <summary>Their offer: the player chooses where, or not tonight. In person, in the panel, with the person beside them or not there at all (a party's end).</summary>
        public static DialogEntry Offer(SocialPerson p, SceneTalk talk)
        {
            LoveOffer o = talk.Offer;
            Dictionary<string, string> data = SocialLife.Data(p);
            string[] pool = o.Kind == NightKind.Party ? LoveCatalogue.OfferParty : o.Facts.Drinks >= 2f && o.Variant % 2 == 0 ? LoveCatalogue.OfferTipsy : LoveCatalogue.OfferDrinks;
            DialogEntry entry = Say(p, pool[o.Variant % pool.Length], data);
            entry.ConfirmTextOverride = "reallife_love_btn_theirs".Localize();
            entry.OnConfirm = () =>
            {
                o.Chosen = 1;
                return Done();
            };
            entry.SecondOptionTextOverride = "reallife_love_btn_mine";
            entry.OnSecondOption = () =>
            {
                o.Chosen = 2;
                LoveNight.AskedBack(o);
                if (o.Accepted)
                {
                    DialogEntry yes = Say(p, Pick(LoveCatalogue.SaysYes, SocialLife.Day, p.Index), data);
                    yes.ConfirmTextOverride = "reallife_love_btn_letsgo".Localize();
                    yes.OnConfirm = Done;
                    return yes;
                }
                DialogEntry no = Say(p, Pick(LoveCatalogue.SaysNo, SocialLife.Day, p.Index), data);
                no.ConfirmTextOverride = "reallife_love_btn_fine".Localize();
                no.OnConfirm = Done;
                return no;
            };
            entry.CancelTextOverride = "reallife_love_btn_notnight";
            entry.OnCancel = () =>
            {
                o.Chosen = 3;
                SocialLife.Toast(NotificationType.Info, Pick(LoveCatalogue.Declined, SocialLife.Day, p.Index), data, 8f);
                Leave();
            };
            return entry;
        }

        /// <summary>The morning after, told in the panel with no character: where the player wakes, how the head is, what the night came to. Never described: only what is said and felt.</summary>
        public static DialogEntry Morning(SceneTalk talk)
        {
            LoveOffer o = talk.Offer;
            SocialPerson p = o.Person;
            LoveHook h = o.Hook;
            int day = SocialLife.Day;
            Dictionary<string, string> data = SocialLife.Data(p);
            data["amount"] = SocialLife.Money(Prices.At(LoveRules.MishapCost(p.Index + h.Day)));
            data["partner"] = SocialState.Partner != null ? SocialLife.First(SocialState.Partner) : string.Empty;
            string first = SocialLife.T(Pick(h.Mine ? LoveCatalogue.MorningMine : LoveCatalogue.MorningTheirs, day, p.Index), data);
            if (h.Drinks >= 20)
            {
                first += " " + SocialLife.T(Pick(LoveCatalogue.MorningHead, day, p.Index), data);
            }
            string second = SocialLife.T(Pick(LoveCatalogue.Outcome[(int)h.Outcome], day, p.Index + (int)h.Outcome), data);
            if (h.Taken)
            {
                second += " " + SocialLife.T(Pick(LoveCatalogue.MorningGuilt, day, p.Index), data);
            }
            DialogEntry page = Built(p, first);
            page.ConfirmTextOverride = "reallife_social_btn_go_on".Localize();
            page.OnConfirm = () =>
            {
                DialogEntry next = Built(p, second);
                next.ConfirmTextOverride = "reallife_love_btn_leave".Localize();
                next.OnConfirm = Done;
                return next;
            };
            return page;
        }

        /// <summary>What the night did, as an outing's card tells it: the figures that moved, a cost, how it leaves the player, what comes next.</summary>
        public static string CardOf(LoveOffer o)
        {
            LoveHook h = o.Hook;
            SocialPerson p = o.Person;
            LoveRules.Figures(h.Outcome, out float fam, out float aff, out float trust);
            var text = new System.Text.StringBuilder(256);
            if (h.Outcome == NightOutcome.Mishap)
            {
                text.Append(SocialLife.T("reallife_social_card_cost")).Append("   <color=#FF8A70>-").Append(SocialLife.Money(Prices.At(LoveRules.MishapCost(p.Index + h.Day)))).Append("</color>\n");
            }
            text.Append(SocialLife.T("reallife_social_card_familiarity")).Append("   ").Append(LoveLife.Signed(fam)).Append('\n');
            text.Append(SocialLife.T("reallife_social_card_affection")).Append("   ").Append(LoveLife.Signed(aff)).Append('\n');
            text.Append(SocialLife.T("reallife_social_card_trust")).Append("   ").Append(LoveLife.Signed(trust)).Append('\n');
            bool up = LoveRules.MoodLifts(h.Outcome);
            bool down = LoveRules.MoodDips(h.Outcome, h.Taken);
            text.Append(SocialLife.T("reallife_social_card_mood")).Append("   ").
                    Append(SocialLife.T(up ? "reallife_social_card_mood_up" : down ? "reallife_social_card_mood_down" : "reallife_love_card_mood_same")).Append('\n');
            text.Append(SocialLife.T(h.Outcome == NightOutcome.Awkward ? "reallife_love_card_next_quiet" : h.Outcome == NightOutcome.WantsMore ? "reallife_love_card_next_date"
                : h.Outcome == NightOutcome.GotAround ? "reallife_love_card_next_around" : h.Outcome == NightOutcome.Regret ? "reallife_love_card_next_regret" : "reallife_love_card_next_text",
                SocialLife.Data(p)));
            return text.ToString();
        }

        // ---------------------------------------------------------------- brought up in later talks, and on the People page

        /// <summary>A night, a forgiven catch or a child they bring up in a call, now and then: a topic of its own. Null for nothing to bring up.</summary>
        public static SocialTopic Topic(SocialPerson p)
        {
            if (!LoveState.Loaded || p == null || p.Stage < SocialStage.Friend)
            {
                return null;
            }
            int day = SocialLife.Day;
            LoveFile f = LoveState.File;
            if (p.LastTopic != LoveCatalogue.TopicNight && Recent(p, MemoryCode.Night, day, 6) || Recent(p, MemoryCode.NightAwkward, day, 6) || Recent(p, MemoryCode.NightRegret, day, 6))
            {
                if (p.LastTopic != LoveCatalogue.TopicNight && LoveRules.Roll(LoveState.Seed, day, p.Index, LoveRules.SaltLine) < 550)
                {
                    return LoveCatalogue.Topics[0];
                }
            }
            if (p.LastTopic != LoveCatalogue.TopicForgave && Recent(p, MemoryCode.Forgave, day, 10) && LoveRules.Roll(LoveState.Seed, day, p.Index + 1, LoveRules.SaltLine) < 500)
            {
                return LoveCatalogue.Topics[1];
            }
            LoveBaby b = f.BabyOf(p.Index);
            if (b != null && b.Born && b.Choice != LoveChoice.StayOut && p.LastTopic != LoveCatalogue.TopicChild && LoveRules.Roll(LoveState.Seed, day, p.Index + 2, LoveRules.SaltLine) < 300)
            {
                return LoveCatalogue.Topics[2];
            }
            return null;
        }

        private static bool Recent(SocialPerson p, MemoryCode code, int day, int days)
        {
            for (int i = p.Memories.Count - 1; i >= 0; i--)
            {
                if (p.Memories[i].Code == code && day - p.Memories[i].Day <= days)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The lines the love life adds to a person's page in People: whose second they are, a child, an offer to date, and what comes next after living together.</summary>
        public static string PeopleLine(SocialPerson p)
        {
            if (!LoveState.Loaded || p == null)
            {
                return null;
            }
            LoveFile f = LoveState.File;
            var text = new System.Text.StringBuilder(160);
            SocialPerson first = SocialState.Partner;
            if (LoveState.IsSecond(p.Index) && first != null && first != p)
            {
                text.Append(SocialLife.T(LoveCatalogue.PeopleSecond, SocialLife.Data(p, "partner", SocialLife.First(first))));
            }
            else if (f.Second >= 0 && p == first && LoveState.SecondPerson != null)
            {
                text.Append(SocialLife.T(LoveCatalogue.PeopleSecondFor, SocialLife.Data(p, "other", SocialLife.First(LoveState.SecondPerson))));
            }
            LoveBaby b = LoveBabies.ChildOf(p);
            if (b != null)
            {
                text.Append(text.Length > 0 ? "\n" : string.Empty).Append(SocialLife.T(LoveCatalogue.PeopleCoParent, SocialLife.Data(p, "child",
                        b.Name ?? SocialLife.T("reallife_love_child_unnamed"))));
            }
            if (f.DateOfferWho == p.Index)
            {
                text.Append(text.Length > 0 ? "\n" : string.Empty).Append(SocialLife.T(LoveCatalogue.PeopleDateOffer, SocialLife.Data(p)));
            }
            return text.Length > 0 ? text.ToString() : null;
        }
    }
}
