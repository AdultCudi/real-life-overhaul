using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// One conversation in the game's dialogue panel: the person's line over the choice that fits (blue), "Something else"
    /// (orange) and the player's own words (grey). Each line shows when its words come or <see cref="StreetRules.Hold"/> is over.
    /// COST: on a press or a click, a few short strings; while open, the director's tick keeps the person stopped.
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
        private Action _onSpeaking;
        private string _turnSaid;              // what the player chose or typed this turn, for the turn's log line
        private StreetAsk _aheadAsk = StreetAsk.Count;   // the blue button whose answer was asked ahead, and its line
        private StreetLine _aheadLine;
        private bool _aheadNow;                // the click is the blue button asked ahead (its words may be ready)
        private bool _testAge;                 // the age test's talk: the age question is the first blue button until asked, and its checks are written

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

        /// <summary>
        /// Stops somebody for the player and asks for their first line; false if they cannot be stopped. For the age test
        /// (<paramref name="testAge"/>) they have time, and today's answer is forgotten, so the question is there to ask.
        /// </summary>
        internal bool Begin(BaseHuman p, Pedestrian walker, StreetFigure kind, float now, bool testAge = false)
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
            chat.Speaking = _onSpeaking ?? (_onSpeaking = AboutToSpeak);
            if (testAge)
            {
                chat.Approach.Brush = StreetBrush.None;
                memo.AgeTold = false;
            }
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
            _testAge = testAge;
            _aheadAsk = StreetAsk.Count;
            _lastKey = null;
            _turnSaid = "stopped them (\"" + StreetText.T(StreetLines.Opener) + "\")";
            _met = player.transform.position;
            StreetGame.TurnPlayer(this, p.transform.position);
            _firstLine = chat.Approach.Brush != StreetBrush.None ? StreetRules.BrushLine(chat.Approach.Brush) : chat.Approach.Opening;
            _firstAsked = false;
            _firstWords = chat.Approach.Brush == StreetBrush.None && TakeGuess != null ? TakeGuess(chat) : null;   // guessed while the player looked at them
            _firstCame = _firstWords != null;
            chat.Said(true, StreetText.T(StreetLines.Opener));
            if (!_firstCame && Words != null)
            {
                _firstAsked = Words(chat, _firstLine, Data(chat), StreetText.T(StreetLines.Opener), _onFirst ?? (_onFirst = FirstCame));
            }
            _openAt = now + StreetRules.Beat;
            _openBy = now + (float)Math.Max(StreetRules.Hold, chat.Waits);   // longer only for the player's own AI service
            Logger?.Info("Street talk: " + who.Name + ", " + who.Age + " (" + KindName(_hold.Kind) + "), stopped"
                         + (chat.Approach.Brush != StreetBrush.None ? " and has no time (" + chat.Approach.Brush + ")" : string.Empty)
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

        // The player's AI service has begun to answer: while the line is still awaited, the person talks with their hands.
        private void AboutToSpeak()
        {
            try
            {
                bool awaited = _chat != null && (_pending || (_phase == 1 && _firstAsked && !_firstCame));
                if (!awaited || _hold.Gone || !_hold.Held)
                {
                    return;
                }
                bool shown = _hold.Speak();
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Street talk check (about to speak): your own AI began to answer for " + _chat.Who.Name + "; "
                                 + (shown ? "they start to talk with their hands while the rest comes: PASS." : "no gesture (" + _hold.Why + "); the line still waits: PASS."));
                }
            }
            catch (Exception e)
            {
                Fault("the person about to speak", e);
            }
        }

        // While one is on: the panel opens after the beat, the person is kept stopped, and it ends if they or the player went.
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

        private void End(string why)
        {
            StreetChat chat = _chat;
            if (chat == null)
            {
                return;
            }
            _chat = null;
            _testAge = false;
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

        // A line of theirs: the model's words, or a written line of the kind. Every line but the "go on" between choices is
        // the turn's: the next round follows from it.
        private DialogEntry Line(StreetLine kind, Dictionary<string, string> data, string words)
        {
            string key = StreetLines.Pick(kind, _chat.Who.Seed, _chat.Turns, _lastKey, kind != StreetLine.Waiting ? _chat.Memo.Lines : null);
            _lastKey = key;
            _hold.Quiet();   // hands that moved while it was awaited come to rest
            if (kind != StreetLine.Waiting)
            {
                string said = words ?? StreetText.T(key, data);
                _chat.Said(false, said);
                _chat.Last = kind;
                Spoken?.Invoke(_chat, kind, data);
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

        private void TurnLine(StreetLine kind, string key, string said, bool model)
        {
            StreetChat c = _chat;
            string how = model ? c.Heard ?? "RLO Texts"
                : "the written line " + key + (c.Heard != null ? ": " + c.Heard
                                                   : kind == StreetLine.Tired ? ": their time is up (always written)" : ": RLO Texts was not asked");
            Logger?.Info("Street talk turn " + c.Turns + " with " + c.Who.Name + ": the player " + (_turnSaid ?? "said nothing new") + "; " + kind + ": \"" + said + "\" (" + how + "); warmth "
                         + c.Approach.Warmth + ", rapport " + c.Rapport + " (today " + c.Memo.Rapport + "), " + Math.Max(0, StreetRules.Turns(c) - c.Turns) + " of "
                         + StreetRules.Turns(c) + " turn(s) left.");
            _turnSaid = null;
        }

        private DialogEntry Menu(DialogEntry entry)
        {
            StreetRules.Choices(_chat, _facts, _choices);
            if (_choices.Count == 0)
            {
                return Closing(entry);
            }
            StreetAsk ask = _choices[_chat.Menu % _choices.Count];
            if (_testAge && _chat.Menu == 0 && _choices.IndexOf(StreetAsk.Age) >= 0)
            {
                ask = StreetAsk.Age;
            }
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

        // The blue button's answer asked of RLO Texts as soon as it is shown, when the rules decide it without the game: the
        // line they choose now is the one they will choose at the click.
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
            _aheadLine = StreetRules.Answer(_chat, ask, _facts, StreetRules.Seed(_chat.Who.Seed, _chat.Turns + 1, (int)ask)).Line;   // the click's turn's roll, as Act makes it
            Ahead(_chat, _aheadLine, data, StreetText.T(Label(ask), data));
        }

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

        // The grey button: the player's own words in the game's text box; from the game's cancel key, goodbye.
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

        private DialogEntry Farewell()
        {
            Dictionary<string, string> data = Data(_chat);
            _turnSaid = "said goodbye";
            return Respond(StreetRules.Answer(_chat, StreetAsk.Bye, _facts, 0).Line, data, StreetText.T(StreetLines.Asks[(int)StreetAsk.Bye]), true);
        }

        // What the player says for a choice: the remark that suits the moment, or a wording of the question by their seed.
        private string Label(StreetAsk ask)
        {
            int seed = _chat.Who.Seed;
            switch (ask)
            {
                case StreetAsk.SmallTalk: return StreetLines.Small[StreetRules.SmallTopic(_chat.Who, _facts)];
                case StreetAsk.Invite: return _flirted ? StreetLines.AskDinner : StreetLines.Asks[(int)StreetAsk.Invite];
                case StreetAsk.Day: return StreetLines.DayAsks[StreetRules.Roll(seed, 42, StreetLines.DayAsks.Length)];
                case StreetAsk.Work:
                    return _chat.Who.Job == SocialCatalogue.Jobs.Length - 1 ? StreetLines.AskWorkRetired : StreetLines.WorkAsks[StreetRules.Roll(seed, 43, StreetLines.WorkAsks.Length)];
                case StreetAsk.Age: return StreetLines.AgeAsks[StreetRules.Roll(seed, 44, StreetLines.AgeAsks.Length)];
                case StreetAsk.Thanks: return _chat.Last == StreetLine.DirNone || _chat.Last == StreetLine.AreaNothing ? StreetLines.AskThanksAnyway : StreetLines.Asks[(int)ask];
                default: return StreetLines.Asks[(int)ask];
            }
        }

        // The values every line may name, and the card's words of the moment (t_), decided before the turn.
        private Dictionary<string, string> Data(StreetChat c)
        {
            StreetStranger s = c.Who;
            return new Dictionary<string, string>(12)
            {
                { "age", AgeShown(c).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "t_thanks", StreetRules.WarmThanks(c) ? "warm" : "cool" },
                { "t_weather", _facts.Raining ? "raining" : string.Empty },
                { "first", s.First },
                { "player", _facts.Player ?? string.Empty },
                { "job", SocialCatalogue.Jobs[Mathf.Clamp(s.Job, 0, SocialCatalogue.Jobs.Length - 1)].GetLocalization() },
                { "district", SocialPlaces.District(s.District) },
                { "shop", _facts.Shops.Count > 0 ? _facts.Shops[0] : string.Empty },
                { "event", _facts.EventToday ?? string.Empty },
                { "t_event", _facts.EventWords ?? string.Empty }
            };
        }

        // Their age: as the People page shows it once they are somebody the player knows, else the figure's own (the same
        // number: met in the street, their age there is the one kept, true from that day).
        private static int AgeShown(StreetChat c)
        {
            int cast = c.Memo != null ? c.Memo.Cast : -1;
            int known = cast >= 0 && StreetBridges.AgeOf != null ? StreetBridges.AgeOf(cast) : -1;
            return known >= 0 ? known : c.Who.Age;
        }

        private DialogEntry Choose(StreetAsk ask)
        {
            if (_chat == null || _pending || _typing)
            {
                return null;
            }
            _aheadNow = ask == _aheadAsk;
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

        // The kinds of place, one at a time: blue the kind, orange the next kind, grey back to the talk.
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
                            m.Told = true;   // the player gave their name, whatever came back
                            break;
                        case StreetAsk.AboutThem: c.Shared = line == StreetLine.AboutJob || line == StreetLine.AboutRetired; break;
                        case StreetAsk.Age:
                            m.AgeTold = true;
                            AgeCheck(line, data);
                            break;
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

        private DialogEntry Respond(StreetLine line, Dictionary<string, string> data, string player, bool ends)
        {
            bool ahead = _aheadNow && line == _aheadLine;
            _aheadNow = false;
            _aheadAsk = StreetAsk.Count;
            _chat.Said(true, player);
            string words = ahead && TakeAhead != null ? TakeAhead(_chat) : null;
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
                return null;   // shown by WordsCame
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
            _chat.Menu = 0;   // the round starts again from what fits now
            return Menu(said);
        }
    }

    /// <summary>The conversation in the game's dialogue panel; the game's factory makes it when the panel opens.</summary>
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
