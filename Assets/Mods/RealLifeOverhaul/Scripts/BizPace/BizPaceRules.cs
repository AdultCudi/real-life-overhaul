using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.BizPace
{
    /// <summary>The three moves in BizMan's business view that the mod's own handlers answer to.</summary>
    public enum BizStep
    {
        Open = 0,     // BizMan's business view switched on (the app opened on a business, or a business picked from the list)
        Switch = 1,   // another business picked in the view that is already open (the name drop-down, or BizMan.Open on another address)
        Tab = 2       // another tab of the same business
    }

    /// <summary>
    /// What the mod may add to BizMan, and how the handlers' time is counted into it. A BizMan step is an episode: it begins at the
    /// game's own move (the mark on the view or on a tab's page) and collects the time of the mod's handlers (<see cref="BizPaceWatch"/>)
    /// for <see cref="Window"/> more frames. The owner's budgets (27 Oct 2026, after a player's report that opening a business and
    /// switching tabs got slower with each update): under <see cref="OpenBudgetMs"/> ms to an open, under <see cref="SwitchBudgetMs"/>
    /// ms to a switch or a tab, on a large save. Game-free: checked in EconomyCheck. COST: nothing per frame; a few adds an episode.
    /// </summary>
    public static class BizPaceRules
    {
        public const double OpenBudgetMs = 2.0;     // milliseconds the mod may add to BizMan opening a business
        public const double SwitchBudgetMs = 1.0;   // ... to another business picked in the open view
        public const double TabBudgetMs = 1.0;      // ... to another tab of the business shown
        public const int Window = 3;                // frames after the mark that still count: the game's own "after one frame" work and the late updates
        public const int Steps = 3;                 // Open, Switch, Tab

        /// <summary>The most the mod may add to one step, in milliseconds.</summary>
        public static double Budget(BizStep step)
        {
            switch (step)
            {
                case BizStep.Open: return OpenBudgetMs;
                case BizStep.Switch: return SwitchBudgetMs;
                default: return TabBudgetMs;
            }
        }

        /// <summary>The step's word in the log ("open", "switch", "tab") and its plural.</summary>
        public static string Word(BizStep step, bool many)
        {
            switch (step)
            {
                case BizStep.Open: return many ? "opens" : "open";
                case BizStep.Switch: return many ? "switches" : "switch";
                default: return many ? "tab changes" : "tab change";
            }
        }

        /// <summary>True if the mod's time for a step is within its budget.</summary>
        public static bool Within(BizStep step, double ms) => ms <= Budget(step);

        /// <summary>
        /// The step a new mark makes when an episode is already collecting: the stronger of the two (an open holds the tab and the switch
        /// it brings with it; a switch holds its tab). Open is the strongest.
        /// </summary>
        public static BizStep Stronger(BizStep held, BizStep marked) => (int)marked < (int)held ? marked : held;

        /// <summary>The step a mark on a tab's page is: another business if the view's address moved since the last mark, else another tab.</summary>
        public static BizStep OfTabMark(bool addressMoved) => addressMoved ? BizStep.Switch : BizStep.Tab;

        /// <summary>True while an episode that began in frame <paramref name="began"/> still collects in <paramref name="frame"/>.</summary>
        public static bool Collects(int began, int frame) => frame >= began && frame - began <= Window;

        /// <summary>
        /// A building's few facts that a handler of the game's registration change event keys its work on, as one number: two events that
        /// leave it the same ask for no new look. Bits: a unit to let and empty, an office business in it, rented by the player, owned by the
        /// player, another player's, held by a feature of the mod's, listed for sale. Game-free so that its cost is measured offline.
        /// </summary>
        public static int Signature(bool vacant, bool officeBusiness, bool rented, bool owned, bool foreign, bool held, bool onSale)
        {
            return (vacant ? 1 : 0) | (officeBusiness ? 2 : 0) | (rented ? 4 : 0) | (owned ? 8 : 0) | (foreign ? 16 : 0) | (held ? 32 : 0) | (onSale ? 64 : 0);
        }

        /// <summary>True when a handler's work is wanted: the building's facts differ from those its last look was made on.</summary>
        public static bool Moved(int looked, int now) => looked != now;

        /// <summary>The facts of the type a handler keeps for: only a warehouse can become a data centre, whatever other building the event is for.</summary>
        public static bool KeepsFor(string buildingType, string wanted) => buildingType != null && string.Equals(buildingType, wanted, StringComparison.Ordinal);

        /// <summary>
        /// The verdict of a walk: every step the walk made is within its budget in its worst episode, and the walk made an open and a tab
        /// change at least once, and a switch too where the player has a second business to switch to (<paramref name="switchPossible"/>).
        /// <paramref name="worst"/> and <paramref name="counts"/> are by <see cref="BizStep"/>.
        /// </summary>
        public static bool Passes(double[] worst, int[] counts, bool switchPossible, out string reason)
        {
            reason = null;
            for (int i = 0; i < Steps; i++)
            {
                var step = (BizStep)i;
                if (counts[i] <= 0)
                {
                    if (step == BizStep.Switch && !switchPossible)
                    {
                        continue;   // one business: there is none to switch to
                    }
                    reason = "no " + Word(step, false) + " was made";
                    return false;
                }
                if (!Within(step, worst[i]))
                {
                    reason = "the worst " + Word(step, false) + " took " + Ms(worst[i]) + " ms of the mod's, over " + Ms(Budget(step)) + " ms";
                    return false;
                }
            }
            return true;
        }

        /// <summary>Milliseconds to two places, the same in every language of the machine.</summary>
        public static string Ms(double ms) => ms.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The episodes counted by step: how many, the time of the mod's handlers in all and in the worst, and the handler that took most in
    /// the worst. One for the session's minute and one for a test's walk. Game-free; the names are handed in.
    /// </summary>
    public sealed class BizPaceBook
    {
        public readonly int[] Count = new int[BizPaceRules.Steps];
        public readonly int[] Over = new int[BizPaceRules.Steps];
        public readonly double[] TotalMs = new double[BizPaceRules.Steps];
        public readonly double[] WorstMs = new double[BizPaceRules.Steps];
        public readonly string[] WorstHandler = new string[BizPaceRules.Steps];
        public readonly double[] WorstHandlerMs = new double[BizPaceRules.Steps];
        private string _slowest;
        private double _slowestMs;

        /// <summary>True when an episode has been counted.</summary>
        public bool Any
        {
            get
            {
                for (int i = 0; i < BizPaceRules.Steps; i++)
                {
                    if (Count[i] > 0)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>The handler that took most in any one episode counted, and its time; null when none.</summary>
        public string Slowest => _slowest;
        public double SlowestMs => _slowestMs;

        /// <summary>One episode closed: the mod's handlers took <paramref name="ms"/> in all, <paramref name="handler"/> the most of it.</summary>
        public void Add(BizStep step, double ms, string handler, double handlerMs)
        {
            int i = (int)step;
            Count[i]++;
            TotalMs[i] += ms;
            if (!BizPaceRules.Within(step, ms))
            {
                Over[i]++;
            }
            if (ms > WorstMs[i])
            {
                WorstMs[i] = ms;
                WorstHandler[i] = handler;
                WorstHandlerMs[i] = handlerMs;
            }
            if (handler != null && handlerMs > _slowestMs)
            {
                _slowest = handler;
                _slowestMs = handlerMs;
            }
        }

        public void Clear()
        {
            Array.Clear(Count, 0, Count.Length);
            Array.Clear(Over, 0, Over.Length);
            Array.Clear(TotalMs, 0, TotalMs.Length);
            Array.Clear(WorstMs, 0, WorstMs.Length);
            Array.Clear(WorstHandler, 0, WorstHandler.Length);
            Array.Clear(WorstHandlerMs, 0, WorstHandlerMs.Length);
            _slowest = null;
            _slowestMs = 0.0;
        }

        /// <summary>
        /// The session line's part: "BizMan: 3 opens (the mod added 0.40 ms at most, wanted under 2.00), 5 switches (...), ..., the slowest
        /// handler Interiors: InteriorTab.Write 0.31 ms". Nothing is appended when no episode was counted.
        /// </summary>
        public void Append(StringBuilder into)
        {
            if (!Any)
            {
                return;
            }
            into.Append("; BizMan:");
            bool first = true;
            for (int i = 0; i < BizPaceRules.Steps; i++)
            {
                if (Count[i] == 0)
                {
                    continue;
                }
                var step = (BizStep)i;
                into.Append(first ? " " : ", ").Append(Count[i]).Append(' ').Append(BizPaceRules.Word(step, Count[i] != 1)).Append(" (the mod added ")
                    .Append(BizPaceRules.Ms(WorstMs[i])).Append(" ms at most, wanted under ").Append(BizPaceRules.Ms(BizPaceRules.Budget(step)))
                    .Append(Over[i] > 0 ? ", " + Over[i] + " over" : string.Empty).Append(')');
                first = false;
            }
            if (_slowest != null)
            {
                into.Append(", the slowest handler ").Append(_slowest).Append(' ').Append(BizPaceRules.Ms(_slowestMs)).Append(" ms");
            }
        }
    }
}
