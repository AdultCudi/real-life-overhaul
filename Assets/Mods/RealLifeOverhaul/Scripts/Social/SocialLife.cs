using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>What the Law feature tells the social life, through <see cref="SocialLinks.Law"/>.</summary>
    public interface ISocialLaw
    {
        bool HasRecord { get; }

        /// <summary>The player's standing, 0 (notorious) to 100 (spotless).</summary>
        int Reputation { get; }
    }

    /// <summary>What the Home and health feature is told, through <see cref="SocialLinks.Wellbeing"/>.</summary>
    public interface ISocialWellbeing
    {
        /// <summary>Stress down (negative) or up, in that feature's units of 0 to 100.</summary>
        void Stress(float change);

        void Ate();
    }

    /// <summary>The social life's neighbours, set by Wiring. Each is null while nobody fills it in, and is then not asked.</summary>
    public static class SocialLinks
    {
        public static ISocialLaw Law;
        public static ISocialWellbeing Wellbeing;

        /// <summary>Who the player is with has changed: for the family life.</summary>
        public static Action PartnerChanged;

        /// <summary>An outing went to its end, with whom and what it was: for Flare.</summary>
        public static Action<SocialPerson, PlanKind> OutingDone;

        /// <summary>RLO Texts: true when it holds a text for the model's words, to send it itself through the last argument.</summary>
        public static Func<SocialPerson, string, Dictionary<string, string>, Action<string, Dictionary<string, string>>, bool> HoldText;

        /// <summary>RLO Texts: the player walks up to somebody they meet (who, the outing, the place, minutes late).</summary>
        public static Action<SocialPerson, PlanKind, string, int> GuessGreeting;

        /// <summary>RLO Texts: the greeting guessed, taken at the click; null for the written line.</summary>
        public static Func<SocialPerson, string> TakeGreeting;

        public const string WordsKey = "reallife_texts_line";
    }

    /// <summary>What happens between the player and a person, with its effects in the game. The phone, scenes and director go through here.</summary>
    public static class SocialLife
    {
        public const int QuietFrom = 22;   // hour from which no text is sent...
        public const int QuietTo = 8;      // ...until this one

        public static IModLogger Logger;

        public static int Day => TimeHelper.CurrentDay;
        public static int Hour => TimeHelper.CurrentHour;
        public static float Now => TimeHelper.NowInMinutes();
        public static bool Weekend(int day)
        {
            DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(Mathf.Max(1, day));
            return weekday == DayOfWeekOrdered.Saturday || weekday == DayOfWeekOrdered.Sunday;
        }

        public static string T(string key)
        {
            return JobKeys.Fill(key.GetLocalization());
        }

        /// <summary>A line with its placeholders filled in. Builds a string: for when something changes, never per frame.</summary>
        public static string T(string key, Dictionary<string, string> data)
        {
            return Filled(JobKeys.Fill(key.GetLocalization()), data);
        }

        /// <summary>A prompt's words without the leading "{key}: ", which the game's panel writes on its button.</summary>
        public static string Say(string key, Dictionary<string, string> data)
        {
            return Capital(Filled(key.GetLocalization().Replace("{key}: ", string.Empty), data));
        }

        private static string Filled(string text, Dictionary<string, string> data)
        {
            if (data != null && text.IndexOf('{') >= 0)
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    text = text.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty);
                }
            }
            return text;
        }

        public static string First(SocialPerson person)
        {
            int space = person.Name.IndexOf(' ');
            return space > 0 ? person.Name.Substring(0, space) : person.Name;
        }

        /// <summary>{name}, {first}, {job}, {district}: a new dictionary each time, as the game keeps what it is given.</summary>
        public static Dictionary<string, string> Data(SocialPerson person)
        {
            return new Dictionary<string, string>(8)
            {
                { "name", person.Name },
                { "first", First(person) },
                { "job", SocialCatalogue.Jobs[Mathf.Clamp(person.Job, 0, SocialCatalogue.Jobs.Length - 1)].GetLocalization() },
                { "district", SocialPlaces.District(person.District) }
            };
        }

        /// <summary>A person's values with the plan's for RLO Texts' card, each with "t_" before it so that no written line uses them.</summary>
        public static Dictionary<string, string> PlanData(SocialPerson person, SocialPlan plan, float waited)
        {
            Dictionary<string, string> data = Data(person);
            int minute = Mathf.FloorToInt(plan.Time) % 1440;
            data["t_place"] = plan.Place ?? string.Empty;
            data["t_time"] = (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
            data["t_kind"] = plan.Kind.ToString();
            if (waited > 0f)
            {
                data["t_waited"] = (Mathf.RoundToInt(waited / 5f) * 5).ToString();
            }
            return data;
        }

        public static Dictionary<string, string> Data(SocialPerson person, string name, string value)
        {
            Dictionary<string, string> data = Data(person);
            data[name] = value ?? string.Empty;
            return data;
        }

        /// <summary>For a title made from words written small ("coffee with Dana").</summary>
        public static string Capital(string text)
        {
            return string.IsNullOrEmpty(text) || !char.IsLower(text[0]) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        /// <summary>A time as it is said: "15:00" today, "tomorrow 10:00", else the weekday.</summary>
        public static string Clock(float minutes)
        {
            var stamp = new Timestamp(minutes);
            string time = stamp.Hour.GetFormattedTime(0f, true);
            int days = stamp.Day - Day;
            if (days <= 0)
            {
                return time;
            }
            return days == 1
                ? T("reallife_social_time_tomorrow").Replace("{time}", time)
                : TimeHelper.GetDayOfWeek(stamp.Day).GetLocalization() + " " + time;
        }

        public static string StageName(SocialPerson person)
        {
            if (person.IsFamily)
            {
                return T(SocialCatalogue.StageFamily);
            }
            if (person.Drifted)
            {
                return T(SocialCatalogue.StageDrifted);
            }
            string stage = T(SocialCatalogue.StageNames[(int)person.Stage]);
            return person.MovedIn ? stage + T(SocialCatalogue.StageLivesWith) : stage;
        }

        public static string MoodName(SocialPerson person)
        {
            return T(SocialCatalogue.MoodNames[Mathf.Clamp(person.Mood + 2, 0, 4)]);
        }

        /// <summary>The last thing they said, kept for the People page.</summary>
        public static void Said(SocialPerson person, string key, Dictionary<string, string> data)
        {
            if (person != null && key != null)
            {
                person.LastSaid = T(key, data);
            }
        }

        public static void Toast(NotificationType type, string key, Dictionary<string, string> data, float seconds = 8f)
        {
            Notifications.Show(type, key, data, seconds);
        }

        /// <summary>A word was had: neglect counts from today, and somebody who had drifted is back.</summary>
        public static void Contacted(SocialPerson person)
        {
            person.LastContactDay = Day;
            if (person.Drifted)
            {
                person.Drifted = false;
                Logger?.Info("Social: " + person.Name + " is back in touch.");
            }
        }

        public static void Change(SocialPerson person, float familiarity, float affection, float trust)
        {
            if (!Settings.SocialRelationships && (familiarity < 0f || affection < 0f || trust < 0f))
            {
                // Relationships frozen: nothing is lost, and what is earned is still earned.
                familiarity = Mathf.Max(0f, familiarity);
                affection = Mathf.Max(0f, affection);
                trust = Mathf.Max(0f, trust);
            }
            SocialRules.Change(person, familiarity, affection, trust);
            Restage(person);
        }

        public static void Remember(SocialPerson person, MemoryCode code)
        {
            person.Remember(code, Day);
        }

        public static void Restage(SocialPerson person)
        {
            if (!person.Met)
            {
                return;
            }
            if (SocialRules.BrokenUp(person))
            {
                BreakUp(person);
                return;
            }
            SocialStage earned = SocialRules.Earned(person, Day);
            if (earned == person.Stage)
            {
                return;
            }
            bool up = earned > person.Stage;
            person.Stage = earned;
            person.StageDay = Day;
            Dictionary<string, string> data = Data(person, "stage", StageName(person));
            Toast(up ? NotificationType.Success : NotificationType.Warning, up ? "reallife_social_stage_up" : "reallife_social_stage_down", data, 10f);
            if (up)
            {
                ModState.AddModifier(ModState.Social);
            }
            Logger?.Info("Social: " + person.Name + " is now " + earned + " (familiarity " + person.Familiarity.ToString("F0") + ", affection "
                         + person.Affection.ToString("F0") + ", trust " + person.Trust.ToString("F0") + ", shared " + person.Shared + ").");
        }

        /// <summary>A stage asked for and agreed to (dating, partner) rather than earned by the figures.</summary>
        public static void SetStage(SocialPerson person, SocialStage stage)
        {
            person.Stage = stage;
            person.StageDay = Day;
            person.Dates = 0;
            Toast(NotificationType.Success, "reallife_social_stage_up", Data(person, "stage", StageName(person)), 10f);
            ModState.AddModifier(ModState.MoodUpBig);
            Logger?.Info("Social: " + person.Name + " is now " + stage + ".");
            TellPartnerChanged();
        }

        private static void TellPartnerChanged()
        {
            try
            {
                SocialLinks.PartnerChanged?.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        public static void BreakUp(SocialPerson person)
        {
            if (!person.Together)
            {
                return;
            }
            person.Stage = SocialStage.Acquaintance;
            person.StageDay = Day;
            person.MovedIn = false;
            person.Dates = 0;
            Remember(person, MemoryCode.BreakUp);
            SendText(person, SocialLines.Pick(SocialCatalogue.TextBreakUp), Data(person), true);
            Toast(NotificationType.Error, "reallife_social_broke_up", Data(person), 12f);
            ModState.AddModifier(ModState.MoodDownBig);
            TellPartnerChanged();
            if (SocialState.Plan.Active && SocialState.Plan.Person == person.Index)
            {
                SocialState.Plan.Clear();
            }
            Logger?.Info("Social: " + person.Name + " ended it (affection " + person.Affection.ToString("F0") + ", trust " + person.Trust.ToString("F0") + ").");
        }

        public static void Meet(SocialPerson person, bool warm)
        {
            if (person.Met)
            {
                return;
            }
            person.Met = true;
            person.MetDay = Day;
            person.Stage = SocialStage.Acquaintance;
            person.StageDay = Day;
            person.Familiarity = warm ? 12f : 8f;
            person.Affection = warm ? 14f : 8f;
            person.Trust = warm ? 12f : 9f;
            if (person.Origin == SocialOrigin.Helped)
            {
                person.Trust += 14f;   // they have seen what the player is like
                person.Affection += 8f;
            }
            person.LastContactDay = Day;
            Remember(person, MemoryCode.Met);
            SocialState.LastMeetDay = Day;
            SocialPhone.Add(person, true);
            Toast(NotificationType.Success, "reallife_social_met", Data(person), 10f);
            ModState.AddModifier(ModState.Social);
            Logger?.Info("Social: met " + person.Name + " (" + person.Origin + "), traits " + person.Traits + (person.Compatible ? ", a romance is possible" : string.Empty) + ".");
        }

        public static bool CanAfford(int amount)
        {
            return SaveGameManager.Current != null && SaveGameManager.Current.Money >= amount;
        }

        /// <summary>Through the game's own transaction. False, and nothing taken, if the player has not got it.</summary>
        public static bool Pay(int amount, string transactionKey)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (!CanAfford(amount))
            {
                return false;
            }
            return GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transactionKey), null, null, force: false, showNotification: false);
        }

        public static void Give(int amount, string transactionKey)
        {
            if (amount > 0)
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(transactionKey), null, null, force: true);
            }
        }

        /// <summary>The switch, the day's cap and the quiet hours.</summary>
        public static bool MayText()
        {
            if (!Settings.Social || !Settings.SocialTexts)
            {
                return false;
            }
            int hour = Hour;
            if (hour >= QuietFrom || hour < QuietTo)
            {
                return false;
            }
            NewTextDay();
            return SocialState.TextsToday < Mathf.Clamp(Settings.SocialTextsMax, 1, 6);
        }

        private static void NewTextDay()
        {
            if (SocialState.TextsDay != Day)
            {
                SocialState.TextsDay = Day;
                SocialState.TextsToday = 0;
            }
        }

        /// <summary>Refused by <see cref="MayText"/> and once they texted today, unless <paramref name="must"/>; with texts off, a notification.</summary>
        public static bool SendText(SocialPerson person, string key, Dictionary<string, string> data, bool must = false)
        {
            if (person == null || key == null || !person.Met || !SocialState.Loaded)
            {
                return false;
            }
            try
            {
                if (must && (!Settings.Social || !Settings.SocialTexts))
                {
                    Said(person, key, data);
                    Toast(NotificationType.Info, key, data, 10f);
                    return true;
                }
                if (!must && (!MayText() || person.LastTextDay == Day))
                {
                    return false;
                }
                Contact contact = SocialPhone.ContactOf(person);
                if (contact == null)
                {
                    return false;
                }
                if (SocialLinks.HoldText == null || !SocialLinks.HoldText(person, key, data, (k, d) => Deliver(person, contact, k, d, must)))
                {
                    contact.SendMessage(new TextMessage(key, data), notify: true, sendNotificationInstantly: must);
                }
                NewTextDay();
                SocialState.TextsToday++;
                person.LastTextDay = Day;
                Said(person, key, data);
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        private static void Deliver(SocialPerson person, Contact contact, string key, Dictionary<string, string> data, bool must)
        {
            try
            {
                contact.SendMessage(new TextMessage(key, data), notify: true, sendNotificationInstantly: must);
                Said(person, key, data);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Agrees a time and a place. False, with a reason's locale key, if a plan is on already or the place is no use.</summary>
        public static bool MakePlan(SocialPerson person, PlanKind kind, float time, in Venue venue, bool theirIdea, out string reason)
        {
            reason = null;
            SocialPlan plan = SocialState.Plan;
            if (plan.Active)
            {
                reason = "reallife_social_plan_already";
                return false;
            }
            if (!venue.Valid)
            {
                reason = "reallife_social_plan_nowhere";
                return false;
            }
            plan.Clear();
            plan.Person = person.Index;
            plan.Kind = kind;
            plan.Time = time;
            plan.Street = venue.Street;
            plan.Number = venue.Number;
            plan.Place = venue.Name;
            plan.X = venue.Front.x;
            plan.Y = venue.Front.y;
            plan.Z = venue.Front.z;
            plan.TheirIdea = theirIdea;
            plan.Arrives = time + SocialRules.ArrivalOffset(person, Random.value);
            if (kind == PlanKind.Lift || kind == PlanKind.Moving)
            {
                plan.Arrives = Mathf.Min(plan.Arrives, time - 2f);   // it is their errand: they are there
            }
            Contacted(person);
            float offset = plan.Arrives - time;
            Logger?.Info("Social: plan made with " + person.Name + ": " + kind + " at " + venue.Name + ", " + Clock(time) + ", "
                         + (time - Now).ToString("F0") + " min from now (they will get there " + Mathf.Abs(offset).ToString("F0") + " min "
                         + (offset < 0f ? "before" : "after") + " that time).");
            return true;
        }

        /// <summary>A small cost, a bigger one at short notice.</summary>
        public static void CancelByPlayer()
        {
            SocialPlan plan = SocialState.Plan;
            SocialPerson person = SocialState.Get(plan.Person);
            if (!plan.Active || person == null)
            {
                plan.Clear();
                return;
            }
            bool shortNotice = plan.Time - Now < 60f;
            Change(person, 0f, shortNotice ? SocialRules.CancelledAffection : -1f, shortNotice ? SocialRules.CancelledTrust : -1f);
            Logger?.Info("Social: the player called off " + plan.Kind + " with " + person.Name + (shortNotice ? " at short notice." : "."));
            plan.Clear();
        }

        /// <summary>The player never came to the plan.</summary>
        public static void StoodUp()
        {
            SocialPlan plan = SocialState.Plan;
            SocialPerson person = SocialState.Get(plan.Person);
            Dictionary<string, string> data = person != null ? PlanData(person, plan, Mathf.Max(0f, Now - plan.Time)) : null;   // before the plan is cleared
            plan.Clear();
            if (person == null)
            {
                return;
            }
            float weight = person.Has(SocialTrait.Punctual) ? 1.4f : person.Has(SocialTrait.Late) ? 0.7f : 1f;
            Change(person, 0f, SocialRules.StoodUpAffection * weight, SocialRules.StoodUpTrust * weight);
            Remember(person, MemoryCode.StoodUp);
            SendText(person, SocialLines.Pick(SocialCatalogue.TextStoodUp), data, true);
            ModState.AddModifier(ModState.MoodDown);
            Logger?.Info("Social: the player stood " + person.Name + " up.");
        }

        /// <summary>The next whole hour from a time at which an outing can be agreed with somebody; negative if none within two days.</summary>
        public static float NextSlot(SocialPerson person, PlanKind kind, float from)
        {
            OutingDef def = SocialCatalogue.Outing(kind);
            float start = Mathf.Ceil(from / 60f) * 60f;
            for (int i = 0; i < 48; i++)
            {
                float at = start + i * 60f;
                var stamp = new Timestamp(at);
                int hour = stamp.Hour;
                bool open = hour >= def.From && hour < def.To || (def.To > 24 && hour < def.To - 24);
                if (open && person.FreeAt(hour, Weekend(stamp.Day)) && !person.Asleep(hour))
                {
                    return at;
                }
            }
            return -1f;
        }
    }
}
