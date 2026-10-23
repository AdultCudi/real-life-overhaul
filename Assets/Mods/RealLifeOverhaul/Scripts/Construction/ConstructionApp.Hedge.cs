using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A project's materials and hedge page: what it will still buy of each material and from when, at today's prices, and
    /// the futures on the raw materials exchange that would hedge it (their margin and commission), bought with Hedge; once
    /// hedged, what the hedge has saved or cost so far, and at the project's end what it saved or cost. Written on a press.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private static readonly string[] UnitKeys =
        {
            "reallife_bld_unit_cement", "reallife_bld_unit_steel", "reallife_bld_unit_bricks", "reallife_bld_unit_glass", "reallife_bld_unit_timber"
        };

        private readonly List<HedgeLine> _hedgeLines = new List<HedgeLine>(8);

        /// <summary>Opens the app on a project's materials and hedge page.</summary>
        public void WantHedge(int project)
        {
            _wantPage = 2;
            Want(ConstructionDirector.TabProjects, project);
        }

        // The page's button, in a row of its own, while the stock market's builders and hedging are on.
        private void HedgeButton(InfoBox box)
        {
            if (Settings.MarketsBuilders)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_bld_button_hedge", () => { _page = 2; Refresh(); });
            }
        }

        // The finished page's line: what the hedge saved or cost, the futures still to settle said.
        private void HedgeEndRow(InfoBox box, ProjectFile p)
        {
            if (p.HedgeDay <= 0)
            {
                return;
            }
            _director.LookAtHedge(p);
            long saved = ConstructionDirector.HedgeSaved(p);
            box.Row(saved >= 0 ? "reallife_bld_row_hedge_saved" : "reallife_bld_row_hedge_cost", ConstructionText.Money(Math.Abs(saved)), saved >= 0 ? CellTone.Green : CellTone.Yellow);
            if (p.Hedges.Count > 0)
            {
                box.Note(ConstructionText.Fill("reallife_bld_hedge_open", "count", p.Hedges.Count.ToString()));
            }
        }

        private void WriteHedge(InfoBox box, ProjectFile p)
        {
            _director.LookAtHedge(p);
            List<MaterialNeed> needs = _director.Exposure(p);
            box.Section("reallife_bld_sec_exposure");
            bool any = false;
            for (int m = 0; m < (int)Material.Count; m++)
            {
                long money = ExposureRules.Total(needs, (Material)m, out int tenths);
                if (tenths <= 0)
                {
                    continue;
                }
                any = true;
                box.RowWords(ConstructionText.T(BuildCatalogue.MaterialKeys[m]), ConstructionText.Fill("reallife_bld_hedge_exposure", "loads", (tenths / 10.0).ToString("0.#"),
                    "money", ConstructionText.Money(money), "day", ConstructionText.Day(FirstDay(needs, (Material)m))));
            }
            if (!any)
            {
                box.Note(ConstructionText.T(p.Stage == ProjectStage.Finished ? "reallife_bld_hedge_all_bought" : "reallife_bld_hedge_none"));
            }
            box.Section("reallife_bld_sec_hedge");
            if (p.HedgeDay > 0)
            {
                long saved = ConstructionDirector.HedgeSaved(p);
                box.RowPair("reallife_bld_row_hedged", ConstructionText.Day(p.HedgeDay), saved >= 0 ? "reallife_bld_row_hedge_saved" : "reallife_bld_row_hedge_cost",
                    ConstructionText.Money(Math.Abs(saved)));
                if (p.Hedges.Count > 0)
                {
                    box.Note(ConstructionText.Fill("reallife_bld_hedge_open", "count", p.Hedges.Count.ToString()));
                }
            }
            Transform buttons = box.ButtonRow();
            if (any && p.Stage != ProjectStage.Finished && !ConstructionDirector.HedgeOpen(p))
            {
                bool planned = _director.HedgeQuoteOf(p, _hedgeLines, out HedgeQuote q);
                for (int m = 0; planned && m < (int)Material.Count; m++)
                {
                    long units = 0;
                    int legs = 0, last = 0;
                    for (int i = 0; i < _hedgeLines.Count; i++)
                    {
                        if (_hedgeLines[i].Material == (Material)m)
                        {
                            units += _hedgeLines[i].Units;
                            legs++;
                            last = Math.Max(last, _hedgeLines[i].SettleDay);
                        }
                    }
                    if (legs > 0)
                    {
                        box.Line(ConstructionText.Fill("reallife_bld_hedge_leg", "material", ConstructionText.T(BuildCatalogue.MaterialKeys[m]), "units", units.ToString("N0"),
                            "unit", ConstructionText.T(UnitKeys[m]), "day", ConstructionText.Day(last)), CellTone.Plain);
                    }
                }
                if (planned && q.Legs > 0)
                {
                    box.Line(ConstructionText.Fill("reallife_bld_hedge_quote", "margin", ConstructionText.Money(q.Margin), "fees", ConstructionText.Money(q.Fees)), CellTone.Plain);
                }
                if (q.TooNear > 0)
                {
                    box.Note(ConstructionText.Fill("reallife_bld_hedge_near", "money", ConstructionText.Money(q.TooNear)));
                }
                if (q.Cut > 0)
                {
                    box.Note(ConstructionText.Fill("reallife_bld_hedge_cut", "units", q.Cut.ToString("N0")));
                }
                if (!string.IsNullOrEmpty(q.WhyKey))
                {
                    box.Line(ConstructionText.T(q.WhyKey), CellTone.Red);
                }
                else if (planned && q.Legs > 0)
                {
                    long cost = q.Margin + q.Fees;
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_hedge_buy", () =>
                        Confirm("reallife_bld_confirm_hedge", "reallife_bld_confirm_hedge_body", _director.ProjectName(p), cost, () => _director.Hedge(p), false));
                }
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_button_site", () => { _page = 0; Refresh(); });
            Meta.Guide.Point(box, "reallife_meta_title_markets_builders");
        }

        private static int FirstDay(List<MaterialNeed> needs, Material material)
        {
            int first = int.MaxValue;
            for (int i = 0; i < needs.Count; i++)
            {
                if (needs[i].Material == material && needs[i].Day < first)
                {
                    first = needs[i].Day;
                }
            }
            return first == int.MaxValue ? 0 : first;
        }
    }
}
