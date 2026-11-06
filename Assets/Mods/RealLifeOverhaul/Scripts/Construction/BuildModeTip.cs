using System;
using Localizor.LanguageChangeEvent;
using Tooltip;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The game's own tooltip on a part of build mode's interface (a TooltipTarget, as the game's BasicTooltip is): after the
    /// game's tenth of a second under the pointer its tooltip shows the part's name as its header and one line under it, in
    /// white or, for a reason it cannot be used yet, in the game's orange. The words are final (written when the part's state
    /// changes), so nothing is looked up while the pointer moves. Costs nothing a frame: the game's TooltipSystem places it.
    /// </summary>
    internal sealed class BuildModeTip : TooltipTarget
    {
        private static readonly FeatureGuard TipGuard = Guard.For("construction", "BuildModeTip", typeof(BuildModeTip));   // the game's tooltip asks it

        public static readonly Color Why = new Color(1f, 0.82f, 0.54f, 1f);   // the line of a part that cannot be used yet

        public string Title;
        public string Line;
        public bool Warn;

        /// <summary>The words shown next time the pointer rests on it (and at once if it is resting there now).</summary>
        public void Set(string title, string line, bool warn)
        {
            Title = title;
            Line = line;
            Warn = warn;
        }

        protected override void ShowTooltip()
        {
            try
            {
                if (TipGuard.Off)
                {
                    return;   // its feature was switched off after its faults
                }
                bool head = !string.IsNullOrEmpty(Title);
                if (head)
                {
                    TooltipSystem.AddHeader(new LanguageChangeEventDataHolder { Key = Title });   // a header that is not a key is written as it is (TooltipSystem.SetHeader)
                }
                if (!string.IsNullOrEmpty(Line))
                {
                    if (head)
                    {
                        TooltipSystem.AddSplitter();
                    }
                    TooltipSystem.AddLabel(Line, Warn ? Why : Color.white);
                }
            }
            catch (Exception e)
            {
                Guard.Fail(TipGuard, e);
            }
        }
    }
}
