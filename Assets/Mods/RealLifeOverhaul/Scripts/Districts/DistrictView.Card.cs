using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Extensions;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Districts
{
    /// <summary>The district view, part: the window's key, the card and the list of districts, written on a change only.</summary>
    public sealed partial class DistrictView
    {
        // The district the card is about: the one pointed at in the list or on the map, else the one kept, else the player's.
        private MapDistrict Carded()
        {
            string id = _listHover ?? _hover ?? _kept ?? DistrictGame.PlayerDistrict();
            for (int i = 0; id != null && i < _districts.Count; i++)
            {
                if (_districts[i].Id == id)
                {
                    return _districts[i];
                }
            }
            return null;
        }

        private void Card()
        {
            if (!_showing || _panel == null || !_panel.Alive)
            {
                return;
            }
            int layer = Layer;
            if (layer < 0)
            {
                return;
            }
            MapDistrict card = Carded();
            string key = layer + "|" + (card != null ? card.Id : string.Empty) + "|" + _readDay;
            if (key == _carded)
            {
                return;
            }
            _carded = key;
            DistrictLayer which = (DistrictLayer)layer;
            string id = DistrictRules.Ids[layer];
            _panel.Begin(T(Mod + "_dv_row_" + id), T(Mod + "_dv_about_" + id) + "\n<color=#C9CED3>" + T("reallife_dv_hint") + "</color>");

            // The key.
            _panel.Heading(T("reallife_dv_key"));
            LayerSense sense = DistrictRules.Sense(which);
            int[] edges = _edges[layer];
            bool any = false, none = false;
            for (int i = 0; i < _districts.Count; i++)
            {
                any |= _districts[i].HasFigure[layer];
                none |= !_districts[i].HasFigure[layer];
            }
            if (any && edges == null)
            {
                _panel.Swatch(DistrictRules.Colour(sense, DistrictRules.Bands / 2), T("reallife_dv_all_alike"));
            }
            else if (any)
            {
                for (int band = DistrictRules.Bands - 1; band >= 0; band--)
                {
                    if (!DistrictRules.HasRange(edges, band))
                    {
                        continue;   // (a range too narrow for five steps leaves a band no district can be in)
                    }
                    string words = T(Mod + "_dv_band_" + id + "_" + band.ToString(CultureInfo.InvariantCulture));
                    _panel.Swatch(DistrictRules.Colour(sense, band), words + "  <color=#C9CED3>" + Range(which, edges, band) + "</color>");
                }
            }
            if (none)
            {
                _panel.Swatch(DistrictRules.NoFigure, T(Mod + "_dv_none_" + id));
            }

            // The card.
            if (card != null)
            {
                _panel.Heading(card.Name);
                CardLines(card, which);
            }

            // The districts in order, highest first; those with no figure last.
            _panel.Heading(T("reallife_dv_districts"));
            var order = new List<MapDistrict>(_districts);
            order.Sort((a, b) =>
            {
                if (a.HasFigure[layer] != b.HasFigure[layer])
                {
                    return a.HasFigure[layer] ? -1 : 1;
                }
                int by = b.Figure[layer].CompareTo(a.Figure[layer]);
                return by != 0 ? by : string.CompareOrdinal(a.Name, b.Name);
            });
            for (int i = 0; i < order.Count; i++)
            {
                MapDistrict d = order[i];
                string figure = d.HasFigure[layer] ? Figure(which, d.Figure[layer]) : T("reallife_dv_no_figure");
                _panel.District(DistrictRules.Colour(sense, d.Band[layer]), d.Id, "<b>" + d.Name + "</b>  <color=#C9CED3>" + figure + "</color>", d.Centre);
            }
            _panel.End();
        }

        // Every figure of a district, the layer shown first.
        private void CardLines(MapDistrict d, DistrictLayer first)
        {
            var order = new List<DistrictLayer>(DistrictRules.Layers) { first };
            for (int i = 0; i < DistrictRules.Layers; i++)
            {
                if ((DistrictLayer)i != first)
                {
                    order.Add((DistrictLayer)i);
                }
            }
            string unit = DistrictGame.AreaUnit();
            for (int i = 0; i < order.Count; i++)
            {
                switch (order[i])
                {
                    case DistrictLayer.Wealth:
                        if (d.HasIncome)
                        {
                            Line(d.IncomeLive ? "reallife_dv_card_income" : "reallife_dv_card_income_start", "{level}", d.Income.ToString(CultureInfo.InvariantCulture));
                        }
                        Line("reallife_dv_card_classes", "{working}", Whole(d.Working), "{middle}", Whole(d.Middle), "{upper}", Whole(d.Upper));
                        break;
                    case DistrictLayer.Rent:
                        if (d.CommercialRent > 0)
                        {
                            Line("reallife_dv_card_rent", "{rent}", Money(DistrictGame.PerArea(d.CommercialRent / 100f)), "{unit}", unit);
                        }
                        if (d.HomeRent > 0)
                        {
                            Line("reallife_dv_card_home_rent", "{rent}", Money(DistrictGame.PerArea(d.HomeRent / 100f)), "{unit}", unit);
                        }
                        break;
                    case DistrictLayer.Property:
                        if (d.Value > 0)
                        {
                            Line("reallife_dv_card_value", "{value}", Money(DistrictGame.PerArea(d.Value)), "{unit}", unit);
                        }
                        break;
                    case DistrictLayer.Footfall:
                        if (d.Traffic > 0)
                        {
                            Line("reallife_dv_card_traffic", "{traffic}", Whole((d.Traffic + 5) / 10));
                        }
                        float mean = MeanDemand();
                        if (mean > 0f && d.DemandWeight > 0f)
                        {
                            Line("reallife_dv_card_demand", "{percent}", Whole(Mathf.RoundToInt(d.DemandWeight / mean * 100f)));
                        }
                        if (d.Footfall.HasValue && d.Footfall.Value != 0)
                        {
                            Line(d.Footfall.Value > 0 ? "reallife_dv_card_footfall_up" : "reallife_dv_card_footfall_down", "{percent}", Whole(Math.Abs(d.Footfall.Value)));
                        }
                        if (d.Bears.HasValue)
                        {
                            Line("reallife_dv_card_bears", "{percent}", Whole((d.Bears.Value + 5) / 10));
                        }
                        break;
                    case DistrictLayer.Crime:
                        Line("reallife_dv_card_crime", "{count}", Whole(d.Crimes), "{days}", Whole(DistrictRules.CrimeDays));
                        break;
                    case DistrictLayer.Standing:
                        if (d.HasStanding)
                        {
                            Line("reallife_dv_card_opinion", "{opinion}", Whole(d.Opinion));
                            Line("reallife_dv_card_known", "{known}", Whole(d.Known));
                        }
                        break;
                    case DistrictLayer.Supply:
                        if (d.HasSupply && d.Accounts > 0)
                        {
                            Line(d.Cut > 0 ? "reallife_dv_card_supply_cut" : "reallife_dv_card_supply", "{supplied}", Whole(d.Supplied), "{accounts}", Whole(d.Accounts), "{cut}", Whole(d.Cut));
                        }
                        else if (d.HasSupply)
                        {
                            Line("reallife_dv_card_supply_none");
                        }
                        break;
                    case DistrictLayer.Internet:
                        if (d.HasInternet)
                        {
                            Line(d.Down > 0 ? "reallife_dv_card_internet_down" : "reallife_dv_card_internet", "{providers}", Whole(d.Providers), "{down}", Whole(d.Down));
                            if (d.Fibre == 2)
                            {
                                Line("reallife_dv_card_fibre_open", "{share}", Whole((d.Share + 5) / 10));
                            }
                            else if (d.Fibre == 1)
                            {
                                Line("reallife_dv_card_fibre_dug", "{day}", Whole(d.ReadyDay));
                            }
                        }
                        break;
                    case DistrictLayer.Competition:
                        Line("reallife_dv_card_businesses", "{count}", Whole(d.Businesses), "{mine}", Whole(d.Mine), "{rivals}", Whole(d.Rivals));
                        if (d.Types.Count > 0)
                        {
                            var types = new StringBuilder(64);
                            for (int t = 0; t < d.Types.Count; t++)
                            {
                                if (t > 0)
                                {
                                    types.Append(", ");
                                }
                                types.Append(DistrictGame.TypeName(d.Types[t].Key)).Append(" (").Append(d.Types[t].Value.ToString(CultureInfo.InvariantCulture)).Append(')');
                            }
                            Line("reallife_dv_card_types", "{types}", types.ToString());
                        }
                        break;
                    case DistrictLayer.Council:
                        if (d.Parking > 0f)
                        {
                            Line("reallife_dv_card_parking", "{price}", Money(d.Parking));
                        }
                        if (d.HasCouncil)
                        {
                            if (d.Sites > 0)
                            {
                                Line("reallife_dv_card_sites", "{sites}", Whole(d.Sites));
                            }
                            if (d.Electorate > 0)
                            {
                                Line("reallife_dv_card_electorate", "{voters}", Whole(d.Electorate));
                            }
                            if (!string.IsNullOrEmpty(d.Winner))
                            {
                                Line("reallife_dv_card_result", "{winner}", d.Winner, "{percent}", Whole(d.WinnerPercent));
                            }
                        }
                        break;
                }
            }
        }

        private float MeanDemand()
        {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < _districts.Count; i++)
            {
                if (_districts[i].DemandWeight > 0f)
                {
                    sum += _districts[i].DemandWeight;
                    count++;
                }
            }
            return count > 0 ? sum / count : 0f;
        }

        private void Line(string key, string a = null, string va = null, string b = null, string vb = null, string c = null, string vc = null)
        {
            string words = T(key);
            if (a != null)
            {
                words = words.Replace(a, Bold(va));
            }
            if (b != null)
            {
                words = words.Replace(b, Bold(vb));
            }
            if (c != null)
            {
                words = words.Replace(c, Bold(vc));
            }
            _panel.Line(words);
        }

        private static string Bold(string value)
        {
            return "<b>" + (value ?? string.Empty) + "</b>";
        }

        // A layer's figure as the key and the list write it.
        private static string Figure(DistrictLayer layer, long value)
        {
            switch (layer)
            {
                case DistrictLayer.Rent:
                    return Money(DistrictGame.PerArea(value / 100f)) + " / " + DistrictGame.AreaUnit();
                case DistrictLayer.Property:
                    return Money(DistrictGame.PerArea(value)) + " / " + DistrictGame.AreaUnit();
                case DistrictLayer.Footfall:
                    return Whole((int)((value + 5) / 10));
                case DistrictLayer.Supply:
                    return Whole((int)value) + "%";
                case DistrictLayer.Council:
                    return Money(value / 100f);
                default:
                    return Whole((int)value);
            }
        }

        // A band's range in the key: "under 88", "88 to 95", "112 or more".
        private static string Range(DistrictLayer layer, int[] edges, int band)
        {
            int? from, to;
            DistrictRules.Range(edges, band, out from, out to);
            if (!from.HasValue && to.HasValue && to.Value <= 0)
            {
                return Figure(layer, 0);   // (nothing at all: "0", not "under 1")
            }
            if (layer == DistrictLayer.Supply && from.HasValue && !to.HasValue)
            {
                return Figure(layer, from.Value);   // (the top of a share: "100%", not "100% or more")
            }
            if (!from.HasValue && to.HasValue)
            {
                return T("reallife_dv_range_under").Replace("{to}", Figure(layer, to.Value + 1L));
            }
            if (from.HasValue && !to.HasValue)
            {
                return T("reallife_dv_range_over").Replace("{from}", Figure(layer, from.Value));
            }
            if (from.HasValue && to.Value == from.Value)
            {
                return Figure(layer, from.Value);
            }
            return from.HasValue ? T("reallife_dv_range_between").Replace("{from}", Figure(layer, from.Value)).Replace("{to}", Figure(layer, to.Value)) : string.Empty;
        }

        private static string Money(float amount)
        {
            return amount >= 100f ? Mathf.Round(amount).ToCurrencyFormat().Replace(".00", string.Empty) : amount.ToCurrencyFormat();
        }

        private static string Whole(int value)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture);
        }

        private static string T(string key)
        {
            return key.GetLocalization();
        }
    }
}
