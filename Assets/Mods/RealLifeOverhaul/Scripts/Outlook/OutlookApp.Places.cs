using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// Part of the Economy app: under the districts, the player's businesses against what their districts bear and their buildings at
    /// today's value, which the Finance app's page for the city used to list.
    /// </summary>
    internal sealed partial class OutlookApp
    {
        private const float PlaceRow = 108f;

        // Both lists under the district cards; returns the page's height with them.
        private float Places(float y, float width)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            int shops = _r.Businesses.Count, estates = _r.Estates.Count;
            if (shops == 0 && estates == 0)
            {
                return y;
            }
            y += 16f;
            Heading(page, OutlookText.T("reallife_econapp_places_title"), Margin, y, width);
            string note = OutlookText.T("reallife_econapp_places_note");
            page.Text(note, Margin, y + 54f, width, 90f, SizeBody, p.Muted, TextAlignmentOptions.TopLeft, false, true);
            y += 54f + Mathf.Max(40f, page.Fit(note, SizeBody, width)) + 24f;
            float shopsH = shops > 0 ? PlacesHeight(shops, _r.MoreBusinesses) : 0f;
            float estatesH = estates > 0 ? PlacesHeight(estates, _r.MoreEstates) : 0f;
            if (shops > 0 && estates > 0 && Columns(width, 1360f, 2) == 2)
            {
                float cell = CellWidth(width, 2), height = Mathf.Max(shopsH, estatesH);
                PlacesCard(Margin, y, cell, height, false);
                PlacesCard(Margin + cell + Gap, y, cell, height, true);
                return y + height + Gap;
            }
            if (shops > 0)
            {
                PlacesCard(Margin, y, width, shopsH, false);
                y += shopsH + Gap;
            }
            if (estates > 0)
            {
                PlacesCard(Margin, y, width, estatesH, true);
                y += estatesH + Gap;
            }
            return y;
        }

        private static float PlacesHeight(int rows, int more)
        {
            return Pad * 2f + 62f + rows * PlaceRow + (more > 0 ? 48f : 0f);
        }

        private void PlacesCard(float x, float y, float w, float h, bool buildings)
        {
            OutlookPage page = _body;
            OutlookPalette p = page.Palette;
            List<OutlookPlace> list = buildings ? _r.Estates : _r.Businesses;
            page.Card(x, y, w, h, p.Card, Radius);
            Heading(page, OutlookText.T(buildings ? "reallife_econapp_places_estates" : "reallife_econapp_places_shops"), x + Pad, y + Pad - 6f, w - Pad * 2f);
            float inner = w - Pad * 2f, row = y + Pad + 62f;
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    page.Box(x + Pad, row - 4f, inner, 2f, p.Grid, 1f);
                }
                if (buildings)
                {
                    EstateRow(page, p, list[i], x + Pad, row, inner);
                }
                else
                {
                    ShopRow(page, p, list[i], x + Pad, row, inner);
                }
                row += PlaceRow;
            }
            int more = buildings ? _r.MoreEstates : _r.MoreBusinesses;
            if (more > 0)
            {
                page.Text(OutlookText.T("reallife_econapp_places_more", "n", more.ToString(Inv)), x + Pad, row + 8f, inner, 40f, SizeSmall, p.Muted, TextAlignmentOptions.TopLeft);
            }
        }

        // The place's name, the colour of its district beside it and the district's name under it.
        private void PlaceName(OutlookPage page, OutlookPalette p, OutlookPlace place, float x, float y, float w)
        {
            bool district = !string.IsNullOrEmpty(place.District);
            if (district)
            {
                page.Pill(x, y + 18f, 20f, 20f, OutlookTheme.Visible(place.Colour, p.Card, p.Dark));
            }
            page.Text(place.Name, x + 34f, y + 4f, w - 34f, 46f, 32f, p.Text, TextAlignmentOptions.MidlineLeft, true);
            if (district)
            {
                page.Text(OutlookText.Words.District(place.District), x + 34f, y + 54f, w - 34f, 36f, SizeSmall, p.Muted, TextAlignmentOptions.TopLeft);
            }
        }

        // A business: the customers its district, the economy and its prices add or take, and whether its prices suit its district.
        private void ShopRow(OutlookPage page, OutlookPalette p, OutlookPlace place, float x, float y, float w)
        {
            PlaceName(page, p, place, x, y, w * 0.40f);
            string customers = !place.Known ? OutlookText.T("reallife_macro_shop_no_figure")
                : place.Customers == 0 ? OutlookText.T("reallife_macro_shop_none")
                : OutlookText.T("reallife_macro_shop_customers", "percent", OutlookWords.Whole(place.Customers));
            int ink = !place.Known || place.Customers == 0 ? p.Muted : place.Customers > 0 ? p.Good : p.Bad;
            page.Text(customers, x + w * 0.42f, y + 10f, w * 0.26f, 80f, SizeSmall, ink, TextAlignmentOptions.MidlineLeft, ink != p.Muted, true);
            if (place.Prices == 0)
            {
                return;
            }
            bool over = place.Prices == 2;
            string verdict = OutlookText.T(over ? "reallife_macro_shop_over" : "reallife_macro_shop_suit");
            float pill = Mathf.Min(w * 0.30f, page.Measure(verdict, SizeSmall, true) + 44f);
            page.Pill(x + w - pill, y + 24f, pill, 52f, over ? p.BadSoft : p.GoodSoft);
            page.Text(verdict, x + w - pill + 22f, y + 24f, pill - 44f, 52f, SizeSmall, over ? p.Bad : p.Good, TextAlignmentOptions.Midline, true);
        }

        // A building: the game's value, what it is worth today, and the difference.
        private void EstateRow(OutlookPage page, OutlookPalette p, OutlookPlace place, float x, float y, float w)
        {
            PlaceName(page, p, place, x, y, w * 0.40f);
            page.Text(OutlookText.T("reallife_econapp_places_game_value", "amount", OutlookText.Money(place.Value)), x + w * 0.42f, y + 8f, w * 0.30f, 36f,
                SizeSmall, p.Muted, TextAlignmentOptions.MidlineLeft);
            page.Text(OutlookText.T("reallife_macro_property_today", "amount", OutlookText.Money(place.Today)), x + w * 0.42f, y + 48f, w * 0.30f, 44f,
                SizeBody, p.Text, TextAlignmentOptions.MidlineLeft, true);
            int move = place.Factor - 1000;
            string change = OutlookWords.Factor(place.Factor);
            int ink = move > 0 ? p.Good : move < 0 ? p.Bad : p.Muted, fill = move > 0 ? p.GoodSoft : move < 0 ? p.BadSoft : p.Flat;
            float pill = page.Measure(change, SizeSmall, true) + 44f;
            page.Pill(x + w - pill, y + 24f, pill, 52f, fill);
            page.Text(change, x + w - pill, y + 24f, pill, 52f, SizeSmall, ink, TextAlignmentOptions.Midline, true);
        }
    }
}
