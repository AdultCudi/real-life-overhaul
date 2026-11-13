using System;
using RealLifeOverhaul.Construction;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class BuildFace
    {
        // ------------------------------------------------------------------ the tool panel: what BuildMode's sheet calls, as the model's lines

        // A colour BuildMode writes a line in, as the tone the kit writes it in.
        private static UiRole Tone(Color c)
        {
            if (Same(c, BuildModeBar.Red))
            {
                return UiRole.DangerText;
            }
            if (Same(c, BuildModeBar.Amber))
            {
                return UiRole.WarnText;
            }
            if (Same(c, BuildModeBar.Good))
            {
                return UiRole.OkText;
            }
            return Same(c, BuildModeBar.Soft) || Same(c, BuildModeBar.Muted) ? UiRole.Text2 : UiRole.Text;
        }

        private static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;

        private sealed class SheetWriter : IBuildSheet
        {
            private readonly BuildFace _f;

            public SheetWriter(BuildFace face)
            {
                _f = face;
            }

            private ref UiBuildLine Next(UiBuildLineKind kind)
            {
                UiBuildModel m = _f._m;
                if (m.LineCount == m.Lines.Length)
                {
                    Array.Resize(ref m.Lines, m.Lines.Length * 2);   // a panel that grows is scrolled, never cut
                }
                int i = m.LineCount;
                m.Lines[i] = new UiBuildLine { Kind = kind, Rgb = -1, Chosen = -1, Tone = UiRole.Text2 };
                m.LineCount = i + 1;
                return ref m.Lines[i];
            }

            public void Begin(string title, string count)
            {
                _f._m.SheetTitle = title;
                _f._m.SheetCount = count;
                _f._m.LineCount = 0;
            }

            public void Line(string text, Color colour)
            {
                ref UiBuildLine l = ref Next(UiBuildLineKind.Text);
                l.Label = text;
                l.Tone = Tone(colour);
            }

            public void Fact(string name, string value, Color colour)
            {
                ref UiBuildLine l = ref Next(UiBuildLineKind.Fact);
                l.Label = name;
                l.Value = value;
                l.Tone = Tone(colour) == UiRole.Text2 ? UiRole.Text : Tone(colour);
            }

            public void Swatch(int id, Color colour, string name, string status, Color statusColour, bool chosen, bool usable)
            {
                ref UiBuildLine l = ref Next(UiBuildLineKind.Zone);
                l.Id = id;
                l.Rgb = colour.a <= 0.01f ? -1 : Rgb(colour);
                l.Label = name;
                l.Value = status;
                l.Tone = Tone(statusColour);
                l.On = chosen;
                l.Usable = usable;
            }

            public void Buttons(int mainId, string main, bool mainUsable, int sideId, string side, bool sideUsable)
            {
                ref UiBuildLine l = ref Next(UiBuildLineKind.Buttons);
                l.Id = mainId;
                l.Id2 = sideId;
                l.Label = mainId != 0 ? main : null;
                l.Value = sideId != 0 ? side : null;
                l.Usable = mainUsable;
                l.Usable2 = sideUsable;
            }

            public void Choices(int firstId, string[] names, int chosen, bool usable)
            {
                ref UiBuildLine l = ref Next(UiBuildLineKind.Choices);
                int n = 0;
                while (n < names.Length && names[n] != null)
                {
                    n++;
                }
                var shown = new string[n];
                Array.Copy(names, shown, n);
                l.Names = shown;
                l.Id = firstId;
                l.Chosen = chosen;
                l.Usable = usable;
            }

            public void Site(int id, string name, string fits, bool mine)
            {
                ref UiBuildLine l = ref Next(UiBuildLineKind.Site);
                l.Id = id;
                l.Label = name;
                l.Value = fits;
                l.On = mine;
                l.Usable = true;
            }

            public void End()
            {
                _f._dirty = true;
            }
        }

        // ------------------------------------------------------------------ the legend

        private sealed class LegendWriter : IBuildLegend
        {
            private readonly BuildFace _f;

            public LegendWriter(BuildFace face)
            {
                _f = face;
            }

            public void Begin() => _f._m.KeyCount = 0;

            public void Add(Color fill, Color edge, bool edgeOnly, string text)
            {
                UiBuildModel m = _f._m;
                if (m.KeyCount >= m.Keys.Length)
                {
                    return;
                }
                m.Keys[m.KeyCount++] = new UiBuildKey { FillRgb = edgeOnly ? -1 : Rgb(fill), EdgeRgb = Rgb(edge), Words = text };
            }

            public void End(string title, string more)
            {
                _f._m.LegendTitle = title;
                // The entries past those shown come as a list in the tooltip's words: one quiet line says them; a view's own tip is already under its name.
                _f._m.LegendMore = !string.IsNullOrEmpty(more) && more.IndexOf('\n') >= 0 ? more.Replace("\n", ", ") : null;
                _f._dirty = true;
            }
        }

        // ------------------------------------------------------------------ IBuildCatalogue

        // The window is the face's own part: shown while the model holds one; destroying the window shuts it, never the face.
        bool IBuildCatalogue.Alive => !_gone;

        bool IBuildCatalogue.Shown => _m.Catalogue != null;

        void IBuildCatalogue.Destroy() => ((IBuildCatalogue)this).Show(false);

        void IBuildCatalogue.Show(bool shown, float toolbarTop) => ((IBuildCatalogue)this).Show(shown);

        void IBuildCatalogue.Show(bool shown)
        {
            if (shown == (_m.Catalogue != null))
            {
                return;
            }
            if (shown)
            {
                _m.Catalogue = new UiBuildCatalogueModel
                {
                    SearchPlaceholder = UiText.T("reallife_ui_build_search"), FavouritesLabel = UiText.T("reallife_ui_build_favourites"),
                    FavouriteLabel = UiText.T("reallife_ui_build_favourite"), WantedLabel = UiText.T("reallife_ui_build_wanted"),
                    PriceLabel = UiText.T("reallife_ui_build_price"), SizeLabel = UiText.T("reallife_ui_build_size"), BuildTimeLabel = UiText.T("reallife_ui_build_time"),
                    RentLabel = UiText.T("reallife_ui_build_rent"), PlanningLabel = UiText.T("reallife_ui_build_planning"), DemandLabel = UiText.T("reallife_ui_build_demand"),
                    PlaceLabel = UiText.T("reallife_bld_cat_place"), BackLabel = UiText.T("reallife_bld_cat_back"), NoneLabel = UiText.T("reallife_ui_build_none"),
                    WeeksWords = UiText.T("reallife_ui_build_weeks")
                };
            }
            else
            {
                _m.Catalogue = null;
            }
            _dirty = true;
        }

        void IBuildCatalogue.Uses(int[] counts, int use)
        {
            Array.Copy(counts, _counts, Math.Min(counts.Length, _counts.Length));
            _use = use;
        }

        string IBuildCatalogue.Heading => _m.Catalogue != null ? _m.Catalogue.Context : string.Empty;

        int IBuildCatalogue.CardsShown => _m.Catalogue != null ? UiBuildPanels.Shown(_m.Catalogue) : 0;

        float IBuildCatalogue.Scale => 1f;

        string IBuildCatalogue.Overlap(float toolbarTop) => null;

        void IBuildCatalogue.Begin(string context, string[] filterNames, int chosen)
        {
            UiBuildCatalogueModel c = _m.Catalogue;
            if (c == null)
            {
                return;
            }
            c.Context = context;
            if (c.Tabs.Length != _counts.Length)
            {
                c.Tabs = new string[_counts.Length];
            }
            for (int u = 0; u < _counts.Length; u++)
            {
                c.Tabs[u] = ConstructionText.T(BuildCatalogue.UseKeys[u]) + " " + _counts[u];
            }
            c.Tab = _use;
            if (c.Filters.Length != filterNames.Length)
            {
                c.Filters = new string[filterNames.Length];
            }
            Array.Copy(filterNames, c.Filters, filterNames.Length);
            c.Filter = chosen;
            c.PickLine = ConstructionText.T("reallife_bld_cat_pick");
            c.CardCount = 0;
        }

        void IBuildCatalogue.Add(ref CatalogueCard card)
        {
            UiBuildCatalogueModel c = _m.Catalogue;
            if (c == null || c.CardCount >= c.Cards.Length)
            {
                return;
            }
            c.Cards[c.CardCount++] = new UiBuildCard
            {
                Index = card.Index, Name = card.Name, Kind = card.Kind, Size = card.Size, Cost = card.Cost, Usable = card.Usable, Chosen = card.Chosen, Wanted = card.Wanted,
                Favourite = card.Favourite, Picture = "build:use:" + card.Use, Planning = card.Planning, PlanningOk = card.PlanningOk, Floors = card.Floors, Width = card.Width,
                Depth = card.Depth, Weeks = card.Weeks, Demand = card.Demand, WhyNot = card.WhyNot,
                Rent = card.RentMonth > 0L ? UiText.Fill("reallife_ui_build_rent_month", "amount", ConstructionText.Money(card.RentMonth)) : null
            };
        }

        void IBuildCatalogue.End(string summary, bool placeUsable, bool top)
        {
            UiBuildCatalogueModel c = _m.Catalogue;
            if (c == null)
            {
                return;
            }
            c.SummaryLine = summary;
            c.PlaceUsable = placeUsable;
            if (top)
            {
                c.Scroll = 0f;
            }
            _dirty = true;
        }

        // ------------------------------------------------------------------ IBuildTour

        public bool Running => _tourStep != TourStep.Done;

        public TourStep Step => _tourStep;

        public void Start() => Go(TourStep.HintBar);

        public void Next() => Go(BuildModeHintRules.TourNext(_tourStep, false));

        public void Skip() => Go(TourStep.Done);

        void IBuildTour.Hide()
        {
            _tourStep = TourStep.Done;
            _m.Tour = null;
            _dirty = true;
        }

        public string Check() => null;

        private void Go(TourStep step)
        {
            _tourStep = step;
            if (step == TourStep.Done)
            {
                _m.Tour = null;
                BuildModeTour.Seen = true;
                _dirty = true;
                return;
            }
            int i = (int)step;
            string more = BuildModeHintRules.TourMore[i];
            _m.Tour = new UiBuildTourModel
            {
                Step = i,
                Count = ConstructionText.Fill("reallife_bld_tour_count", "number", ConstructionText.N(i + 1), "count", ConstructionText.N(BuildModeHintRules.TourTitles.Length)),
                Title = ConstructionText.T(BuildModeHintRules.TourTitles[i]),
                Body = ConstructionText.T(BuildModeHintRules.TourBodies[i]) + (more == null ? string.Empty : " " + ConstructionText.T(more)),
                Next = ConstructionText.T(step == TourStep.Demand ? "reallife_bld_tour_done" : "reallife_bld_tour_next"),
                Skip = ConstructionText.T("reallife_bld_tour_skip")
            };
            _dirty = true;
        }
    }
}
