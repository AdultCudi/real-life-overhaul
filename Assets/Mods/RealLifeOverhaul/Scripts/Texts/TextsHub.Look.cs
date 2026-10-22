using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>The hub's look: what came back checked and handed over, a guess taken, the AI service's misses given to the pack, every line counted.</summary>
    public sealed partial class TextsHub
    {
        /// <summary>The look: words that came back checked and handed over, deadlines past handed over as the written line; how many.</summary>
        public int Look(double now)
        {
            _missed.Clear();
            _queue.TakeMissed(_missed);   // their state says so already, read below with the rest
            _serviceQueue?.TakeMissed(_missed);
            int handed = 0;
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                Waiting w = _waiting[i];
                TextJobState state = w.Job.State;
                if (w.ViaService && !w.Told && w.Job.Speaking && !w.Request.Guess && state == TextJobState.Running)
                {
                    w.Told = true;
                    w.Request.Started?.Invoke();   // its asker may show the person about to speak; the words wait until they are whole
                }
                if (state == TextJobState.Written)
                {
                    if (w.Request.Guess)
                    {
                        if (now > w.Job.Deadline)
                        {
                            End(i, null, false, TextShownWhy.Unwanted, true);   // never taken: nothing was shown for it
                            handed++;
                        }
                        continue;   // its asker takes it, or its time runs out
                    }
                    if (now > w.Job.Deadline + 0.25)
                    {
                        w.Request.Why = (w.ViaService ? "the service's " : string.Empty) + "words came after the hold";
                        if (w.ViaService && Fallback(i, now))
                        {
                            continue;   // a spoken line: the time kept for the pack
                        }
                        Over(i, !w.ViaService, TextShownWhy.Slow);
                    }
                    else
                    {
                        string words = Checked(w);
                        if (words == null && Fallback(i, now))
                        {
                            continue;   // the service's words failed the checks: the pack tries in the time left
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
                        continue;   // the service failed or rests: the pack tries in the time left
                    }
                    Over(i, !w.ViaService && state == TextJobState.Missed && BudgetMiss(w.Job), state == TextJobState.Dropped ? TextShownWhy.Unwanted : Missed(w.Job));
                    handed++;
                }
                else if (now > w.Job.Deadline)
                {
                    double held = w.ViaService ? w.Job.Deadline - w.Job.Asked : w.Request.Seconds;
                    w.Request.Why = "the hold of " + held.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " s ran out while it was "
                                    + (state == TextJobState.Running ? "being written" : "waiting in the queue") + (w.ViaService ? " of the service" : string.Empty);
                    QueueOf(w).Drop(w.Job);   // a running one comes back later and is let go
                    if (w.ViaService && !w.Request.Guess && Fallback(i, now))
                    {
                        continue;   // a spoken line the service was late with: the pack in the time kept for it
                    }
                    Over(i, !w.ViaService && BudgetMiss(w.Job), TextShownWhy.Slow);
                    handed++;
                }
            }
            return handed;
        }

        // A missed request's reason: the model's own failure, the queue full, or the time.
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

        /// <summary>Takes a guess when its line is needed: the checked words if they are written, else null; let go either way.</summary>
        public string TakeGuess(string key) => TakeGuess(key, out _);

        /// <summary>As <see cref="TakeGuess(string)"/>, and where its words came from, for the asker's log line.</summary>
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

        /// <summary>Where words came from, as every log line names it: "your own AI (ChatGPT)", "the pack" or "the written line".</summary>
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

        /// <summary>Hands over every waiting request marked to flush as the written line, the game saving; how many.</summary>
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

        /// <summary>Hands over everything waiting as the written line.</summary>
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

        /// <summary>Remembers something a speaker said, words or a written line, for the repeat check.</summary>
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

        private void Hand(int i, string words)
        {
            if (!_waiting[i].ViaService)
            {
                _budget.Ended(false);
                _budget.Apply(_queue);
            }
            End(i, words, true, TextShownWhy.Refused);
        }

        // A request the AI service did not word asked of the pack instead, in the time left, with the pack's own card. False
        // when the pack cannot take it: the written line.
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
                r.Lang = lang;   // still the service's card: its written line follows
                return false;
            }
            ServiceFell++;
            r.ServiceMiss = r.Why;   // why the service's words were not used, for the log
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
        /// Whether a miss tells the budget the model is too busy: one the worker ran, or one of the kinds the budget lets
        /// through. Never a background request the queue held back itself: counted, those would hold the budget at its lowest
        /// steps for good.
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

        // The words checked: null if they fail, counted by verdict.
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

        // A request handed over, counted, kept and remembered when there are words; Done is called last. A guess never shown
        // counts only in the session's figures.
        private void End(int i, string words, bool counted, TextShownWhy why, bool unseen = false)
        {
            Waiting w = _waiting[i];
            _waiting.RemoveAt(i);
            if (words != null && w.Request.Wanted != null && !w.Request.Wanted())
            {
                words = null;   // too late for where it was going: the written line stays
                counted = true;
                why = TextShownWhy.Unwanted;
                w.Request.Why = "no longer wanted when the words came";
            }
            w.Request.Took = w.Job.Took;
            w.Request.ByService = words != null && w.ViaService;
            w.Request.Source = SourceOf(words, w);
            if (unseen)
            {
                Fallbacks++;   // the tally counts only lines shown
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

        // Counts one line handed over: the model's words under the way that wrote them, a written line under the pack.
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
            ServiceFell += w.ViaService && !w.Request.ServiceOnly ? 1 : 0;   // a service request that ended in the written line without reaching the pack
            bool refused = why == TextShownWhy.Refused && _verdict != TextVerdict.Ok;
            Tally.Count(Pack, w.Request.Intent, refused ? TextShownWhy.Refused : why == TextShownWhy.Refused ? TextShownWhy.Slow : why, refused ? _verdict : TextVerdict.Ok);
            _verdict = TextVerdict.Ok;
        }
    }
}
