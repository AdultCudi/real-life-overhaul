using System;
using System.Collections.Generic;
using RealLifeOverhaul.Floors;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A block's show flat on its money page: the designs it may be dressed in (their quality against the district's standard and
    /// what they cost), the one open with its viewings, sales, agent and way there. Written on a page's opening, a pick or a press.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private readonly List<ShowDesignView> _shows = new List<ShowDesignView>(8);
        private int _show;

        private void WriteShowFlat(InfoBox box, ProjectFile p)
        {
            if (!_director.ShowFlatsOn || !ShowFlatRules.Allowed(p, ConstructionDirector.FlatsOf(p)))
            {
                return;
            }
            box.Section("reallife_bld_sec_show");
            if (p.ShowOpen)
            {
                WriteShowOpen(box, p);
                return;
            }
            string block = _director.ShowBlock(p);
            if (block != null)
            {
                box.Line(ConstructionText.T(block), CellTone.Yellow);
                return;
            }
            if (!_director.ShowChoices(p, _shows) || _shows.Count == 0)
            {
                box.Line(ConstructionText.T("reallife_bld_show_no_designs"), CellTone.Yellow);
                return;
            }
            int standard = ConstructionDirector.StandardOf(p.District);
            _show = Mathf.Clamp(_show, 0, _shows.Count - 1);
            box.Selector(_shows.Count, _show, i => ConstructionText.T("reallife_bld_sel_show", "design", _shows[i].Name,
                "quality", ShowFlatRules.Quality(_shows[i].Interior, _shows[i].Decor).ToString(), "cost", ConstructionText.Money(ShowFlatRules.FitCost(_shows[i].Furniture, _shows[i].Fee))),
                i => { _show = i; Refresh(); });
            ShowDesignView d = _shows[_show];
            int quality = ShowFlatRules.Quality(d.Interior, d.Decor);
            box.Row("reallife_bld_row_show_quality", ConstructionText.Fill("reallife_bld_value_show_quality", "quality", quality.ToString(), "interior", d.Interior.ToString(),
                "decor", d.Decor.ToString(), "standard", standard.ToString()), quality >= standard ? CellTone.Green : CellTone.Yellow);
            box.Row("reallife_bld_row_show_viewings", ConstructionText.T("reallife_bld_value_show_viewings", "count", ShowFlatRules.Viewings(quality, standard).ToString(),
                "price", ConstructionText.Share(ShowFlatRules.PricePermille(quality, standard))));
            box.Row("reallife_bld_row_show_day", ConstructionText.Money(ShowFlatRules.DayCost(false, ConstructionGame.Prices)));
            string id = d.Id;
            long cost = ShowFlatRules.FitCost(d.Furniture, d.Fee);
            InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bld_button_show_open", () =>
                Confirm("reallife_bld_confirm_show", "reallife_bld_confirm_show_body", _director.ProjectName(p), cost, () => _director.OpenShow(p, id, false), false));
            box.Note(ConstructionText.T("reallife_bld_show_note"));
        }

        private void WriteShowOpen(InfoBox box, ProjectFile p)
        {
            int quality = ConstructionDirector.ShowQuality(p);
            box.Row("reallife_bld_row_show_quality", ConstructionText.Fill("reallife_bld_value_show_quality", "quality", quality.ToString(), "interior", p.ShowInterior.ToString(),
                "decor", p.ShowDecor.ToString(), "standard", p.ShowStandard.ToString()), quality >= p.ShowStandard ? CellTone.Green : CellTone.Yellow);
            box.Row("reallife_bld_row_show_viewings", ConstructionText.T("reallife_bld_value_show_viewings", "count", ShowFlatRules.Viewings(quality, p.ShowStandard).ToString(),
                "price", ConstructionText.Share(ShowFlatRules.PricePermille(quality, p.ShowStandard))));
            box.RowPair("reallife_bld_row_show_sold", ConstructionText.T("reallife_bld_value_show_sold", "sold", p.ShowSold.ToString(), "viewings", p.ShowViewings.ToString()),
                "reallife_bld_row_spent", ConstructionText.Money(p.ShowSpent));
            box.Row("reallife_bld_row_show_day", ConstructionText.Money(ShowFlatRules.DayCost(p.ShowAgent, ConstructionGame.Prices)));
            if (_director.ShowWhere(out FloorWhere at))
            {
                box.Row("reallife_bld_row_show_where", ConstructionText.T("reallife_bld_show_mark", "floor", at.Floor.ToString(), "address", ConstructionGame.AddressText(at.Street, at.Number)));
            }
            else
            {
                box.Line(ConstructionText.T("reallife_bld_show_no_floor"), CellTone.Yellow);
            }
            box.Selector(2, p.ShowAgent ? 1 : 0, i => ConstructionText.T(i == 1 ? "reallife_bld_sel_agent" : "reallife_bld_sel_no_agent", "amount",
                ConstructionText.Money(ShowFlatRules.AgentCost(ConstructionGame.Prices))), i => Answer(_director.SetAgent(p, i == 1)));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_show_way", () =>
            {
                _director.GuideToShow(out string why);
                Answer(why);
            });
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_show_close", () =>
                Confirm("reallife_bld_confirm_show_close", "reallife_bld_confirm_show_close_body", _director.ProjectName(p), p.ShowSpent, () => _director.CloseShow(p, "the player closed it"), true));
            box.Note(ConstructionText.T(p.ShowAgent ? "reallife_bld_show_agent_note" : "reallife_bld_show_alone_note"));
        }
    }
}
