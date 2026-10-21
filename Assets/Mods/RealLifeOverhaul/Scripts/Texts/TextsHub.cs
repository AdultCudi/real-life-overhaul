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
        public bool ServiceOnly;            // sent to the player's own AI service only, never the pack (the options' test of the service)
        public bool ByService;              // filled by the hub: the words came from the player's own AI service
        public bool Spoken;                 // said face to face while the player waits (street talk, a call): with the AI service up it may wait for it (TextsHub.SpokenWait)
        public double Waits;                // filled by the hub: real seconds from the ask it waits at most (Seconds, or the AI service's spoken wait)
        public TextSource Source;           // filled by the hub: where the words came from (the written line when there are none)
        public string ServiceMiss;          // filled by the hub, with an AI service chosen: why it did not word it (not asked, late, failed, refused); null when it did
        public Action Started;              // on the game's thread, at most once: the AI service has begun to answer it (its first words came; nothing is shown until all are in and checked)
        public string Person;               // data v6: whose memory the card carries (TextMemoryRules.KeyOk); null: none
        public readonly List<TextFact> Memory = new List<TextFact>(8);   // data v6: what that person remembers, filled by the asker (TextsDirector.Recall); the hub picks the card's lines
        public string Lang;                 // filled by the hub: the language the card asks for (data v6), or null below
    }

    /// <summary>Where a request's words came from, for the asker's log line.</summary>
    public enum TextSource
    {
        Written = 0,   // none came: the written line
        Pack = 1,      // the RLO Texts pack on this PC
        Service = 2    // the player's own AI service
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
    ///
    /// YOUR OWN AI: with an AI service of the player's running (<see cref="Service"/>), a request goes to it first, through
    /// a queue of its own, with the newest cards (a card of data v6 finished in the game's language, memory and all, sent
    /// with the packs' system turn and the language named as its natives write it, TextCard.ServiceSystem); its words are
    /// checked by the same TextCheck in that language (JudgeSix: the language itself, the names, the facts and the rest),
    /// and when they fail, come too late for the service's queue, or the service fails or rests, the request falls back to
    /// the pack (if one runs, has the card and writes the language, with the time left, the pack's own card) and then to
    /// the written line. The pack and the service each judge the language on their own (<see cref="WritesLanguage"/>,
    /// <see cref="ServiceWrites"/>); Dutch and Turkish, which TextCheck cannot judge, keep their written lines.
    /// A shared request (Going Public's shared feed and paper, which must agree on every machine) never goes to the
    /// service: the pack and the written lines only.
    /// A SPOKEN LINE (face to face, the player waiting: <see cref="TextRequest.Spoken"/>) is held about a second for the
    /// pack, less than a service takes to answer. With the service up such a line waits for it instead, a short pause
    /// before the person answers, as people do (<see cref="SpokenWait"/>: the service's measured time and a margin, at
    /// most <see cref="SpokenMost"/>), and the end of that wait is kept for the pack, so a service that is still late
    /// hands the line to the pack and then to the written line, as before. A guess the service is still writing when its
    /// line is needed is waited for as that line (<see cref="Adopt"/>), never asked again. When the service's first words
    /// come (a streamed reply), the asker is told once (<see cref="TextRequest.Started"/>: the person may be shown about to
    /// speak); the words are handed over only when they are whole and checked. Without a service nothing of this applies:
    /// a spoken line is held as long as it always was.
    /// COST: a request is a card of a few hundred characters; a look is a pass over the waiting requests (a few).
    /// </summary>
    public sealed class TextsHub
    {
        public const int RecentMost = 4;         // things a speaker said that a new reply must not repeat
        public const int SpeakersMost = 256;     // speakers remembered for it: past this (a long session of street talk) they start again
        public const double SpokenMargin = 0.6;  // real seconds a spoken line waits for the AI service past its measured time (its replies vary by about that)
        public const double SpokenMost = 3.5;    // real seconds a spoken line waits at most: past this a pause before an answer is no longer natural
        public const double PackSlack = 0.2;     // real seconds kept for the pack past its own measured time, at the end of a spoken wait (two looks of the director)
        public const string NoPack = "no pack";  // the pack name lines are counted under while none runs
        public const string ServiceTally = "your own AI";   // the name the AI service's own words are counted under (a written line is the pack's)

        private sealed class Waiting
        {
            public TextRequest Request;
            public TextIntent Intent;
            public TextSheet Sheet;
            public string Card;
            public TextJob Job;
            public bool ViaService;           // the job is the AI service's (its queue); false: the pack's
            public double Until;              // the request's last moment: the job's deadline, or later by the time kept for the pack (a spoken line to the service)
            public bool Told;                 // its asker was told the service began to answer (TextRequest.Started)
            public string Lang;               // data v6: the card's language
            public string Untold;             // ...the player's name, when the speaker does not know it
            public bool Lowercase;            // ...the speaker writes in lower case
        }

        private static int _newest;

        private readonly TextQueue _queue;
        private readonly TextQueue _serviceQueue;
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

        public TextsHub(TextQueue queue, TextQueue serviceQueue = null)
        {
            _queue = queue;
            _serviceQueue = serviceQueue;
            _budget.Apply(_queue);
        }

        /// <summary>The runner in use; null while none is started.</summary>
        public ITextRunner Runner { get; set; }

        /// <summary>The player's own AI service's runner (Your own AI); null while it is off. Needs the service queue given to the hub.</summary>
        public ITextServiceRunner Service { get; set; }

        /// <summary>The newest data version of the cards (the AI service is given the newest card of each intent).</summary>
        public static int Newest
        {
            get
            {
                if (_newest == 0)
                {
                    int newest = TextIntents.DataV2;
                    for (int i = 0; i < TextIntents.All.Count; i++)
                    {
                        newest = Math.Max(newest, TextIntents.All[i].Data);
                    }
                    _newest = newest;
                }
                return _newest;
            }
        }

        /// <summary>True while the AI service takes requests: ready, and not resting after a fault.</summary>
        public bool ServiceUp => Service != null && _serviceQueue != null && Service.State == TextServerState.Ready && !Service.Resting;

        private bool PackUp => Runner != null && Runner.State != TextServerState.Failed && Runner.State != TextServerState.Off;

        /// <summary>
        /// Which uses may ask now (the options, Going Public's rule); set by the director. The language is the hub's own
        /// (<see cref="WritesLanguage"/>, <see cref="ServiceWrites"/>).
        /// </summary>
        public Func<TextUse, bool, bool> Allowed;

        /// <summary>
        /// The data version of the running pack's model (TextPack.Data): an intent of a later data version is never asked of
        /// it (the written line). Set by the director when it starts a pack.
        /// </summary>
        public int Data
        {
            get => ServiceUp ? Math.Max(_packData, Newest) : _packData;   // (with the service up, the newest cards: the pack's own are used when it falls back)
            set => _packData = value;
        }

        private int _packData = TextIntents.DataV2;

        /// <summary>The running pack's own data version, whatever the service.</summary>
        public int PackData => _packData;

        /// <summary>The game's language as a card of data v6 writes it (TextLanguages.Of); set by the director.</summary>
        public string Language { get; set; } = TextLanguages.English;

        /// <summary>True while the game shows English (TextsGame.English); set by the director with <see cref="Language"/>. A pack below data v6 writes English alone.</summary>
        public bool English { get; set; } = true;

        /// <summary>The languages the running pack was trained and passed in (TextPack.Languages); English alone by default.</summary>
        public string[] Languages { get; set; } = { TextLanguages.English };

        /// <summary>The running pack's name as lines are counted under it; <see cref="NoPack"/> while none runs.</summary>
        public string Pack { get; set; } = NoPack;

        /// <summary>Seconds a reply is expected to take on the route a request goes by now: the service's while it is up, else the pack's.</summary>
        public double Expect => ServiceUp ? _serviceQueue.Expect : _queue.Expect;

        /// <summary>This machine writes only what is asked ahead (TextSpeed.AheadOnly).</summary>
        public bool AheadOnly { get; set; }

        /// <summary>Words were kept for a shared item (the host): the session's part has a new line.</summary>
        public Action<TextKept> SharedKept;

        public TextQueue Queue => _queue;
        public TextQueue ServiceQueue => _serviceQueue;
        public TextBudget Budget => _budget;
        public int Pending => _waiting.Count;

        /// <summary>This session: words shown, written lines used where the model was asked, and of those how many the checker refused.</summary>
        public int Shown { get; private set; }
        public int Fallbacks { get; private set; }
        public int Refused { get; private set; }

        /// <summary>This session: words the AI service wrote that were shown, and its requests that fell back to the pack or the written line.</summary>
        public int ServiceShown { get; private set; }
        public int ServiceFell { get; private set; }

        public int Verdicts(TextVerdict v) => _verdicts[(int)v];

        /// <summary>
        /// True when the running pack writes the game's language now: a pack of data v6 the languages it was trained and
        /// passed in (Dutch and Turkish never: TextLanguages.Unwritten), an older pack English alone (<see cref="English"/>).
        /// </summary>
        public bool WritesLanguage => _packData >= TextIntents.DataV6 ? TextLanguages.Has(Languages, Language) : English;

        /// <summary>
        /// True when the AI service is asked in the game's language: one of the seven, which TextCheck judges in full. Dutch
        /// and Turkish are not: TextCheck has no little words of theirs (TextCheck.InLanguage lets any line in Latin letters
        /// pass as theirs, an English one too), no machine talk of theirs (MachineIn) and Flare's list no swearing of theirs,
        /// so a reply in them could not be checked and their written lines are kept. A language the mod has no texts in
        /// reads English (TextLanguages.Of), and the service writes English, as the packs do.
        /// </summary>
        public bool ServiceWrites => TextLanguages.Known(Language);

        /// <summary>True when a request of this use could go to the model now (asked before the card is built, so that nothing is built for nothing).</summary>
        public bool Wants(TextUse use, bool shared)
        {
            return ((PackUp && WritesLanguage) || (ServiceUp && !shared && ServiceWrites)) && (Allowed == null || Allowed(use, shared));
        }

        /// <summary>
        /// A line the asker shows with its written words without asking (it asked <see cref="Wants"/> first and was told no):
        /// counted with why (no pack, off, the language, a pack that could not run).
        /// </summary>
        public void NotAsked(TextUse use, bool shared, string intent)
        {
            Tally.Count(Pack, intent, Unwanted(use, shared));
        }

        // Why Wants says no: no way to word it (no pack, or one that could not run, and no AI service for it), else off, else
        // the language (neither the pack nor the service writes it).
        private TextShownWhy Unwanted(TextUse use, bool shared)
        {
            if (!PackUp && !(ServiceUp && !shared))
            {
                return Runner == null && Pack == NoPack ? TextShownWhy.NoPack : TextShownWhy.Failed;
            }
            return Allowed != null && !Allowed(use, shared) ? TextShownWhy.Off : TextShownWhy.Language;
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
            r.Why = null;
            r.Lang = null;
            r.ByService = false;
            r.Source = TextSource.Written;
            r.ServiceMiss = null;
            r.Waits = r.Seconds;
            string service = ServiceWhy(r.Use, r.Shared, r.Intent, r.Branch);
            if (service == null && ToService(r, now))
            {
                return true;
            }
            if (r.ServiceOnly)
            {
                r.Why = "not asked: " + (service ?? r.Why ?? "the service's queue had no time for it");
                return false;
            }
            string toService = service ?? r.Why;
            r.ServiceMiss = Service != null ? toService : null;
            r.Lang = null;   // (a card made for the service that could not go: the pack's own is finished below)
            r.Why = PackWhy(r.Use, r.Shared, r.Intent, r.Branch, r.Priority, r.Guess, r.Seconds, out _whyNot);
            if (r.Why != null)
            {
                if (Service != null && toService != null)
                {
                    r.Why += "; " + toService;
                }
                Tally.Count(Pack, r.Intent, _whyNot);
                return false;
            }
            TextIntent intent = TextIntents.Find(r.Intent, r.Branch, _packData);   // (the card of the pack's own data: v4's for a data 4 pack, v5's for a data 5 one)
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
                Six(r, intent, sheet, w, PackLanguage);
            }
            job.Prompt = TextCard.ChatMl(sheet, six ? TextCard.SystemSix : TextCard.System);
            w.Card = TextCard.Card(sheet);
            GiveWay(r.Key);
            if (!_queue.Add(job, now))
            {
                r.Why = "not asked: " + job.Why + " (a reply is expected to take " + _queue.Expect.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s, the line waits "
                        + r.Seconds.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " s)";
                Tally.Count(Pack, r.Intent, job.Why == "the queue was full" ? TextShownWhy.Busy : TextShownWhy.Slow);
                return false;
            }
            w.Until = job.Deadline;
            _waiting.Add(w);
            Runner.Nudge();
            return true;
        }

        // The request to the AI service, with the newest card: false, with why, when it cannot go (a value missing, no time in
        // its queue). A card of data v6 is finished in the game's language (one of the seven: ServiceWhy) and sent with the
        // system turn of that language (TextCard.ServiceSystem). A spoken line waits for the service (SpokenWait); the end
        // of the wait is kept for the pack.
        private bool ToService(TextRequest r, double now)
        {
            TextIntent intent = TextIntents.Find(r.Intent, r.Branch, Newest);
            var sheet = new TextSheet();
            if (!TextIntents.Build(intent, r.Who, r.Slots, r.History, sheet))
            {
                r.Why = "a value the card " + r.Intent + " needs is missing";
                return false;
            }
            double reserve = Spoken(r) ? PackReserve(r) : 0.0;
            double waits = Spoken(r) ? SpokenWait(r.Seconds, _serviceQueue.Expect, reserve) : r.Seconds;
            var job = new TextJob { Key = r.Key ?? r.Intent, Priority = r.Priority, Deadline = now + waits - reserve, Guess = r.Guess };
            var w = new Waiting { Request = r, Intent = intent, Sheet = sheet, Job = job, ViaService = true, Until = now + waits };
            if (intent.Data >= TextIntents.DataV6)
            {
                Six(r, intent, sheet, w, Language);
            }
            job.Card = w.Card = TextCard.Card(sheet);
            job.System = TextCard.ServiceSystem(intent.Data, Language);
            GiveWay(r.Key);
            if (!_serviceQueue.Add(job, now))
            {
                r.Why = "the service's queue: " + job.Why + " (a reply is expected to take " + _serviceQueue.Expect.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s"
                        + (Spoken(r) ? ", more than a spoken line waits, " + SpokenMost.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s at most" : string.Empty) + ")";
                return false;
            }
            r.Waits = waits;
            _waiting.Add(w);
            Service.Nudge();
            return true;
        }

        // The language a pack's card of data v6 asks for: the game's when it is one of the seven, else English.
        private string PackLanguage => TextLanguages.Known(Language) ? Language : TextLanguages.English;

        // A card of data v6: its language, the speaker's memory (never on a shared card), and the request's grammar, length and the name kept out.
        private void Six(TextRequest r, TextIntent intent, TextSheet sheet, Waiting w, string lang)
        {
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

        // A line the player waits for, face to face: never a guess (it waits for its click), never the options' test.
        private static bool Spoken(TextRequest r) => r.Spoken && !r.Guess && !r.ServiceOnly;

        /// <summary>
        /// How long a spoken line waits with the AI service up: the service's measured time, a margin and the time kept
        /// for the pack, at most <see cref="SpokenMost"/>, and never less than the line's own hold. Game-free and pure.
        /// </summary>
        public static double SpokenWait(double hold, double serviceExpect, double packReserve)
        {
            return Math.Max(hold, Math.Min(SpokenMost, serviceExpect + SpokenMargin + packReserve));
        }

        // The end of a spoken wait kept for the pack, when it would take the line: its measured time and a slack, within the
        // line's own hold (what the pack has always been given). None without a pack that would.
        private double PackReserve(TextRequest r)
        {
            if (PackWhy(r.Use, r.Shared, r.Intent, r.Branch, r.Priority, r.Guess, r.Seconds, out _) != null)
            {
                return 0.0;
            }
            return Math.Min(r.Seconds, _queue.Expect + PackSlack);
        }

        /// <summary>
        /// True while a guess under this key is the AI service's and its words have not come yet (waiting in its queue or
        /// being written): its asker may wait for it as the line itself (<see cref="Adopt"/>) rather than take it now.
        /// </summary>
        public bool Writing(string key)
        {
            for (int i = _waiting.Count - 1; key != null && i >= 0; i--)
            {
                Waiting w = _waiting[i];
                if (w.Request.Guess && w.Request.Key == key)
                {
                    TextJobState state = w.Job.State;
                    return w.ViaService && (state == TextJobState.Waiting || state == TextJobState.Running);
                }
            }
            return false;
        }

        /// <summary>
        /// The player clicked while the AI service was still writing the guess under <paramref name="key"/>: the guess
        /// becomes request <paramref name="r"/> (the same card: intent and branch), waited for as a spoken line from now
        /// (<see cref="SpokenWait"/> on the time the service is still expected to take), never asked again; its words or
        /// the written line come through r's Done. False when there is no such guess, it guessed another card, or the
        /// service could not finish it within the wait: the guess is let go, and the asker asks as usual.
        /// </summary>
        public bool Adopt(string key, TextRequest r, double now)
        {
            if (key == null || r == null || r.Done == null)
            {
                return false;
            }
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                Waiting w = _waiting[i];
                if (!w.Request.Guess || w.Request.Key != key)
                {
                    continue;
                }
                TextJobState state = w.Job.State;
                bool open = state == TextJobState.Waiting || state == TextJobState.Running || state == TextJobState.Written;
                double reserve = PackReserve(r);
                double left = Math.Max(0.0, w.Job.Asked + _serviceQueue.Expect - now);   // (what the service is still expected to take)
                double waits = SpokenWait(r.Seconds, left, reserve);
                bool same = w.ViaService && open && ServiceUp && Spoken(r) && !r.Shared && w.Request.Intent == r.Intent && w.Request.Branch == r.Branch;
                if (!same || (state != TextJobState.Written && waits - reserve < left))
                {
                    QueueOf(w).Drop(w.Job);
                    End(i, null, false, TextShownWhy.Unwanted, true);   // (the guess's own Done: it was never shown)
                    return false;
                }
                r.Took = 0;
                r.Why = null;
                r.ByService = false;
                r.Source = TextSource.Written;
                r.ServiceMiss = null;
                r.Waits = waits;
                r.Lang = w.Request.Lang;   // (the guess's card is the line's)
                _serviceQueue.Urge(w.Job, now + waits - reserve);
                w.Request = r;
                w.Until = now + waits;
                w.Told = false;   // (its first words may have come already: the next look tells the line's asker)
                Service.Nudge();
                return true;
            }
            return false;
        }

        // Asked again under the same key: the older request gives way (the written line), its job taken out of its queue.
        private void GiveWay(string key)
        {
            for (int i = _waiting.Count - 1; key != null && i >= 0; i--)
            {
                if (_waiting[i].Request.Key != null && _waiting[i].Request.Key == key)
                {
                    QueueOf(_waiting[i]).Drop(_waiting[i].Job);
                    End(i, null, false, TextShownWhy.Unwanted);
                }
            }
        }

        private TextQueue QueueOf(Waiting w) => w.ViaService ? _serviceQueue : _queue;

        /// <summary>
        /// Why a request would not go to the AI service now (null when it would): none chosen, a shared text (Going
        /// Public), off for this kind of message, the game in a language whose replies TextCheck cannot judge (Dutch,
        /// Turkish: <see cref="ServiceWrites"/>), the service not ready or resting, or no card of the intent in the game's
        /// language (a card below data v6 has no lang line: English only).
        /// </summary>
        public string ServiceWhy(TextUse use, bool shared, string intent, string branch)
        {
            if (Service == null || _serviceQueue == null)
            {
                return "no AI service is chosen";
            }
            if (shared)
            {
                return "shared text never goes to your own AI service";
            }
            if (Allowed != null && !Allowed(use, false))
            {
                return "RLO Texts is off for this kind of message";
            }
            if (!ServiceWrites)
            {
                return "your own AI is not asked in " + Language + ": the mod cannot check a reply in it, so its written lines are kept";
            }
            if (!ServiceUp)
            {
                return "your own AI service is " + (Service.State == TextServerState.Failed ? "stopped" : Service.Resting ? "resting after a fault" : "not ready yet");
            }
            TextIntent card = TextIntents.Find(intent, branch, Newest);
            if (card == null)
            {
                return "no card " + intent;
            }
            return card.Data < TextIntents.DataV6 && Language != TextLanguages.English ? "the card " + intent + " is English only, and the game is in " + Language : null;
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
            string service = ServiceWhy(use, shared, intent, branch);
            if (service == null && seconds >= _serviceQueue.Expect)
            {
                why = TextShownWhy.Model;
                return null;
            }
            string pack = PackWhy(use, shared, intent, branch, priority, guess, seconds, out why);
            return pack != null && Service != null ? pack + "; " + (service ?? "your own AI service is expected to take longer than that") : pack;
        }

        // The pack's own reasons, as before the AI service, with what the written line shown for it is counted as.
        private string PackWhy(TextUse use, bool shared, string intent, string branch, TextPriority priority, bool guess, double seconds, out TextShownWhy why)
        {
            bool off = Allowed != null && !Allowed(use, shared);
            if (!PackUp || off || !WritesLanguage)
            {
                why = !PackUp ? (Runner == null && Pack == NoPack ? TextShownWhy.NoPack : TextShownWhy.Failed) : off ? TextShownWhy.Off : TextShownWhy.Language;
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
            if (TextIntents.Find(intent, branch, _packData) == null)
            {
                why = TextShownWhy.NoCard;
                return "not asked: the pack (data v" + _packData + ") has no card " + intent + (branch != null ? " " + branch : string.Empty);   // (a v2 pack is never asked a v3 intent)
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
            _serviceQueue?.TakeMissed(_missed);
            int handed = 0;
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                Waiting w = _waiting[i];
                TextJobState state = w.Job.State;
                if (w.ViaService && !w.Told && w.Job.Speaking && !w.Request.Guess && state == TextJobState.Running)
                {
                    w.Told = true;
                    w.Request.Started?.Invoke();   // (the service began to answer: its asker may show the person about to speak; the words wait until they are whole)
                }
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
                        w.Request.Why = (w.ViaService ? "the service's " : string.Empty) + "words came after the hold";
                        if (w.ViaService && Fallback(i, now))
                        {
                            continue;   // (a spoken line: the time kept for the pack)
                        }
                        Over(i, !w.ViaService, TextShownWhy.Slow);
                    }
                    else
                    {
                        string words = Checked(w);
                        if (words == null && Fallback(i, now))
                        {
                            continue;   // (the service's words failed the checks: the pack tries in the time left)
                        }
                        Hand(i, words);
                    }
                    handed++;
                }
                else if (state == TextJobState.Missed || state == TextJobState.Dropped)
                {
                    w.Request.Why = (w.ViaService ? "the service " : string.Empty) + (state == TextJobState.Missed ? "missed: " + (w.Job.Why ?? "the model gave nothing") : "asked again or dropped");
                    if (w.ViaService && Fallback(i, now))
                    {
                        continue;   // (the service failed or rests: the pack tries in the time left)
                    }
                    Over(i, !w.ViaService && state == TextJobState.Missed && BudgetMiss(w.Job), state == TextJobState.Dropped ? TextShownWhy.Unwanted : Missed(w.Job));
                    handed++;
                }
                else if (now > w.Job.Deadline)
                {
                    double held = w.ViaService ? w.Job.Deadline - w.Job.Asked : w.Request.Seconds;
                    w.Request.Why = "the hold of " + held.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " s ran out while it was "
                                    + (state == TextJobState.Running ? "being written" : "waiting in the queue") + (w.ViaService ? " of the service" : string.Empty);
                    QueueOf(w).Drop(w.Job);   // (a running one comes back later and is let go)
                    if (w.ViaService && !w.Request.Guess && Fallback(i, now))
                    {
                        continue;   // (a spoken line the service was late with: the pack in the time kept for it)
                    }
                    Over(i, !w.ViaService && BudgetMiss(w.Job), TextShownWhy.Slow);
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
        public string TakeGuess(string key) => TakeGuess(key, out _);

        /// <summary>As <see cref="TakeGuess(string)"/>, and where its words came from (the pack or the AI service), for the asker's log line.</summary>
        public string TakeGuess(string key, out TextSource from)
        {
            from = TextSource.Written;
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
                    if (!w.ViaService)
                    {
                        _budget.Ended(false);
                        _budget.Apply(_queue);
                    }
                    w.Request.ByService = words != null && w.ViaService;
                    w.Request.Source = from = SourceOf(words, w);
                    Count(w, words, TextShownWhy.Refused);
                    return words;
                }
                w.Request.Why = "the guess was not written yet";
                QueueOf(w).Drop(w.Job);
                _waiting.RemoveAt(i);
                Count(w, null, TextShownWhy.Slow);
                return null;
            }
            return null;
        }

        private static TextSource SourceOf(string words, Waiting w) => words == null ? TextSource.Written : w.ViaService ? TextSource.Service : TextSource.Pack;

        /// <summary>
        /// Where words came from, as a log line names it: "your own AI (ChatGPT)", "the pack" or "the written line". Every
        /// log line that says where a reply came from uses it, so the AI service, the pack and the written line are told apart.
        /// </summary>
        public string From(TextSource source)
        {
            switch (source)
            {
                case TextSource.Service:
                    string name = Service is TextService runner && runner.Provider != null ? runner.Provider.Name : null;
                    return "your own AI" + (name != null ? " (" + name + ")" : string.Empty);
                case TextSource.Pack:
                    return "the pack";
                default:
                    return "the written line";
            }
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
                    QueueOf(_waiting[i]).Drop(_waiting[i].Job);
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
                QueueOf(_waiting[i]).Drop(_waiting[i].Job);
                End(i, null, false, TextShownWhy.Off);
            }
            _queue.Clear();
            _serviceQueue?.Clear();
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

        private void Hand(int i, string words)
        {
            if (!_waiting[i].ViaService)
            {
                _budget.Ended(false);
                _budget.Apply(_queue);
            }
            End(i, words, true, TextShownWhy.Refused);
        }

        // A request the AI service did not word (it failed, rested, or its words were refused) asked of the pack instead, in
        // the time left to its deadline, with the pack's own card (of data v6: in the game's language, with its grammar).
        // False when the pack cannot take it (none, off, too slow, or it does not write the game's language): the written line.
        private bool Fallback(int i, double now)
        {
            Waiting w = _waiting[i];
            if (!w.ViaService || w.Request.ServiceOnly)
            {
                return false;
            }
            TextRequest r = w.Request;
            string why = PackWhy(r.Use, r.Shared, r.Intent, r.Branch, r.Priority, r.Guess, w.Until - now, out _);
            TextIntent intent = why == null ? TextIntents.Find(r.Intent, r.Branch, _packData) : null;
            var sheet = new TextSheet();
            if (intent == null || !TextIntents.Build(intent, r.Who, r.Slots, r.History, sheet))
            {
                return false;
            }
            bool six = intent.Data >= TextIntents.DataV6;
            var job = new TextJob { Key = r.Key ?? r.Intent, Priority = r.Priority, Deadline = w.Until, Guess = r.Guess };
            var pack = new Waiting { Request = r, Intent = intent, Sheet = sheet, Job = job };
            string lang = r.Lang;
            if (six)
            {
                Six(r, intent, sheet, pack, PackLanguage);
            }
            job.Prompt = TextCard.ChatMl(sheet, six ? TextCard.SystemSix : TextCard.System);
            if (!_queue.Add(job, now))
            {
                r.Lang = lang;   // (still the service's card: its written line follows)
                return false;
            }
            ServiceFell++;
            r.ServiceMiss = r.Why;   // (why the service's words were not used: the pack's words come with it, for the log)
            r.Why = null;
            r.Lang = pack.Lang;
            w.Job = job;
            w.Intent = intent;
            w.Sheet = sheet;
            w.Card = TextCard.Card(sheet);
            w.Lang = pack.Lang;
            w.Untold = pack.Untold;
            w.Lowercase = pack.Lowercase;
            w.ViaService = false;
            Runner.Nudge();
            return true;
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
        // else as the reason given; a guess never shown only in the session's figures), kept and remembered when there are
        // words. Done is called last.
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
            w.Request.ByService = words != null && w.ViaService;
            w.Request.Source = SourceOf(words, w);
            if (unseen)
            {
                Fallbacks++;   // (the session's figures as ever; the tally counts only lines shown)
                ServiceFell += w.ViaService && !w.Request.ServiceOnly ? 1 : 0;
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

        // One line handed over, counted: the session's figures and the tally (the model's words under the way that wrote
        // them, a written line under the pack; a refusal under the checker's rule).
        private void Count(Waiting w, string words, TextShownWhy why)
        {
            if (words != null)
            {
                Shown++;
                ServiceShown += w.ViaService ? 1 : 0;
                Tally.Count(w.ViaService ? ServiceTally : Pack, w.Request.Intent, TextShownWhy.Model);
                return;
            }
            Fallbacks++;
            ServiceFell += w.ViaService && !w.Request.ServiceOnly ? 1 : 0;   // (a service request that ended in the written line without reaching the pack)
            bool refused = why == TextShownWhy.Refused && _verdict != TextVerdict.Ok;
            Tally.Count(Pack, w.Request.Intent, refused ? TextShownWhy.Refused : why == TextShownWhy.Refused ? TextShownWhy.Slow : why, refused ? _verdict : TextVerdict.Ok);
            _verdict = TextVerdict.Ok;
        }
    }
}
