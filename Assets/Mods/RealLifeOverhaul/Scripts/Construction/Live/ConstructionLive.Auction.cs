using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Land auctions and offers on city lots in the app (<see cref="ConstructionDirector"/>, Auction): on the Land tab a row for each lot up or lately knocked down and each offer the
    /// player has made; an auction's page with the leading bid and who made it, the last bids, the auctioneer, and a step of bids with Place bid; a space's page with the offer under its
    /// list price and its chance. Written on a page's opening or a press; nothing is read per frame.
    /// </summary>
    public sealed partial class ConstructionLive
    {
        private const int ClosedShownDays = 14, OffersShown = 6, BidSteps = 6;

        private readonly List<AuctionFile> _auctions = new List<AuctionFile>(8);
        private int _bidStep, _bidAuction;          // the bid selector's step, and the auction it was last set for
        private int _cityOfferStep = 3;         // the offer selector's step

        /// <summary>One row for each auction up or lately over and each offer of the player's for a space; the page of the one picked is written.</summary>
        private void WriteAuctionRows(KitBox box, ref string selected, out bool written)
        {
            written = false;
            int today = ConstructionGame.Day;
            Director.AuctionsIn(_auctions);
            AuctionFile pickedAuction = null;
            for (int i = 0; i < _auctions.Count; i++)
            {
                AuctionFile a = _auctions[i];
                if (!a.Live && today - a.Closed > ClosedShownDays || !Director.Lot(a.Lot, out LotView lot))
                {
                    continue;
                }
                TableRow row = Pick("auc:" + a.Id, ref selected);
                row.Kind = ConstructionTables.LandAuction;
                row.Set(0, ConstructionGame.AddressText(lot.Street, lot.Number));
                row.Set(1, ConstructionText.T("reallife_bld_value_size", "width", lot.Width.ToString(), "depth", lot.Depth.ToString()), lot.Width * lot.Depth);
                row.Set(2, ConstructionText.ZonesWords(Director.ClassZones(lot.Class)));
                long shown = a.Leader == -2 ? a.Opening : a.Bid;
                row.Set(3, ConstructionText.Money(shown), shown);
                row.Set(4, AuctionWords(a), AuctionTone(a));
                SqmCell(row, shown, lot.Width * lot.Depth);
                if (row.Id == selected)
                {
                    pickedAuction = a;
                }
            }
            CityOfferFile pickedOffer = null;
            int shownOffers = 0;
            for (int i = CityOffersOf().Count - 1; i >= 0 && shownOffers < OffersShown; i--)
            {
                CityOfferFile o = CityOffersOf()[i];
                if (!Director.Lot(o.Lot, out LotView lot))
                {
                    continue;
                }
                shownOffers++;
                TableRow row = Pick("cof:" + o.Id, ref selected);
                row.Kind = ConstructionTables.LandMyOffer;
                row.Set(0, ConstructionGame.AddressText(lot.Street, lot.Number));
                row.Set(1, ConstructionText.T("reallife_bld_value_size", "width", lot.Width.ToString(), "depth", lot.Depth.ToString()), lot.Width * lot.Depth);
                row.Set(2, ConstructionText.ZonesWords(Director.ClassZones(lot.Class)));
                row.Set(3, ConstructionText.Money(o.Price), o.Price);
                row.Set(4, OfferState(o), o.Waiting ? CellTone.Yellow : o.State == CityOfferState.Accepted ? CellTone.Green : CellTone.Plain);
                if (row.Id == selected)
                {
                    pickedOffer = o;
                }
            }
            if (pickedAuction != null)
            {
                WriteAuction(box, pickedAuction);
                written = true;
            }
            else if (pickedOffer != null)
            {
                WriteCityOffer(box, pickedOffer);
                written = true;
            }
        }

        private List<CityOfferFile> CityOffersOf() => Director.State.CityOffers;

        private static string AuctionWords(AuctionFile a)
        {
            switch (a.State)
            {
                case AuctionState.Open:
                    return ConstructionText.T(a.Leader == -1 ? "reallife_auc_state_you_lead" : "reallife_auc_state_live", "day", ConstructionText.Day(a.Ends));
                case AuctionState.Sold:
                    return ConstructionText.T("reallife_auc_state_sold", "who", ConstructionDirector.AuctionWho(a.Leader));
                case AuctionState.Lapsed:
                    return ConstructionText.T("reallife_auc_state_lapsed");
                default:
                    return ConstructionText.T("reallife_auc_state_passed");
            }
        }

        private static CellTone AuctionTone(AuctionFile a)
        {
            return a.Live ? (a.Leader == -1 ? CellTone.Green : a.Mine > 0L ? CellTone.Red : CellTone.Yellow) : a.State == AuctionState.Sold && a.Leader == -1 ? CellTone.Green : CellTone.Plain;
        }

        private static string OfferState(CityOfferFile o)
        {
            switch (o.State)
            {
                case CityOfferState.Waiting: return ConstructionText.T("reallife_auc_offer_waiting", "day", ConstructionText.Day(o.Due));
                case CityOfferState.Accepted: return ConstructionText.T("reallife_auc_offer_yes");
                case CityOfferState.Refused: return ConstructionText.T("reallife_auc_offer_no");
                default: return ConstructionText.T("reallife_auc_offer_lapsed");
            }
        }

        // An auction: where the lot is, the leading bid and who made it, the reserve met or not, the room's last bids, the auctioneer, and the step of bids to place.
        private void WriteAuction(KitBox box, AuctionFile a)
        {
            Director.Lot(a.Lot, out LotView lot);
            box.Title(ConstructionGame.AddressText(lot.Street, lot.Number));
            box.Subtitle(AuctionWords(a));
            string lotId = a.Lot;
            MapRow(box, () => Director.ShowLot(lotId));
            box.RowPair("reallife_bld_row_size", ConstructionText.T("reallife_bld_value_size", "width", lot.Width.ToString(), "depth", lot.Depth.ToString()),
                "reallife_bld_row_traffic", ConstructionText.N(lot.Traffic));
            box.Row("reallife_bld_row_zoned", ConstructionText.ZonesWords(Director.ClassZones(lot.Class)));
            box.RowPair("reallife_auc_row_list", ConstructionText.Money(a.List), "reallife_auc_row_opening", ConstructionText.Money(a.Opening));
            if (a.Leader != -2)
            {
                box.RowPair("reallife_auc_row_leading", ConstructionText.Money(a.Bid), "reallife_auc_row_by", ConstructionDirector.AuctionWho(a.Leader));
                box.Check(ConstructionText.T(a.Bid >= a.Reserve ? "reallife_auc_reserve_met" : "reallife_auc_reserve_not"), a.Bid >= a.Reserve);
            }
            box.RowPair("reallife_auc_row_ends", ConstructionText.Day(a.Ends), "reallife_auc_row_bids", ConstructionText.N(a.Bids));
            box.Row("reallife_auc_row_hall", string.IsNullOrEmpty(a.Hall) ? "-" : a.Hall);
            if (a.LogCount > 0)
            {
                box.Section("reallife_auc_sec_room");
                for (int k = a.LogCount - 1; k >= 0; k--)
                {
                    box.RowWords(ConstructionText.Day(a.LogDay[k]), ConstructionText.T("reallife_auc_log_line", "who", ConstructionDirector.AuctionWho(a.LogWho[k]), "amount",
                        ConstructionText.Money(a.LogBid[k])));
                }
            }
            KitRow buttons = box.ButtonRow();
            if (a.Live)
            {
                if (_bidAuction != a.Id)
                {
                    _bidAuction = a.Id;
                    _bidStep = 0;
                }
                int step = Mathf.Clamp(_bidStep, 0, BidSteps - 1);
                AuctionFile shown = a;
                box.Selector(BidSteps, step, i => ConstructionText.Money(AuctionRules.BidAt(shown, i)), i => UiClone.Safe(() =>
                {
                    _bidStep = i;
                    Refresh();
                }));
                long amount = AuctionRules.BidAt(a, step);
                box.Row("reallife_auc_row_your_bid", ConstructionText.Money(amount));
                string block = Director.BidBlock(a, amount);
                if (block != null)
                {
                    box.Line(ConstructionText.T(block), CellTone.Yellow);
                }
                else
                {
                    int id = a.Id;
                    KitBox.Button(buttons, KitBox.Positive, "reallife_auc_button_bid", () => Answer(Director.Bid(id, amount)));
                }
                box.Note(ConstructionText.T("reallife_auc_note_live"));
            }
            else if (a.State == AuctionState.Sold && a.Leader == -1)
            {
                KitBox.Button(buttons, KitBox.Positive, "reallife_auc_button_plan", () => Jump(ConstructionDirector.TabLand, "own:" + lotId));
            }
            else if (a.State == AuctionState.Sold)
            {
                GrowFile site = Director.GrowOf(a.Lot);
                if (site != null && site.Owner != GrowOwner.Player && (site.Stage == GrowStage.Plans || site.Stage == GrowStage.Planning))
                {
                    int growId = site.Id;
                    box.Line(ConstructionText.T("reallife_auc_won_by", "who", ConstructionDirector.AuctionWho(a.Leader), "use",
                        ConstructionText.T(BuildCatalogue.UseKeys[(int)site.Use])), CellTone.Plain);
                    KitBox.Button(buttons, KitBox.Positive, "reallife_land_button_offer", () => Jump(ConstructionDirector.TabLand, "rvl:" + growId));
                }
            }
            else if (a.State == AuctionState.Passed)
            {
                box.Note(ConstructionText.T("reallife_auc_note_passed"));
            }
            KitBox.Button(buttons, KitBox.Additional, "reallife_bld_back", () => Back());
            Guide(box);
        }

        // The player's offer for a space: what it was, the list price, the chance it had, and the answer.
        private void WriteCityOffer(KitBox box, CityOfferFile o)
        {
            Director.Lot(o.Lot, out LotView lot);
            box.Title(ConstructionGame.AddressText(lot.Street, lot.Number));
            box.Subtitle(OfferState(o));
            string lotId = o.Lot;
            MapRow(box, () => Director.ShowLot(lotId));
            box.RowPair("reallife_land_row_your_offer", ConstructionText.Money(o.Price), "reallife_auc_row_list", ConstructionText.Money(o.List));
            box.RowPair("reallife_auc_row_made", ConstructionText.Day(o.Made), "reallife_auc_row_answer", ConstructionText.Day(o.Waiting ? o.Due : o.Answered));
            box.Row("reallife_auc_row_chance", ConstructionText.Share(AuctionRules.OfferOdds(o.Price, o.List)));
            KitRow buttons = box.ButtonRow();
            if (o.State == CityOfferState.Accepted)
            {
                KitBox.Button(buttons, KitBox.Positive, "reallife_auc_button_plan", () => Jump(ConstructionDirector.TabLand, "own:" + lotId));
            }
            else if (o.State == CityOfferState.Refused)
            {
                box.Note(ConstructionText.T("reallife_auc_note_refused"));
            }
            KitBox.Button(buttons, KitBox.Additional, "reallife_bld_back", () => Back());
            Guide(box);
        }

        /// <summary>On a free space's page: the auction it is up at, or an offer under its list price with the chance it has and the player's last offer.</summary>
        private void CityOfferBox(KitBox box, string spaceId)
        {
            long list = Director.ListPrice(spaceId);
            AuctionFile at = Director.State.AuctionOn(spaceId);
            if (at != null)
            {
                box.Section("reallife_auc_sec_offer");
                box.Line(ConstructionText.T("reallife_auc_at_auction", "day", ConstructionText.Day(at.Ends)), CellTone.Yellow);
                int id = at.Id;
                KitBox.Button(box.ButtonRow(), KitBox.Positive, "reallife_auc_button_to_auction", () => Jump(ConstructionDirector.TabLand, "auc:" + id));
                return;
            }
            if (list <= 0L)
            {
                return;
            }
            box.Section("reallife_auc_sec_offer");
            CityOfferFile last = Director.LastCityOffer(spaceId);
            if (last != null && last.Waiting)
            {
                box.Line(ConstructionText.T("reallife_auc_offer_line_waiting", "amount", ConstructionText.Money(last.Price), "day", ConstructionText.Day(last.Due)), CellTone.Yellow);
            }
            else if (last != null && last.State == CityOfferState.Refused)
            {
                box.Line(ConstructionText.T("reallife_auc_offer_line_refused", "amount", ConstructionText.Money(last.Price)), CellTone.Red);
            }
            int step = Mathf.Clamp(_cityOfferStep, 0, AuctionRules.OfferPoints - 1);
            box.Selector(AuctionRules.OfferPoints, step, i => ConstructionText.Money(AuctionRules.OfferAt(list, i)), i => UiClone.Safe(() =>
            {
                _cityOfferStep = i;
                Refresh();
            }));
            long price = AuctionRules.OfferAt(list, step);
            box.RowPair("reallife_land_row_your_offer", ConstructionText.Money(price), "reallife_auc_row_chance", ConstructionText.Share(AuctionRules.OfferOdds(price, list)));
            string block = Director.CityOfferBlock(spaceId, price);
            if (block != null)
            {
                box.Line(ConstructionText.T(block), CellTone.Yellow);
            }
            else
            {
                KitBox.Button(box.ButtonRow(), KitBox.Positive, "reallife_auc_button_offer", () => Answer(Director.OfferCity(spaceId, price)));
            }
            box.Note(ConstructionText.T("reallife_auc_offer_note"));
        }
    }
}
