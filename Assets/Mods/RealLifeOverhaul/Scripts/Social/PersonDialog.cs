using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A call to or from somebody the player knows: a chat, plans, what they asked, and the other things to say. A chat's
    /// topic is answered with one of two answers, or with the player's own typed words (PersonDialog.Composer). A call to
    /// somebody who just texted opens on what they wrote about, as its first topic (PersonDialog.CallBack). A call placed to
    /// somebody whose ring is on is their call answered (SocialDirector.TakeRing). Mom's, on a family dinner day while her
    /// table is set, opens on whether the player is coming over, and "Something else" offers it too (PhoneContacts.MomsDinner).
    /// </summary>
    public sealed partial class PersonDialog : Dialog
    {
        private static int GiftPrice => Prices.At(60);

        /// <summary>
        /// A test's topic for the first chat of the next call with this person (SocialDirector.TestComposer); the next call to
        /// open, with anybody, clears it. -1 for none.
        /// </summary>
        internal static int TestTopic = -1, TestTopicFor = -1;

        private readonly SocialPerson _p;
        private readonly List<string> _actions = new List<string>(8);
        private int _topics;
        private float _moodRoll = -1f;   // the call's one draw of whether a rough day says no to plans (SocialRules.MoodSaysNo); -1 until plans come up
        private int _testTopic;          // a test's topic for this call's first chat; -1 for the topic by what happened

        public PersonDialog(int index)
        {
            _testTopic = TestTopicFor == index ? TestTopic : -1;
            TestTopic = TestTopicFor = -1;
            _p = SocialState.Get(index);
            // Answered from the ring's panel or notice (Incoming), or by calling them from the phone while it rings.
            bool incoming = SocialPhone.Incoming == index || SocialDirector.TakeRing(index);
            SocialPhone.Incoming = -1;
            if (_p == null || !_p.Met || !Settings.Social)
            {
                Show(PhoneContacts.Closing("reallife_social_call_off"));
                return;
            }
            npcNameKey = SocialPhone.IdOf(_p);
            // Mom's text on a dinner day says to ring her: her call asks first whether the player is coming over now, and she
            // stays up for it.
            string dinner = _p.IsFamily ? PhoneContacts.MomsDinnerNow() : null;
            if (!incoming && dinner == null && _p.Asleep(SocialLife.Hour))
            {
                Show(Close(SocialLines.Pick(SocialCatalogue.Voicemail)));
                return;
            }
            SocialOffer offer = SocialState.Offer;
            SocialPlan plan = SocialState.Plan;
            Func<DialogEntry> open;
            if (incoming)
            {
                open = () =>
                {
                    DialogEntry entry = Line(SocialLines.Pick(SocialCatalogue.RingOpen));
                    entry.ConfirmTextOverride = "reallife_social_btn_whats_up".Localize();
                    entry.OnConfirm = () => offer.Active && offer.Person == _p.Index ? Asked() : Chat();
                    return entry;
                };
            }
            else if (offer.Active && offer.Person == _p.Index)
            {
                open = Asked;
            }
            else if (plan.Active && plan.Person == _p.Index)
            {
                open = Planned;
            }
            else
            {
                open = Root;
            }
            Show(dinner != null ? PhoneContacts.DinnerAsk(dinner, open, PhoneContacts.HangUp) : open());
        }

        private static void Show(DialogEntry entry)
        {
            DialogController.current.ShowEntry(Guard.Entry(entry));
        }

        private DialogEntry Line(string key, Dictionary<string, string> data = null)
        {
            data = data ?? SocialLife.Data(_p);
            SocialLife.Said(_p, key, data);
            TheySaid(_p.LastSaid);
            return new DialogEntry { messageData = key.Localize(data), OnCancel = PhoneContacts.HangUp };
        }

        private DialogEntry Close(string key, Dictionary<string, string> data = null)
        {
            _typing = null;
            data = data ?? SocialLife.Data(_p);
            SocialLife.Said(_p, key, data);
            return PhoneContacts.Closing(key, data);
        }

        private DialogEntry Root()
        {
            DialogEntry callBack = CallBack();
            if (callBack != null)
            {
                return callBack;   // (what they just texted about, as the call's first topic: PersonDialog.CallBack)
            }
            string[] pool = _p.Drifted ? SocialCatalogue.CallDrifted
                : _p.LastCallDay == SocialLife.Day ? SocialCatalogue.CallAgain
                : _p.IsFamily ? SocialCatalogue.CallMom
                : _p.Together ? SocialCatalogue.CallLove
                : _p.Stage == SocialStage.CloseFriend ? SocialCatalogue.CallClose
                : _p.Stage == SocialStage.Friend ? SocialCatalogue.CallFriend
                : SocialCatalogue.CallAcquaintance;
            DialogEntry entry = Line(SocialLines.Pick(pool));
            entry.ConfirmTextOverride = "reallife_social_btn_chat".Localize();
            entry.OnConfirm = Chat;
            entry.SecondOptionTextOverride = "reallife_social_btn_else";
            entry.OnSecondOption = () => Menu(0);
            return entry;
        }

        private DialogEntry Chat()
        {
            if (SocialRules.ChatHad(_topics, _p.LastCallDay, SocialLife.Day))
            {
                // One chat a day; anything else can still be asked.
                SocialLife.Contacted(_p);
                DialogEntry again = Line("reallife_social_chat_again");
                again.ConfirmTextOverride = "reallife_social_btn_else".Localize();
                again.OnConfirm = () => Menu(0);
                return again;
            }
            SocialTopic topic = SocialTalk.Pick(_p, out Dictionary<string, string> data, _testTopic);
            _testTopic = -1;
            DialogEntry entry = Line(SocialLines.Pick(topic.Open), data);
            if (Composing(topic, data, entry))
            {
                return entry;   // (the player's own words answer it: PersonDialog.Composer)
            }
            entry.ConfirmTextOverride = topic.ReplyA.Localize(data);
            entry.OnConfirm = () => Reply(topic, true, data);
            entry.SecondOptionTextOverride = topic.ReplyB;
            entry.OnSecondOption = () => Reply(topic, false, data);
            return entry;
        }

        private DialogEntry Reply(SocialTopic topic, bool warm, Dictionary<string, string> data)
        {
            Chose(topic, warm, data);
            return Onward(Line(Answered(topic, warm), data));
        }

        // The answer the player pressed, kept in the contact's talk history as the player's line, before their reaction is.
        private static void Chose(SocialTopic topic, bool warm, Dictionary<string, string> data)
        {
            TalkLogGame.Said(SocialLife.T(warm ? topic.ReplyA : topic.ReplyB, data));
        }

        // What an answer is worth, a choice's or typed words' alike, settled now; the key of their written reaction.
        private string Answered(SocialTopic topic, bool warm)
        {
            string react = SocialTalk.Answer(_p, topic, warm, false, _topics == 0);
            _topics++;
            return react;
        }

        // Their reaction, and after it another topic or the goodbye, or something else.
        private DialogEntry Onward(DialogEntry entry)
        {
            if (_topics < SocialRules.CallTopics)
            {
                entry.ConfirmTextOverride = "reallife_social_btn_go_on".Localize();
                entry.OnConfirm = Chat;
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_social_btn_bye".Localize();
                entry.OnConfirm = () => Close(SocialLines.Pick(_p.IsFamily ? SocialCatalogue.ChatByeMom : SocialCatalogue.ChatBye));
            }
            entry.SecondOptionTextOverride = "reallife_social_btn_else";
            entry.OnSecondOption = () => Menu(0);
            return entry;
        }

        // One thing at a time, and only what is possible now.
        private DialogEntry Menu(int at)
        {
            _typing = null;
            _actions.Clear();
            int day = SocialLife.Day;
            if (_p.IsFamily && PhoneContacts.MomsDinnerNow() != null)
            {
                _actions.Add("dinner");   // first: her table is set today, and only today
            }
            string favour = SocialPhone.NetworkLabel != null && SocialPhone.NetworkAsk != null ? SocialPhone.NetworkLabel(_p) : null;
            if (favour != null)
            {
                _actions.Add("network");   // they texted to say they could help
            }
            _actions.Add("plans");
            string quiet = SocialPhone.QuietLabel != null && SocialPhone.QuietAsk != null ? SocialPhone.QuietLabel(_p) : null;
            if (quiet != null)
            {
                _actions.Add("quiet");   // after the plans: a quiet second choice, never the first thing said
            }
            if (SocialTalk.BirthdayIn(_p) == 0 && _p.BirthdayYear != Year(day))
            {
                _actions.Add("birthday");
            }
            if (_p.Debt > 0)
            {
                _actions.Add("repay");
            }
            if (_p.Compatible && !_p.Together && _p.Stage >= SocialStage.Friend && SocialState.Partner == null)
            {
                _actions.Add("askout");
            }
            if (_p.Stage == SocialStage.Dating)
            {
                _actions.Add("official");
            }
            if (_p.Stage == SocialStage.Partner && !_p.MovedIn)
            {
                _actions.Add("movein");
            }
            if (SocialPhone.FamilyMatters != null && SocialPhone.HasFamilyMatters != null && SocialPhone.HasFamilyMatters(_p))
            {
                _actions.Add("family");
            }
            if (_p.Stage >= SocialStage.Friend && _p.Debt == 0)
            {
                _actions.Add("borrow");
            }
            if (_p.Stage >= SocialStage.Friend && SocialDirector.AnyStranger())
            {
                _actions.Add("introduce");
            }
            string action = _actions[at % _actions.Count];
            int next = (at + 1) % _actions.Count;

            DialogEntry entry = new DialogEntry { messageData = "reallife_social_menu".Localize(SocialLife.Data(_p)), OnCancel = PhoneContacts.HangUp };
            switch (action)
            {
                case "birthday":
                    entry.ConfirmTextOverride = "reallife_social_btn_birthday".Localize();
                    entry.OnConfirm = Birthday;
                    break;
                case "repay":
                    entry.ConfirmTextOverride = "reallife_social_btn_repay".Localize(SocialLife.Data(_p, "amount", SocialLife.Money(_p.Debt)));
                    entry.OnConfirm = Repay;
                    entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(_p.Debt));
                    break;
                case "askout":
                    entry.ConfirmTextOverride = "reallife_social_btn_askout".Localize();
                    entry.OnConfirm = AskOut;
                    break;
                case "official":
                    entry.ConfirmTextOverride = "reallife_social_btn_official".Localize();
                    entry.OnConfirm = Official;
                    break;
                case "movein":
                    entry.ConfirmTextOverride = "reallife_social_btn_movein".Localize();
                    entry.OnConfirm = MoveIn;
                    break;
                case "borrow":
                    entry.ConfirmTextOverride = "reallife_social_btn_borrow".Localize(SocialLife.Data(_p, "amount", SocialLife.Money(BorrowAmount())));
                    entry.OnConfirm = Borrow;
                    entry.Gets(DialogButton.Confirm, "reallife_ui_dialog_cost_ask_in", "amount", SocialLife.Money(BorrowAmount()));
                    break;
                case "introduce":
                    entry.ConfirmTextOverride = "reallife_social_btn_introduce".Localize();
                    entry.OnConfirm = Introduce;
                    break;
                case "network":
                    entry.ConfirmTextOverride = favour.Localize();
                    entry.OnConfirm = () => SocialPhone.NetworkAsk(_p);
                    break;
                case "quiet":
                    entry.ConfirmTextOverride = quiet.Localize();
                    entry.OnConfirm = () => SocialPhone.QuietAsk(_p);
                    break;
                case "family":
                    entry.ConfirmTextOverride = "reallife_social_btn_family".Localize();
                    entry.OnConfirm = SocialPhone.FamilyMatters;
                    break;
                case "dinner":
                    entry.ConfirmTextOverride = "reallife_family_do_moms_yes".Localize();
                    entry.OnConfirm = () => PhoneContacts.GoToMoms != null ? PhoneContacts.GoToMoms() : Close("reallife_social_call_off");
                    break;
                default:
                    entry.ConfirmTextOverride = "reallife_social_btn_plans".Localize();
                    entry.OnConfirm = () => Plans(0, 0);
                    break;
            }
            if (_actions.Count > 1)
            {
                entry.SecondOptionTextOverride = "reallife_social_btn_else";
                entry.OnSecondOption = () => Menu(next);
            }
            return entry;
        }

        private static int Year(int day)
        {
            return (Mathf.Max(1, day) - 1) / SocialPlaces.DaysPerYear;
        }

        // The one they would like first, then the rest in order; only what can be done from here, at an hour they are free
        // (found now and kept: what they put forward is what they agree to). Whether they will not make plans at all is said
        // here, before anything is put forward; noHour says some outing was left out for want of a free hour.
        private DialogEntry Plans(int at, int tried, bool noHour = false)
        {
            SocialPlan plan = SocialState.Plan;
            if (plan.Active)
            {
                SocialPerson other = SocialState.Get(plan.Person);
                Dictionary<string, string> busy = SocialLife.Data(_p, "time", SocialLife.Clock(plan.Time));
                busy["other"] = other != null ? other.Name : string.Empty;
                return Close("reallife_social_plan_already", busy);
            }
            if (!Settings.SocialOutings)
            {
                return Close("reallife_social_plan_off");
            }
            if (_moodRoll < 0f)
            {
                _moodRoll = Random.value;
            }
            if (SocialRules.MoodSaysNo(_p.Mood, false, _moodRoll))
            {
                return Close(SocialLines.Pick(SocialCatalogue.PlanMood));
            }
            const int kinds = (int)PlanKind.Business + 1;
            if (tried >= kinds)
            {
                return noHour ? Close(SocialLines.Pick(SocialCatalogue.PlanBusy)) : Close("reallife_social_plan_nowhere");
            }
            var kind = (PlanKind)(((int)_p.Wants + at) % kinds);
            OutingDef def = SocialCatalogue.Outing(kind);
            if (_p.Stage < def.Needs && !_p.IsFamily)
            {
                return Plans(at + 1, tried + 1, noHour);
            }
            if (kind == PlanKind.Workout && !_p.Has(SocialTrait.Sporty) && _p.Stage < SocialStage.Friend)
            {
                return Plans(at + 1, tried + 1, noHour);
            }
            if (kind == PlanKind.Drive && JobDirector.NearestCar(250f) == null)
            {
                return Plans(at + 1, tried + 1, noHour);
            }
            PlanFind found = SocialLife.PlaceAndHour(_p, kind, SocialDirector.Where(), 40f, 1800f, SocialLife.Now + SocialRules.PlanLead, out Venue venue, out float first);
            if (found == PlanFind.NoHour)
            {
                return Plans(at + 1, tried + 1, true);
            }
            if (found == PlanFind.NoPlace)
            {
                return Plans(at + 1, tried + 1, noHour);
            }
            if (found == PlanFind.Shut)
            {
                return ShutWalk(kind, false, () => Plans(at + 1, 0)) ?? Plans(at + 1, tried + 1, noHour);
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "place", venue.Name);
            data["cost"] = SocialLife.Money(def.Cost);
            DialogEntry entry = Line(def.Offer, data);
            entry.ConfirmTextOverride = "reallife_social_btn_lets".Localize();
            entry.OnConfirm = () => When(kind, venue, first, false);
            entry.SecondOptionTextOverride = "reallife_social_btn_other_idea";
            entry.OnSecondOption = () => Plans(at + 1, 0);
            entry.Says(DialogButton.Confirm, "reallife_social_says_minutes", "minutes", def.Minutes.ToString());
            return def.Cost > 0 ? entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_on_the_day", "amount", SocialLife.Money(def.Cost)) : entry;   // each pays their share at the outing
        }

        // Every place of the kind nearby is shut at every free hour of theirs looked at: a walk instead, said as such, with
        // the other button for something else, or, when it was their invitation, for not this time. Null when no walk can be
        // had either (no free hour, or outings of the kind not yet theirs to suggest), for the caller to go on as before.
        private DialogEntry ShutWalk(PlanKind shut, bool theirIdea, Func<DialogEntry> other)
        {
            OutingDef walk = SocialCatalogue.Outing(PlanKind.Walk);
            if ((_p.Stage < walk.Needs && !_p.IsFamily)
                || SocialLife.PlaceAndHour(_p, PlanKind.Walk, SocialDirector.Where(), 40f, 1800f, SocialLife.Now + SocialRules.PlanLead, out Venue venue, out float first) != PlanFind.Found)
            {
                return null;
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "place", venue.Name);
            data["outing"] = SocialLife.T(SocialCatalogue.Outing(shut).Name);
            DialogEntry entry = Line("reallife_social_plan_shut_walk", data);
            entry.ConfirmTextOverride = "reallife_social_btn_lets".Localize();
            entry.OnConfirm = () => When(PlanKind.Walk, venue, first, theirIdea);
            entry.SecondOptionTextOverride = theirIdea ? "reallife_social_btn_not_this_time" : "reallife_social_btn_other_idea";
            entry.OnSecondOption = other;
            return entry;
        }

        // The hour found when the outing was put forward, and a later one if they have it and the place is open then too:
        // no refusal is left at this step.
        private DialogEntry When(PlanKind kind, Venue venue, float first, bool theirIdea)
        {
            float second = SocialLife.NextOpenSlot(_p, kind, venue, first + 180f);
            Dictionary<string, string> data = SocialLife.Data(_p, "a", SocialLife.Clock(first));
            data["b"] = second > 0f ? SocialLife.Clock(second) : string.Empty;
            DialogEntry entry = Line(second > 0f ? "reallife_social_when_two" : "reallife_social_when_one", data);
            entry.ConfirmTextOverride = (second > 0f ? "reallife_social_btn_first" : "reallife_social_btn_fine").Localize();
            entry.OnConfirm = () => Agree(kind, venue, first, theirIdea);
            if (second > 0f)
            {
                entry.SecondOptionTextOverride = "reallife_social_btn_second";
                entry.OnSecondOption = () => Agree(kind, venue, second, theirIdea);
            }
            return entry;
        }

        private DialogEntry Agree(PlanKind kind, Venue venue, float time, bool theirIdea)
        {
            if (!SocialLife.MakePlan(_p, kind, time, venue, theirIdea, out string reason))
            {
                return Close(reason ?? "reallife_social_plan_nowhere");
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "place", venue.Name);
            data["time"] = SocialLife.Clock(time);
            return Close(SocialLines.Pick(SocialCatalogue.PlanYes), data);
        }

        private DialogEntry Planned()
        {
            SocialPlan plan = SocialState.Plan;
            Dictionary<string, string> data = SocialLife.Data(_p, "place", plan.Place);
            data["time"] = SocialLife.Clock(plan.Time);
            data["outing"] = SocialLife.T(SocialCatalogue.Outing(plan.Kind).Name);
            DialogEntry entry = Line("reallife_social_plan_reminder", data);
            entry.ConfirmTextOverride = "reallife_social_btn_see_you".Localize();
            entry.OnConfirm = () => Close(SocialLines.Pick(SocialCatalogue.ChatBye));
            entry.SecondOptionTextOverride = "reallife_social_btn_cancel_plan";
            entry.OnSecondOption = () =>
            {
                if (SocialDirector.Busy)
                {
                    return Close("reallife_social_plan_too_late");
                }
                SocialLife.CancelByPlayer();
                return Close(SocialLines.Pick(SocialCatalogue.PlanCancelled));
            };
            return entry;
        }

        private DialogEntry Asked()
        {
            SocialOffer offer = SocialState.Offer;
            int kindId = offer.Kind;
            int amount = offer.Amount;
            if (kindId == SocialOffer.Loan)
            {
                DialogEntry loan = Line(SocialLines.Pick(SocialCatalogue.LoanAsk), SocialLife.Data(_p, "amount", SocialLife.Money(amount)));
                loan.ConfirmTextOverride = "reallife_social_btn_lend".Localize();
                loan.OnConfirm = () =>
                {
                    SocialState.Offer.Clear();
                    if (!SocialLife.Pay(amount, "reallife_transaction_social_loan"))
                    {
                        return Close("reallife_social_loan_short");
                    }
                    _p.Loan = amount;
                    _p.LoanDue = SocialLife.Day + 5 + Random.Range(0, 5);
                    SocialLife.Remember(_p, MemoryCode.Loan);
                    SocialLife.Contacted(_p);
                    SocialLife.Change(_p, 1f, SocialRules.FavourAffection * 0.5f, SocialRules.FavourTrust * 0.5f);
                    return Close(SocialLines.Pick(SocialCatalogue.LoanThanks));
                };
                loan.SecondOptionTextOverride = "reallife_social_btn_cant";
                loan.OnSecondOption = () => Refuse(SocialLines.Pick(SocialCatalogue.LoanRefused));
                return loan;
            }

            var kind = (PlanKind)kindId;
            OutingDef def = SocialCatalogue.Outing(kind);
            Vector3 player = SocialDirector.Where();
            if (SocialState.Plan.Active)
            {
                SocialState.Offer.Clear();
                return Close("reallife_social_asked_busy");
            }
            if (kind == PlanKind.Lift)
            {
                if (!SocialPlaces.Find(kind, player, 70f, 450f, out Venue pickup) || !SocialPlaces.Find(kind, pickup.Front, 350f, 1200f, out Venue drop, pickup.Building))
                {
                    SocialState.Offer.Clear();
                    return Close("reallife_social_asked_sorted");
                }
                Dictionary<string, string> data = SocialLife.Data(_p, "place", pickup.Name);
                data["to"] = drop.Name;
                DialogEntry lift = Line(def.Offer, data);
                lift.ConfirmTextOverride = "reallife_social_btn_on_my_way".Localize();
                lift.OnConfirm = () =>
                {
                    SocialState.Offer.Clear();
                    if (JobDirector.NearestCar(120f) == null)
                    {
                        return Close("reallife_social_lift_nocar");
                    }
                    if (!SocialLife.MakePlan(_p, kind, SocialLife.Now + 5f, pickup, true, out string reason))
                    {
                        return Close(reason ?? "reallife_social_asked_sorted");
                    }
                    SocialState.Plan.ToStreet = drop.Street;
                    SocialState.Plan.ToNumber = drop.Number;
                    return Close("reallife_social_lift_coming", data);
                };
                lift.SecondOptionTextOverride = "reallife_social_btn_cant";
                lift.OnSecondOption = () => Refuse("reallife_social_favour_refused");
                return lift;
            }
            if (kind == PlanKind.Moving)
            {
                float time = SocialLife.NextSlot(_p, PlanKind.Walk, SocialLife.Now + SocialRules.PlanLead);
                if (time < 0f || !SocialPlaces.Find(kind, player, 60f, 700f, out Venue old) || !SocialPlaces.Find(kind, old.Front, 22f, 75f, out Venue fresh, old.Building))
                {
                    SocialState.Offer.Clear();
                    return Close("reallife_social_asked_sorted");
                }
                Dictionary<string, string> data = SocialLife.Data(_p, "place", old.Name);
                data["to"] = fresh.Name;
                DialogEntry moving = Line(def.Offer, data);
                moving.ConfirmTextOverride = "reallife_social_btn_ill_help".Localize();
                moving.OnConfirm = () =>
                {
                    SocialState.Offer.Clear();
                    if (!SocialLife.MakePlan(_p, kind, time, old, true, out string reason))
                    {
                        return Close("reallife_social_asked_sorted");
                    }
                    SocialState.Plan.ToStreet = fresh.Street;
                    SocialState.Plan.ToNumber = fresh.Number;
                    data["time"] = SocialLife.Clock(time);
                    return Close("reallife_social_moving_agreed", data);
                };
                moving.SecondOptionTextOverride = "reallife_social_btn_cant";
                moving.OnSecondOption = () => Refuse("reallife_social_favour_refused");
                return moving;
            }

            // An invitation: the place and the hour are found now, near the player, before it is put to them, the place open
            // for the whole outing then; no free hour in the days ahead is said now, not after the player says yes, and with
            // every place of the kind shut at their free hours they put a walk forward instead.
            Venue venue = default;
            float first = -1f;
            PlanFind found = PlanFind.NoPlace;
            if (Settings.SocialOutings)
            {
                found = SocialLife.PlaceAndHour(_p, kind, player, 40f, 1800f, SocialLife.Now + SocialRules.PlanLead, out venue, out first);
            }
            if (found != PlanFind.Found)
            {
                SocialState.Offer.Clear();
                DialogEntry walk = found == PlanFind.Shut ? ShutWalk(kind, true, Declined) : null;
                return walk ?? Close(found == PlanFind.NoHour ? SocialLines.Pick(SocialCatalogue.PlanBusy) : "reallife_social_plan_nowhere");
            }
            Dictionary<string, string> invite = SocialLife.Data(_p, "place", venue.Name);
            invite["cost"] = SocialLife.Money(def.Cost);
            DialogEntry entry = Line(def.Offer, invite);
            entry.ConfirmTextOverride = "reallife_social_btn_im_in".Localize();
            entry.OnConfirm = () =>
            {
                SocialState.Offer.Clear();
                return When(kind, venue, first, true);
            };
            entry.SecondOptionTextOverride = "reallife_social_btn_not_this_time";
            entry.OnSecondOption = Declined;
            entry.Says(DialogButton.Confirm, "reallife_social_says_minutes", "minutes", def.Minutes.ToString());
            return def.Cost > 0 ? entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_on_the_day", "amount", SocialLife.Money(def.Cost)) : entry;
        }

        // Their invitation turned down, the outing they asked for or the walk they put forward in its place: the same small
        // cost either way.
        private DialogEntry Declined()
        {
            SocialState.Offer.Clear();
            SocialLife.Contacted(_p);
            SocialLife.Change(_p, 0f, -1f, 0f);
            return Close("reallife_social_invite_declined");
        }

        private DialogEntry Refuse(string line)
        {
            SocialState.Offer.Clear();
            SocialLife.Contacted(_p);
            SocialLife.Change(_p, 0f, SocialRules.FavourRefusedAffection, 0f);
            return Close(line);
        }

        private DialogEntry Birthday()
        {
            _p.BirthdayYear = Year(SocialLife.Day);
            SocialLife.Remember(_p, MemoryCode.Birthday);
            SocialLife.Contacted(_p);
            SocialLife.Change(_p, 1f, SocialRules.BirthdayAffection, SocialRules.BirthdayTrust);
            DialogEntry entry = Line(SocialLines.Pick(SocialCatalogue.BirthdayThanks));
            entry.ConfirmTextOverride = "reallife_social_btn_gift".Localize(SocialLife.Data(_p, "amount", SocialLife.Money(GiftPrice)));
            entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(GiftPrice));
            entry.OnConfirm = () =>
            {
                if (!SocialLife.Pay(GiftPrice, "reallife_transaction_social_gift"))
                {
                    return Close("reallife_social_gift_short");
                }
                SocialLife.Change(_p, 0f, 4f, 1f);
                return Close("reallife_social_gift_thanks");
            };
            entry.SecondOptionTextOverride = "reallife_social_btn_bye";
            entry.OnSecondOption = () => Close(SocialLines.Pick(_p.IsFamily ? SocialCatalogue.ChatByeMom : SocialCatalogue.ChatBye));
            return entry;
        }

        private DialogEntry Repay()
        {
            if (!SocialLife.Pay(_p.Debt, "reallife_transaction_social_repay"))
            {
                return Close("reallife_social_repay_short");
            }
            _p.Debt = 0;
            SocialLife.Contacted(_p);
            SocialLife.Change(_p, 0f, 1f, 3f);
            return Close("reallife_social_repay_thanks");
        }

        private int BorrowAmount()
        {
            float amount = _p.IsFamily ? 500f : _p.Together ? 800f : _p.Stage == SocialStage.CloseFriend ? 400f : 150f;
            if (_p.Has(SocialTrait.Generous))
            {
                amount *= 1.5f;
            }
            if (_p.Has(SocialTrait.Careful))
            {
                amount *= 0.5f;
            }
            return Mathf.RoundToInt(Prices.At(amount) / 25f) * 25;
        }

        private DialogEntry Borrow()
        {
            int day = SocialLife.Day;
            if (day - _p.AskDay > SocialRules.AskWindowDays)
            {
                _p.AsksInWindow = 0;
            }
            _p.AskDay = day;
            _p.AsksInWindow++;
            SocialLife.Contacted(_p);
            if (_p.AsksInWindow > SocialRules.AsksAllowed)
            {
                SocialLife.Change(_p, 0f, -1f, SocialRules.TooManyAsksTrust);
                return Close("reallife_social_borrow_toomany");
            }
            if (_p.Trust < 35f)
            {
                SocialLife.Change(_p, 0f, 0f, SocialRules.AskTrust);
                return Close(SocialLines.Pick(SocialCatalogue.BorrowNo));
            }
            int amount = BorrowAmount();
            SocialLife.Give(amount, "reallife_transaction_social_borrowed");
            _p.Debt = amount;
            _p.DebtDay = day;
            SocialLife.Remember(_p, MemoryCode.Borrowed);
            SocialLife.Change(_p, 0f, 0f, SocialRules.AskTrust);
            Dictionary<string, string> data = SocialLife.Data(_p, "amount", SocialLife.Money(amount));
            data["days"] = SocialRules.RepayDays.ToString();
            return Close(SocialLines.Pick(SocialCatalogue.BorrowYes), data);
        }

        // Said as a number of days to wait, one day or more; the key is the "_one" text for a day.
        private DialogEntry Wait(string key, int days)
        {
            return Close(days == 1 ? key + "_one" : key, SocialLife.Data(_p, "days", days.ToString()));
        }

        private DialogEntry AskOut()
        {
            SocialLife.Contacted(_p);
            int wait = SocialRules.AskOutWait(_p, SocialLife.Day);
            if (wait > 0)
            {
                // Asked again too soon: nothing is lost and nothing is remembered, and the answer says when to ask.
                return Wait("reallife_social_askout_wait", wait);
            }
            SocialLife.Remember(_p, MemoryCode.AskedOut);
            if (SocialRules.WouldDate(_p) && SocialState.Partner == null)
            {
                SocialLife.SetStage(_p, SocialStage.Dating);
                return Close(SocialLines.Pick(SocialCatalogue.AskOutYes));
            }
            SocialLife.Change(_p, 0f, -1f, 0f);
            return Close(SocialLines.Pick(SocialCatalogue.AskOutNo));
        }

        private DialogEntry Official()
        {
            SocialLife.Contacted(_p);
            if (!SocialRules.WouldCommit(_p, SocialLife.Day))
            {
                int wait = SocialRules.CommitWait(_p, SocialLife.Day);
                return wait > 0 ? Wait("reallife_social_official_wait", wait) : Close("reallife_social_official_no");
            }
            SocialLife.Remember(_p, MemoryCode.Official);
            SocialLife.SetStage(_p, SocialStage.Partner);
            return Close("reallife_social_official_yes");
        }

        private DialogEntry MoveIn()
        {
            SocialLife.Contacted(_p);
            if (!SocialRules.WouldMoveIn(_p, SocialLife.Day))
            {
                int wait = SocialRules.MoveInWait(_p, SocialLife.Day);
                return wait > 0 ? Wait("reallife_social_movein_wait", wait) : Close("reallife_social_movein_no");
            }
            if (SocialPhone.FamilyKeepsMoveIn != null && SocialPhone.FamilyMoveIn != null && SocialPhone.FamilyKeepsMoveIn(_p))
            {
                // Family life keeps where its partner lives (room, the home, the bed, the rent share): it moves them in, and tells
                // Social through FamilyEvent.MovedIn, which marks them and writes the memory.
                string why = SocialPhone.FamilyMoveIn();
                if (why != null)
                {
                    return Close(why);
                }
            }
            else
            {
                _p.MovedIn = true;
                SocialLife.Remember(_p, MemoryCode.MovedIn);
            }
            ModState.AddModifier(ModState.MoodUpBig);
            return Close("reallife_social_movein_yes");
        }

        private DialogEntry Introduce()
        {
            SocialLife.Contacted(_p);
            SocialPerson stranger = SocialDirector.NextStranger();
            if (stranger == null || _p.Trust < 40f || SocialDirector.Instance == null || !SocialDirector.Instance.QueueIntroduction(_p, stranger))
            {
                return Close("reallife_social_introduce_no");
            }
            return Close("reallife_social_introduce_yes", SocialLife.Data(_p, "other", stranger.Name));
        }
    }
}
