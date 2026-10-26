using System;
using System.Globalization;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Let it grow in build mode's Place panel (plans/growables.md section 3): beside Choose a building, Let it grow opens the three
    /// budget tiers (basic, standard, premium) as a row of the panel, each with the builder's estimate for the squares picked, the
    /// rent level it asks and its build quality; Let it grow for the price asks the game's question box, then the builder's design is
    /// placed and its plans asked (ConstructionDirector.LetItGrow). Back returns to the panel. COST: nothing per frame; the estimate
    /// worked once a tier is chosen.
    /// </summary>
    public sealed partial class BuildMode
    {
        private const int RowGrowGo = RowGrow + 1;
        private static readonly string[] ShapeTips = { "reallife_grow_shape_fill_tip", "reallife_grow_shape_rect_tip", "reallife_grow_shape_brush_tip", "reallife_grow_shape_unzone_tip" };
        private readonly string[] _tierNames = new string[BuildModeSheet.ChoicesMost];
        private bool _growPanel;                  // the tiers shown in place of the designs' buttons
        private GrowTier _tier = GrowTier.Standard;
        private long _growCost;
        private string _growDesign;

        /// <summary>True while the Place panel shows Let it grow's tiers, for the tests.</summary>
        public bool GrowShown => _growPanel;

        // The Place panel's Let it grow part: the tiers, the chosen one's estimate, rent level and quality, and the buttons.
        private void WriteGrow(BuildModeSheet s)
        {
            for (int i = 0; i < (int)GrowTier.Count; i++)
            {
                _tierNames[i] = T(GrowRules.TierKeys[i]);
            }
            _tierNames[(int)GrowTier.Count] = null;
            s.Line(T("reallife_grow_sheet_line"), BuildModeBar.Soft);
            s.Choices(RowTier, _tierNames, (int)_tier, !_state.ViewOnly);
            int t = (int)_tier;
            s.Fact(T("reallife_grow_sheet_cost"), _growCost > 0L ? ConstructionText.Money(_growCost) : "-", BuildModeBar.Text);
            s.Fact(T("reallife_grow_sheet_rent"), ConstructionText.Share(GrowRules.TierRentPermille[t]), BuildModeBar.Text);
            s.Fact(T("reallife_grow_sheet_quality"), GrowRules.TierQuality[t].ToString(CultureInfo.InvariantCulture), BuildModeBar.Text);
            if (_growCost <= 0L)
            {
                s.Line(T("reallife_grow_answer_no_design"), BuildModeBar.Red);
            }
            s.Buttons(RowGrowGo, ConstructionText.Fill("reallife_grow_sheet_go", "amount", _growCost > 0L ? ConstructionText.Money(_growCost) : "-"), _growCost > 0L && !_state.ViewOnly,
                RowGrowBack, T("reallife_grow_sheet_back"), true);
        }

        // The tier's estimate for the squares picked, worked again.
        private void GrowAssess()
        {
            _growCost = _siteWhy == null && _pickMine ? _d.GrowEstimate(_site.FirstColumn, _site.Count, _site.MinRows, _tier, out _growDesign) : 0L;
        }

        // Let it grow's rows; false for a row of another part.
        private bool ActGrow(int id)
        {
            if (id == RowGrow)
            {
                _growPanel = true;
                GrowAssess();
                Write(false);
                return true;
            }
            if (id >= RowTier && id < RowTier + (int)GrowTier.Count)
            {
                _tier = (GrowTier)(id - RowTier);
                GrowAssess();
                Write(false);
                return true;
            }
            if (id == RowGrowBack)
            {
                _growPanel = false;
                Write(false);
                return true;
            }
            if (id != RowGrowGo || _growCost <= 0L || _siteWhy != null || !_pickMine)
            {
                return false;
            }
            CellSite site = _site;
            GrowTier tier = _tier;
            Ask("reallife_grow_confirm", "reallife_grow_confirm_body", _growCost, () =>
            {
                int project = _d.LetItGrow(site.FirstColumn, site.Count, site.MinRows, tier, out string why);
                _lastProject = project;
                _growPanel = false;
                ClearPick();
                Refresh(why);
            });
            return true;
        }

        /// <summary>Let it grow on the squares picked at a tier, as its buttons do but without the question box (for the tests); the project's id or 0, with why.</summary>
        public int GrowForTest(GrowTier tier, out string why)
        {
            why = null;
            if (_siteWhy != null || !_pickMine)
            {
                why = _siteWhy ?? "reallife_bld_hint_place_not_yours";
                return 0;
            }
            _tier = tier;
            GrowAssess();
            int project = _d.LetItGrow(_site.FirstColumn, _site.Count, _site.MinRows, tier, out why);
            _lastProject = project;
            _growPanel = false;
            ClearPick();
            Refresh(null);
            return project;
        }

        /// <summary>The estimate the panel shows for a tier on the squares picked now (for the tests).</summary>
        public long GrowCostForTest(GrowTier tier)
        {
            _tier = tier;
            GrowAssess();
            return _growCost;
        }
    }
}
