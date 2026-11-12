using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The other tech firms' screens (SectorViews: data only) drawn in the Tech app's box under Living population: a firm's
    /// page, a product's market and the data centres' markets. This is the drawing until the mod's own design system draws the
    /// same screens; it holds no rule of its own. Drawn on a refresh, nothing per frame.
    /// </summary>
    public sealed partial class TechApp
    {
        /// <summary>A firm's page under the sector, with its focus and the firm's own buttons below; false when the sector does not run (the firms' own page is drawn then).</summary>
        private bool WriteSectorFirm(InfoBox box, FirmsFile f, FirmPlace firm, int day)
        {
            StudioScreen v = _director.SectorFirmScreen(firm, _studioWords);
            if (v == null)
            {
                return false;
            }
            Firm m = firm.File;
            string place = m.Place;
            box.Title(TechText.Fill(v.TitleKey, v.TitlePairs ?? System.Array.Empty<string>()));
            box.Subtitle(TechText.FirmName(m.K));
            box.Chip(TechText.T(day < m.RegisteredUntil ? "reallife_ibox_chip_registered" : "reallife_ibox_chip_unregistered"), day < m.RegisteredUntil ? CellTone.Green : CellTone.Yellow);
            if (v.Next != null)
            {
                box.Line(Words(v.Next), Tone(v.Next.Tone));
            }
            if (v.Primary != null && v.Primary.Enabled)
            {
                ViewAction a = v.Primary;
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, a.Key, () => SectorAction(a.Act, place, a.Arg));
            }
            for (int s = 0; s < v.Sections.Count; s++)
            {
                DrawSection(box, v.Sections[s]);
            }
            if (_director.StudioRuns(m.K))
            {
                WriteStudioPart(box, firm);
            }
            if (box.HasSelector)
            {
                box.Selector((int)Focus.Count, m.Focus, i => TechText.T("reallife_tech_firm_focus", "focus", TechText.FocusName((Focus)i)), i => Do(() => _director.SetFocus(place, i)));
            }
            bool makes = FirmsCatalogue.MakesProducts[m.Kind] != 0, register = day >= m.RegisteredUntil - 7;
            Transform buttons = box.ButtonRow();
            if (makes && v.Primary?.Act != SectorAct.NewProduct)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_tech_button_new_product", () => SectorAction(SectorAct.NewProduct, place, 0));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_tech_button_rivals", () => Set(() => _picked = RivalsRow + place));
            Transform more = box.ButtonRow();
            if (register)
            {
                InfoBox.Button(more, InfoBox.Positive, "reallife_tech_button_register", () => Do(() => _director.Register(place)));
            }
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, SectorGuideTitle);
            return true;
        }

        private const string SectorGuideTitle = "reallife_meta_title_firms";

        /// <summary>A section of a screen: its title, then its lines with their weight.</summary>
        private void DrawSection(InfoBox box, ViewSection section)
        {
            if (section == null)
            {
                return;
            }
            box.Section(section.TitleKey);
            for (int i = 0; i < section.Lines.Count; i++)
            {
                box.Line(Words(section.Lines[i]), Tone(section.Lines[i].Tone));
            }
        }

        // What a firm's page's button asks: the app's own (a new product, the contracts, close) or the director's.
        private void SectorAction(int act, string place, int arg)
        {
            switch (act)
            {
                case SectorAct.NewProduct:
                    Set(() =>
                    {
                        _picked = NewProductRow + place;
                        _newKind = -1;
                    });
                    return;
                case SectorAct.Contracts:
                    Set(() =>
                    {
                        _tab = TechDirector.TabContracts;
                        _picked = null;
                    });
                    return;
                case SectorAct.Close:
                    ClosePick();
                    return;
                default:
                    Do(() => _director.SectorDo(act, place, arg));
                    return;
            }
        }

        // The products' limits as they stand: the sector's while it runs, else the firms' own.
        private int ProductsPerFirmNow => _director.SectorOn ? SectorCatalogue.ProductsPerFirm : FirmsCatalogue.ProductsPerFirm;

        private int ProductsMostNow => _director.SectorOn ? SectorCatalogue.ProductsMost : FirmsCatalogue.ProductsMost;
    }
}
