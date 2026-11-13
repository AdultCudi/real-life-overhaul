using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Garage's list window on the RLO interface as the garage strip (UiGarageStrip), docked at the left while the
    /// turntable shows the car: the custom shop's pages (pay, paint, finish, lights, body, parts, the figures, all stock),
    /// the workshop's list, "My vehicles" and a stand's rack. Every row, price, state and button is the list's own words;
    /// a press on a card is the list's own pick, so each owner's rules and refusals stay its own, and the strip is laid
    /// again after each press and each change of the list (its money, its page). Back is the window's Cancel, as before:
    /// leaving the shop without paying puts the car back as it came in. The window stays off while this shows and is
    /// handed back when the interface or this screen's switch goes off, or the screen faults. Cost: a number compared a
    /// frame; a layout a change.
    /// </summary>
    public sealed class GarageShopScreen : IUiScreen
    {
        private readonly UiGarageStripModel _m = new UiGarageStripModel();
        private TravelList _list;
        private int[] _ids = new int[0];
        private int _seen = -1, _page = -1;
        private bool _dirty = true;

        public string Id => GarageFace.Id;

        /// <summary>The strip's title and glyph, as the host is bound with them.</summary>
        public string Title => _m.Title;

        public UiIcon Icon => _m.Icon;

        /// <summary>The model last read, for the tests.</summary>
        public UiGarageStripModel Model => _m;

        public bool Dirty => _dirty || (_list != null && _list.Version != _seen);

        /// <summary>The list to draw; null when none.</summary>
        public void Show(TravelList list)
        {
            _list = list;
            _seen = -1;
            _page = -1;
            _dirty = true;
            _m.Offset = 0f;
            _m.Reveal = -1;
            if (list != null)
            {
                Read();
            }
        }

        private void Read()
        {
            _seen = _list.Version;
            int page = GarageShopLive.PageKey();
            if (page != _page)
            {
                _page = page;
                _m.Offset = 0f;   // another page of the shop starts at its top
            }
            GarageShopLive.Read(_list, _m, ref _ids);
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_list != null && _list.Version != _seen)
            {
                Read();
            }
            _dirty = false;
            UiGarageStrip.Lay(s, _m, frame, dark, keys);
        }

        public bool Press(int command, int arg)
        {
            if (_list == null)
            {
                return false;
            }
            switch (command)
            {
                case UiGarageStrip.CardCommand:
                    if (arg < 0 || arg >= _m.CardCount || arg >= _ids.Length || _m.Cards[arg].Note || !_m.Cards[arg].Enabled)
                    {
                        return false;
                    }
                    bool done = _list.Pick(_ids[arg]);   // the list's own pick: the owner's rules answer, and refill the list
                    _dirty = true;
                    return done;
                case UiDraw.CloseCommand:
                    return Back();
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        /// <summary>Back closes the list as its Cancel did, so the shop is left without paying.</summary>
        public bool Back()
        {
            if (_list == null || _list.Locked)
            {
                return false;
            }
            _list.Close();
            return true;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            if (index >= 0 && index < _m.CardCount)
            {
                _m.Reveal = index;
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            _m.Offset = System.Math.Max(0f, _m.Offset - notches * UiTokens.Row);
            _dirty = true;
        }
    }
}
