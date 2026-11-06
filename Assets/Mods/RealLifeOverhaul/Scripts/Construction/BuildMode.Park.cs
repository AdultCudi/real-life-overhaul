using System;
using System.Globalization;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Car parks in build mode's Place panel (docs/features/construction.md, "Parking lots"; the owner, 27 Oct 2026: "In build mode they
    /// must be able to build parking lots"): a Parking lot button beside Choose a building and Let it grow. It shows what a car park on
    /// the squares picked would be (its bays, its cost, the days to the keys, the street rate, the zoning) and builds it for the price in
    /// the game's question box; the squares of a car park that stands show its panel instead: for the player's own how it is let (public
    /// pay parking or leases), its price an hour inside the band, the offers waiting, and Close the car park; for a builder's the bays the
    /// player may lease there. Back returns to the designs. COST: nothing per frame; the facts worked once a pick changes.
    /// </summary>
    public sealed partial class BuildMode
    {
        private const int RowCarPark = 100, RowCarParkGo = 101, RowCarParkBack = 102, RowParkMode = 110, RowParkLower = 120, RowParkRaise = 121, RowParkAccept = 122, RowParkDecline = 123,
            RowParkClose = 124, RowParkLeaseLess = 125, RowParkLeaseMore = 126, RowParkLease = 127, RowParkEndMine = 128;
        private static readonly string[] ParkModeKeys = { "reallife_park_how_public", "reallife_park_how_lease" };
        private readonly string[] _parkModeNames = new string[BuildModeSheet.ChoicesMost];
        private bool _parkMode;               // the Place tool lays out a car park: its panel in place of the designs' buttons
        private ParkFacts _parkFacts;         // the squares picked as a car park
        private CarParkFile _parkPicked;      // the car park that takes the squares picked, if one does
        private int _parkBays = 2;            // the bays asked for at a car park of the city's

        /// <summary>True while the Place panel shows a car park's panel (the new one's or a standing one's), for the tests.</summary>
        public bool ParkShown => _parkMode || _parkPicked != null;

        /// <summary>The car park the squares picked belong to, for the tests.</summary>
        public CarParkFile ParkPicked => _parkPicked;

        /// <summary>The facts of a car park on the squares picked, for the tests.</summary>
        public ParkFacts ParkFactsNow => _parkFacts;

        // The squares picked read as a car park: the car park that takes them, or what one built there would be.
        private void ParkRead()
        {
            _parkFacts = default(ParkFacts);
            _parkPicked = null;
            if (_picked.Count == 0 || _state.Tool != BuildTool.Place)
            {
                return;
            }
            _parkPicked = _d.ParkOfColumn(_picked[0].Column);
            if (_parkPicked == null && _parkMode && _siteWhy == null && _pickMine)
            {
                _parkFacts = _d.ParkAssess(_site.FirstColumn, _site.Count, _site.MinRows);
            }
            if (_parkPicked != null)
            {
                _parkBays = Math.Max(1, Math.Min(_parkBays, Math.Max(1, _d.ParkLeaseRoom(_parkPicked))));
            }
        }

        // The Place panel's car park part for squares of the player's: what one would be, and the two buttons.
        private void WritePark(BuildModeSheet s)
        {
            ParkFacts f = _parkFacts;
            s.Line(T("reallife_park_sheet_line"), BuildModeBar.Soft);
            if (!f.Ok)
            {
                s.Line(T(f.Why ?? "reallife_park_answer_small"), BuildModeBar.Red);
                if (!string.IsNullOrEmpty(f.Blocker))
                {
                    s.Line(ConstructionText.Fill("reallife_park_sheet_blocker", "what", f.Blocker), BuildModeBar.Soft);
                }
                s.Buttons(0, null, false, RowCarParkBack, T("reallife_park_sheet_back"), true);
                return;
            }
            s.Fact(T("reallife_park_sheet_ground"), ConstructionText.Fill("reallife_park_value_ground", "width", ConstructionText.N(f.Width), "depth", ConstructionText.N(f.Depth)), BuildModeBar.Text);
            s.Fact(T("reallife_park_sheet_bays"), ConstructionText.Fill("reallife_park_value_bays", "bays", ConstructionText.N(f.Bays), "blue", ConstructionText.N(f.Accessible)), BuildModeBar.Text);
            s.Fact(T("reallife_bld_sheet_cost"), ConstructionText.Money(f.Cost), BuildModeBar.Text);
            s.Fact(T("reallife_bld_sheet_days"), ConstructionText.Fill("reallife_bld_sheet_days_value", "days", ConstructionText.N(f.Days)), BuildModeBar.Text);
            s.Fact(T("reallife_park_sheet_rate"), ConstructionText.Fill("reallife_park_value_rate", "price", ConstructionText.Cents(f.Fair)), BuildModeBar.Text);
            s.Line(T(f.Homes ? "reallife_park_sheet_homes" : "reallife_park_sheet_zoned"), BuildModeBar.Soft);
            s.Buttons(RowCarParkGo, ConstructionText.Fill("reallife_park_sheet_go", "amount", ConstructionText.Money(f.Cost)), !_state.ViewOnly, RowCarParkBack, T("reallife_park_sheet_back"), true);
        }

        // The panel of a car park that stands: the player's to run, a builder's to lease bays at.
        private void WriteParkManage(BuildModeSheet s)
        {
            CarParkFile c = _parkPicked;
            s.Line(ConstructionText.Fill("reallife_park_sheet_name", "name", _d.ParkName(c), "owner", ConstructionDirector.ParkOwnerName(c)), BuildModeBar.Text);
            s.Fact(T("reallife_park_sheet_bays"), ConstructionText.N(c.Bays), BuildModeBar.Text);
            s.Fact(T("reallife_park_sheet_now"), _d.ParkNow(c), BuildModeBar.Text);
            if (c.UnderWay)
            {
                s.Line(T("reallife_park_sheet_wait"), BuildModeBar.Soft);
                s.Buttons(0, null, false, RowClear, T("reallife_bld_sheet_clear"), true);
                return;
            }
            bool free = !_state.ViewOnly && !GoingPublic.InSession;
            if (c.Owner == CarParkOwner.Player)
            {
                for (int i = 0; i < ParkModeKeys.Length; i++)
                {
                    _parkModeNames[i] = T(ParkModeKeys[i]);
                }
                _parkModeNames[ParkModeKeys.Length] = null;
                s.Choices(RowParkMode, _parkModeNames, (int)c.Mode, free);
                if (c.Mode == CarParkMode.Public)
                {
                    int fair = _d.ParkFair(c.Lot);
                    s.Fact(T("reallife_park_sheet_price"), ConstructionText.Cents(c.Price), BuildModeBar.Text);
                    s.Fact(T("reallife_park_sheet_band"), _d.ParkBandWords(c), BuildModeBar.Soft);
                    s.Buttons(RowParkLower, T("reallife_park_sheet_lower"), free && c.Price > CarParkRules.PriceLeast(fair), RowParkRaise, T("reallife_park_sheet_raise"),
                        free && c.Price < CarParkRules.PriceMost(fair));
                }
                else
                {
                    s.Fact(T("reallife_park_sheet_leased"), ConstructionText.Fill("reallife_park_value_leased", "leased", ConstructionText.N(c.Leased), "bays", ConstructionText.N(c.Bays)),
                        BuildModeBar.Text);
                    string offer = _d.ParkOfferWords(c);
                    if (offer != null)
                    {
                        s.Line(offer, BuildModeBar.Text);
                        s.Buttons(RowParkAccept, T("reallife_park_desk_accept"), free, RowParkDecline, T("reallife_park_desk_decline"), free);
                    }
                    else
                    {
                        s.Line(ConstructionText.Fill("reallife_park_desk_no_offers", "metres", ConstructionText.N(CarParkRules.ReachMetres)), BuildModeBar.Soft);
                    }
                }
                s.Fact(T("reallife_park_sheet_week"), ConstructionText.Money(_d.ParkWeek(c)), BuildModeBar.Text);
                s.Buttons(0, null, false, RowParkClose, T("reallife_park_sheet_close"), free);
            }
            else
            {
                s.Fact(T("reallife_park_sheet_how"), ConstructionDirector.ParkModeWords(c), BuildModeBar.Text);
                int room = _d.ParkLeaseRoom(c);
                if (room > 0)
                {
                    int bays = Math.Max(1, Math.Min(_parkBays, room));
                    long each = _d.ParkLeasePrice(c) / 100L;
                    s.Fact(T("reallife_park_sheet_lease_price"), ConstructionText.Fill("reallife_park_value_lease_price", "each", ConstructionText.Money(each), "bays", ConstructionText.N(bays),
                        "total", ConstructionText.Money(each * bays)), BuildModeBar.Text);
                    s.Buttons(RowParkLeaseLess, T("reallife_park_sheet_fewer"), free && bays > 1, RowParkLeaseMore, T("reallife_park_sheet_more"), free && bays < room);
                    s.Buttons(RowParkLease, ConstructionText.Fill("reallife_park_sheet_lease", "amount", ConstructionText.Money(each * bays)), free, 0, null, false);
                }
                else
                {
                    s.Line(T("reallife_park_sheet_no_room"), BuildModeBar.Soft);
                }
                if (c.LeasedByPlayer > 0)
                {
                    s.Fact(T("reallife_park_sheet_yours"), ConstructionText.N(c.LeasedByPlayer), BuildModeBar.Text);
                    s.Buttons(0, null, false, RowParkEndMine, T("reallife_park_sheet_end_mine"), free && ParkMyLease(c) != null);
                }
            }
            s.Buttons(0, null, false, RowClear, T("reallife_bld_sheet_clear"), true);
        }

        // The first lease of the player's at a car park of the city's that is not marked to end, or null.
        private static CarParkLease ParkMyLease(CarParkFile c)
        {
            for (int i = 0; c != null && i < c.Leases.Count; i++)
            {
                if (c.Leases[i].Who == CarParkLease.Player && !c.Leases[i].Ending)
                {
                    return c.Leases[i];
                }
            }
            return null;
        }

        // The Bulldoze tool on a car park of the player's: closing it.
        private void WriteParkClose(BuildModeSheet s, CarParkFile c, bool demolish)
        {
            s.Begin(_d.ParkName(c), null);
            if (!demolish)
            {
                s.Line(T("reallife_park_refit_none"), BuildModeBar.Red);
                s.Buttons(0, null, false, RowClear, T("reallife_bld_sheet_clear"), true);
                return;
            }
            s.Line(T("reallife_park_close_line"), BuildModeBar.Soft);
            s.Buttons(RowDemolish, T("reallife_park_sheet_close"), !_state.ViewOnly && !GoingPublic.InSession, RowClear, T("reallife_bld_sheet_clear"), true);
        }

        // A car park's rows; false for a row of another part.
        private bool ActPark(int id)
        {
            switch (id)
            {
                case RowCarPark:
                    _parkMode = true;
                    _catalogue?.Show(false);   // the designs window opened by the pick gives way to the car park's panel
                    ParkRead();
                    Write(false);
                    return true;
                case RowCarParkBack:
                    _parkMode = false;
                    ParkRead();
                    Write(false);
                    return true;
                case RowCarParkGo:
                    return ParkGo();
                case RowParkLower:
                case RowParkRaise:
                    if (_parkPicked != null)
                    {
                        _d.ParkStepPrice(_parkPicked, id == RowParkRaise ? 1 : -1);
                        Refresh(null);
                    }
                    return true;
                case RowParkAccept:
                    if (_parkPicked != null && _parkPicked.Offers.Count > 0)
                    {
                        Refresh(_d.ParkAccept(_parkPicked, ConstructionDirector.ParkFirstOffer(_parkPicked)));
                    }
                    return true;
                case RowParkDecline:
                    if (_parkPicked != null && _parkPicked.Offers.Count > 0)
                    {
                        Refresh(_d.ParkDecline(_parkPicked, ConstructionDirector.ParkFirstOffer(_parkPicked)));
                    }
                    return true;
                case RowParkLeaseLess:
                case RowParkLeaseMore:
                    _parkBays = Math.Max(1, _parkBays + (id == RowParkLeaseMore ? 1 : -1));
                    ParkRead();
                    Write(false);
                    return true;
                case RowParkLease:
                    if (_parkPicked != null)
                    {
                        CarParkFile c = _parkPicked;
                        int bays = _parkBays;
                        long month = _d.ParkLeasePrice(c) * bays / 100L;
                        Ask("reallife_park_lease_confirm", "reallife_park_lease_confirm_body", month, () => Refresh(_d.ParkLeaseFrom(c, bays)));
                    }
                    return true;
                case RowParkEndMine:
                    if (_parkPicked != null && ParkMyLease(_parkPicked) != null)
                    {
                        Refresh(_d.ParkEnd(_parkPicked, ParkMyLease(_parkPicked).Id));
                    }
                    return true;
                case RowParkClose:
                    if (_parkPicked != null)
                    {
                        CarParkFile c = _parkPicked;
                        Ask("reallife_park_close_confirm", "reallife_park_close_confirm_body", 0L, () =>
                        {
                            string answer = _d.ParkClose(c);
                            ClearPick();
                            Refresh(answer ?? "reallife_park_closed");
                        });
                    }
                    return true;
                default:
                    if (id >= RowParkMode && id < RowParkMode + ParkModeKeys.Length)
                    {
                        return ParkMode((CarParkMode)(id - RowParkMode));
                    }
                    return false;
            }
        }

        // The squares' car park built for the price, in the game's question box; its project's plans asked at once.
        private bool ParkGo()
        {
            if (!_parkFacts.Ok || _siteWhy != null || !_pickMine || _state.ViewOnly)
            {
                return true;
            }
            CellSite site = _site;
            Ask("reallife_park_confirm", "reallife_park_confirm_body", _parkFacts.Cost, () =>
            {
                int project = _d.PlacePark(site.FirstColumn, site.Count, site.MinRows, out string why);
                _lastProject = project;
                _parkMode = false;
                ClearPick();
                Refresh(why);
            });
            return true;
        }

        // The mode of the player's car park changed: going public ends the leases, so the game asks first when there are some.
        private bool ParkMode(CarParkMode mode)
        {
            CarParkFile c = _parkPicked;
            if (c == null || c.Owner != CarParkOwner.Player || c.Mode == mode)
            {
                return true;
            }
            if (mode == CarParkMode.Public && c.Leases.Count > 0)
            {
                Ask("reallife_park_confirm_public", "reallife_park_confirm_public_amount", _d.ParkLeaseRefund(c), () => Refresh(_d.ParkSetMode(c, CarParkMode.Public)));
                return true;
            }
            Refresh(_d.ParkSetMode(c, mode));
            return true;
        }

        /// <summary>The car park on the squares picked built as the panel's button does, without the question box (for the tests); the project's id or 0, with why.</summary>
        public int ParkForTest(out string why)
        {
            why = null;
            _parkMode = true;
            ParkRead();
            if (!_parkFacts.Ok)
            {
                why = _parkFacts.Why ?? _siteWhy ?? "reallife_bld_hint_place_not_yours";
                return 0;
            }
            int project = _d.PlacePark(_site.FirstColumn, _site.Count, _site.MinRows, out why);
            _lastProject = project;
            _parkMode = false;
            ClearPick();
            Refresh(null);
            return project;
        }
    }
}
