namespace RealLifeOverhaul.Ui
{
    /// <summary>Which page of the Economy window shows.</summary>
    public enum NewsPage : byte
    {
        Tab = 0,       // the tab chosen in the window's list
        People,        // the city's people, the window's last tab
        District,      // one district's page, opened from the people's table, the busiest district's link or a district row of the districts' tab
        Renters,       // who pays the most rent
        Household      // one household, opened from the list of renters or a district's table
    }

    /// <summary>
    /// Where the Economy window stands: on its tab, or on a page opened from a tab (a district, the renters, a household), each a step on the way from
    /// the tab. A press on a tab, Back and the window closing all put these pages away, one step or all, so the window opens on its tab and never on a
    /// page left from before. Game-free; nothing a frame.
    /// </summary>
    public sealed class NewsPath
    {
        public const int PeopleTab = 7;

        private bool _rent, _home;
        private int _district = -1;

        /// <summary>The district with most households, set by the people's page for the link that opens it; -1 where nobody lives anywhere.</summary>
        public int Busy = -1;

        public bool RentersOpen => _rent;
        public bool HomeOpen => _home;
        public int District => _district;
        public bool DistrictOpen => _district >= 0;

        /// <summary>The page to show on a tab.</summary>
        public NewsPage Page(int tab)
        {
            return _home ? NewsPage.Household : _rent ? NewsPage.Renters : _district >= 0 ? NewsPage.District : tab == PeopleTab ? NewsPage.People : NewsPage.Tab;
        }

        /// <summary>
        /// A press of the page's own controls: the back button, the link to who pays the most rent, the link to the busiest district, and a tab of the window
        /// (every page opened from a tab is put away). True when it did what it says; false when it does nothing now (Back with nothing open).
        /// </summary>
        public bool Press(int command)
        {
            if (command == NewsCommands.Back)
            {
                return Back();
            }
            if (command == NewsCommands.Rent)
            {
                return OpenRenters();
            }
            if (command == NewsCommands.Busy)
            {
                return OpenBusy();
            }
            if (command == UiAppWindow.NavCommand)
            {
                Clear();
                return true;
            }
            return false;
        }

        /// <summary>The link to who pays the most rent.</summary>
        public bool OpenRenters()
        {
            _rent = true;
            _home = false;
            return true;
        }

        /// <summary>The link to the busiest district; false while nobody lives anywhere.</summary>
        public bool OpenBusy()
        {
            return Busy >= 0 && OpenDistrict(Busy);
        }

        /// <summary>A district's page, from a table's row or a link; false for no district, or while a page is open already.</summary>
        public bool OpenDistrict(int district)
        {
            if (district < 0 || _district >= 0 || _rent || _home)
            {
                return false;
            }
            _district = district;
            return true;
        }

        /// <summary>A household's page from the list or the table it stands in.</summary>
        public bool OpenHome()
        {
            if (_home || (!_rent && _district < 0))
            {
                return false;
            }
            _home = true;
            return true;
        }

        /// <summary>One step back: a household to its list or district, the list or the district to its tab. False when none is open.</summary>
        public bool Back()
        {
            if (_home)
            {
                _home = false;
                return true;
            }
            if (_rent)
            {
                _rent = false;
                return true;
            }
            if (_district >= 0)
            {
                _district = -1;
                return true;
            }
            return false;
        }

        /// <summary>A tab pressed, the window closed or a notice's link asking for a tab: every page opened from a tab is put away. True when one was open.</summary>
        public bool Clear()
        {
            bool open = _home || _rent || _district >= 0;
            _home = false;
            _rent = false;
            _district = -1;
            return open;
        }

        /// <summary>A household that is gone from the city: its page is put away and the list or district under it shows.</summary>
        public void HomeGone()
        {
            _home = false;
        }

        /// <summary>A district that is gone from the table: its page is put away.</summary>
        public void DistrictGone()
        {
            _district = -1;
        }
    }
}
