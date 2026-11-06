using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A plot's land against what its site needs (LandRules.SiteFit, Civic's PlotRules.Fit): asked at the first step, when the land is
    /// planned on (<see cref="NewPlot"/>), and again at every step after it that draws, pays or starts (the design, the plans, the
    /// engineer, City Hall's application, the appeal, the quotes, the hire, the start), so land that can never take its site is refused
    /// before a fee is taken, with the reason in plain words: the land's size against the size it needs. A project of an older save
    /// that is already past the first step has a way out: it is cancelled and every fee paid for it comes back (<see cref="CancelUnfit"/>).
    /// The city's own picks stop at the same answer (KeepFitting's, CornerDesigns'): no design is offered on land its site cannot use.
    /// Deterministic: the land's size and the project's own record alone. COST: nothing per frame; a lot's read per project when a page is written or a step is pressed.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private string _answerKey;
        private string[] _answerPairs;

        /// <summary>
        /// Why the land of a plot project cannot take its site, as a text key (<see cref="LandRules.FitKeys"/>), or null when it can, and
        /// for any project that is not a plot of the player's own on a lot. The words' figures wait for <see cref="BlockWords"/> and the notice.
        /// </summary>
        public string SiteBlock(ProjectFile p)
        {
            if (p == null || p.Kind != ProjectKind.Plot || string.IsNullOrEmpty(p.Lot) || !Lot(p.Lot, out LotView lot))
            {
                return null;
            }
            return LandBlock(lot, PlaceWords(p));
        }

        /// <summary>Why land cannot take a plot's site, as a text key, or null when it can; the figures are kept for the notice that says it.</summary>
        public string LandBlock(LotView lot, string address)
        {
            PlotFit fit = LandRules.SiteFit(lot.Width, lot.Depth, out int need);
            if (fit == PlotFit.Fits)
            {
                return null;
            }
            string key = LandRules.FitKeys[(int)fit];
            Answering(key, FitPairs(address, lot.Width, lot.Depth, need, null));
            return key;
        }

        private static string[] FitPairs(string address, int width, int depth, int need, string builder)
        {
            return new[]
            {
                "address", address ?? string.Empty, "width", width.ToString(CultureInfo.InvariantCulture), "depth", depth.ToString(CultureInfo.InvariantCulture),
                "need", need.ToString(CultureInfo.InvariantCulture), "builder", builder ?? string.Empty
            };
        }

        /// <summary>
        /// A block's words with its figures: a short fall's text filled with the project's land (and the builder asked, if one was), any
        /// other key as it is. Asked where a block is written on a page, so the figures are never left as their names.
        /// </summary>
        public string BlockWords(ProjectFile p, string key, string builder = null)
        {
            if (!LandRules.IsFitKey(key) || p == null || !Lot(p.Lot, out LotView lot))
            {
                return ConstructionText.T(key);
            }
            LandRules.SiteFit(lot.Width, lot.Depth, out int need);
            return ConstructionText.Fill(key, FitPairs(PlaceWords(p), lot.Width, lot.Depth, need, builder));
        }

        // A builder asked to do what the land cannot take: the same short fall in the builder's own words, named.
        private string BuilderRefused(ProjectFile p, string key, int rival)
        {
            if (!LandRules.IsFitKey(key) || !Lot(p.Lot, out LotView lot))
            {
                return key;
            }
            PlotFit fit = LandRules.SiteFit(lot.Width, lot.Depth, out int need);
            if (fit == PlotFit.Fits)
            {
                return key;
            }
            string name = ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(rival, 0, BuildCatalogue.RivalCount - 1)]);
            string said = LandRules.FitBuilderKeys[(int)fit];
            Answering(said, FitPairs(PlaceWords(p), lot.Width, lot.Depth, need, name));
            Logger?.Info("Construction: project " + p.Id + "'s builder could not set its site out: the land is " + lot.Width + " by " + lot.Depth + " m, short of the " + need
                         + " m a site needs; nothing was charged.");
            return said;
        }

        /// <summary>
        /// A notice's data for the text key just returned (the land's size and the like), once: null for any other key. The notice shows
        /// the game's own text with these filled in.
        /// </summary>
        public Dictionary<string, string> AnswerData(string key)
        {
            if (_answerKey == null || _answerPairs == null || !string.Equals(key, _answerKey, StringComparison.Ordinal))
            {
                return null;
            }
            var data = new Dictionary<string, string>(_answerPairs.Length / 2);   // a fresh one each: the game keeps a notice's data
            for (int i = 0; i + 1 < _answerPairs.Length; i += 2)
            {
                data[_answerPairs[i]] = _answerPairs[i + 1];
            }
            _answerKey = null;
            _answerPairs = null;
            return data;
        }

        private void Answering(string key, string[] pairs)
        {
            _answerKey = key;
            _answerPairs = pairs;
        }

        /// <summary>
        /// True when a project's fees can all be given back with it: it is not past the first stages, nothing has gone out under it
        /// (no site, no investor, no flat sold off plan, no loan drawn), and its land cannot take its site.
        /// </summary>
        public bool CanCancel(ProjectFile p)
        {
            return LandRules.Cancellable(p) && SiteBlock(p) != null;
        }

        /// <summary>
        /// A project whose land cannot take its site given up with every fee paid for it, the plans' practice, the engineer's and City
        /// Hall's, paid back to the player (the land stays theirs). Null for a project that cannot be cancelled so (the app shows
        /// the button only for one that can). Returns a text key.
        /// </summary>
        public string CancelUnfit(ProjectFile p)
        {
            if (!CanCancel(p))
            {
                return null;
            }
            long back = LandRules.Refund(p);
            string name = ProjectName(p);
            int width = p.Width, depth = p.Depth;
            if (Lot(p.Lot, out LotView lot))
            {
                width = lot.Width;
                depth = lot.Depth;
            }
            Abandon(p);   // the land kept, its empty days counting again, the crews and plant free
            if (back > 0L)
            {
                ConstructionGame.Give(back, "reallife_transaction_bld_fees_back", name);
                _state.PayBack(back);
                p.Spent = 0L;
            }
            Logger?.Info("Construction: project " + p.Id + " cancelled: its land is " + width + " by " + depth + " m, too small for its site; " + back + " in fees came back.");
            Answering("reallife_bld_answer_cancelled_refund", new[] { "name", name, "amount", ConstructionText.Money(back) });
            Changed();
            return "reallife_bld_answer_cancelled_refund";
        }
    }
}
