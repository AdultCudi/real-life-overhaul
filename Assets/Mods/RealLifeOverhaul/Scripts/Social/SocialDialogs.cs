using System;
using System.Collections.Generic;
using System.Text;
using BigAmbitions.DayNightCycle;
using Dialogs;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What there is to talk about right now, drawn from what really happened: the news log
    /// (the player's crash, an arrest, an accident they were near), the books of their shops,
    /// the shift they worked, the weather, the other person's own day. Shared by the phone and
    /// by conversations face to face.
    /// </summary>
    public static class SocialTalk
    {
        private static readonly List<SocialTopic> Fitting = new List<SocialTopic>(20);
        private static readonly List<float> Weights = new List<float>(20);

        /// <summary>Picks a topic that fits and fills in what its lines need. Never the one this person talked about last.</summary>
        public static SocialTopic Pick(SocialPerson person, out Dictionary<string, string> data)
        {
            Fitting.Clear();
            Weights.Clear();
            float total = 0f;
            foreach (SocialTopic topic in SocialCatalogue.Topics)
            {
                if (topic.Id == person.LastTopic)
                {
                    continue;
                }
                float weight = Weight(topic.Id, person);
                if (weight <= 0f)
                {
                    continue;
                }
                Fitting.Add(topic);
                Weights.Add(weight);
                total += weight;
            }
            SocialTopic picked = SocialCatalogue.Topics[SocialCatalogue.TopicQuiet];
            float roll = Random.value * total;
            for (int i = 0; i < Fitting.Count; i++)
            {
                roll -= Weights[i];
                if (roll <= 0f)
                {
                    picked = Fitting[i];
                    break;
                }
            }
            person.LastTopic = picked.Id;
            data = SocialLife.Data(person);
            Fill(picked.Id, person, data);
            return picked;
        }

        // How much a topic wants to come up; zero if nothing of the kind has happened.
        private static float Weight(int id, SocialPerson person)
        {
            switch (id)
            {
                case SocialCatalogue.TopicCrash: return News.Recent(NewsKind.PlayerCrash, 2, out _, out _) ? 4f : 0f;
                case SocialCatalogue.TopicArrest: return News.Recent(NewsKind.PlayerArrested, 3, out _, out _) ? 5f : 0f;
                case SocialCatalogue.TopicAccident: return News.Recent(NewsKind.Accident, 1, out _, out _) ? 3f : 0f;
                case SocialCatalogue.TopicCrime: return News.Recent(NewsKind.Crime, 1, out _, out _) ? 3f : 0f;
                case SocialCatalogue.TopicShopGood: return Shop(out _, out _, out float profit) && profit >= 0f ? 3.5f : 0f;
                case SocialCatalogue.TopicShopBad: return Shop(out _, out _, out float loss) && loss < 0f ? 3.5f : 0f;
                case SocialCatalogue.TopicWork: return JobDirector.EarnedToday > 0 ? 3.5f : 0f;
                case SocialCatalogue.TopicGoodDay: return person.Mood >= 1 ? 3f : 0f;
                case SocialCatalogue.TopicBadDay: return person.Mood <= -1 ? 3.5f : 0f;
                case SocialCatalogue.TopicTheirJob: return person.Job == SocialCatalogue.Jobs.Length - 1 ? 0f : 1f;
                case SocialCatalogue.TopicRain: return RainHelper.isRaining ? 1.5f : 0f;
                case SocialCatalogue.TopicFine: return !RainHelper.isRaining && SocialLife.Hour >= 8 && SocialLife.Hour < 19 ? 1f : 0f;
                case SocialCatalogue.TopicMemory: return LastOuting(person) != MemoryCode.None ? 2.5f : 0f;
                case SocialCatalogue.TopicBirthday: return BirthdayIn(person) >= 1 && BirthdayIn(person) <= 5 ? 4f : 0f;
                case SocialCatalogue.TopicRich:
                    return SaveGameManager.Current != null && SaveGameManager.Current.Money >= 50000f && person.Stage >= SocialStage.Friend ? 1.5f : 0f;
                case SocialCatalogue.TopicSick: return ModState.IsSick ? 5f : 0f;
                case SocialCatalogue.TopicBreakIn: return News.Recent(NewsKind.BreakIn, 3, out _, out _) ? 4f : 0f;
                case SocialCatalogue.TopicKnocked: return News.Recent(NewsKind.PlayerKnocked, 2, out _, out _) ? 4f : 0f;
                case SocialCatalogue.TopicDistrict: return 1f;
                default: return 1f;
            }
        }

        private static void Fill(int id, SocialPerson person, Dictionary<string, string> data)
        {
            Vector3 where;
            string detail;
            switch (id)
            {
                case SocialCatalogue.TopicCrash:
                    if (News.Recent(NewsKind.PlayerCrash, 2, out where, out _)) data["street"] = SocialPlaces.StreetAt(where);
                    break;
                case SocialCatalogue.TopicAccident:
                    if (News.Recent(NewsKind.Accident, 1, out where, out _)) data["street"] = SocialPlaces.StreetAt(where);
                    break;
                case SocialCatalogue.TopicCrime:
                    if (News.Recent(NewsKind.Crime, 1, out where, out detail))
                    {
                        data["street"] = SocialPlaces.StreetAt(where);
                        data["what"] = SocialCatalogue.CrimeNoun(detail).GetLocalization();
                    }
                    break;
                case SocialCatalogue.TopicShopGood:
                case SocialCatalogue.TopicShopBad:
                    if (Shop(out string shop, out float sales, out float profit))
                    {
                        data["shop"] = shop;
                        data["amount"] = SocialLife.Money(id == SocialCatalogue.TopicShopGood ? sales : -profit);
                    }
                    break;
                case SocialCatalogue.TopicWork:
                    data["amount"] = SocialLife.Money(JobDirector.EarnedToday);
                    break;
                case SocialCatalogue.TopicMemory:
                    data["outing"] = SocialLife.T(SocialCatalogue.MemoryName(LastOuting(person)));
                    break;
                case SocialCatalogue.TopicBirthday:
                    data["days"] = BirthdayIn(person).ToString();
                    break;
                case SocialCatalogue.TopicBreakIn:
                    if (News.Recent(NewsKind.BreakIn, 3, out _, out detail)) data["shop"] = detail ?? string.Empty;
                    break;
            }
            // A line never shows a bare placeholder: anything that could not be worked out reads as plain words.
            if (!data.ContainsKey("street")) data["street"] = "reallife_news_street_unknown".GetLocalization();
            if (!data.ContainsKey("shop")) data["shop"] = "reallife_social_your_shop".GetLocalization();
            if (!data.ContainsKey("amount")) data["amount"] = string.Empty;
        }

        /// <summary>The player's best shop of the last day the game closed the books on. False if they have none trading.</summary>
        public static bool Shop(out string name, out float sales, out float profit)
        {
            name = null;
            sales = 0f;
            profit = 0f;
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || save.financialSummaries == null || save.financialSummaries.Count == 0)
                {
                    return false;
                }
                FinancialSummary summary = save.financialSummaries[save.financialSummaries.Count - 1];
                if (summary == null || summary.businessIncomeStatements == null || summary.dayNumber < save.Day - 2)
                {
                    return false;
                }
                FinancialSummary.BusinessIncomeStatement best = null;
                foreach (FinancialSummary.BusinessIncomeStatement statement in summary.businessIncomeStatements)
                {
                    if (statement != null && statement.Address != null && (best == null || statement.TotalSales > best.TotalSales))
                    {
                        best = statement;
                    }
                }
                if (best == null)
                {
                    return false;
                }
                BuildingRegistration registration = BuildingHelper.GetBuildingRegistration(best.Address);
                if (registration == null || !registration.RentedByPlayer || string.IsNullOrEmpty(registration.BusinessName))
                {
                    return false;
                }
                name = registration.BusinessName;
                sales = best.TotalSales;
                profit = best.TotalProfit;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static MemoryCode LastOuting(SocialPerson person)
        {
            for (int i = person.Memories.Count - 1; i >= 0; i--)
            {
                MemoryCode code = person.Memories[i].Code;
                if (code >= MemoryCode.Coffee && code <= MemoryCode.Business)
                {
                    return code;
                }
            }
            return MemoryCode.None;
        }

        /// <summary>Days until their next birthday; 0 if it is today.</summary>
        public static int BirthdayIn(SocialPerson person)
        {
            int perYear = SocialPlaces.DaysPerYear;
            int today = (Mathf.Max(1, SocialLife.Day) - 1) % perYear + 1;
            return ((person.Birthday - today) % perYear + perYear) % perYear;
        }

        /// <summary>
        /// The player answered: what a first exchange of the day is worth, more face to face, and
        /// what the answer itself did. Returns their reply (a locale key).
        /// </summary>
        public static string Answer(SocialPerson person, SocialTopic topic, bool warm, bool inPerson, bool firstOfTalk)
        {
            float familiarity = 0f, affection = 0f, trust = 0f;
            if (firstOfTalk && (person.LastCallDay != SocialLife.Day || inPerson))
            {
                float scale = inPerson ? 1.5f : 1f;
                familiarity = SocialRules.CallFamiliarity * scale;
                affection = SocialRules.CallAffection * scale;
                trust = SocialRules.CallTrust * scale;
                if (person.LastTextDay > 0 && person.LastTextDay >= SocialLife.Day - 1)
                {
                    familiarity += SocialRules.TextReply;   // they wrote, and the player got back to them
                    affection += 0.5f;
                }
                ModState.AddModifier(person.IsFamily ? ModState.Family : ModState.Social);
            }
            if (warm)
            {
                affection += SocialRules.WarmReply;
            }
            else if (!topic.BothFine)
            {
                affection += SocialRules.BluntReply;
                if (person.Affection + affection < 15f)
                {
                    SocialLife.Remember(person, MemoryCode.Rude);
                }
            }
            else
            {
                affection += 0.5f;
            }
            SocialLife.Contacted(person);
            if (!inPerson)
            {
                person.LastCallDay = SocialLife.Day;
            }
            SocialLife.Change(person, familiarity, affection, trust);
            return warm ? topic.ReactA : topic.ReactB;
        }
    }

    /// <summary>A call to (or from) somebody the player knows.</summary>
    public sealed class PersonDialog : Dialog
    {
        private const int GiftPrice = 60;
        private const int AskOutGapDays = 5;

        private readonly SocialPerson _p;
        private readonly List<string> _actions = new List<string>(8);
        private int _topics;

        public PersonDialog(int index)
        {
            _p = SocialState.Get(index);
            bool incoming = SocialPhone.Incoming == index;
            SocialPhone.Incoming = -1;
            if (_p == null || !_p.Met || !Settings.Social)
            {
                Show(PhoneContacts.Closing("reallife_social_call_off"));
                return;
            }
            npcNameKey = SocialPhone.IdOf(_p);
            if (!incoming && _p.Asleep(SocialLife.Hour))
            {
                Show(Close(SocialLines.Pick(SocialCatalogue.Voicemail)));
                return;
            }
            SocialOffer offer = SocialState.Offer;
            SocialPlan plan = SocialState.Plan;
            if (incoming)
            {
                // They rang: a word first, then whatever they rang about.
                DialogEntry entry = Line(SocialLines.Pick(SocialCatalogue.RingOpen));
                entry.ConfirmTextOverride = "reallife_social_btn_whats_up".Localize();
                entry.OnConfirm = () => offer.Active && offer.Person == _p.Index ? Asked() : Chat();
                Show(entry);
            }
            else if (offer.Active && offer.Person == _p.Index)
            {
                Show(Asked());
            }
            else if (plan.Active && plan.Person == _p.Index)
            {
                Show(Planned());
            }
            else
            {
                Show(Root());
            }
        }

        private static void Show(DialogEntry entry)
        {
            DialogController.current.ShowEntry(entry);
        }

        // ------------------------------------------------------------------ building blocks

        private DialogEntry Line(string key, Dictionary<string, string> data = null)
        {
            data = data ?? SocialLife.Data(_p);
            SocialLife.Said(_p, key, data);
            return new DialogEntry { messageData = key.Localize(data), OnCancel = PhoneContacts.HangUp };
        }

        private DialogEntry Close(string key, Dictionary<string, string> data = null)
        {
            data = data ?? SocialLife.Data(_p);
            SocialLife.Said(_p, key, data);
            return PhoneContacts.Closing(key, data);
        }

        // ------------------------------------------------------------------ the call

        private DialogEntry Root()
        {
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
            if (_topics == 0 && _p.LastCallDay == SocialLife.Day)
            {
                // Twice in a day is once too often for a chat; anything else can still be asked.
                SocialLife.Contacted(_p);
                DialogEntry again = Line("reallife_social_chat_again");
                again.ConfirmTextOverride = "reallife_social_btn_else".Localize();
                again.OnConfirm = () => Menu(0);
                return again;
            }
            SocialTopic topic = SocialTalk.Pick(_p, out Dictionary<string, string> data);
            DialogEntry entry = Line(SocialLines.Pick(topic.Open), data);
            entry.ConfirmTextOverride = topic.ReplyA.Localize(data);
            entry.OnConfirm = () => Reply(topic, true, data);
            entry.SecondOptionTextOverride = topic.ReplyB;
            entry.OnSecondOption = () => Reply(topic, false, data);
            return entry;
        }

        private DialogEntry Reply(SocialTopic topic, bool warm, Dictionary<string, string> data)
        {
            string react = SocialTalk.Answer(_p, topic, warm, false, _topics == 0);
            _topics++;
            DialogEntry entry = Line(react, data);
            if (_topics < 2)
            {
                entry.ConfirmTextOverride = "reallife_social_btn_go_on".Localize();
                entry.OnConfirm = Chat;
                entry.SecondOptionTextOverride = "reallife_social_btn_else";
                entry.OnSecondOption = () => Menu(0);
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_social_btn_bye".Localize();
                entry.OnConfirm = () => Close(SocialLines.Pick(_p.IsFamily ? SocialCatalogue.ChatByeMom : SocialCatalogue.ChatBye));
                entry.SecondOptionTextOverride = "reallife_social_btn_else";
                entry.OnSecondOption = () => Menu(0);
            }
            return entry;
        }

        // Everything else there is to say, one thing at a time: only what is possible right now is offered.
        private DialogEntry Menu(int at)
        {
            _actions.Clear();
            int day = SocialLife.Day;
            _actions.Add("plans");
            if (SocialTalk.BirthdayIn(_p) == 0 && _p.BirthdayYear != Year(day)) _actions.Add("birthday");
            if (_p.Debt > 0) _actions.Add("repay");
            if (_p.Compatible && !_p.Together && _p.Stage >= SocialStage.Friend && SocialState.Partner == null && !AskedLately(day)) _actions.Add("askout");
            if (_p.Stage == SocialStage.Dating) _actions.Add("official");
            if (_p.Stage == SocialStage.Partner && !_p.MovedIn) _actions.Add("movein");
            if (_p.Stage >= SocialStage.Friend && _p.Debt == 0) _actions.Add("borrow");
            if (_p.Stage >= SocialStage.Friend && SocialDirector.NextStranger() != null) _actions.Add("introduce");
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
                    break;
                case "introduce":
                    entry.ConfirmTextOverride = "reallife_social_btn_introduce".Localize();
                    entry.OnConfirm = Introduce;
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

        private bool AskedLately(int day)
        {
            for (int i = 0; i < _p.Memories.Count; i++)
            {
                if (_p.Memories[i].Code == MemoryCode.AskedOut && day - _p.Memories[i].Day < AskOutGapDays)
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ making plans

        // One outing at a time, the one they would like first; only what can actually be done from here is offered.
        private DialogEntry Plans(int at, int tried)
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
            const int kinds = (int)PlanKind.Business + 1;
            if (tried >= kinds)
            {
                return Close("reallife_social_plan_nowhere");
            }
            // The first offer is what they have been wanting to do; then the rest in order.
            var kind = (PlanKind)(((int)_p.Wants + at) % kinds);
            OutingDef def = SocialCatalogue.Outing(kind);
            if (_p.Stage < def.Needs && !_p.IsFamily)
            {
                return Plans(at + 1, tried + 1);
            }
            if (kind == PlanKind.Workout && !_p.Has(SocialTrait.Sporty) && _p.Stage < SocialStage.Friend)
            {
                return Plans(at + 1, tried + 1);
            }
            if (kind == PlanKind.Drive && JobDirector.NearestCar(250f) == null)
            {
                return Plans(at + 1, tried + 1);   // nothing to drive
            }
            if (!SocialPlaces.Find(kind, SocialDirector.Where(), 40f, 1800f, out Venue venue))
            {
                return Plans(at + 1, tried + 1);
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "place", venue.Name);
            data["cost"] = SocialLife.Money(def.Cost);
            DialogEntry entry = Line(def.Offer, data);
            entry.ConfirmTextOverride = "reallife_social_btn_lets".Localize();
            entry.OnConfirm = () => When(kind, venue, false);
            entry.SecondOptionTextOverride = "reallife_social_btn_other_idea";
            entry.OnSecondOption = () => Plans(at + 1, 0);
            return entry;
        }

        private DialogEntry When(PlanKind kind, Venue venue, bool theirIdea)
        {
            float first = SocialLife.NextSlot(_p, kind, SocialLife.Now + 45f);
            if (first < 0f)
            {
                return Close(SocialLines.Pick(SocialCatalogue.PlanBusy));
            }
            float second = SocialLife.NextSlot(_p, kind, first + 180f);
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
            if (!theirIdea && _p.Mood <= -2 && Random.value < 0.5f)
            {
                return Close(SocialLines.Pick(SocialCatalogue.PlanMood));
            }
            if (!SocialLife.MakePlan(_p, kind, time, venue, theirIdea, out string reason))
            {
                return Close(reason ?? "reallife_social_plan_nowhere");
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "place", venue.Name);
            data["time"] = SocialLife.Clock(time);
            return Close(SocialLines.Pick(SocialCatalogue.PlanYes), data);
        }

        // A plan is on with this person: what it is, and the way out of it.
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

        // ------------------------------------------------------------------ what they asked

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
                if (!SocialPlaces.Find(kind, player, 60f, 700f, out Venue old) || !SocialPlaces.Find(kind, old.Front, 22f, 75f, out Venue fresh, old.Building))
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
                    float time = SocialLife.NextSlot(_p, PlanKind.Walk, SocialLife.Now + 45f);
                    if (time < 0f || !SocialLife.MakePlan(_p, kind, time, old, true, out string reason))
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

            // An invitation: the place is found now, near where the player is.
            if (!Settings.SocialOutings || !SocialPlaces.Find(kind, player, 40f, 1800f, out Venue venue))
            {
                SocialState.Offer.Clear();
                return Close("reallife_social_plan_nowhere");
            }
            Dictionary<string, string> invite = SocialLife.Data(_p, "place", venue.Name);
            invite["cost"] = SocialLife.Money(def.Cost);
            DialogEntry entry = Line(def.Offer, invite);
            entry.ConfirmTextOverride = "reallife_social_btn_im_in".Localize();
            entry.OnConfirm = () =>
            {
                SocialState.Offer.Clear();
                return When(kind, venue, true);
            };
            entry.SecondOptionTextOverride = "reallife_social_btn_not_this_time";
            entry.OnSecondOption = () =>
            {
                SocialState.Offer.Clear();
                SocialLife.Contacted(_p);
                SocialLife.Change(_p, 0f, -1f, 0f);
                return Close("reallife_social_invite_declined");
            };
            return entry;
        }

        private DialogEntry Refuse(string line)
        {
            SocialState.Offer.Clear();
            SocialLife.Contacted(_p);
            SocialLife.Change(_p, 0f, SocialRules.FavourRefusedAffection, 0f);
            return Close(line);
        }

        // ------------------------------------------------------------------ the other things to say

        private DialogEntry Birthday()
        {
            _p.BirthdayYear = Year(SocialLife.Day);
            SocialLife.Remember(_p, MemoryCode.Birthday);
            SocialLife.Contacted(_p);
            SocialLife.Change(_p, 1f, SocialRules.BirthdayAffection, SocialRules.BirthdayTrust);
            DialogEntry entry = Line(SocialLines.Pick(SocialCatalogue.BirthdayThanks));
            entry.ConfirmTextOverride = "reallife_social_btn_gift".Localize(SocialLife.Data(_p, "amount", SocialLife.Money(GiftPrice)));
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
            if (_p.Has(SocialTrait.Generous)) amount *= 1.5f;
            if (_p.Has(SocialTrait.Careful)) amount *= 0.5f;
            return Mathf.RoundToInt(amount / 25f) * 25;
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

        private DialogEntry AskOut()
        {
            SocialLife.Remember(_p, MemoryCode.AskedOut);
            SocialLife.Contacted(_p);
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
                return Close("reallife_social_official_no");
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
                return Close("reallife_social_movein_no");
            }
            _p.MovedIn = true;
            SocialLife.Remember(_p, MemoryCode.MovedIn);
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

    /// <summary>What a face-to-face conversation is about, handed to the dialog by the scene, and what came of it.</summary>
    public sealed class SceneTalk
    {
        public const int Meet = 0, Outing = 1, DropBy = 2, Introduction = 3;
        public const int Treat = 1, Split = 2, Theirs = 3;

        public SocialPerson Person;
        public SocialPerson Introducer;
        public int Mode;
        public string Opener;
        public Dictionary<string, string> OpenerData;
        public int Cost;                // per head; 0 = nothing to pay
        public bool TheyOffer;          // they would pay for both
        public string GoLabel;          // what the button that gets on with it says

        public bool PersonSpeaks;       // an introduction: the friend has had their say and the new person is talking
        public bool Warm, Swapped, Brushed, Chatted;
        public int Pay;
    }

    /// <summary>
    /// A conversation in the street with somebody of the cast: meeting them, greeting them before
    /// an outing, a visit at the shop. In the game's own dialogue panel; the scene that opened it
    /// animates the two of them while it is open and reads <see cref="SceneTalk"/> when it closes.
    /// </summary>
    public sealed class SceneDialog : Dialog
    {
        private readonly SceneTalk _talk;
        private readonly SocialPerson _p;

        public SceneDialog()
        {
            _talk = SocialDirector.Talk;
            _p = _talk != null ? _talk.Person : null;
            if (_p == null)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_social_call_off"));
                return;
            }
            npcNameKey = SocialPhone.IdOf(_p);
            DialogController.current.ShowEntry(_talk.Mode == SceneTalk.Introduction ? Introduced() : _talk.Mode == SceneTalk.Meet ? MeetFirst() : Greeting());
        }

        private static void Leave()
        {
            DialogController.current.CancelDialog();
        }

        private static DialogEntry Done()
        {
            DialogController.current.FinishDialog();
            return null;
        }

        private DialogEntry Say(string key, Dictionary<string, string> data = null)
        {
            data = data ?? SocialLife.Data(_p);
            SocialLife.Said(_p, key, data);
            return new DialogEntry { messageData = key.Localize(data) };
        }

        // ---- meeting somebody new
        private DialogEntry Introduced()
        {
            SocialPerson friend = _talk.Introducer;
            Dictionary<string, string> data = SocialLife.Data(_p);
            data["friend"] = friend != null ? SocialLife.First(friend) : string.Empty;
            DialogEntry entry = new DialogEntry { messageData = SocialLines.Pick(SocialCatalogue.Introduces).Localize(data) };
            entry.ConfirmTextOverride = "reallife_social_btn_hello".Localize();
            entry.OnConfirm = MeetFirst;
            return entry;
        }

        private DialogEntry MeetFirst()
        {
            _talk.PersonSpeaks = true;
            DialogEntry entry = Say(_talk.Opener, _talk.OpenerData);
            entry.ConfirmTextOverride = "reallife_social_meet_reply_warm".Localize();
            entry.OnConfirm = () => MeetSecond(true);
            entry.SecondOptionTextOverride = "reallife_social_meet_reply_cool";
            entry.OnSecondOption = () => MeetSecond(false);
            entry.CancelTextOverride = "reallife_social_meet_reply_brush";
            entry.OnCancel = () =>
            {
                _talk.Brushed = true;
                Leave();
            };
            return entry;
        }

        private DialogEntry MeetSecond(bool warm)
        {
            _talk.Warm = warm;
            DialogEntry entry = Say(SocialLines.Pick(warm ? SocialCatalogue.MeetWarm : SocialCatalogue.MeetCool));
            entry.ConfirmTextOverride = "reallife_social_btn_swap".Localize();
            entry.OnConfirm = () =>
            {
                _talk.Swapped = true;
                DialogEntry last = Say(SocialLines.Pick(SocialCatalogue.MeetNumbers));
                last.CancelTextOverride = "reallife_social_btn_see_you";
                last.OnCancel = Leave;
                return last;
            };
            entry.CancelTextOverride = "reallife_social_btn_nice_meeting";
            entry.OnCancel = Leave;
            return entry;
        }

        // ---- somebody the player knows
        private DialogEntry Greeting()
        {
            DialogEntry entry = Say(_talk.Opener, _talk.OpenerData);
            entry.ConfirmTextOverride = "reallife_social_btn_chat".Localize();
            entry.OnConfirm = Chat;
            entry.SecondOptionTextOverride = _talk.GoLabel ?? "reallife_social_btn_lets_go";
            entry.OnSecondOption = Onward;
            return entry;
        }

        private DialogEntry Chat()
        {
            SocialTopic topic = SocialTalk.Pick(_p, out Dictionary<string, string> data);
            DialogEntry entry = Say(SocialLines.Pick(topic.Open), data);
            entry.ConfirmTextOverride = topic.ReplyA.Localize(data);
            entry.OnConfirm = () => Answered(topic, true, data);
            entry.SecondOptionTextOverride = topic.ReplyB;
            entry.OnSecondOption = () => Answered(topic, false, data);
            return entry;
        }

        private DialogEntry Answered(SocialTopic topic, bool warm, Dictionary<string, string> data)
        {
            string react = SocialTalk.Answer(_p, topic, warm, true, !_talk.Chatted);
            _talk.Chatted = true;
            DialogEntry entry = Say(react, data);
            entry.ConfirmTextOverride = (_talk.GoLabel ?? "reallife_social_btn_lets_go").Localize();
            entry.OnConfirm = Onward;
            return entry;
        }

        // The conversation is over; if there is a bill coming, who pays it is settled here.
        private DialogEntry Onward()
        {
            if (_talk.Mode != SceneTalk.Outing || _talk.Cost <= 0)
            {
                return Done();
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "cost", SocialLife.Money(_talk.Cost * 2));
            data["half"] = SocialLife.Money(_talk.Cost);
            DialogEntry entry = Say(_talk.TheyOffer ? "reallife_social_pay_ask_offer" : "reallife_social_pay_ask", data);
            entry.ConfirmTextOverride = "reallife_social_btn_treat".Localize();
            entry.OnConfirm = () =>
            {
                _talk.Pay = SceneTalk.Treat;
                return Done();
            };
            entry.SecondOptionTextOverride = "reallife_social_btn_split";
            entry.OnSecondOption = () =>
            {
                _talk.Pay = SceneTalk.Split;
                return Done();
            };
            if (_talk.TheyOffer)
            {
                entry.CancelTextOverride = "reallife_social_btn_theirs";
                entry.OnCancel = () =>
                {
                    _talk.Pay = SceneTalk.Theirs;
                    Leave();
                };
            }
            return entry;
        }
    }

    /// <summary>The "People" page: everybody the player knows, how things stand with each, and what they would like to do.</summary>
    public sealed class PeopleDialog : Dialog
    {
        private static readonly string[] Hints =
        {
            null, null, "reallife_social_hint_gym", "reallife_social_hint_work", "reallife_social_hint_cart", "reallife_social_hint_neighbour",
            "reallife_social_hint_helped", "reallife_social_hint_customer"
        };

        private readonly List<SocialPerson> _known = new List<SocialPerson>(SocialRules.CastSize);

        public PeopleDialog()
        {
            npcNameKey = SocialPhone.PeopleId;
            if (!SocialState.Loaded || !Settings.Social)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_social_call_off"));
                return;
            }
            // Closest first.
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && p.Met)
                {
                    _known.Add(p);
                }
            }
            _known.Sort((a, b) => b.Closeness.CompareTo(a.Closeness));
            DialogController.current.ShowEntry(Overview());
        }

        private DialogEntry Overview()
        {
            var text = new StringBuilder(256);
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && !p.Met && Hints[i] != null)
                {
                    text.Append(text.Length > 0 ? ", " : string.Empty).Append(SocialLife.T(Hints[i]));
                }
            }
            SocialPlan plan = SocialState.Plan;
            SocialPerson with = SocialState.Get(plan.Person);
            string planLine = plan.Active && with != null
                ? SocialLife.T("reallife_social_people_plan", new Dictionary<string, string>
                {
                    { "name", with.Name }, { "outing", SocialLife.T(SocialCatalogue.Outing(plan.Kind).Name) }, { "place", plan.Place ?? string.Empty },
                    { "time", SocialLife.Clock(plan.Time) }
                })
                : SocialLife.T("reallife_social_people_noplan");
            var data = new Dictionary<string, string>
            {
                { "count", _known.Count.ToString() },
                { "plan", planLine },
                { "hints", text.Length > 0 ? SocialLife.T("reallife_social_people_hints").Replace("{list}", text.ToString()) : SocialLife.T("reallife_social_people_all_met") }
            };
            var entry = new DialogEntry { messageData = "reallife_social_people_overview".Localize(data), OnCancel = PhoneContacts.HangUp };
            if (_known.Count > 0)
            {
                entry.ConfirmTextOverride = "reallife_social_btn_next".Localize();
                entry.OnConfirm = () => Page(0);
            }
            return entry;
        }

        private DialogEntry Page(int at)
        {
            SocialPerson p = _known[at % _known.Count];
            Dictionary<string, string> data = SocialLife.Data(p);
            data["stage"] = SocialLife.StageName(p);
            data["mood"] = SocialLife.MoodName(p);
            data["closeness"] = p.Closeness.ToString();
            data["f"] = Mathf.RoundToInt(p.Familiarity).ToString();
            data["a"] = Mathf.RoundToInt(p.Affection).ToString();
            data["t"] = Mathf.RoundToInt(p.Trust).ToString();
            data["said"] = string.IsNullOrEmpty(p.LastSaid) ? SocialLife.T("reallife_social_people_nothing_said") : p.LastSaid;
            data["wants"] = SocialLife.T(SocialCatalogue.Outing(p.Wants).Name);
            int birthday = SocialTalk.BirthdayIn(p);
            data["birthday"] = birthday == 0 ? SocialLife.T("reallife_social_people_birthday_today")
                : SocialLife.T("reallife_social_people_birthday_in").Replace("{days}", birthday.ToString());
            var entry = new DialogEntry { messageData = "reallife_social_people_page".Localize(data), OnCancel = PhoneContacts.HangUp };
            entry.ConfirmTextOverride = "reallife_social_btn_next".Localize();
            entry.OnConfirm = () => Page(at + 1);
            entry.SecondOptionTextOverride = "reallife_social_btn_history";
            entry.OnSecondOption = () => History(at);
            return entry;
        }

        private DialogEntry History(int at)
        {
            SocialPerson p = _known[at % _known.Count];
            int day = SocialLife.Day;
            var text = new StringBuilder(256);
            for (int i = p.Memories.Count - 1; i >= 0; i--)
            {
                int ago = Mathf.Max(0, day - p.Memories[i].Day);
                text.Append(text.Length > 0 ? "\n" : string.Empty).Append(SocialLife.T(SocialCatalogue.MemoryName(p.Memories[i].Code))).Append(" (")
                    .Append(ago == 0 ? SocialLife.T("reallife_social_people_today") : SocialLife.T("reallife_social_people_days_ago").Replace("{days}", ago.ToString()))
                    .Append(')');
            }
            var traits = new StringBuilder(64);
            for (int i = 0; i < SocialCatalogue.TraitNames.Length; i++)
            {
                if (((int)p.Traits & (1 << i)) != 0)
                {
                    traits.Append(traits.Length > 0 ? ", " : string.Empty).Append(SocialLife.T(SocialCatalogue.TraitNames[i]));
                }
            }
            Dictionary<string, string> data = SocialLife.Data(p);
            data["memories"] = text.Length > 0 ? text.ToString() : SocialLife.T("reallife_social_people_no_memories");
            data["traits"] = traits.ToString();
            data["days"] = Mathf.Max(0, day - p.MetDay).ToString();
            data["money"] = p.Debt > 0 ? SocialLife.T("reallife_social_people_you_owe").Replace("{amount}", SocialLife.Money(p.Debt))
                : p.Loan > 0 ? SocialLife.T("reallife_social_people_owes_you").Replace("{amount}", SocialLife.Money(p.Loan))
                : string.Empty;
            var entry = new DialogEntry { messageData = "reallife_social_people_history".Localize(data), OnCancel = PhoneContacts.HangUp };
            entry.ConfirmTextOverride = "reallife_social_btn_next".Localize();
            entry.OnConfirm = () => Page(at + 1);
            return entry;
        }
    }

    /// <summary>The City Ledger read out: today's paper, one piece at a time.</summary>
    public sealed class LedgerDialog : Dialog
    {
        public LedgerDialog()
        {
            npcNameKey = SocialPhone.LedgerId;
            if (!Settings.SocialNewspaper || News.Today.Count == 0)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_news_none"));
                return;
            }
            DialogController.current.ShowEntry(Piece(0));
        }

        private static DialogEntry Piece(int at)
        {
            NewsItem item = News.Today[at];
            var entry = new DialogEntry { messageData = item.Key.Localize(item.Data), OnCancel = PhoneContacts.HangUp };
            entry.ConfirmTextOverride = "reallife_social_btn_next".Localize();
            entry.OnConfirm = () => at + 1 < News.Today.Count ? Piece(at + 1) : PhoneContacts.Closing("reallife_news_end");
            return entry;
        }
    }
}
