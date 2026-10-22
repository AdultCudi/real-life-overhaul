using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The director's part that runs once a game hour: the line of what was shown, and the city's new talk. With a pack of
    /// data 6 the staff text and gossip passes events on; with a pack or without one the people of the player's life text
    /// after the player's own events, through the social life's own texts and caps. One life text and one piece of gossip
    /// an hour at most. Each player's own: nothing of it is sent in a Going Public session.
    /// COST: once a game hour, a pass over the staff and the people, a few dozen each.
    /// </summary>
    public sealed partial class TextsDirector
    {
        public const double StaffHold = 4.0;   // real seconds a message of the staff waits for its words, as the phone's texts do
        public const string StaffStem = "reallife" + "_texts_staff_";   // the staff's keys without their kind and number, built so they are not read as keys

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly TextCityViews Views = new TextCityViews();
        private static readonly List<TextLifeEvent> NewEvents = new List<TextLifeEvent>(4);
        private int _hourDay = -1, _hour = -1;

        private void Hour()
        {
            GameInstance save = SaveGameManager.Current;
            if (!_loaded || save == null || (save.Hour == _hour && save.Day == _hourDay))
            {
                return;
            }
            bool first = _hour < 0;
            _hour = save.Hour;
            _hourDay = save.Day;
            if (first)
            {
                return;   // the first look of a session only notes the hour
            }
            if (Meta.Dev.Verbose)
            {
                string line = Hub.Tally.Line("day " + save.Day.ToString(Inv) + ", " + save.Hour.ToString("00", Inv) + ":00");
                if (line != null)
                {
                    Logger?.Info(line);
                }
            }
            if (Settings.TextsCity)
            {
                try
                {
                    City(save.Day, save.Hour, Six);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);   // the next hour tries again
                }
            }
        }

        // The city's new talk this hour: the life texts always, the staff and gossip with a pack of data 6.
        private static void City(int day, int hour, bool six)
        {
            if (six && TextsBridges.Staff != null)
            {
                ReadStaff();
                for (int i = 0; i < Views.Staff.Count; i++)
                {
                    TextStaff s = Views.Staff[i];
                    TextStaffSay say = TextsCityRules.Staff(s, CityStore.StaffMark(s.Id), day, hour);
                    if (say != TextStaffSay.None)
                    {
                        StaffMessage(s, say, day);
                    }
                }
                _cityChanged |= Views.Staff.Count > 0;
            }
            if (TextsBridges.Life != null)
            {
                Views.Life.Day = day;
                TextsBridges.Life(Views.Life);
                NewEvents.Clear();
                TextsCityRules.Life(Views.Life, CityStore.Mark, TextsBridges.BigFloor != null ? TextsBridges.BigFloor() : 25000, NewEvents);
                CityStore.Events.AddRange(NewEvents);
                _cityChanged = true;
            }
            if (TextsBridges.People == null || TextsBridges.PersonSay == null || CityStore.Events.Count == 0)
            {
                return;
            }
            Views.People.Clear();
            TextsBridges.People(Views.People);
            LifeTexts(day);
            if (six)
            {
                Gossip(day, hour);
            }
        }

        // One life event's text this hour: the oldest still to be sent today or yesterday.
        private static void LifeTexts(int day)
        {
            for (int i = 0; i < CityStore.Events.Count; i++)
            {
                TextLifeEvent e = CityStore.Events[i];
                if (e.Done)
                {
                    continue;
                }
                if (day - e.Day > 1)
                {
                    e.Done = true;   // nobody could text about it in time: let go
                    continue;
                }
                if (!TextsCityRules.Sender(e, Views.People, day, out TextLifeSender sender))
                {
                    continue;   // nobody may text now: the next hour tries again
                }
                if (TextsBridges.PersonSay(sender.Index, sender.Key, LifeData(e, null)))
                {
                    e.Texted = sender.Index;
                    e.Done = true;
                    _cityChanged = true;
                    return;
                }
            }
        }

        // One piece of gossip this hour.
        private static void Gossip(int day, int hour)
        {
            for (int i = 0; i < CityStore.Events.Count; i++)
            {
                TextLifeEvent e = CityStore.Events[i];
                if (!TextsCityRules.Gossip(e, Views.People, day, hour, out int teller, out int hearer))
                {
                    continue;
                }
                string tellerFirst = null;
                for (int k = 0; k < Views.People.Count; k++)
                {
                    tellerFirst = Views.People[k].Index == teller ? Views.People[k].First : tellerFirst;
                }
                e.Gossiped = true;   // once, sent or not: the moment has passed
                _cityChanged = true;
                if (tellerFirst != null && TextsBridges.PersonSay(hearer, TextsCityRules.GossipKey(e.Kind, TextsCityRules.Hash(e.Day, e.Kind, hearer)), LifeData(e, tellerFirst)))
                {
                    Remember("s:" + hearer.ToString(Inv), TextMemoryKinds.Event, e.Kind, e.Value, tellerFirst);
                    return;
                }
            }
        }

        // A life event's values for its written line and its card.
        private static Dictionary<string, string> LifeData(TextLifeEvent e, string other)
        {
            var data = new Dictionary<string, string>(4);
            switch (e.Kind)
            {
                case "crash":
                case "knocked":
                    if (e.Value != null)
                    {
                        data["street"] = e.Value;
                    }
                    break;
                case "opened": data["shop"] = e.Value ?? string.Empty; break;
                case "listed": data["company"] = e.Value ?? string.Empty; break;
                case "graduated":
                    if (e.Value != null)
                    {
                        data["degree"] = e.Value;
                    }
                    break;
                case "married":
                    if (e.Value != null)
                    {
                        data["name"] = e.Value;
                    }
                    break;
                case "baby":
                    if (e.Value != null)
                    {
                        data["name"] = e.Value;
                    }
                    if (e.Child != null)
                    {
                        data["child"] = e.Child;
                    }
                    break;
                case "win":
                case "loss":
                    long sum = long.TryParse(e.Value, NumberStyles.Integer, Inv, out long v) ? v : 0;
                    data["amount"] = TextsGame.Money(sum);   // as the game writes money: the card carries the same figure
                    break;
            }
            if (other != null)
            {
                data["other"] = other;
            }
            return data;
        }

        // The player's staff as running a business has them, with the game's own facts of each.
        private static void ReadStaff()
        {
            Views.Staff.Clear();
            TextsBridges.Staff?.Invoke(Views.Staff);
            if (Views.Staff.Count == 0)
            {
                return;
            }
            TextsGame.IndexEmployees();
            for (int i = Views.Staff.Count - 1; i >= 0; i--)
            {
                if (!TextsGame.ReadEmployee(Views.Staff[i]))
                {
                    Views.Staff.RemoveAt(i);   // no longer the game's employee: nothing to say
                }
            }
        }

        // A message of one of the staff in the game's thread with them: the model's words, or the written line.
        private static void StaffMessage(TextStaff s, TextStaffSay say, int day)
        {
            string intent = StaffIntent(say);
            string key = StaffStem + StaffWord(say) + "_" + (1 + TextsCityRules.Hash(day, intent, s.Seed) % 2).ToString(Inv);
            var data = new Dictionary<string, string>(6)
            {
                { "name", s.First }, { "shop", s.Shop ?? string.Empty }, { "paid", TextsGame.Wage(s.Paid) }, { "asked", TextsGame.Wage(s.AskTo) },
                { "going", TextsGame.Wage(s.Going) }, { "hours", s.WeekHours.ToString(Inv) }, { "most", s.MostHours.ToString(Inv) }
            };
            string id = s.Id;
            if (!Hub.Wants(TextUse.People, false))
            {
                StaffRemember(id, say, intent, data);
                Hub.NotAsked(TextUse.People, false, intent);
                TextsGame.EmployeeSay(id, key, data);
                return;
            }
            var r = new TextRequest
            {
                Use = TextUse.People, Intent = intent, Key = "staff:" + id, Speaker = "e:" + id, Person = "e:" + id, Priority = TextPriority.Soon, Seconds = StaffHold,
                Flush = true, Day = day
            };
            r.Who.Name = s.Name;
            r.Who.First = s.First;
            r.Who.Job = s.Job;
            r.Who.Works = s.Shop;
            r.Who.Style = TextWords.StaffStyle(s.Seed);
            r.Who.Player = TextsGame.PlayerName;
            r.Who.When = TextsGame.When();
            r.Slots.Set("shop", s.Shop).Set("paid", data["paid"]).Set("asked", data["asked"]).Set("going", data["going"]).Set("hours", data["hours"]).Set("most", data["most"]);
            Recall(r.Person, r.Memory);
            r.Memory.Add(new TextFact(TextMemoryKinds.Met, "staff", day, s.Shop));   // a standing fact: no day said
            StaffRemember(id, say, intent, data);   // after the card's own memory: its news says it already
            GameInstance save = SaveGameManager.Current;   // never sent into another save
            r.Done = words =>
            {
                if (ReferenceEquals(save, SaveGameManager.Current))
                {
                    TextsGame.EmployeeSay(id, words != null ? TextsText.Line : key, words != null ? new Dictionary<string, string>(1) { { "text", words } } : data);
                }
            };
            if (!Hub.Ask(r, TextClock.Now))
            {
                TextsGame.EmployeeSay(id, key, data);
            }
        }

        // What one of the staff remembers of the message: what it was about, and a raise on the day it was seen.
        private static void StaffRemember(string id, TextStaffSay say, string intent, Dictionary<string, string> data)
        {
            Remember("e:" + id, TextMemoryKinds.Talked, "text", TextMemoryTopics.Text(intent));
            if (say == TextStaffSay.RaiseThanks)
            {
                Remember("e:" + id, TextMemoryKinds.Favour, "raise", data["paid"]);
            }
        }

        private static string StaffIntent(TextStaffSay say)
        {
            switch (say)
            {
                case TextStaffSay.RaiseAsk: return "employee.raise_ask";
                case TextStaffSay.Sick: return "employee.sick";
                case TextStaffSay.RaiseThanks: return "employee.raise_thanks";
                default: return "employee.hours";
            }
        }

        private static string StaffWord(TextStaffSay say)
        {
            switch (say)
            {
                case TextStaffSay.RaiseAsk: return "raise_ask";
                case TextStaffSay.Sick: return "sick";
                case TextStaffSay.RaiseThanks: return "raise_thanks";
                default: return "hours";
            }
        }
    }
}
