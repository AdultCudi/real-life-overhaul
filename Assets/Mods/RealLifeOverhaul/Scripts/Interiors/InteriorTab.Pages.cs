using System;
using Helpers;
using Localizor;
using RealLifeOverhaul.BizPace;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>The Interior tab's pages: the scores, what each figure does, the items, today's customers and the game's rules.</summary>
    public static partial class InteriorTab
    {
        private static readonly int BizWrite = HotSpots.Slot("Interiors: InteriorTab.Write");

        /// <summary>
        /// The page switched on: written for the business BizMan shows now, unless the boxes hold it already, written for this business and this page in this
        /// showing of the business view and in this language (the game is paused with its menu open, so what the page says cannot have moved). Flipping back to
        /// the tab then costs nothing; a click that changed what it says writes it afresh (<see cref="Rewrite"/>).
        /// </summary>
        internal static void Write()
        {
            if (_business == null || _left == null || _right == null)
            {
                return;
            }
            using var timed = BizPaceWatch.Time(BizWrite);   // a step of BizMan's tab change (BizPace)
            if (!Written())
            {
                Rewrite();
            }
        }

        // True if the boxes hold the page of the business shown, as it was written in this showing of the view, this language and this page.
        private static bool Written()
        {
            if (_wroteShop == null || _wroteSession != Session || _wrotePage != _shown || !string.Equals(_wroteLocale, LocalizorManager.LoadedLocale, StringComparison.Ordinal))
            {
                return false;
            }
            BuildingRegistration reg = _business.buildingRegistration;
            return ReferenceEquals(reg, _wroteReg) && ReferenceEquals(InteriorDirector.At(reg), _wroteShop);
        }

        /// <summary>The page written afresh for the business BizMan shows now.</summary>
        internal static void Rewrite()
        {
            if (_business == null || _left == null || _right == null)
            {
                return;
            }
            BuildingRegistration reg = _business.buildingRegistration;
            InteriorShop s = InteriorDirector.At(reg);
            _wroteShop = null;
            _left.Clear();
            _right.Clear();
            if (s == null)
            {
                _left.Title(BusinessGame.Name(reg));
                _left.Line("reallife_interior_none".GetLocalization());
                Meta.Guide.Point(_left, GuideTitle);
                _left.Done();
                _right.Done();
                return;
            }
            WriteScores(s);
            _left.Done();
            WriteRight(s, reg);
            _wroteReg = reg;
            _wroteShop = s;
            _wroteSession = Session;
            _wroteLocale = LocalizorManager.LoadedLocale;
            if (!_fitLogged && Meta.Dev.Verbose)
            {
                _fitLogged = true;   // once a session, and only when the line is read
                LogFit();
            }
        }

        // How the columns fit (they scroll when they do not). Fit works the layout out at once, twice: a step of BizMan never asks for it unless the line is read.
        private static void LogFit()
        {
            InteriorDirector.Logger?.Info("Interiors: the Interior tab's columns: " + _left.Fit() + "; " + _right.Fit() + ".");
        }

        // The page selector, then the page chosen.
        private static void WriteRight(InteriorShop s, BuildingRegistration reg)
        {
            _right.Clear();
            _right.Selector(Pages, _shown, InteriorText.Page, page =>
            {
                _shown = page;
                InteriorShop now = InteriorDirector.At(reg);
                if (now != null)
                {
                    WriteRight(now, reg);
                }
            });
            switch (InteriorRules.Clamp(_shown, 0, Pages - 1))
            {
                case 1: WriteItems(s); break;
                case 2: WriteCustomers(s, reg); break;
                case 3: WriteRules(reg); break;
                default: WriteEffects(s, reg); break;
            }
            Meta.SettingsApp.Link(_right, "interiors");
            Meta.Guide.Point(_right, GuideTitle);
            _right.Done();
            _wrotePage = _shown;
            if (_rightScroll != null)
            {
                _rightScroll.verticalNormalizedPosition = 1f;   // a new page starts at its top
            }
        }

        // Every placed item that counts, by category, and each category's total against its share.
        private static void WriteItems(InteriorShop s)
        {
            InfoBox b = _right;
            b.Title(InteriorText.Page(1));
            if (s.Placed.Count == 0)
            {
                b.Note("reallife_interior_items_none".GetLocalization());
            }
            for (int c = 0; c < InteriorRules.Categories; c++)
            {
                Sorted.Clear();
                for (int i = 0; i < s.Placed.Count; i++)
                {
                    if ((int)s.Placed[i].Category == c)
                    {
                        Sorted.Add(s.Placed[i]);
                    }
                }
                if (Sorted.Count == 0)
                {
                    continue;
                }
                Sorted.Sort((x, y) => y.Points != x.Points ? y.Points.CompareTo(x.Points) : string.CompareOrdinal(x.Name, y.Name));
                int cap = (int)((long)s.Target * InteriorRules.CapShare[c] / 1000);
                b.HeadlineWords(InteriorText.T("reallife_interior_items_head", "category", InteriorText.Category(c), "points", InteriorText.Tenths(s.Capped[c]), "cap", InteriorText.Tenths(cap)));
                for (int i = 0; i < Sorted.Count; i++)
                {
                    b.Line(InteriorText.T("reallife_interior_items_line", "item", InteriorGame.ItemName(Sorted[i].Name), "copies", InteriorText.N(Sorted[i].Copies),
                        "points", InteriorText.Tenths(Sorted[i].Points)));
                }
            }
            Sorted.Clear();
            b.Note(InteriorText.T("reallife_interior_items_note", "repeat", InteriorText.N(InteriorRules.RepeatShare / 10), "most", InteriorText.N(InteriorRules.RepeatMost),
                "target", InteriorText.Tenths(s.Target)));
        }

        private static void WriteCustomers(InteriorShop s, BuildingRegistration reg)
        {
            InfoBox b = _right;
            b.Title(InteriorText.Page(2));
            InteriorDirector d = InteriorDirector.Instance;
            CustomerCount c;
            if (d == null || !InteriorGame.Count(reg, d.Ours, out c))
            {
                b.Note("reallife_interior_cust_none".GetLocalization());
                return;
            }
            CustomerSplit split = InteriorRules.Split(c.Drawn, c.Booked, c.OtherMod, c.BaseMille, c.Traffic, c.Own, c.Written, BusinessCatalogue.ClampInterior(s.Footfall), c.OtherPercent);
            b.Line(InteriorText.T("reallife_interior_cust_total", "total", InteriorText.N(split.Total), "served", InteriorText.N(c.Served)));
            b.Line(InteriorText.T("reallife_interior_cust_game", "count", InteriorText.N(split.Game)));
            b.Line(InteriorText.T("reallife_interior_cust_marketing", "count", InteriorText.N(split.Marketing)));
            b.Line(InteriorText.T("reallife_interior_cust_interiors", "count", InteriorText.SignedN(split.Interiors)));
            b.Line(InteriorText.T("reallife_interior_cust_others", "count", InteriorText.SignedN(split.Others)));
            if (OtherMods.MeaningfulAmbience.Loaded)
            {
                b.Line(InteriorText.T("reallife_interior_cust_ambience", "mod", OtherMods.MeaningfulAmbience.Name, "count", InteriorText.N(split.OtherMod)));
            }
            if (c.Booked > 0)
            {
                b.Line(InteriorText.T("reallife_interior_cust_booked", "count", InteriorText.N(c.Booked)));
            }
            if (s.Stop != SaleStop.None)
            {
                b.Line(InteriorText.T("reallife_interior_line_paused", "reason", InteriorText.Stop(s.Stop)), CellTone.Red);
            }
            b.Note(InteriorText.T("reallife_interior_cust_note", "traffic", InteriorText.N(c.Traffic), "marketing", InteriorText.N(c.Marketing), "figure", InteriorText.N(c.Written)));
        }

        private static void WriteRules(BuildingRegistration reg)
        {
            InfoBox b = _right;
            b.Title(InteriorText.Page(3));
            InteriorGame.Rules(reg, GameRules);
            int demands = 0, needs = 0;
            b.Headline("reallife_interior_rules_demands_head");
            for (int i = 0; i < GameRules.Count; i++)
            {
                if (GameRules[i].Demand)
                {
                    demands++;
                    Rule(b, GameRules[i]);
                }
            }
            if (demands == 0)
            {
                b.Note("reallife_interior_rules_none".GetLocalization());
            }
            b.Headline("reallife_interior_rules_needs_head");
            for (int i = 0; i < GameRules.Count; i++)
            {
                if (!GameRules[i].Demand)
                {
                    needs++;
                    Rule(b, GameRules[i]);
                }
            }
            if (needs == 0)
            {
                b.Note("reallife_interior_rules_none".GetLocalization());
            }
            b.Note("reallife_interior_rules_note".GetLocalization());
            GameRules.Clear();
        }

        private static void Rule(InfoBox b, GameRule rule)
        {
            string state = (rule.Met ? "reallife_interior_rules_met" : "reallife_interior_rules_missing").GetLocalization();
            if (rule.Demand)
            {
                b.Line(InteriorText.T("reallife_interior_rules_demand", "name", InteriorText.Demand(rule), "state", state, "chance", InteriorText.N(Math.Max(0, rule.Chance))));
                string detail = InteriorText.DemandDetail(rule);
                if (detail != null)
                {
                    b.Line(detail);
                }
            }
            else
            {
                b.Line(InteriorText.T(rule.Need > 1 ? "reallife_interior_rules_need_count" : "reallife_interior_rules_need", "name", InteriorGame.ItemName(rule.Kind), "state", state,
                    "have", InteriorText.N(Math.Max(0, rule.Have)), "need", InteriorText.N(rule.Need)));
            }
            if (rule.Placed.Count > 0)
            {
                b.Line(InteriorText.T("reallife_interior_rules_by", "items", string.Join(", ", rule.Placed)));
            }
            else if (rule.Examples.Count > 0)
            {
                b.Line(InteriorText.T("reallife_interior_rules_add", "items", string.Join(", ", rule.Examples)));
            }
        }

        private static void WriteScores(InteriorShop s)
        {
            InfoBox b = _left;
            b.Title(s.Name);
            if (!OtherMods.MeaningfulInteriors.Loaded && s.Fitting && s.Kind == InteriorKind.Shop && InteriorDirector.StandardOn)
            {
                b.Line(InteriorText.T("reallife_interior_line_fitting", "day", InteriorText.N(s.StandardFrom)));
            }
            b.Headline("reallife_interior_head_place");
            b.Line(InteriorText.T("reallife_interior_line_overall", "overall", InteriorText.N(s.Overall)));
            if (OtherMods.MeaningfulInteriors.Loaded)
            {
                b.Line(InteriorText.T("reallife_interior_line_minimum_other", "minimum", InteriorText.N(s.GameMinimum), "mod", OtherMods.MeaningfulInteriors.Name));
            }
            else
            {
                b.Line(InteriorText.T("reallife_interior_line_standard", "standard", InteriorText.N(s.Standard), "district", Districts.DistrictGame.Name(s.District)));
            }
            b.Line(InteriorText.T(s.InteriorNow != s.Interior ? "reallife_interior_line_interior_worn" : "reallife_interior_line_interior",
                "score", InteriorText.N(s.Interior), "now", InteriorText.N(s.InteriorNow)));
            b.Line(InteriorText.T(s.Decor != s.DecorRaw ? "reallife_interior_line_decor_worn" : "reallife_interior_line_decor",
                "score", InteriorText.N(s.DecorRaw), "now", InteriorText.N(s.Decor)));
            b.Headline("reallife_interior_head_decor");
            for (int i = 0; i < InteriorRules.Categories; i++)
            {
                b.Line(InteriorText.T("reallife_interior_line_category", "category", InteriorText.Category(i), "filled", InteriorText.N(InteriorText.Filled(s, i)),
                    "items", InteriorText.N(s.Items[i])));
            }
            b.Line(InteriorText.T("reallife_interior_line_area", "sqm", InteriorText.N(s.Sqm)));
            b.Headline("reallife_interior_head_condition");
            b.Line(InteriorText.T(InteriorDirector.Parts.WearScale > 0 ? "reallife_interior_line_wear" : "reallife_interior_line_wear_off", "wear", InteriorText.N(s.Wear / 10)));
            b.Line(InteriorText.T("reallife_interior_line_clean", "clean", InteriorText.N(s.Clean)));
            Meta.Guide.Point(b, GuideTitle);
        }

        // What the interior does to a shop's customers and takings today, each figure with its reason.
        private static void WriteNow(InfoBox b, InteriorShop s)
        {
            InteriorParts parts = InteriorDirector.Parts;
            string district = Districts.DistrictGame.Name(s.District);
            if (InteriorDirector.StandardOn)
            {
                string standard = InteriorText.N(s.Standard), overall = InteriorText.N(s.Overall);
                if (s.Fitting && s.Overall < s.Standard)
                {
                    b.Line(InteriorText.T("reallife_interior_now_fitting", "district", district, "standard", standard, "overall", overall, "day", InteriorText.N(s.StandardFrom)));
                }
                else if (s.StandardFootfall < 0)
                {
                    b.Line(InteriorText.T("reallife_interior_now_short", "district", district, "standard", standard, "overall", overall, "percent", InteriorText.N(-s.StandardFootfall)));
                }
                else if (s.StandardFootfall > 0)
                {
                    b.Line(InteriorText.T("reallife_interior_now_over", "district", district, "standard", standard, "overall", overall, "percent", InteriorText.N(s.StandardFootfall)));
                }
                else
                {
                    b.Line(InteriorText.T("reallife_interior_now_level", "district", district, "standard", standard, "overall", overall));
                }
                if (s.StandardFull < 0 && s.StandardFootfall > InteriorRules.Scale(s.StandardFull, s.Ramp))
                {
                    b.Line(InteriorText.T("reallife_interior_now_giveway", "dip", InteriorText.N(-Math.Min(0, s.IssueDip)), "bound", InteriorText.N(s.Bound)));
                }
            }
            if (InteriorDirector.DecorOn)
            {
                b.Line(InteriorText.T("reallife_interior_now_decor", "decor", InteriorText.N(s.Decor), "percent", InteriorText.N(s.DecorFootfall)));
            }
            if (s.Ramp < 1000 && (s.StandardFull != 0 || s.DecorFull != 0 || s.Bears != 0 || s.Basket != 0))
            {
                b.Line(InteriorText.T("reallife_interior_now_phase", "share", InteriorText.N(s.Ramp / 10), "day", InteriorText.N(InteriorRules.FullFrom(s.PhaseFrom))));
            }
            if (parts.PriceScale <= 0)
            {
                b.Line("reallife_interior_line_prices_off".GetLocalization());
            }
            else
            {
                b.Line(s.Bears > 0 ? InteriorText.T("reallife_interior_now_prices", "bears", InteriorText.Mille(s.Bears)) : "reallife_interior_now_prices_none".GetLocalization());
            }
            b.Line(InteriorText.T("reallife_interior_now_total", "customers", InteriorText.Signed(s.Footfall), "takings", InteriorText.SignedMille(InteriorRules.Takings(s.Footfall, s.Basket))));
        }

        private static void WriteEffects(InteriorShop s, BuildingRegistration reg)
        {
            InfoBox b = _right;
            b.Title(InteriorText.Page(0));
            if (s.Stop != SaleStop.None)
            {
                b.Line(InteriorText.T("reallife_interior_line_paused", "reason", InteriorText.Stop(s.Stop)), CellTone.Red);
            }
            if (s.Kind == InteriorKind.Shop)
            {
                WriteNow(b, s);
                b.Line(InteriorDirector.BasketOn ? InteriorText.T("reallife_interior_line_basket", "share", InteriorText.Mille(s.Basket),
                        "most", InteriorText.Mille(InteriorRules.BasketShare(InteriorRules.BasketFullAt, s.Standard, InteriorDirector.Parts.PriceScale)),
                        "full", InteriorText.N(InteriorRules.BasketFullAt))
                    : OtherMods.MeaningfulInteriors.Loaded && Settings.InteriorsBasket ? InteriorText.T("reallife_interior_line_basket_other", "mod", OtherMods.MeaningfulInteriors.Name)
                    : "reallife_interior_line_basket_off".GetLocalization());
                if (InteriorDirector.StandardOn)
                {
                    b.Line(s.ComplaintChance > 0
                        ? InteriorText.T("reallife_interior_line_complaints", "chance", InteriorText.Mille(s.ComplaintChance))
                        : "reallife_interior_line_complaints_none".GetLocalization());
                }
                if (Settings.InteriorsStanding)
                {
                    b.Line(InteriorText.T("reallife_interior_line_standing", "brand", InteriorText.SignedN(s.BrandPoints), "standing", InteriorText.SignedN(s.StandingPoints)));
                }
            }
            else
            {
                b.Note("reallife_interior_line_office".GetLocalization());
                if (s.OfficeBoost > 0)
                {
                    int standing;
                    int booked = InteriorDirector.Instance != null ? InteriorDirector.Instance.BookedToday(reg, out standing) : 0;
                    b.Line(InteriorText.T("reallife_interior_line_clients", "boost", InteriorText.SignedMille(s.OfficeBoost), "booked", InteriorText.N(booked)));
                }
                else if (OtherMods.MeaningfulAmbience.Loaded)
                {
                    b.Line(InteriorText.T("reallife_interior_line_clients_other", "mod", OtherMods.MeaningfulAmbience.Name));
                }
            }
            if (Settings.InteriorsStaff)
            {
                b.Line(InteriorText.T("reallife_interior_line_comfort", "comfort", InteriorText.N(s.Comfort), "most", InteriorText.N(InteriorRules.ComfortMost)));
                b.Line(InteriorText.T("reallife_interior_line_bids", "bids", InteriorText.Mille(s.OfficeBid)));
                if (s.Kind == InteriorKind.Office)
                {
                    b.Line(InteriorText.T("reallife_interior_line_esteem", "esteem", InteriorText.N(s.OfficeEsteem)));
                }
            }
            if (OtherMods.MeaningfulInteriors.Loaded)
            {
                b.Note(InteriorText.T("reallife_interior_line_other_interiors", "mod", OtherMods.MeaningfulInteriors.Name));
            }
            if (OtherMods.MeaningfulAmbience.Loaded)
            {
                b.Note(InteriorText.T("reallife_interior_line_other_ambience", "mod", OtherMods.MeaningfulAmbience.Name));
            }
            b.Headline("reallife_interior_head_next");
            int next = InteriorRules.Next(s);
            b.Line(InteriorText.Next(next));
            if (s.Kind == InteriorKind.Shop && InteriorDirector.StandardOn && s.Overall < s.Standard && s.InteriorNow < s.Standard)
            {
                b.Note("reallife_interior_next_walls".GetLocalization());
            }
            if (Settings.InteriorsStaff && !s.RestCorner)
            {
                b.Line("reallife_interior_next_rest".GetLocalization());
            }
            if (InteriorDirector.Parts.WearScale > 0)
            {
                if (s.Wear >= InteriorRules.WornFrom)
                {
                    b.Line("reallife_interior_next_worn".GetLocalization());
                }
                if (s.Wear >= InteriorRules.RefreshFrom)
                {
                    Transform row = b.ButtonRow();
                    InfoBox.Button(row, InfoBox.Positive, "reallife_interior_refresh_button", () =>
                    {
                        InteriorDirector d = InteriorDirector.Instance;
                        if (d != null && d.Refresh(reg))
                        {
                            UiClone.Safe(Rewrite);
                        }
                    });
                    b.Line(InteriorText.T("reallife_interior_refresh_cost", "amount", Economy.FinanceText.Money(InteriorGame.RefreshCost(s.Sqm))));
                }
            }
            if (s.RefitNow > 0)
            {
                b.Line(InteriorText.T("reallife_interior_line_refit", "points", InteriorText.N(s.RefitNow)));
            }
            Action<BuildingRegistration> renovate = InteriorBridges.Renovate;
            if (renovate != null && InteriorBridges.Renovates != null && InteriorBridges.Renovates())
            {
                string under = InteriorBridges.Renovation != null ? InteriorBridges.Renovation(reg) : null;
                if (under != null)
                {
                    b.Line(under);
                }
                else
                {
                    InfoBox.Button(b.ButtonRow(), InfoBox.Negative, "reallife_interior_renovate_button", () => renovate(reg));
                    b.Note("reallife_interior_renovate_note".GetLocalization());
                }
            }
        }
    }
}
