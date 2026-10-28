using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The land market in the app (docs/features/construction.md, "Land market"): on the Land tab a row for each site a rival developer
    /// holds before its works and for each offer a rival has made for the player's land; the rival's site page with its ask as a range, a
    /// selector for the player's price and Offer to buy (or Raise the offer after a refusal); a rival's offer page with Accept and Refuse; and
    /// on a site of the player's own, Sell this land and the neighbour a rival holds. Written on a page's opening, a pick or a press;
    /// nothing is read per frame.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private readonly List<GrowFile> _rivalLand = new List<GrowFile>(8);
        private readonly List<LandOffer> _offersIn = new List<LandOffer>(4);
        private int _offerStep = -1;        // the price step of the offer selector, -1 until a rival's site is opened
        private int _offerFor;              // the rival's site the step was last set for

        /// <summary>One row for each site a rival holds before its works and each rival's offer for the player's land; the page of the one picked is written.</summary>
        private void WriteMarketRows(InfoBox box, ref string selected, out bool written)
        {
            written = false;
            _director.RivalLand(_rivalLand);
            int day = ConstructionGame.Day;
            GrowFile pickedSite = null;
            for (int i = 0; i < _rivalLand.Count; i++)
            {
                GrowFile g = _rivalLand[i];
                if (!_director.Lot(g.Lot, out LotView site))
                {
                    continue;
                }
                LandAsk ask = _director.AskOf(g, day);
                TableRow row = Pick("rvl:" + g.Id, ref selected);
                row.Kind = ConstructionTables.LandRival;
                row.Set(0, ConstructionGame.AddressText(site.Street, site.Number));
                row.Set(1, ConstructionText.T("reallife_bld_value_spaces", "count", g.Spaces.ToString(), "width", site.Width.ToString(), "depth", site.Depth.ToString()), site.Width * site.Depth);
                row.Set(2, ConstructionText.T(BuildCatalogue.UseKeys[(int)g.Use]));
                row.Set(3, ConstructionText.T("reallife_land_value_range", "low", ConstructionText.Money(ask.Low), "high", ConstructionText.Money(ask.High)), ask.Ask);
                row.Set(4, ConstructionText.T("reallife_land_state_rival", "developer", ConstructionDirector.GrowDeveloper(g)), CellTone.Yellow);
                if (row.Id == selected)
                {
                    pickedSite = g;
                }
            }
            _director.OffersIn(_offersIn);
            LandOffer pickedOffer = null;
            for (int i = 0; i < _offersIn.Count; i++)
            {
                LandOffer o = _offersIn[i];
                if (!_director.Lot(o.Lot, out LotView site))
                {
                    continue;
                }
                TableRow row = Pick("ofr:" + o.Id, ref selected);
                row.Kind = ConstructionTables.LandOfferIn;
                row.Set(0, ConstructionGame.AddressText(site.Street, site.Number));
                row.Set(1, ConstructionText.T("reallife_bld_value_spaces", "count", o.Spaces.ToString(), "width", site.Width.ToString(), "depth", site.Depth.ToString()), site.Width * site.Depth);
                row.Set(2, ConstructionText.T(BuildCatalogue.UseKeys[(int)o.Use]));
                row.Set(3, ConstructionText.Money(o.Price), o.Price);
                row.Set(4, ConstructionText.T("reallife_land_state_offer", "developer", ConstructionDirector.OfferDeveloper(o), "day", ConstructionText.Day(o.Due)), CellTone.Yellow);
                if (row.Id == selected)
                {
                    pickedOffer = o;
                }
            }
            if (pickedOffer != null)
            {
                WriteOfferIn(box, pickedOffer);
                written = true;
            }
            else if (pickedSite != null)
            {
                WriteRivalSite(box, pickedSite);
                written = true;
            }
        }

        // A site a rival developer holds: what it paid, how far its plans have gone, its ask as a range, and the player's price to offer.
        private void WriteRivalSite(InfoBox box, GrowFile g)
        {
            _director.Lot(g.Lot, out LotView site);
            int day = ConstructionGame.Day;
            LandAsk ask = _director.AskOf(g, day);
            box.Title(ConstructionGame.AddressText(site.Street, site.Number));
            box.Subtitle(ConstructionText.T("reallife_land_rival_sub", "developer", ConstructionDirector.GrowDeveloper(g)));
            string lotId = g.Lot;
            MapRow(box, () => _director.ShowLot(lotId));
            box.RowPair("reallife_bld_row_spaces", ConstructionText.T("reallife_bld_value_spaces", "count", g.Spaces.ToString(), "width", site.Width.ToString(), "depth", site.Depth.ToString()),
                "reallife_bld_row_traffic", ConstructionText.N(site.Traffic));
            box.RowPair("reallife_land_row_plans", ConstructionText.T(BuildCatalogue.UseKeys[(int)g.Use]), "reallife_land_row_stage",
                ConstructionText.T(g.Stage == GrowStage.Planning ? "reallife_land_stage_planning" : "reallife_land_stage_plans", "day", ConstructionText.Day(g.Until)));
            box.Row("reallife_land_row_paid", ConstructionText.Money(g.Price));
            box.Row("reallife_land_row_ask", ConstructionText.T("reallife_land_value_range", "low", ConstructionText.Money(ask.Low), "high", ConstructionText.Money(ask.High)), CellTone.Yellow);
            if (ask.OnlySite)
            {
                box.Line(ConstructionText.T("reallife_land_hint_only"), CellTone.Plain);
            }
            if (ask.Short)
            {
                box.Line(ConstructionText.T("reallife_land_hint_short"), CellTone.Green);
            }
            LandOffer last = _director.LastOffer(g.Lot);
            bool refused = last != null && last.State == LandOfferState.Refused;
            LandOffer waiting = last != null && last.Waiting ? last : null;
            if (waiting != null)
            {
                box.Line(ConstructionText.T("reallife_land_offer_waiting", "amount", ConstructionText.Money(waiting.Price), "day", ConstructionText.Day(waiting.Due)), CellTone.Yellow);
            }
            else if (refused)
            {
                box.Line(ConstructionText.T("reallife_land_offer_refused", "amount", ConstructionText.Money(last.Price)), CellTone.Red);
            }
            int growId = g.Id;
            if (_offerStep < 0 || _offerFor != growId)
            {
                _offerFor = growId;
                _offerStep = LandMarketRules.StartStep(ask.Low, ask.High);
                if (refused)
                {
                    while (_offerStep < LandMarketRules.PricePoints - 1 && LandMarketRules.PriceAt(ask.Low, ask.High, _offerStep) <= last.Price)
                    {
                        _offerStep++;   // a raise starts above the offer that was refused
                    }
                }
            }
            long low = ask.Low, high = ask.High;
            int step = Mathf.Clamp(_offerStep, 0, LandMarketRules.PricePoints - 1);
            box.Selector(LandMarketRules.PricePoints, step, i => ConstructionText.Money(LandMarketRules.PriceAt(low, high, i)), i => UiClone.Safe(() =>
            {
                _offerStep = i;
                Refresh();
            }));
            long price = LandMarketRules.PriceAt(low, high, step);
            box.Row("reallife_land_row_your_offer", ConstructionText.Money(price));
            string block = _director.OfferBlock(g, price);
            Transform buttons = box.ButtonRow();
            if (block != null)
            {
                box.Line(ConstructionText.T(block), CellTone.Yellow);
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Positive, refused ? "reallife_land_button_raise" : "reallife_land_button_offer", () =>
                    Confirm("reallife_land_confirm_offer", "reallife_land_confirm_offer_body", _director.LotWords(lotId), price, () => _director.Offer(growId, price), false));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            box.Note(ConstructionText.T("reallife_land_rival_note"));
            Guide(box);
        }

        // A rival developer's offer for the player's land: who, why, what it pays against what the land is worth and cost, and Accept and Refuse.
        private void WriteOfferIn(InfoBox box, LandOffer o)
        {
            _director.Lot(o.Lot, out LotView site);
            box.Title(ConstructionGame.AddressText(site.Street, site.Number));
            box.Subtitle(ConstructionText.T("reallife_land_offer_sub", "developer", ConstructionDirector.OfferDeveloper(o)));
            string lotId = o.Lot;
            MapRow(box, () => _director.ShowLot(lotId));
            LotFile own = _director.State.Lot(o.Lot);
            box.RowPair("reallife_land_row_offer", ConstructionText.Money(o.Price), "reallife_land_row_worth", ConstructionText.Money(_director.SiteValue(o.Lot)));
            box.RowPair("reallife_bld_row_paid", own != null ? ConstructionText.Money(own.Paid) : "-", "reallife_land_row_until", ConstructionText.Day(o.Due));
            box.Row("reallife_land_row_why", ConstructionDirector.ReasonWords(o));
            box.Row("reallife_land_row_to_build", ConstructionText.T(BuildCatalogue.UseKeys[(int)o.Use]));
            string block = _director.AcceptBlock(o);
            Transform buttons = box.ButtonRow();
            int id = o.Id;
            if (block != null)
            {
                box.Line(ConstructionText.T(block), CellTone.Yellow);
            }
            else
            {
                long price = o.Price;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_land_button_accept", () =>
                    Confirm("reallife_land_confirm_accept", "reallife_land_confirm_accept_body", _director.LotWords(lotId), price, () => _director.Accept(id), true));
            }
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_land_button_refuse", () => Answer(_director.Refuse(id)));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            box.Note(ConstructionText.T("reallife_land_offer_note"));
            Guide(box);
        }

        /// <summary>On a site of the player's own with nothing on it: sell it back to the city at its price, and the neighbour a rival developer holds.</summary>
        private void MarketButtons(InfoBox box, string firstId)
        {
            GrowFile beside = _director.RivalBeside(firstId);
            if (beside != null)
            {
                box.Section("reallife_land_sec_next");
                box.RowWords(ConstructionDirector.GrowDeveloper(beside), ConstructionText.T(beside.Stage == GrowStage.Plans || beside.Stage == GrowStage.Planning
                    ? "reallife_land_next_plans" : "reallife_land_next_works", "use", ConstructionText.T(BuildCatalogue.UseKeys[(int)beside.Use])));
                if (beside.Stage == GrowStage.Plans || beside.Stage == GrowStage.Planning)
                {
                    int growId = beside.Id;
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_land_button_offer", () => Jump(ConstructionDirector.TabLand, "rvl:" + growId));
                }
                box.Note(ConstructionText.T("reallife_land_next_note"));
            }
            box.Section("reallife_land_sec_sell");
            string block = _director.SellBlock(firstId);
            long price = _director.SellPrice(firstId);
            LotFile own = _director.State.Lot(firstId);
            box.RowPair("reallife_land_row_city_price", price > 0 ? ConstructionText.Money(price) : "-", "reallife_land_row_worth", ConstructionText.Money(_director.SiteValue(firstId)));
            if (block != null)
            {
                box.Line(ConstructionText.T(block), CellTone.Yellow);
            }
            else
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_land_button_sell", () =>
                    Confirm("reallife_land_confirm_sell", "reallife_land_confirm_sell_body", _director.LotWords(firstId), price, () => _director.Sell(firstId), true));
            }
            box.Note(ConstructionText.T("reallife_land_sell_note", "paid", own != null ? ConstructionText.Money(own.Paid) : "-", "share", ConstructionText.Share(LandMarketRules.BackPermille)));
        }
    }
}
