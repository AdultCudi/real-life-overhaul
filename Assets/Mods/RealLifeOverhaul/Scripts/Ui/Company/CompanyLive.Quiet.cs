using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    // Another feature's quiet second choice on a card of the company's (a policy, the return, a business's books, a
    // campaign): one button in words only under the card's own, where that feature gives it words, and none otherwise.
    internal sealed partial class CompanyLive
    {
        // The card's blocks with the quiet button under them, when there is one for this matter at this place.
        internal static void Quiet(List<CompanyBlock> blocks, int matter, string place, int figure)
        {
            string words = CompanyBridges.QuietWords(matter, place, figure);
            if (words != null)
            {
                blocks.Add(Buttons(Btn(CompanyText.T(words), UiButtonKind.Quiet, CompanyAct.Of(CompanyDo.Quiet, place, matter, figure))));
            }
        }

        internal static CompanyBlock[] Quiet(CompanyBlock[] blocks, int matter, string place, int figure)
        {
            string words = CompanyBridges.QuietWords(matter, place, figure);
            if (words == null || blocks == null)
            {
                return blocks;
            }
            var with = new CompanyBlock[blocks.Length + 1];
            Array.Copy(blocks, with, blocks.Length);
            with[blocks.Length] = Buttons(Btn(CompanyText.T(words), UiButtonKind.Quiet, CompanyAct.Of(CompanyDo.Quiet, place, matter, figure)));
            return with;
        }
    }
}
