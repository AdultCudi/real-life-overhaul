using UI.Dialog;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a game form says only when the pointer rests on something, which the kit cannot rest on: the bank's investment
    /// shows a tooltip once a fund is chosen ("Investments will randomly fluctuate over time within this range", beside its
    /// risk). It stands as a note under the risk's figure, in the game's own words, while the game shows its tooltip.
    /// Game side. Cost: a scan of the rows a read, when the form is read.
    /// </summary>
    internal sealed partial class DialogForm
    {
        private void Hints(Transform form, int heading)
        {
            if (form == null || !form.TryGetComponent(out BankInvestmentSettings investment) || investment.selectedFundData == null)
            {
                return;
            }
            string risk = ContactsRules.Plain(UiText.T("common_risk"));
            int at = _rows.Count - 1;
            for (int i = heading; i < _rows.Count; i++)
            {
                if ((_rows[i].Kind == UiFormKind.Figure || _rows[i].Kind == UiFormKind.Heading) && _rows[i].Label == risk)
                {
                    at = i < _rows.Count - 1 && _rows[i].Kind == UiFormKind.Heading && _rows[i + 1].Kind == UiFormKind.Note ? i + 1 : i;
                    break;
                }
            }
            _rows.Insert(at + 1, new UiFormRow { Kind = UiFormKind.Note, Label = UiText.T("dialog_bank_investment_risk_info_tooltip") });
            _widgets.Insert(at + 1, default);
        }
    }
}
