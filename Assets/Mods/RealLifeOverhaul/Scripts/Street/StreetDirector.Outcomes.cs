using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Flare;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Street
{
    /// <summary>What a turn comes to where the game is needed, and the player's own words.</summary>
    public sealed partial class StreetDirector
    {
        private StreetMeet Meeting()
        {
            StreetStranger s = _chat.Who;
            return new StreetMeet
            {
                First = s.First, Last = s.Last, Female = s.Female, Age = s.Age, Job = s.Job, District = s.District, Traits = s.Traits, Warm = StreetRules.Score(_chat) >= StreetRules.NumberAt,
                Look = _hold.Held && !_hold.Gone ? StreetGame.Look(_hold.Body) : null
            };
        }

        // Their number: a yes makes them somebody the player knows.
        private StreetLine Number()
        {
            StreetMemo m = _chat.Memo;
            bool canMeet = StreetBridges.CanMeet != null && StreetBridges.CanMeet();
            StreetLine line = StreetRules.Number(_chat, _facts, !canMeet);
            if (line == StreetLine.NumberFull)
            {
                // The game's notification says what the line cannot: the way to make room.
                StreetGame.Notice(NotificationType.Info, "reallife_street_note_full", null, 12f);
                Logger?.Info("Street talk: " + _chat.Who.Name + " would swap numbers, but every place in the player's life is taken: told to lose touch with somebody first.");
            }
            if (line != StreetLine.NumberYes)
            {
                return line;
            }
            int cast = StreetBridges.Meet != null ? StreetBridges.Meet(Meeting()) : -1;
            if (cast < 0)
            {
                return StreetLine.NumberLater;
            }
            m.Swapped = true;
            m.Named = true;
            m.Cast = cast;
            _chat.Gain(1);
            if (m.AgeTold && (Meta.Dev.Verbose || _testAge))
            {
                int shown = StreetBridges.AgeOf != null ? StreetBridges.AgeOf(cast) : -1;
                Logger?.Info("Street talk check (age): " + _chat.Who.Name + " said they are " + _chat.Who.Age + " and is now somebody the player knows; their People page shows "
                             + shown + (shown == _chat.Who.Age ? ": PASS." : ": FAIL (the page should show the age they said)."));
            }
            return line;
        }

        // The age test's line, and with Developer diagnostics every answer's: the age said, how, and whether the People page
        // shows the same for somebody the player knows.
        private void AgeCheck(StreetLine line, Dictionary<string, string> data)
        {
            if (!_testAge && !Meta.Dev.Verbose)
            {
                return;
            }
            StreetChat c = _chat;
            data.TryGetValue("age", out string said);
            int cast = c.Memo.Cast;
            int shown = cast >= 0 && StreetBridges.AgeOf != null ? StreetBridges.AgeOf(cast) : -1;
            bool told = StreetCards.Aged(line) && !string.IsNullOrEmpty(said);   // (every written answer names {age}: EconomyCheck)
            bool same = cast < 0 || shown.ToString(System.Globalization.CultureInfo.InvariantCulture) == said;
            Logger?.Info("Street talk check (age): " + c.Who.Name + " was asked their age and said " + said + " (" + line + (StreetRules.AgeForward(c.Who) ? ", finds it forward" : string.Empty)
                         + ", rapport " + c.Rapport + ")"
                         + (cast >= 0 ? "; their People page shows " + shown : "; not somebody the player knows yet: swapping numbers keeps " + said + " on their People page")
                         + (told && same ? ": PASS." : ": FAIL (" + (!told ? "the answer does not say the age" : "the People page shows another age") + ")."));
        }

        // An invitation, decided as on Flare; a yes is booked as a call books a plan.
        private StreetLine Invite(PlanKind? typed, Dictionary<string, string> data)
        {
            StreetMemo m = _chat.Memo;
            m.Invited = true;
            FlareInviteAsk a = StreetRules.Invite(_chat, _facts, _flirted, StreetGame.Now());
            if (typed.HasValue)
            {
                a.Kind = typed.Value;
            }
            if (StreetBridges.InviteLook != null)
            {
                StreetBridges.InviteLook(a);
            }
            else
            {
                a.Off = true;
            }
            FlareInviteAnswer answer = FlareInviteRules.Decide(a, out FlareInviteWhy why);
            data["t_outing"] = FlareInviteRules.CardOuting(a.Kind);
            if (answer == FlareInviteAnswer.Yes)
            {
                int cast = StreetBridges.InviteBook != null ? StreetBridges.InviteBook(a, Meeting()) : -1;
                if (cast < 0)
                {
                    answer = FlareInviteAnswer.Maybe;
                    why = FlareInviteWhy.Busy;
                }
                else
                {
                    m.Swapped = true;
                    m.Named = true;
                    m.Cast = cast;
                    _chat.Gain(1);
                    data["place"] = a.Place;
                    data["time"] = a.When;
                    data["t_time"] = a.CardWhen;
                    StreetGame.Notice(NotificationType.Success, "reallife_street_note_booked", new Dictionary<string, string>(4)
                    {
                        { "outing", SocialLife.Capital(a.Outing ?? string.Empty) }, { "name", _chat.Who.First }, { "place", a.Place }, { "time", a.When }
                    }, 12f);
                }
            }
            Logger?.Info("Street talk: an invitation to " + a.Kind + " for " + _chat.Who.Name + ": " + answer + " (" + why + ")"
                         + (answer == FlareInviteAnswer.Yes ? ", " + a.Place + " at " + a.When : string.Empty) + ".");
            return StreetRules.InviteLine(answer, why);
        }

        private StreetLine Business(int shop, int roll, Dictionary<string, string> data)
        {
            int at = shop >= 0 && shop < _facts.Shops.Count ? shop : 0;
            if (_facts.Shops.Count == 0)
            {
                return StreetLine.BusinessNo;
            }
            data["shop"] = _facts.Shops[at];
            StreetLine line = StreetRules.Business(_chat, roll);
            _chat.Memo.Mentioned = true;
            if (StreetRules.Told(line) && _mouth.Tell(_addresses[at], TimeHelper.CurrentDay))
            {
                _chat.Gain(1);
                StreetBridges.MouthChanged?.Invoke(_addresses[at]);
                StreetGame.Notice(NotificationType.Info, "reallife_street_note_mouth", new Dictionary<string, string>(2)
                {
                    { "name", _chat.Memo.Named ? _chat.Who.First : StreetText.T("reallife_street_stranger") }, { "shop", _facts.Shops[at] }
                }, 8f);
                Logger?.Info("Street talk: " + _chat.Who.Name + " will try " + _facts.Shops[at] + ": word of mouth now " + _mouth.Percent(_addresses[at], TimeHelper.CurrentDay) + "% there.");
            }
            return line;
        }

        // What's good around here: the day's street event, the two nearest businesses, the day's trouble, or nothing much.
        private StreetLine Area(int roll, Dictionary<string, string> data)
        {
            bool eventFirst = StreetRules.Roll(roll, 40, 2) == 0;
            if (_facts.EventToday != null && eventFirst)
            {
                data["event"] = _facts.EventToday;
                return StreetLine.AreaEvent;
            }
            if (StreetGame.Around(_met, _two) == 2)
            {
                data["place"] = _two[0];
                data["place_two"] = _two[1];
                return StreetLine.AreaPlaces;
            }
            if (_facts.EventToday != null)
            {
                data["event"] = _facts.EventToday;
                return StreetLine.AreaEvent;
            }
            if (_facts.NewsDistrict != null)
            {
                data["district"] = _facts.NewsDistrict;
                return StreetLine.AreaNews;
            }
            return StreetLine.AreaNothing;
        }

        // The way to the nearest place of a kind, or a business by name, with the game's destination on it.
        private DialogEntry Way(StreetPlace kind, string named, string player, Dictionary<string, string> data)
        {
            PlayerController p = StreetGame.Player;
            Vector3 from = p != null ? p.transform.position : _met;
            Venue venue = default;
            float metres = 0f;
            bool found = named != null ? StreetGame.Named(named, from, out venue, out metres) : kind < StreetPlace.Count && StreetGame.Nearest(kind, from, out venue, out metres);
            StreetLine line = StreetRules.Directions(found, metres);
            data["what"] = named ?? (kind < StreetPlace.Count ? StreetText.T(StreetLines.Whats[(int)kind]) : StreetText.T("reallife_street_what_coffee"));
            data["t_what"] = named ?? Texts.TextWords.StreetWhats[kind < StreetPlace.Count ? (int)kind : 0];   // the card's words are English in every language
            if (found)
            {
                data["place"] = venue.Name;
                data["street"] = new Address(venue.Street, venue.Number).GetStreetNameLocalized();
                int way = StreetGame.Way(from, p != null ? p.transform.forward : Vector3.forward, venue.Door);
                data["way"] = StreetText.T(StreetLines.Ways[way]);
                data["t_way"] = Texts.TextWords.StreetWays[way];
                int minutes = StreetRules.Minutes(metres);
                data["walk"] = minutes == 1 ? StreetText.T("reallife_street_walk_one")
                    : StreetText.T("reallife_street_walk_many", new Dictionary<string, string>(1) { { "n", minutes.ToString(System.Globalization.CultureInfo.InvariantCulture) } });
                data["t_walk"] = Texts.TextWords.Walk(minutes);
                StreetGame.Destination(venue);
                _chat.Gain(StreetRules.Score(_chat) >= 0 ? 1 : 0);
            }
            Logger?.Info("Street talk: the way to " + (named ?? kind.ToString()) + ": "
                         + (found ? venue.Name + ", " + metres.ToString("F0") + " m" : "none within " + StreetRules.FindMetres.ToString("F0") + " m") + ".");
            return Respond(line, data, player, false);
        }

        private void Said(string text)
        {
            _typing = false;
            if (_chat == null || _pending)
            {
                return;
            }
            try
            {
                Show(Typed(text));
            }
            catch (Exception e)
            {
                Fault("the player's words", e);
            }
        }

        private void TypingClosed()
        {
            if (!_typing || _chat == null)
            {
                return;
            }
            _typing = false;
            Show(Menu(Line(StreetLine.Waiting, Data(_chat), null)));
        }

        // The player's words, answered as the choice they count as; anything else is just talk.
        private DialogEntry Typed(string text)
        {
            string words = Texts.TextIntents.Plain(text ?? string.Empty).Trim();
            if (words.Length > StreetSay.Most)
            {
                words = words.Substring(0, StreetSay.Most);
            }
            Dictionary<string, string> data = Data(_chat);
            if (words.Length == 0)
            {
                return Menu(Line(StreetLine.Waiting, data, null));
            }
            StreetSaid said = StreetSay.Read(words, _facts.Shops);
            _chat.Turns++;
            _aheadNow = false;
            _turnSaid = "typed \"" + words + "\" (read as " + said.Kind + (said.Place != StreetPlace.Count ? ", " + said.Place : string.Empty) + ")";
            if (said.Kind == StreetSayKind.Threat)
            {
                _facts.PoliceNear = StreetBridges.PoliceNear != null && StreetBridges.PoliceNear(_met, StreetRules.PoliceMetres);
                StreetOutcome o = StreetRules.Threat(_facts);
                _chat.Gain(o.Rapport);
                _chat.Memo.Rude = true;
                if (o.Police)
                {
                    StreetBridges.Noticed?.Invoke();
                    StreetGame.Notice(NotificationType.Warning, "reallife_street_note_police", new Dictionary<string, string>(1)
                    {
                        { "name", _chat.Memo.Named ? _chat.Who.First : StreetText.T("reallife_street_stranger") }
                    }, 10f);
                    Logger?.Info("Street talk: an officer heard the player threaten " + _chat.Who.Name + ": the police watch more closely today.");
                }
                return Respond(o.Line, data, words, true);
            }
            StreetAsk ask = StreetSay.AskOf(said.Kind);
            if (ask == StreetAsk.Count)
            {
                StreetOutcome o = StreetRules.Said(_chat, said.Kind);
                _chat.Gain(o.Rapport);
                return Respond(o.Line, data, words, o.Ends);
            }
            if ((ask == StreetAsk.Number || ask == StreetAsk.Invite) && !_facts.SocialOn)
            {
                return Respond(StreetLine.NumberLater, data, words, false);
            }
            if (ask == StreetAsk.Invite && _chat.Memo.Invited)
            {
                return Respond(StreetLine.InviteMaybeStranger, data, words, false);
            }
            if (ask == StreetAsk.Business && _facts.Shops.Count == 0)
            {
                return Respond(StreetLine.SaidCool, data, words, false);
            }
            _chat.Done[(int)ask] = true;
            return Act(ask, words, data, said);
        }

        // While the player looks at somebody, the line they would open with is asked of RLO Texts ahead.
        private void GuessFor(BaseHuman p)
        {
            if (Guess == null || p == null)
            {
                return;
            }
            int day = TimeHelper.CurrentDay;
            StreetStranger who = Who(p, day);
            if (who == null)
            {
                return;
            }
            StreetGame.Fill(_facts, _addresses);
            StreetMemo memo = _memory.Find(who.Key, day);
            var chat = new StreetChat { Who = who, Memo = memo, Approach = StreetRules.Approach(who, memo, _facts, Settings.StreetBrush, StreetGame.Phone(p)) };
            if (chat.Approach.Brush == StreetBrush.None)
            {
                chat.Said(true, StreetText.T(StreetLines.Opener));
                Guess(chat, chat.Approach.Opening, Data(chat));
            }
        }
    }
}
