using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// What a feature asks of RLO Texts: the situation (an intent the models were trained on, and its branch), the speaker
    /// read from the game, the values, the thread for a reply, how urgent and by when, and what to do with the words.
    /// Made by the asker on an event (never per frame); handed back through <see cref="Done"/> on the game's thread,
    /// exactly once: the words, or null for the written line.
    /// </summary>
    public sealed class TextRequest
    {
        public TextUse Use;
        public string Key;                  // what it is for; kept words go under it ("fp:12"); the same key asked again replaces the request
        public string Intent;               // "friend.invite.coffee"
        public string Branch;               // the outcome the game decided, for intents with more than one card ("yes", "late"); null for one
        public readonly TextWho Who = new TextWho();
        public readonly TextSlots Slots = new TextSlots();
        public readonly List<TextSaid> History = new List<TextSaid>(3);   // the last two or three messages of the thread, oldest first: replies only
        public string Speaker;              // who says it, for the repeat check ("c:12", "p:3")
        public TextPriority Priority = TextPriority.Soon;
        public double Seconds = 4.0;        // real seconds from now until it is shown: the deadline
        public bool Guess;                  // asked before the player clicked; taken with TextsHub.TakeGuess, never handed over by itself
        public bool Keep;                   // the words are kept under Key in the save (Flare: worded again each time it is drawn)
        public bool Shared;                 // the host's words for the session's shared feed or paper: kept and sent to the guests
        public bool Flush;                  // handed over at once (the written line) when the game saves: a message held back for its words
        public int Day;                     // the game day (for kept words)
        public Func<bool> Wanted;           // asked when the words come back: false if they are no longer wanted (the post went up meanwhile); null: always
        public Action<string> Done;         // on the game's thread: the words, or null for the written line
    }

    /// <summary>The runner as the hub sees it (TextServer; the checks put a pretend one in).</summary>
    public interface ITextRunner
    {
        TextServerState State { get; }
        bool Quiet { set; }
        void Nudge();
    }

    /// <summary>
    /// The heart of RLO Texts on the game's thread, game-free (EconomyCheck, TextsChecks.cs drives it with a pretend runner):
    /// a request becomes a card (TextIntents), a prompt (TextCard) and a job in the one queue (TextQueue); at each look
    /// (<see cref="Look"/>, a few times a second while anything waits, from TextsDirector) what came back is checked
    /// (TextCheck) and handed to its asker, and what missed its deadline is handed over as null, the written line. A
    /// guess (face to face) waits until its asker takes it (<see cref="TakeGuess"/>). The adaptive budget (TextBudget)
    /// hears every end. Words that pass are remembered by speaker for the repeat check, and kept in the store
    /// (TextsStore) when the asker wants them kept. Only the intents of the running pack's data version or older are
    /// asked (<see cref="Data"/>).
    /// COST: a request is a card of a few hundred characters; a look is a pass over the waiting requests (a few).
    /// </summary>
    public sealed class TextsHub
    {
        public const int RecentMost = 4;         // things a speaker said that a new reply must not repeat

        private sealed class Waiting
        {
            public TextRequest Request;
            public TextIntent Intent;
            public TextSheet Sheet;
            public string Card;
            public TextJob Job;
        }

        private readonly TextQueue _queue;
        private readonly TextBudget _budget = new TextBudget();
        private readonly List<Waiting> _waiting = new List<Waiting>(16);
        private readonly List<TextJob> _missed = new List<TextJob>(8);
        private readonly Dictionary<string, List<string>> _recent = new Dictionary<string, List<string>>(32, StringComparer.Ordinal);
        private readonly int[] _verdicts = new int[Enum.GetValues(typeof(TextVerdict)).Length];

        public readonly TextsStore Store = new TextsStore();

        public TextsHub(TextQueue queue)
        {
            _queue = queue;
            _budget.Apply(_queue);
        }

        /// <summary>The runner in use; null while none is started.</summary>
        public ITextRunner Runner { get; set; }

        /// <summary>Which uses may ask now (the options, the game's language, Going Public's rule); set by the director.</summary>
        public Func<TextUse, bool, bool> Allowed;

        /// <summary>
        /// The data version of the running pack's model (TextPack.Data): an intent of a later data version is never asked of
        /// it (the written line). Set by the director when it starts a pack.
        /// </summary>
        public int Data { get; set; } = TextIntents.DataV2;

        /// <summary>This machine writes only what is asked ahead (TextSpeed.AheadOnly).</summary>
        public bool AheadOnly { get; set; }

        /// <summary>Words were kept for a shared item (the host): the session's part has a new line.</summary>
        public Action<TextKept> SharedKept;

        public TextQueue Queue => _queue;
        public TextBudget Budget => _budget;
        public int Pending => _waiting.Count;

        /// <summary>This session: words shown, written lines used where the model was asked, and of those how many the checker refused.</summary>
        public int Shown { get; private set; }
        public int Fallbacks { get; private set; }
        public int Refused { get; private set; }

        public int Verdicts(TextVerdict v) => _verdicts[(int)v];

        /// <summary>True when a request of this use could go to the model now (asked before the card is built, so that nothing is built for nothing).</summary>
        public bool Wants(TextUse use, bool shared)
        {
            return Runner != null && Runner.State != TextServerState.Failed && Runner.State != TextServerState.Off && (Allowed == null || Allowed(use, shared));
        }

        /// <summary>
        /// Asks for words. True: they come through the request's Done, on a later look, words or null. False: not asked
        /// (switched off, no pack, no such intent or a value missing, too slow for it, no time): the asker uses the written
        /// line now, and Done is not called.
        /// </summary>
        public bool Ask(TextRequest r, double now)
        {
            if (r == null || r.Done == null || !Wants(r.Use, r.Shared) || !TextSpeed.Allows(AheadOnly, r.Priority, r.Guess, r.Seconds))
            {
                return false;
            }
            if (Runner.State == TextServerState.Loading && r.Seconds < TextSpeed.AheadSeconds)
            {
                return false;   // (still loading: what is due soon takes its written line now rather than wait for nothing)
            }
            TextIntent intent = TextIntents.Find(r.Intent, r.Branch);
            if (intent == null || intent.Data > Data)
            {
                return false;   // (no such intent, or one the pack's model never learnt: a v2 pack is never asked a v3 intent)
            }
            var sheet = new TextSheet();
            if (!TextIntents.Build(intent, r.Who, r.Slots, r.History, sheet))
            {
                return false;
            }
            var job = new TextJob { Key = r.Key ?? r.Intent, Prompt = TextCard.ChatMl(sheet), Priority = r.Priority, Deadline = now + r.Seconds, Guess = r.Guess };
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                if (_waiting[i].Request.Key != null && _waiting[i].Request.Key == r.Key)
                {
                    End(i, null, false);   // (asked again: the older one gives way)
                }
            }
            if (!_queue.Add(job, now))
            {
                return false;
            }
            _waiting.Add(new Waiting { Request = r, Intent = intent, Sheet = sheet, Card = TextCard.Card(sheet), Job = job });
            Runner.Nudge();
            return true;
        }

        /// <summary>
        /// The look: words that came back checked and handed over, deadlines past handed over as the written line.
        /// Returns how many were handed over.
        /// </summary>
        public int Look(double now)
        {
            _missed.Clear();
            _queue.TakeMissed(_missed);   // (their state says so already; read below with the rest)
            int handed = 0;
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                Waiting w = _waiting[i];
                TextJobState state = w.Job.State;
                if (state == TextJobState.Written)
                {
                    if (w.Request.Guess)
                    {
                        if (now > w.Job.Deadline)
                        {
                            End(i, null, false);   // (never taken: the player went another way)
                            handed++;
                        }
                        continue;   // (its asker takes it, or its time runs out)
                    }
                    if (now > w.Job.Deadline + 0.25)
                    {
                        Over(i, true);
                    }
                    else
                    {
                        Hand(i);
                    }
                    handed++;
                }
                else if (state == TextJobState.Missed || state == TextJobState.Dropped)
                {
                    Over(i, state == TextJobState.Missed);
                    handed++;
                }
                else if (now > w.Job.Deadline)
                {
                    _queue.Drop(w.Job);   // (a running one comes back later and is let go)
                    Over(i, true);
                    handed++;
                }
            }
            return handed;
        }

        /// <summary>
        /// A guess, at the moment its line is needed: the checked words if they are written, else null (the written line;
        /// the request is let go either way).
        /// </summary>
        public string TakeGuess(string key)
        {
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                Waiting w = _waiting[i];
                if (!w.Request.Guess || w.Request.Key != key)
                {
                    continue;
                }
                if (w.Job.State == TextJobState.Written)
                {
                    string words = Checked(w);
                    _waiting.RemoveAt(i);
                    _budget.Ended(false);
                    _budget.Apply(_queue);
                    Count(words != null);
                    return words;
                }
                _queue.Drop(w.Job);
                _waiting.RemoveAt(i);
                Count(false);
                return null;
            }
            return null;
        }

        /// <summary>
        /// Every waiting request marked to flush (a message held back for its words) handed over now as the written line:
        /// the game is saving. Returns how many.
        /// </summary>
        public int FlushForSave()
        {
            int n = 0;
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                if (_waiting[i].Request.Flush)
                {
                    _queue.Drop(_waiting[i].Job);
                    End(i, null, false);
                    n++;
                }
            }
            return n;
        }

        /// <summary>Everything waiting handed over as the written line (switched off, the game closing, another save).</summary>
        public void Clear()
        {
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                _queue.Drop(_waiting[i].Job);
                End(i, null, false);
            }
            _queue.Clear();
        }

        /// <summary>The things a speaker said lately, newest last.</summary>
        public IReadOnlyList<string> Recent(string speaker)
        {
            return speaker != null && _recent.TryGetValue(speaker, out List<string> list) ? list : null;
        }

        /// <summary>Remembers something a speaker said (shown words, or a written line the game showed), for the repeat check.</summary>
        public void Said(string speaker, string words)
        {
            if (speaker == null || string.IsNullOrEmpty(words))
            {
                return;
            }
            if (!_recent.TryGetValue(speaker, out List<string> list))
            {
                list = new List<string>(RecentMost + 1);
                _recent[speaker] = list;
            }
            list.Add(words);
            if (list.Count > RecentMost)
            {
                list.RemoveAt(0);
            }
        }

        // ---------------------------------------------------------------- handing over

        private void Hand(int i)
        {
            Waiting w = _waiting[i];
            string words = Checked(w);
            _budget.Ended(false);
            _budget.Apply(_queue);
            End(i, words, true);
        }

        private void Over(int i, bool missed)
        {
            if (missed)
            {
                _budget.Ended(true);
                _budget.Apply(_queue);
            }
            End(i, null, false);
        }

        // The words checked: null if they fail (counted by verdict).
        private string Checked(Waiting w)
        {
            string words = TextCheck.Tidy(w.Job.Raw);
            TextVerdict verdict = TextCheck.Judge(words, w.Intent, w.Sheet, w.Card, Recent(w.Request.Speaker));
            _verdicts[(int)verdict]++;
            if (verdict != TextVerdict.Ok)
            {
                Refused++;
                return null;
            }
            return words;
        }

        private void End(int i, string words, bool counted)
        {
            Waiting w = _waiting[i];
            _waiting.RemoveAt(i);
            if (words != null && w.Request.Wanted != null && !w.Request.Wanted())
            {
                words = null;   // (too late for where it was going: the written line stays)
                counted = true;
            }
            if (counted || words == null)
            {
                Count(words != null);
            }
            TextRequest r = w.Request;
            if (words != null)
            {
                Said(r.Speaker, words);
                if ((r.Keep || r.Shared) && r.Key != null)
                {
                    TextKept k = Store.Keep(r.Key, r.Day, w.Intent.Key, words, r.Shared);
                    if (r.Shared)
                    {
                        SharedKept?.Invoke(k);
                    }
                }
            }
            r.Done(words);
        }

        private void Count(bool shown)
        {
            if (shown)
            {
                Shown++;
            }
            else
            {
                Fallbacks++;
            }
        }
    }
}
