using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Extensions;
using Localizor;
using RealLifeOverhaul.Flare;
using Streets;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// One conversation in the street, in the game's own dialogue panel. Each entry is the person's line with the game's
    /// three buttons: the choice that fits the moment and what they just said (blue: StreetRules.Choices), "Something else"
    /// for the next one (orange: the mod's PersonDialog does the same on the phone) and "Say something of your own" (grey:
    /// the game's text box over the panel, one press away). The game's cancel key (Esc) calls the grey button's action
    /// too (DialogUI.OnCancel: the game's dialogue shows no key hints), and from the key it says goodbye; Goodbye is also
    /// the last choice but rudeness of the round. Without the game's text box the grey button is Goodbye. The way to a
    /// place asks which kind first, the same three ways.
    ///
    /// Speed: the person turns to the player at the press (StreetHold); the panel opens after that beat
    /// (<see cref="StreetRules.Beat"/>) with the first line ready: the model's when it came in time, guessed while the
    /// player looked at them, never later than <see cref="StreetRules.Hold"/>; else the written line. Every later line is
    /// asked when the player clicks and shown when its words come or the hold is over: the panel never waits longer. On a
    /// machine where a reply takes more than half the hold, the answer to the blue button is asked as soon as it is shown
    /// (StreetTexts: guessed ahead), so it is there when it is clicked.
    /// With Developer diagnostics on, one line a turn says what the player chose, what the person said, where its words came
    /// from (RLO Texts: the card and the time, or the written line and why) and the warmth and rapport after it.
    /// COST: on a press or a click, a few short strings; while it is open, the director's tick (ten times a second) keeps
    /// the person stopped and watches that they and the player are still there.
    /// </summary>
    public sealed partial class StreetDirector
    {
        private StreetChat _chat;
        private readonly StreetHold _hold = new StreetHold();
        private int _phase;                    // 0 none, 1 turning to the player, 2 the panel is open
        private float _openAt, _openBy;
        private string _firstWords;
        private bool _firstAsked, _firstCame;
        private StreetLine _firstLine;
        private bool _pending, _typing, _flirted;
        private StreetLine _pendLine;
        private Dictionary<string, string> _pendData;
        private bool _pendEnds;
        private string _lastKey;
        private Vector3 _met;                  // where the player stood when it began
        private readonly List<StreetAsk> _choices = new List<StreetAsk>((int)StreetAsk.Count);
        private readonly string[] _two = new string[2];
        private Action _onClosed, _onTyped, _onSayOrBye, _onGoodbye;
        private Action<string> _onSaid, _onWords, _onFirst;
        private string _turnSaid;              // what the player chose or typed this turn, for the turn's log line
        private StreetAsk _aheadAsk = StreetAsk.Count;   // the blue button whose answer was asked ahead, and its line
        private StreetLine _aheadLine;
        private bool _aheadNow;                // the click is the blue button asked ahead (its words may be ready)

        // ---------------------------------------------------------------- the press

        private void TalkPressed()
        {
            try
            {
                PlayerController player = StreetGame.Player;
                bool usable = _offered != null && (_offeredWalker != null ? StreetGame.Usable(_offeredWalker) : StreetGame.Usable(_offered));
                string what = Meta.Dev.Verbose && player != null ? What(player.transform.position, player.transform.forward) : null;
                if (_chat != null || !usable || StreetGame.DialogOpen)
                {
                    if (what != null)
                    {
                        Logger?.Info("Street talk check (offer): Talk pressed for " + what + ": nothing started ("
                                     + (_chat != null ? "a talk is on already" : !usable ? "they are no longer there to talk to" : "the game's dialogue panel is open") + ").");
                    }
                    return;
                }
                bool began = Begin(_offered, _offeredWalker, _offeredKind, Time.unscaledTime);
                if (what != null)
                {
                    Logger?.Info("Street talk check (offer): Talk pressed for " + what + ": " + (began ? "they stop for the player: PASS." : "FAIL (they could not be stopped)."));
                }
            }
            catch (Exception e)
            {
                Fault("the press of Talk", e);
            }
        }

        /// <summary>Stops somebody for the player: who they are, whether they have time, their first line. False if they cannot be stopped.</summary>
        internal bool Begin(BaseHuman p, Pedestrian walker, StreetFigure kind, float now)
        {
            int day = TimeHelper.CurrentDay;
            StreetStranger who = Who(p, day);
            PlayerController player = StreetGame.Player;
            if (who == null || player == null)
            {
                return false;
            }
            StreetGame.Fill(_facts, _addresses);
            StreetMemo memo = _memory.Get(who.Key, day);
            var chat = new StreetChat { Who = who, Memo = memo, Approach = StreetRules.Approach(who, memo, _facts, Settings.StreetBrush, StreetGame.Phone(p)) };
            if (!_hold.Take(p, walker, kind, player.transform.position, this))
            {
                return false;
            }
            _door.Hide("talking");
            _offered = null;
            _offeredWalker = null;
            _aim.Clear();
            _chat = chat;
            _phase = 1;
            _pending = _typing = _flirted = _aheadNow = false;
            _aheadAsk = StreetAsk.Count;
            _lastKey = null;
            _turnSaid = "stopped them (\"" + StreetText.T(StreetLines.Opener) + "\")";
            _met = player.transform.position;
            StreetGame.TurnPlayer(this, p.transform.position);
            _firstLine = chat.Approach.Brush != StreetBrush.None ? StreetRules.BrushLine(chat.Approach.Brush) : chat.Approach.Opening;
            _firstWords = null;
            _firstAsked = _firstCame = false;
            _firstWords = chat.Approach.Brush == StreetBrush.None && TakeGuess != null ? TakeGuess(chat) : null;   // (guessed while the player looked at them)
            _firstCame = _firstWords != null;
            chat.Said(true, StreetText.T(StreetLines.Opener));
            if (!_firstCame && Words != null)
            {
                _firstAsked = Words(chat, _firstLine, Data(chat), StreetText.T(StreetLines.Opener), _onFirst ?? (_onFirst = FirstCame));
            }
            _openAt = now + StreetRules.Beat;
            _openBy = now + (float)StreetRules.Hold;
            Logger?.Info("Street talk: " + who.Name + ", " + who.Age + " (" + KindName(_hold.Kind) + "), stopped" + (chat.Approach.Brush != StreetBrush.None ? " and has no time (" + chat.Approach.Brush + ")" : string.Empty)
                         + "; warmth " + chat.Approach.Warmth + ", " + chat.Approach.Patience + " turns" + (chat.Approach.Recognised ? ", knows the player's face" : string.Empty)
                         + (memo.Talks > 0 ? ", talked " + memo.Talks + " time(s) today" : string.Empty)
                         + (_firstCame ? "; the first line was guessed ahead." : _firstAsked ? "; the first line is asked of RLO Texts." : "."));
            return true;
        }

        private void FirstCame(string words)
        {
            _firstWords = words;
            _firstCame = true;
        }

        // While one is on: the panel opens after the beat; the person is kept stopped; the conversation ends if they or the player went.
        private void Tend(float now)
        {
            PlayerController player = StreetGame.Player;
            if (_hold.Gone || !_hold.Held)
            {
                End("the game took them");
                return;
            }
            Vector3 at = _hold.Body.transform.position;
            if (player == null || (player.transform.position - at).sqrMagnitude > StreetRules.WalkAway * StreetRules.WalkAway)
            {
                End("the player walked away");
                return;
            }
            _hold.Keep();
            if (_phase == 1)
            {
                if (!StreetGame.OnFootOutside() || (StreetGame.DialogOpen && !Ours()))
                {
                    End("something else took the player");
                    return;
                }
                if (now >= _openBy || (now >= _openAt && (_firstCame || !_firstAsked)))
                {
                    _phase = 2;
                    StreetGame.OpenDialog(_hold.Character, () => new StreetDialog(this), _onClosed);
                }
                return;
            }
            if (!StreetGame.DialogOpen)
            {
                End("the panel closed");
            }
        }

        private void DialogClosed()
        {
            if (_chat != null)
            {
                End("the conversation ended");
            }
        }

        // Every way out: the memory of the day gets what came of it, and the person walks on.
        private void End(string why)
        {
            StreetChat chat = _chat;
            if (chat == null)
            {
                return;
            }
            _chat = null;
            bool open = _phase == 2 && Ours();
            _phase = 0;
            _pending = _typing = false;
            StreetRules.Close(chat);
            _hold.Release();
            if (open)
            {
                try
                {
                    DialogController.current.CancelDialog();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            Logger?.Info("Street talk: " + chat.Who.Name + " walks on (" + why + "); " + chat.Turns + " turn(s), rapport " + chat.Rapport + ", today " + chat.Memo.Rapport + ".");
        }

        private static bool Ours() => DialogController.current != null && DialogController.current.dialog is StreetDialog;

        // ---------------------------------------------------------------- the entries

        /// <summary>The panel's first entry: the opening line with the choices, or a brush-off with only the way out.</summary>
        internal DialogEntry First()
        {
            if (_chat == null)
            {
                return Closing(new DialogEntry { messageData = "reallife_street_stranger".Localize() });
            }
            DialogEntry entry = Line(_firstLine, Data(_chat), _firstCame ? _firstWords : null);
            return _chat.Approach.Brush != StreetBrush.None ? Closing(entry) : Menu(entry);
        }

        // A line of theirs: the model's words, or a written line of the kind (never the one they said last, nor one they said
        // to the player earlier today). Every line but the "go on" between choices is the turn's: it is what the next round
        // follows from, and with Developer diagnostics on it is written to the log.
        private DialogEntry Line(StreetLine kind, Dictionary<string, string> data, string words)
        {
            string key = StreetLines.Pick(kind, _chat.Who.Seed, _chat.Turns, _lastKey, kind != StreetLine.Waiting ? _chat.Memo.Lines : null);
            _lastKey = key;
            if (kind != StreetLine.Waiting)
            {
                string said = words ?? StreetText.T(key, data);
                _chat.Said(false, said);
                _chat.Last = kind;
                if (words == null)
                {
                    _chat.Memo.Said(key);
                }
                if (Meta.Dev.Verbose)
                {
                    TurnLine(kind, key, said, words != null);
                }
                _chat.Heard = null;
            }
            return new DialogEntry
            {
                messageData = words != null ? SocialLinks.WordsKey.Localize(new Dictionary<string, string>(1) { { "text", words } }) : key.Localize(data)
            };
        }

        // The turn's log line (Developer diagnostics): what the player chose or typed, what they said and where its words came
        // from, and how the talk stands after it.
        private void TurnLine(StreetLine kind, string key, string said, bool model)
        {
            StreetChat c = _chat;
            string how = model ? "RLO Texts" + (c.Heard != null ? ", " + c.Heard : string.Empty)
                : "written " + key + (c.Heard != null ? ": " + c.Heard : kind == StreetLine.Tired ? ": their time is up (always written)" : ": RLO Texts was not asked");
            Logger?.Info("Street talk turn " + c.Turns + " with " + c.Who.Name + ": the player " + (_turnSaid ?? "said nothing new") + "; " + kind + ": \"" + said + "\" (" + how + "); warmth "
                         + c.Approach.Warmth + ", rapport " + c.Rapport + " (today " + c.Memo.Rapport + "), " + Math.Max(0, StreetRules.Turns(c) - c.Turns) + " of " + StreetRules.Turns(c) + " turn(s) left.");
            _turnSaid = null;
        }

        // The choices under a line: the one that fits now (blue), "Something else" (orange), the player's own words (grey; the
        // game's cancel key says goodbye from it). The blue button's answer may be asked ahead (RLO Texts on a slow machine).
        private DialogEntry Menu(DialogEntry entry)
        {
            StreetRules.Choices(_chat, _facts, _choices);
            if (_choices.Count == 0)
            {
                return Closing(entry);
            }
            StreetAsk ask = _choices[_chat.Menu % _choices.Count];
            Dictionary<string, string> data = Data(_chat);
            entry.ConfirmTextOverride = Label(ask).Localize(data);
            entry.OnConfirm = () => Choose(ask);
            if (_choices.Count > 1)
            {
                entry.SecondOptionTextOverride = "reallife_social_btn_else";
                entry.OnSecondOption = Else;
            }
            bool type = StreetGame.CanType;
            entry.CancelTextOverride = type ? StreetLines.Asks[(int)StreetAsk.Say] : StreetLines.Asks[(int)StreetAsk.Bye];
            entry.OnCancel = type ? _onSayOrBye : _onGoodbye;
            if (_chat.Menu == 0)
            {
                AskAhead(ask, data);
            }
            return entry;
        }

        // The answer to the blue button asked of RLO Texts as soon as it is shown, when it is one the rules decide without the
        // game (not the way, a number, an invitation, the business, the area, the election or rudeness): the line the rules
        // will choose at the click is the one they choose now.
        private void AskAhead(StreetAsk ask, Dictionary<string, string> data)
        {
            _aheadAsk = StreetAsk.Count;
            if (Ahead == null)
            {
                return;
            }
            switch (ask)
            {
                case StreetAsk.SmallTalk: case StreetAsk.AboutThem: case StreetAsk.Name: case StreetAsk.Compliment: case StreetAsk.Joke: case StreetAsk.Flirt:
                case StreetAsk.Economy: case StreetAsk.Day: case StreetAsk.Event: case StreetAsk.Work: case StreetAsk.Thanks: case StreetAsk.Bye:
                    break;
                default:
                    return;
            }
            _aheadAsk = ask;
            _aheadLine = StreetRules.Answer(_chat, ask, _facts, StreetRules.Seed(_chat.Who.Seed, _chat.Turns + 1, (int)ask)).Line;   // (the roll of the click's turn: Act's)
            Ahead(_chat, _aheadLine, data, StreetText.T(Label(ask), data));
        }

        // The last line: only the way out.
        private DialogEntry Closing(DialogEntry entry)
        {
            entry.OnConfirm = null;
            entry.OnSecondOption = null;
            entry.CancelTextOverride = "reallife_street_btn_leave";
            entry.OnCancel = Leave;
            return entry;
        }

        private static void Leave()
        {
            if (Ours())
            {
                DialogController.current.CancelDialog();
            }
        }

        private static void Show(DialogEntry entry)
        {
            if (entry != null && Ours())
            {
                DialogController.current.ShowEntry(Guard.Entry(entry));
            }
        }

        private DialogEntry Else()
        {
            if (_chat == null || _pending)
            {
                return null;
            }
            _chat.Menu++;
            return Menu(Line(StreetLine.Waiting, Data(_chat), null));
        }

        // The grey button: the player's own words in the game's text box; from the game's cancel key (Esc), goodbye.
        private void SayOrBye()
        {
            if (_chat == null || _pending || _typing)
            {
                return;
            }
            if (StreetGame.CancelKey)
            {
                Goodbye();
                return;
            }
            _typing = StreetGame.AskWords(_onSaid, _onTyped);
        }

        private void Goodbye()
        {
            if (_chat == null || _pending || _typing)
            {
                return;
            }
            Show(Farewell());
        }

        // Goodbye, from the round, the grey button or the key: their goodbye (the model's when it comes in time), then only the way out.
        private DialogEntry Farewell()
        {
            Dictionary<string, string> data = Data(_chat);
            _turnSaid = "said goodbye";
            return Respond(StreetRules.Answer(_chat, StreetAsk.Bye, _facts, 0).Line, data, StreetText.T(StreetLines.Asks[(int)StreetAsk.Bye]), true);
        }

        // What the player says for a choice (the button, and the player's line on the card): the remark that suits the
        // moment, and the person's own wording of a question, by their seed.
        private string Label(StreetAsk ask)
        {
            int seed = _chat.Who.Seed;
            switch (ask)
            {
                case StreetAsk.SmallTalk: return StreetLines.Small[StreetRules.SmallTopic(_chat.Who, _facts)];
                case StreetAsk.Invite: return _flirted ? StreetLines.AskDinner : StreetLines.Asks[(int)StreetAsk.Invite];
                case StreetAsk.Day: return StreetLines.DayAsks[StreetRules.Roll(seed, 42, StreetLines.DayAsks.Length)];
                case StreetAsk.Work: return _chat.Who.Job == SocialCatalogue.Jobs.Length - 1 ? StreetLines.AskWorkRetired : StreetLines.WorkAsks[StreetRules.Roll(seed, 43, StreetLines.WorkAsks.Length)];
                case StreetAsk.Thanks: return _chat.Last == StreetLine.DirNone || _chat.Last == StreetLine.AreaNothing ? StreetLines.AskThanksAnyway : StreetLines.Asks[(int)ask];
                default: return StreetLines.Asks[(int)ask];
            }
        }

        // The values every line may name, and the card's words of the moment (RLO Texts: thanks taken warmly or coolly, decided
        // before the turn as the rules decide it; the rain).
        private Dictionary<string, string> Data(StreetChat c)
        {
            StreetStranger s = c.Who;
            return new Dictionary<string, string>(10)
            {
                { "t_thanks", StreetRules.WarmThanks(c) ? "warm" : "cool" },
                { "t_weather", _facts.Raining ? "raining" : string.Empty },
                { "first", s.First },
                { "player", _facts.Player ?? string.Empty },
                { "job", SocialCatalogue.Jobs[Mathf.Clamp(s.Job, 0, SocialCatalogue.Jobs.Length - 1)].GetLocalization() },
                { "district", SocialPlaces.District(s.District) },
                { "shop", _facts.Shops.Count > 0 ? _facts.Shops[0] : string.Empty },
                { "event", _facts.EventToday ?? string.Empty }
            };
        }

        // ---------------------------------------------------------------- the choices

        private DialogEntry Choose(StreetAsk ask)
        {
            if (_chat == null || _pending || _typing)
            {
                return null;
            }
            _aheadNow = ask == _aheadAsk;   // (the blue button of this round, asked ahead)
            if (ask == StreetAsk.Bye)
            {
                return Farewell();
            }
            _chat.Turns++;
            _chat.Done[(int)ask] = true;
            Dictionary<string, string> data = Data(_chat);
            string player = StreetText.T(Label(ask), data);
            _turnSaid = "chose " + ask + " (\"" + player + "\")";
            switch (ask)
            {
                case StreetAsk.Directions:
                    _chat.Said(true, player);
                    _chat.PlaceAt = 0;
                    return Places(Line(StreetLine.DirAsk, data, null));
                default:
                    return Act(ask, player, data, new StreetSaid { Kind = StreetSayKind.Other, Place = StreetPlace.Count, Shop = -1 });
            }
        }

        // The kinds of place asked the way to, one at a time: blue the kind, orange the next kind, grey back to the talk.
        private DialogEntry Places(DialogEntry entry)
        {
            var kind = (StreetPlace)(_chat.PlaceAt % (int)StreetPlace.Count);
            entry.ConfirmTextOverride = StreetLines.Places[(int)kind].Localize();
            entry.OnConfirm = () =>
            {
                if (_pending || _chat == null)
                {
                    return null;
                }
                string place = StreetText.T(StreetLines.Places[(int)kind]);
                _turnSaid = "asked the way to " + kind + " (\"" + place + "\")";
                return Way(kind, null, place, Data(_chat));
            };
            entry.SecondOptionTextOverride = "reallife_street_btn_somewhere_else";
            entry.OnSecondOption = () =>
            {
                if (_chat == null || _pending)
                {
                    return null;
                }
                _chat.PlaceAt++;
                return Places(Line(StreetLine.Waiting, Data(_chat), null));
            };
            entry.CancelTextOverride = "reallife_street_btn_never_mind";
            entry.OnCancel = () =>
            {
                if (_chat != null && !_pending)
                {
                    Show(Menu(Line(StreetLine.Waiting, Data(_chat), null)));
                }
            };
            return entry;
        }

        // What a choice comes to, by the rules, and its line.
        private DialogEntry Act(StreetAsk ask, string player, Dictionary<string, string> data, StreetSaid said)
        {
            StreetChat c = _chat;
            StreetMemo m = c.Memo;
            int roll = StreetRules.Seed(c.Who.Seed, c.Turns, (int)ask);
            StreetLine line;
            bool ends = false;
            switch (ask)
            {
                case StreetAsk.Number:
                    line = Number();
                    break;
                case StreetAsk.Invite:
                    line = Invite(said.Kind == StreetSayKind.Invite ? said.Outing : (PlanKind?)null, data);
                    break;
                case StreetAsk.Business:
                    line = Business(said.Shop, roll, data);
                    break;
                case StreetAsk.Area:
                    line = Area(roll, data);
                    break;
                case StreetAsk.Directions:
                    return Way(said.Place, said.Target, player, data);
                default:
                    StreetOutcome o = StreetRules.Answer(c, ask, _facts, roll);
                    c.Gain(o.Rapport);
                    line = o.Line;
                    ends = o.Ends;
                    switch (ask)
                    {
                        case StreetAsk.Name:
                            m.Named |= line == StreetLine.NameGive;
                            m.Told = true;   // (the player gave their name, whatever came back)
                            break;
                        case StreetAsk.AboutThem: c.Shared = line == StreetLine.AboutJob || line == StreetLine.AboutRetired; break;
                        case StreetAsk.Compliment: m.Complimented = true; break;
                        case StreetAsk.Joke: m.Joked = true; break;
                        case StreetAsk.Flirt:
                            m.Flirted = true;
                            _flirted = line == StreetLine.FlirtYes;
                            break;
                        case StreetAsk.Rude: m.Rude = true; break;
                        case StreetAsk.Election:
                            if (line == StreetLine.ElectionFor)
                            {
                                data["candidate"] = _facts.Candidates[StreetRules.Candidate(c.Who, _facts.Candidates.Count)];
                            }
                            break;
                    }
                    break;
            }
            return Respond(line, data, player, ends);
        }

        // Their answer to what the player said: the model's words if RLO Texts has them within the hold, else the written line.
        private DialogEntry Respond(StreetLine line, Dictionary<string, string> data, string player, bool ends)
        {
            bool ahead = _aheadNow && line == _aheadLine;
            _aheadNow = false;
            _aheadAsk = StreetAsk.Count;
            _chat.Said(true, player);
            string words = ahead && TakeAhead != null ? TakeAhead(_chat) : null;   // (asked when the button was shown: ready now, or asked as usual)
            if (words != null)
            {
                return Next(Line(line, data, words), ends);
            }
            if (Words != null && Words(_chat, line, data, player, _onWords))
            {
                _pending = true;
                _pendLine = line;
                _pendData = data;
                _pendEnds = ends;
                return null;   // (shown when the words come, or the hold is over: WordsCame)
            }
            return Next(Line(line, data, null), ends);
        }

        private void WordsCame(string words)
        {
            if (!_pending || _chat == null)
            {
                return;
            }
            _pending = false;
            Show(Next(Line(_pendLine, _pendData, words), _pendEnds));
        }

        // After their line: the choices, or the way out when they go or have given all their turns.
        private DialogEntry Next(DialogEntry said, bool ends)
        {
            if (ends)
            {
                return Closing(said);
            }
            if (StreetRules.Spent(_chat))
            {
                Show(said);
                return Closing(Line(StreetLine.Tired, Data(_chat), null));
            }
            _chat.Menu = 0;   // (the round starts again from what fits now: the follow-ups of what they just said)
            return Menu(said);
        }

        // ---------------------------------------------------------------- what needs the game

        private StreetMeet Meeting()
        {
            StreetStranger s = _chat.Who;
            return new StreetMeet
            {
                First = s.First, Last = s.Last, Female = s.Female, Age = s.Age, Job = s.Job, District = s.District, Traits = s.Traits, Warm = StreetRules.Score(_chat) >= StreetRules.NumberAt,
                Look = _hold.Held && !_hold.Gone ? StreetGame.Look(_hold.Body) : null
            };
        }

        // Their number: the rules decide; a yes makes them somebody the player knows (the social life, met in the street).
        private StreetLine Number()
        {
            StreetMemo m = _chat.Memo;
            bool canMeet = StreetBridges.CanMeet != null && StreetBridges.CanMeet();
            StreetLine line = StreetRules.Number(_chat, _facts, !canMeet);
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
            return line;
        }

        // An invitation, decided as an invitation on Flare is; a yes booked as a call books a plan.
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
            data["t_outing"] = FlareInviteRules.CardOuting(a.Kind);   // (the card's words: RLO Texts)
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
            Logger?.Info("Street talk: an invitation to " + a.Kind + " for " + _chat.Who.Name + ": " + answer + " (" + why + ")" + (answer == FlareInviteAnswer.Yes ? ", " + a.Place + " at " + a.When : string.Empty) + ".");
            return StreetRules.InviteLine(answer, why);
        }

        // The player's business: word of mouth when somebody takes to it, capped.
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

        // What's good around here: the street event of the day, the two nearest businesses, the day's trouble, or nothing much.
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

        // The way to a kind of place or a business by name: the nearest, its street, which way and how far, and the game's destination on it.
        private DialogEntry Way(StreetPlace kind, string named, string player, Dictionary<string, string> data)
        {
            PlayerController p = StreetGame.Player;
            Vector3 from = p != null ? p.transform.position : _met;
            Venue venue = default;
            float metres = 0f;
            bool found = named != null ? StreetGame.Named(named, from, out venue, out metres) : kind < StreetPlace.Count && StreetGame.Nearest(kind, from, out venue, out metres);
            StreetLine line = StreetRules.Directions(found, metres);
            data["what"] = named ?? (kind < StreetPlace.Count ? StreetText.T(StreetLines.Whats[(int)kind]) : StreetText.T("reallife_street_what_coffee"));
            if (found)
            {
                data["place"] = venue.Name;
                data["street"] = new Address(venue.Street, venue.Number).GetStreetNameLocalized();
                data["way"] = StreetText.T(StreetLines.Ways[StreetGame.Way(from, p != null ? p.transform.forward : Vector3.forward, venue.Door)]);
                int minutes = StreetRules.Minutes(metres);
                data["walk"] = minutes == 1 ? StreetText.T("reallife_street_walk_one")
                    : StreetText.T("reallife_street_walk_many", new Dictionary<string, string>(1) { { "n", minutes.ToString(System.Globalization.CultureInfo.InvariantCulture) } });
                StreetGame.Destination(venue);
                _chat.Gain(StreetRules.Score(_chat) >= 0 ? 1 : 0);
            }
            Logger?.Info("Street talk: the way to " + (named ?? kind.ToString()) + ": " + (found ? venue.Name + ", " + metres.ToString("F0") + " m" : "none within " + StreetRules.FindMetres.ToString("F0") + " m") + ".");
            return Respond(line, data, player, false);
        }

        // ---------------------------------------------------------------- the player's own words

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

        // The words read with fixed rules and answered as the choice they count as; anything else is just talk.
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

        // ---------------------------------------------------------------- RLO Texts: the first line, guessed ahead

        // While the player looks at somebody, the line they would open with is asked ahead (only when RLO Texts runs).
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

    /// <summary>The conversation in the game's own dialogue panel: the game's factory makes it when the panel opens, and it shows the first entry at once.</summary>
    public sealed class StreetDialog : Dialog
    {
        public StreetDialog(StreetDirector director)
        {
            npcNameKey = "reallife_street_stranger";
            DialogEntry first = director != null ? director.First() : null;
            DialogController.current.ShowEntry(Guard.Entry(first ?? new DialogEntry { messageData = "reallife_street_stranger".Localize(), OnCancel = () => DialogController.current.CancelDialog() }));
        }
    }
}
