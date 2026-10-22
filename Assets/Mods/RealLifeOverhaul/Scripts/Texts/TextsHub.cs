using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The heart of RLO Texts on the game's thread: a request becomes a card, a prompt and a job in a queue; at each
    /// <see cref="Look"/> what came back is checked and handed to its asker, and what missed its deadline is handed over as
    /// null, the written line. With the player's own AI service up a request goes to it first, with the newest card, and
    /// falls back to the pack in the time left, then to the written line; shared text never goes to the service. A spoken
    /// line with the service up waits for it a little longer, the end of the wait kept for the pack. Every line, asked or
    /// not, is counted in <see cref="Tally"/>. Game-free.
    /// COST: a request is a card of a few hundred characters; a look is a pass over the few waiting requests.
    /// </summary>
    public sealed partial class TextsHub
    {
        public const int RecentMost = 4;         // things a speaker said that a new reply must not repeat
        public const int SpeakersMost = 256;     // speakers remembered for it: past this they start again
        public const double SpokenMargin = 0.6;  // real seconds a spoken line waits for the AI service past its measured time, which varies by about that
        public const double SpokenMost = 3.5;    // real seconds a spoken line waits at most: past this a pause before an answer is no longer natural
        public const double PackSlack = 0.2;     // real seconds kept for the pack past its own measured time at the end of a spoken wait: two looks
        public const string NoPack = "no pack";  // the pack name lines are counted under while none runs
        public const string ServiceTally = "your own AI";   // the name the AI service's own words are counted under

        private sealed class Waiting
        {
            public TextRequest Request;
            public TextIntent Intent;
            public TextSheet Sheet;
            public string Card;
            public TextJob Job;
            public bool ViaService;           // the job is in the AI service's queue; false: the pack's
            public double Until;              // the request's last moment: the job's deadline, or later by the time kept for the pack
            public bool Told;                 // its asker was told the service began to answer
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

        /// <summary>Every line counted by pack and intent: the model's words or the written line, and why.</summary>
        public readonly TextShown Tally = new TextShown();

        public TextsHub(TextQueue queue, TextQueue serviceQueue = null)
        {
            _queue = queue;
            _serviceQueue = serviceQueue;
            _budget.Apply(_queue);
        }

        /// <summary>The runner in use; null while none is started.</summary>
        public ITextRunner Runner { get; set; }

        /// <summary>The player's own AI service's runner; null while it is off. Needs the service queue given to the hub.</summary>
        public ITextServiceRunner Service { get; set; }

        /// <summary>The newest data version of the cards: the AI service is given the newest card of each intent.</summary>
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

        /// <summary>Which uses may ask now, by the options and Going Public's rule; set by the director.</summary>
        public Func<TextUse, bool, bool> Allowed;

        /// <summary>
        /// The data version of the running pack's model: an intent of a later version is never asked of it. Set by the
        /// director when it starts a pack.
        /// </summary>
        public int Data
        {
            get => ServiceUp ? Math.Max(_packData, Newest) : _packData;   // with the service up the newest cards; the pack's own when it falls back
            set => _packData = value;
        }

        private int _packData = TextIntents.DataV2;

        /// <summary>The running pack's own data version, whatever the service.</summary>
        public int PackData => _packData;

        /// <summary>The game's language as a card of data v6 writes it; set by the director.</summary>
        public string Language { get; set; } = TextLanguages.English;

        /// <summary>True while the game shows English; set by the director with <see cref="Language"/>. A pack below data v6 writes English alone.</summary>
        public bool English { get; set; } = true;

        /// <summary>The languages the running pack was trained and passed in; English alone by default.</summary>
        public string[] Languages { get; set; } = { TextLanguages.English };

        /// <summary>The running pack's name as lines are counted under it; <see cref="NoPack"/> while none runs.</summary>
        public string Pack { get; set; } = NoPack;

        /// <summary>Seconds a reply is expected to take on the route a request goes by now: the service's while it is up, else the pack's.</summary>
        public double Expect => ServiceUp ? _serviceQueue.Expect : _queue.Expect;

        /// <summary>True when this machine writes only what is asked ahead.</summary>
        public bool AheadOnly { get; set; }

        /// <summary>Words were kept for a shared item on the host: the session's part has a new line.</summary>
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

        /// <summary>True when the running pack writes the game's language now; a pack below data v6 writes English alone.</summary>
        public bool WritesLanguage => _packData >= TextIntents.DataV6 ? TextLanguages.Has(Languages, Language) : English;

        /// <summary>
        /// True when the AI service is asked in the game's language: one of the seven, which TextCheck judges in full. A
        /// reply in Dutch or Turkish could not be checked (no little words, machine talk or swearing list of theirs), so
        /// their written lines are kept.
        /// </summary>
        public bool ServiceWrites => TextLanguages.Known(Language);

        /// <summary>True when a request of this use could go to the model now, asked before the card is built.</summary>
        public bool Wants(TextUse use, bool shared)
        {
            return ((PackUp && WritesLanguage) || (ServiceUp && !shared && ServiceWrites)) && (Allowed == null || Allowed(use, shared));
        }

        /// <summary>Counts a line the asker shows with its written words without asking, after <see cref="Wants"/> said no.</summary>
        public void NotAsked(TextUse use, bool shared, string intent)
        {
            Tally.Count(Pack, intent, Unwanted(use, shared));
        }

        // Why Wants says no: no way to word it, else off, else the language.
        private TextShownWhy Unwanted(TextUse use, bool shared)
        {
            if (!PackUp && !(ServiceUp && !shared))
            {
                return Runner == null && Pack == NoPack ? TextShownWhy.NoPack : TextShownWhy.Failed;
            }
            return Allowed != null && !Allowed(use, shared) ? TextShownWhy.Off : TextShownWhy.Language;
        }

        /// <summary>
        /// Asks for words. True: they come through the request's Done on a later look, words or null. False: not asked, and
        /// the asker uses the written line now without Done. Either way the line is counted.
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
            r.Lang = null;   // a card made for the service that could not go: the pack's own is finished below
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
            TextIntent intent = TextIntents.Find(r.Intent, r.Branch, _packData);   // the card of the pack's own data
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

        // The request to the AI service with the newest card, in the game's language; false, with why, when it cannot go. A
        // spoken line waits for the service, the end of the wait kept for the pack.
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

        // A card of data v6: its language, the speaker's memory, and the request's grammar, length and the name kept out.
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
        /// Finishes a built card of data v6 as it is sent: its lang line, and its speaker's memory lines, none on a shared
        /// card. The contract's fixtures are finished by the same.
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
        /// How long a spoken line waits with the AI service up: the service's measured time, a margin and the time kept for
        /// the pack, at most <see cref="SpokenMost"/> and never less than the line's own hold.
        /// </summary>
        public static double SpokenWait(double hold, double serviceExpect, double packReserve)
        {
            return Math.Max(hold, Math.Min(SpokenMost, serviceExpect + SpokenMargin + packReserve));
        }

        // The end of a spoken wait kept for the pack, when it would take the line: its measured time and a slack, within the
        // line's own hold.
        private double PackReserve(TextRequest r)
        {
            if (PackWhy(r.Use, r.Shared, r.Intent, r.Branch, r.Priority, r.Guess, r.Seconds, out _) != null)
            {
                return 0.0;
            }
            return Math.Min(r.Seconds, _queue.Expect + PackSlack);
        }

        /// <summary>
        /// True while a guess under this key is the AI service's and its words have not come yet, so its asker may wait for it
        /// as the line itself (<see cref="Adopt"/>).
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
        /// Makes the guess under <paramref name="key"/>, which the AI service is still writing, request
        /// <paramref name="r"/> of the same card, waited for as a spoken line from now. False when there is no such guess,
        /// it guessed another card, or the service could not finish it in time: the guess is let go and the asker asks as usual.
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
                double left = Math.Max(0.0, w.Job.Asked + _serviceQueue.Expect - now);   // what the service is still expected to take
                double waits = SpokenWait(r.Seconds, left, reserve);
                bool same = w.ViaService && open && ServiceUp && Spoken(r) && !r.Shared && w.Request.Intent == r.Intent && w.Request.Branch == r.Branch;
                if (!same || (state != TextJobState.Written && waits - reserve < left))
                {
                    QueueOf(w).Drop(w.Job);
                    End(i, null, false, TextShownWhy.Unwanted, true);   // the guess's own Done: it was never shown
                    return false;
                }
                r.Took = 0;
                r.Why = null;
                r.ByService = false;
                r.Source = TextSource.Written;
                r.ServiceMiss = null;
                r.Waits = waits;
                r.Lang = w.Request.Lang;   // the guess's card is the line's
                _serviceQueue.Urge(w.Job, now + waits - reserve);
                w.Request = r;
                w.Until = now + waits;
                w.Told = false;   // its first words may have come already: the next look tells the line's asker
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

        /// <summary>Why a request would not go to the AI service now; null when it would.</summary>
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
        /// Why a request of this use, card and urgency would not be asked now, before anything is built; null when it would,
        /// though the queue may still be full. For a feature that offers something only while words can come.
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

        // The pack's own reasons, with what the written line shown for it is counted as.
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
                return "not asked: the model is still loading";   // what is due soon takes its written line now rather than wait for nothing
            }
            if (TextIntents.Find(intent, branch, _packData) == null)
            {
                why = TextShownWhy.NoCard;
                return "not asked: the pack (data v" + _packData + ") has no card " + intent + (branch != null ? " " + branch : string.Empty);
            }
            why = TextShownWhy.Model;
            return null;
        }
    }
}
