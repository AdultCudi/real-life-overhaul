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
        public string Why;                  // filled by the hub: why there are no words (not asked, refused by the checker, the time ran out), for the asker's log; null with words
        public double Took;                 // filled by the hub: seconds the model took over it (0 when it never ran)
        public string Person;               // data v6: whose memory the card carries (TextMemoryRules.KeyOk); null: none
        public readonly List<TextFact> Memory = new List<TextFact>(8);   // data v6: what that person remembers, filled by the asker (TextsDirector.Recall); the hub picks the card's lines
        public string Lang;                 // filled by the hub: the language the card asks for (data v6), or null below
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
    /// hears every end but a background request the queue held back itself (<see cref="BudgetMiss"/>). Words that pass
    /// are remembered by speaker for the repeat check, and kept in the store (TextsStore) when the asker wants them kept.
    /// Only the intents of the running pack's data version or older are asked (<see cref="Data"/>).
    ///
    /// A pack of data v6 is asked in the game's language when the pack writes it (<see cref="Language"/>,
    /// <see cref="Languages"/>): the card carries its lang and the speaker's memory (<see cref="TextRequest.Memory"/>,
    /// TextMemoryRules.Select; none on a shared card), its request the grammar, the most tokens and the name to keep out
    /// (TextDecoding), its words the checker of the language (TextCheck.JudgeSix). Every line, asked or not, is counted
    /// by pack and intent with what was shown and why (<see cref="Tally"/>, TextShown).
    /// COST: a request is a card of a few hundred characters; a look is a pass over the waiting requests (a few).
    /// </summary>
    public sealed class TextsHub
    {
        public const int RecentMost = 4;         // things a speaker said that a new reply must not repeat
        public const int SpeakersMost = 256;     // speakers remembered for it: past this (a long session of street talk) they start again
        public const string NoPack = "no pack";  // the pack name lines are counted under while none runs

        private sealed class Waiting
        {
            public TextRequest Request;
            public TextIntent Intent;
            public TextSheet Sheet;
            public string Card;
            public TextJob Job;
            public string Lang;           // data v6: the card's language
            public string Untold;         // ...the player's name, when the speaker does not know it
            public bool Lowercase;        // ...the speaker writes in lower case
        }

        private readonly TextQueue _queue;
        private readonly TextBudget _budget = new TextBudget();
        private readonly List<Waiting> _waiting = new List<Waiting>(16);
        private readonly List<TextJob> _missed = new List<TextJob>(8);
        private readonly Dictionary<string, List<string>> _recent = new Dictionary<string, List<string>>(32, StringComparer.Ordinal);
        private readonly int[] _verdicts = new int[Enum.GetValues(typeof(TextVerdict)).Length];
        private TextShownWhy _whyNot;
        private TextVerdict _verdict;

        public readonly TextsStore Store = new TextsStore();

        /// <summary>Every line counted by pack and intent: the model's words or the written line, and why (TextShown).</summary>
        public readonly TextShown Tally = new TextShown();

        public TextsHub(TextQueue queue)
        {
            _queue = queue;
            _budget.Apply(_queue);
        }

        /// <summary>The runner in use; null while none is started.</summary>
        public ITextRunner Runner { get; set; }

        /// <summary>Which uses may ask now (the options, the game's language, Going Public's rule); set by the director.</summary>
        public Func<TextUse, bool, bool> Allowed;

        /// <summary>Why a use may not ask now, when <see cref="Allowed"/> says no (switched off, the language); null: Off.</summary>
        public Func<TextUse, bool, TextShownWhy> Barred;

        /// <summary>
        /// The data version of the running pack's model (TextPack.Data): an intent of a later data version is never asked of
        /// it (the written line). Set by the director when it starts a pack.
        /// </summary>
        public int Data { get; set; } = TextIntents.DataV2;

        /// <summary>The game's language as a card of data v6 writes it (TextLanguages.Of); set by the director.</summary>
        public string Language { get; set; } = TextLanguages.English;

        /// <summary>The languages the running pack was trained and passed in (TextPack.Languages); English alone by default.</summary>
        public string[] Languages { get; set; } = { TextLanguages.English };

        /// <summary>The running pack's name as lines are counted under it; <see cref="NoPack"/> while none runs.</summary>
        public string Pack { get; set; } = NoPack;

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

        /// <summary>True when the running pack writes the game's language now (a pack below data v6 writes English alone).</summary>
        public bool WritesLanguage => TextLanguages.Has(Languages, Data >= TextIntents.DataV6 ? Language : TextLanguages.English);

        /// <summary>True when a request of this use could go to the model now (asked before the card is built, so that nothing is built for nothing).</summary>
        public bool Wants(TextUse use, bool shared)
        {
            return Runner != null && Runner.State != TextServerState.Failed && Runner.State != TextServerState.Off && (Allowed == null || Allowed(use, shared));
        }

        /// <summary>
        /// A line the asker shows with its written words without asking (it asked <see cref="Wants"/> first and was told no):
        /// counted with why (no pack, off, the language, a pack that could not run).
        /// </summary>
        public void NotAsked(TextUse use, bool shared, string intent)
        {
            Tally.Count(Pack, intent, Unwanted(use, shared));
        }

        // Why Wants says no.
        private TextShownWhy Unwanted(TextUse use, bool shared)
        {
            if (Runner == null)
            {
                return Pack == NoPack ? TextShownWhy.NoPack : TextShownWhy.Failed;
            }
            if (Runner.State == TextServerState.Failed || Runner.State == TextServerState.Off)
            {
                return TextShownWhy.Failed;
            }
            return Barred != null ? Barred(use, shared) : TextShownWhy.Off;
        }

        /// <summary>
        /// Asks for words. True: they come through the request's Done, on a later look, words or null. False: not asked
        /// (switched off, no pack, no such intent or a value missing, too slow for it, no time): the asker uses the written
        /// line now, and Done is not called. Either way the line is counted (<see cref="Tally"/>).
        /// </summary>
        public bool Ask(TextRequest r, double now)
        {
            if (r == null || r.Done == null)
            {
                return false;
            }
            r.Took = 0;
            r.Lang = null;
            r.Why = WhyNot(r.Use, r.Shared, r.Intent, r.Branch, r.Priority, r.Guess, r.Seconds, out _whyNot);
            if (r.Why != null)
            {
                Tally.Count(Pack, r.Intent, _whyNot);
                return false;
            }
            TextIntent intent = TextIntents.Find(r.Intent, r.Branch, Data);   // (the card of the pack's own data: v4's for a data 4 pack, v5's for a data 5 one)
            var sheet = new TextSheet();
            if (!TextIntents.Build(intent, r.Who, r.Slots, r.History, sheet))
            {
                r.Why = "not asked: a value the card " + r.Intent + " needs is missing";
                Tally.Count(Pack, r.Intent, TextShownWhy.NoCard);
                return false;
            }
            bool six = intent.Data >= TextIntents.DataV6;
            var job = new TextJob { Key = r.Key ?? r.Intent, Priority = r.Priority, Deadline = now + r.Seconds, Guess = r.Guess };
            var w = new Waiting { Request = r, Intent = intent, Sheet = sheet, Job = job };
            if (six)
            {
                Six(r, intent, sheet, w);
            }
            job.Prompt = TextCard.ChatMl(sheet, six ? TextCard.SystemSix : TextCard.System);
            w.Card = TextCard.Card(sheet);
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                if (_waiting[i].Request.Key != null && _waiting[i].Request.Key == r.Key)
                {
                    End(i, null, false, TextShownWhy.Unwanted);   // (asked again: the older one gives way)
                }
            }
            if (!_queue.Add(job, now))
            {
                r.Why = "not asked: " + job.Why + " (a reply is expected to take " + _queue.Expect.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s, the line waits "
                        + r.Seconds.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " s)";
                Tally.Count(Pack, r.Intent, job.Why == "the queue was full" ? TextShownWhy.Busy : TextShownWhy.Slow);
                return false;
            }
            _waiting.Add(w);
            Runner.Nudge();
            return true;
        }

        // A card of data v6: its language, the speaker's memory (never on a shared card), and the request's grammar, length and the name kept out.
        private void Six(TextRequest r, TextIntent intent, TextSheet sheet, Waiting w)
        {
            string lang = TextLanguages.Known(Language) ? Language : TextLanguages.English;
            r.Lang = lang;
            w.Lang = lang;
            Finish(intent, sheet, lang, r.Memory, r.Day, r.Shared);
            w.Lowercase = TextDecoding.Lowercase(sheet.Style);
            w.Untold = intent.UntoldName && r.Slots.Get("known") != "yes" ? r.Who.Player : null;
            w.Job.Grammar = TextDecoding.Grammar(TextDecoding.GrammarName(lang, w.Lowercase));
            w.Job.Tokens = TextDecoding.Tokens(intent, lang);
            w.Job.BanName = w.Untold != null && TextDecoding.NameForms(w.Untold) != null ? w.Untold : null;
        }

        /// <summary>
        /// A built card of data v6 finished as it is sent: its lang line, and the memory lines of what its speaker remembers
        /// (none on a shared card). The contract's fixtures are finished by the same (EconomyCheck: --texts-spec).
        /// </summary>
        public static void Finish(TextIntent intent, TextSheet sheet, string lang, List<TextFact> memory, int today, bool shared)
        {
            sheet.Lang = lang;
            sheet.Memory.Clear();
            if (!shared && memory != null && memory.Count > 0)
            {
                TextMemoryRules.Select(memory, intent, today, TextIntents.Subject(intent.Key, intent.Branch), sheet.Memory);
            }
        }

        /// <summary>
        /// Why a request of this use, card and urgency would not be asked now, before anything is built: off, no pack or not
        /// running, a machine that writes only what is asked ahead, the model still loading for what is due soon, or a card
        /// the running pack's data version does not have. Null when it would be asked (the queue may still be full). Asked
        /// by <see cref="Ask"/>, and by a feature that offers something only while words can come (a call's composer).
        /// </summary>
        public string WhyNot(TextUse use, bool shared, string intent, string branch, TextPriority priority, bool guess, double seconds)
        {
            return WhyNot(use, shared, intent, branch, priority, guess, seconds, out _);
        }

        /// <summary>The same, with what the written line shown for it is counted as.</summary>
        public string WhyNot(TextUse use, bool shared, string intent, string branch, TextPriority priority, bool guess, double seconds, out TextShownWhy why)
        {
            if (!Wants(use, shared))
            {
                why = Unwanted(use, shared);
                return why == TextShownWhy.Language ? "not asked: the pack does not write the game's language (" + Language + ")"
                    : "not asked: RLO Texts is off, has no pack or is not running";
            }
            if (!TextSpeed.Allows(AheadOnly, priority, guess, seconds))
            {
                why = TextShownWhy.Slow;
                return "not asked: this machine's model writes only what is asked well ahead";
            }
            if (Runner.State == TextServerState.Loading && seconds < TextSpeed.AheadSeconds)
            {
                why = TextShownWhy.Loading;
                return "not asked: the model is still loading";   // (what is due soon takes its written line now rather than wait for nothing)
            }
            if (TextIntents.Find(intent, branch, Data) == null)
            {
                why = TextShownWhy.NoCard;
                return "not asked: the pack (data v" + Data + ") has no card " + intent + (branch != null ? " " + branch : string.Empty);   // (a v2 pack is never asked a v3 intent)
            }
            why = TextShownWhy.Model;
            return null;
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
                            End(i, null, false, TextShownWhy.Unwanted, true);   // (never taken: the player went another way; nothing was shown for it)
                            handed++;
                        }
                        continue;   // (its asker takes it, or its time runs out)
                    }
                    if (now > w.Job.Deadline + 0.25)
                    {
                        w.Request.Why = "the words came after the hold";
                        Over(i, true, TextShownWhy.Slow);
                    }
                    else
                    {
                        Hand(i);
                    }
                    handed++;
                }
                else if (state == TextJobState.Missed || state == TextJobState.Dropped)
                {
                    w.Request.Why = state == TextJobState.Missed ? "missed: " + (w.Job.Why ?? "the model gave nothing") : "asked again or dropped";
                    Over(i, state == TextJobState.Missed && BudgetMiss(w.Job), state == TextJobState.Dropped ? TextShownWhy.Unwanted : Missed(w.Job));
                    handed++;
                }
                else if (now > w.Job.Deadline)
                {
                    w.Request.Why = "the hold of " + w.Request.Seconds.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " s ran out while it was "
                                    + (state == TextJobState.Running ? "being written" : "waiting in the queue");
                    _queue.Drop(w.Job);   // (a running one comes back later and is let go)
                    Over(i, BudgetMiss(w.Job), TextShownWhy.Slow);
                    handed++;
                }
            }
            return handed;
        }

        // A missed request's reason: the model's own failure (it gave nothing, an error), the queue full, or the time.
        private static TextShownWhy Missed(TextJob job)
        {
            string why = job.Why ?? string.Empty;
            if (why == "the queue was full")
            {
                return TextShownWhy.Busy;
            }
            return why.StartsWith("its time", StringComparison.Ordinal) || why.StartsWith("it took", StringComparison.Ordinal) || why == "no time"
                ? TextShownWhy.Slow : TextShownWhy.Failed;
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
                    Count(w, words, TextShownWhy.Refused);
                    return words;
                }
                w.Request.Why = "the guess was not written yet";
                _queue.Drop(w.Job);
                _waiting.RemoveAt(i);
                Count(w, null, TextShownWhy.Slow);
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
                    End(i, null, false, TextShownWhy.Busy);
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
                End(i, null, false, TextShownWhy.Off);
            }
            _queue.Clear();
        }

        /// <summary>How many speakers' recent words are remembered, at most <see cref="SpeakersMost"/>.</summary>
        public int Speakers => _recent.Count;

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
                if (_recent.Count >= SpeakersMost)
                {
                    _recent.Clear();   // the hub lasts the whole run, and every person talked to is a speaker of their own
                }
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
            End(i, words, true, TextShownWhy.Refused);
        }

        /// <summary>
        /// Whether a miss tells the budget the model is too busy (TextBudget.Ended): one the worker ran (late, or failed), or
        /// one of the kinds the budget lets through that still waited past its time. Never a background request the queue
        /// held back itself (the game not quiet, the background's share of the minute used, or below the budget's floor):
        /// counted, those kept the budget at its lowest steps for good, since at step 2 the background is held, its requests
        /// run out, and every one counted as another miss (Flare's comments were then never written).
        /// </summary>
        public bool BudgetMiss(TextJob job)
        {
            return job.Ran || (job.Priority != TextPriority.Background && job.Priority <= _queue.Floor);
        }

        private void Over(int i, bool missed, TextShownWhy why)
        {
            if (missed)
            {
                _budget.Ended(true);
                _budget.Apply(_queue);
            }
            End(i, null, false, why);
        }

        // The words checked: null if they fail (counted by verdict; _verdict says which).
        private string Checked(Waiting w)
        {
            string words = TextCheck.Tidy(w.Job.Raw, w.Lang);
            TextVerdict verdict = w.Lang != null
                ? TextCheck.JudgeSix(words, w.Intent, w.Sheet, w.Card, Recent(w.Request.Speaker), w.Lang, w.Job.Cut, w.Untold, w.Lowercase)
                : TextCheck.Judge(words, w.Intent, w.Sheet, w.Card, Recent(w.Request.Speaker));
            _verdicts[(int)verdict]++;
            _verdict = verdict;
            w.Request.Took = w.Job.Took;
            if (verdict != TextVerdict.Ok)
            {
                Refused++;
                w.Request.Why = "the checker refused \"" + words + "\" (" + verdict + ")";
                return null;
            }
            return words;
        }

        // A request handed over: the words or null, counted (a null after the checker's verdict as Refused with its rule,
        // else as the reason given), kept and remembered when there are words. Done is called last.
        private void End(int i, string words, bool counted, TextShownWhy why, bool unseen = false)
        {
            Waiting w = _waiting[i];
            _waiting.RemoveAt(i);
            if (words != null && w.Request.Wanted != null && !w.Request.Wanted())
            {
                words = null;   // (too late for where it was going: the written line stays)
                counted = true;
                why = TextShownWhy.Unwanted;
                w.Request.Why = "no longer wanted when the words came";
            }
            w.Request.Took = w.Job.Took;
            if (unseen)
            {
                Fallbacks++;   // (the session's figure as ever; the tally counts only lines shown)
            }
            else if (counted || words == null)
            {
                Count(w, words, why);
            }
            TextRequest r = w.Request;
            if (words != null)
            {
                Said(r.Speaker, words);
                if ((r.Keep || r.Shared) && r.Key != null)
                {
                    TextKept k = Store.Keep(r.Key, r.Day, w.Intent.Key, words, r.Shared, false, w.Lang);
                    if (r.Shared)
                    {
                        SharedKept?.Invoke(k);
                    }
                }
            }
            r.Done(words);
        }

        // One line handed over, counted: the session's figures and the tally (a refusal under the checker's rule).
        private void Count(Waiting w, string words, TextShownWhy why)
        {
            if (words != null)
            {
                Shown++;
                Tally.Count(Pack, w.Request.Intent, TextShownWhy.Model);
                return;
            }
            Fallbacks++;
            bool refused = why == TextShownWhy.Refused && _verdict != TextVerdict.Ok;
            Tally.Count(Pack, w.Request.Intent, refused ? TextShownWhy.Refused : why == TextShownWhy.Refused ? TextShownWhy.Slow : why, refused ? _verdict : TextVerdict.Ok);
            _verdict = TextVerdict.Ok;
        }
    }
}
