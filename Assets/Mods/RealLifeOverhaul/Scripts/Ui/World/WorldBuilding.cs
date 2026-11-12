using System;
using System.Collections.Generic;
using System.Reflection;
using AI.Customers.CustomerEntries;
using BigAmbitions.Rivals;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UI.InGameUI;
using UI.Smartphone.Apps.Rivals;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's building box (BuildingResume) on the RLO interface. While the pointer is over a building, the kit's card
    /// stands where the game put its box at the door: the business's name, its kind and neighbourhood, whether it is open,
    /// and the game's own hints (left click to enter, right click for options). Once the building is picked (the game shows
    /// its buttons), it is the info panel docked at the top left, after Cities: Skylines II's building panel: icon tabs for
    /// the building, the business in it and its owner, its address as the strip's title, the business's initials, name and
    /// state, the building's and the business's rows under their state words (Rented out, A rival's, For rent), and the
    /// game's buttons in its footer (BizMan or the taxi, set destination, the sign, and any another mod put beside them,
    /// Voogle Route's own) with the address's camera move after them. An owner who is one of the game's rivals is a link to
    /// their page in the game's rivals app, the neighbourhood a link to the city map; a business's customers this hour stand
    /// against the building's room on a meter. In the HUD's left column when the kit's HUD shows, so the objectives stand
    /// under it; at its door on the world's canvas otherwise, where the game shows its own options. Every word is read from
    /// the game's own labels after the game and other mods wrote them, or from its registration where the game's own box reads it; every press is the game's own button or call.
    /// Cost a frame while it shows: a dozen reference comparisons, the hour and a move; nothing allocated. The game's customer count
    /// is asked once a reading (a building picked or hovered, the hour turning).
    /// </summary>
    internal sealed class WorldBuilding
    {
        private static readonly FieldInfo Panel = GameContract.ResumePanel.Field;
        private const int Words = 14;   // the labels whose words the panel shows

        private readonly UiScene _cardScene = new UiScene();
        private readonly UiScene _dockScene = new UiScene();
        private readonly UiConceal _conceal = new UiConceal();
        private readonly UiPromptCardModel _card = new UiPromptCardModel { Actions = new UiPromptAction[2] };
        private readonly UiInfoModel _info = new UiInfoModel();
        private readonly string[] _seen = new string[Words];
        private readonly bool[] _seenOn = new bool[Words + 6];
        private readonly Component[] _labels = new Component[Words];
        private readonly List<Transform> _foreign = new List<Transform>(4);
        private readonly List<Button> _footer = new List<Button>(6);
        private UiView _cardView, _dockView;
        private float _scale;
        private BuildingResume _resume;
        private RectTransform _panel;
        private readonly Vector3[] _corners = new Vector3[4];
        private Canvas _canvas;
        private Button _main;                 // the footer's wide button: the taxi in taxi mode, else BizMan
        private bool _shown, _docked, _inHud, _off, _dirty = true;
        private bool _awaySaid;            // the line that the picked panel stands at the door, no kit HUD holding it, said once a city
        private float _dockW, _dockH;      // px: the docked panel's size, last laid
        private int _seenKids = -1;
        private int _tab;
        private int _readHour = -1;   // the game's hour the panel was read at: its customers are that hour's
        private const int LinkOwner = 1, LinkBusinessOwner = 2, LinkMap = 3;
        private readonly List<int> _linkAt = new List<int>(4), _linkKind = new List<int>(4);   // the panel's links: LinkCommand's argument, what it opens
        private string _ownerRival, _businessRival;   // the game's rivals behind the owners' links, by id; null for none
        private CityBuildingController _cbc;          // the building read, for the address's camera move
        private int _locate = -1;                     // the footer's camera move: its button's index, -1 when there is none
        private Func<int, int, bool> _chain, _was;
        private Vector2 _at = new Vector2(float.NaN, float.NaN);
        private string _state = "not looked at yet";

        public string State => _off ? _state : _docked ? (_inHud ? "docked in the HUD's left column" : "picked, at its door") : _shown ? "at the door" : _state;

        /// <summary>True while a building is picked and the panel shows its rows and buttons (in the HUD or at the door).</summary>
        public bool Docked => _docked;

        /// <summary>The models last read, for the tests.</summary>
        public UiPromptCardModel Card => _card;
        public UiInfoModel Info => _info;
        public bool Showing => _shown;

        public void Build(RectTransform rect, float scale)
        {
            _scale = scale;
            _cardView = new UiView(rect, "Building") { Scale = scale, Ring = false };
            _cardView.Root.gameObject.SetActive(false);
            _dockView = new UiView(rect, "Building panel") { Scale = scale, Ring = false };
            _dockView.PointerPressed = Guard.Wrap<int>(DockPressed);
            _dockView.PointerEntered = Guard.Wrap<int>(node => _dockView.SetHover(node));
            _dockView.PointerExited = Guard.Wrap<int>(node => _dockView.SetHover(-1));
            _dockView.Root.gameObject.SetActive(false);
            _chain = Press;
            GameUi.WorldPanel.ResumeElsewhere = () => Instance() != null && Instance()._docked && Instance()._inHud;   // at the door it is where the game's box is
        }

        private static WorldBuilding Instance() => WorldHost.Instance != null ? WorldHost.Instance.Building : null;

        public void Rescale(float scale)
        {
            _scale = scale;
            if (_cardView != null)
            {
                _cardView.Scale = scale;
                _dockView.Scale = scale;
            }
            _dirty = true;
        }

        /// <summary>A frame: the card at the door or the docked panel while the game shows its box and the switch is on.</summary>
        public void Frame(UiFrame frame, bool on)
        {
            if (_off || !Find())
            {
                return;
            }
            // A building in the street, or on the city map once it is picked (a click on it): its card stands at the building where the game puts its own box. The map's
            // other picks (a station, a taxi's stop) stay the game's box.
            bool picked = _resume.options != null && _resume.options.gameObject.activeInHierarchy;
            bool up = on && (picked || !CityMap.IsOpen) && _panel.gameObject.activeInHierarchy && _resume.CityBuildingController != null;
            if (!up)
            {
                Hide();
                return;
            }
            int hour = TimeHelper.CurrentHour;   // read again as the hour turns, which the game's own box is not
            if (Changed() || picked != _docked || _dirty || hour != _readHour)
            {
                _dirty = false;
                _readHour = hour;
                if (picked != _docked)
                {
                    _tab = 0;
                }
                _docked = picked;
                Read();
                Lay(frame);
            }
            if (!_shown)
            {
                _shown = true;
                _conceal.Hide();
            }
            if (_docked)
            {
                _cardView.Root.gameObject.SetActive(false);
                Dock(frame);
            }
            else
            {
                Undock();
                _cardView.Root.gameObject.SetActive(true);
                Place(frame);
            }
        }

        /// <summary>The game's box back as it was, the kit's gone.</summary>
        public void Give()
        {
            Hide();
            _conceal.Clear();
            GameUi.WorldPanel.ResumeElsewhere = null;
        }

        private void Hide()
        {
            if (!_shown)
            {
                return;
            }
            _shown = false;
            _docked = false;
            _conceal.Show();
            _cardView.Root.gameObject.SetActive(false);
            Undock();
            Array.Clear(_seen, 0, _seen.Length);
            _dirty = true;
        }

        private bool Find()
        {
            if (_resume != null && _panel != null)
            {
                return true;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            _resume = uis != null ? uis.buildingResume : null;
            if (_resume == null)
            {
                return false;
            }
            _panel = Panel != null ? Panel.GetValue(_resume) as RectTransform : null;
            if (_panel == null && _resume.data != null)
            {
                _panel = _resume.data.parent as RectTransform;   // the box is the parent of its two halves, as WorldPanel.Beside finds it
            }
            if (_panel == null || _resume.addressLabel == null || _resume.businessNameLabel == null || _resume.options == null)
            {
                _off = true;
                _state = "the game's own (its building box is not built as this version expects)";
                GameUi.UiClone.Fallback("the game's building box is not built as expected, so it stays the game's own");
                return false;
            }
            _canvas = _panel.GetComponentInParent<Canvas>(true);
            _labels[0] = Text(_resume.addressLabel);
            _labels[1] = _resume.businessNameLabel;
            _labels[2] = Text(_resume.neighborhoodLabel);
            _labels[3] = Text(_resume.businessTypeLabel);
            _labels[4] = Text(_resume.openStateLabel);
            _labels[5] = _resume.trafficIndexLabel;
            _labels[6] = _resume.customerCapacityLabel;
            _labels[7] = Text(_resume.buildingAreaLabel);
            _labels[8] = Text(_resume.rivalBuildingOwner);
            _labels[9] = Text(_resume.rivalBusinessOwner);
            _labels[10] = _resume.clickToEnterLabel;
            _labels[11] = _resume.clickToOptionsLabel;
            _labels[12] = Text(_resume.taxiTravelButtonText);
            _labels[13] = null;
            _conceal.Add(_panel);
            _state = "waiting for a building";
            return true;
        }

        private static TMP_Text Text(TextLocalizationComponent label) => label != null ? label.TextContainer : null;

        // The game wrote its box again: a label's words, a label or a button shown or hidden.
        private bool Changed()
        {
            bool changed = false;
            for (int i = 0; i < Words; i++)
            {
                var t = _labels[i] as TMP_Text;
                bool on = t != null && t.gameObject.activeSelf;   // its own switch, as the game sets it: its half of the box may be hidden while picked
                string words = on ? t.text : null;
                if (!ReferenceEquals(words, _seen[i]) || on != _seenOn[i])
                {
                    _seen[i] = words;
                    _seenOn[i] = on;
                    changed = true;
                }
            }
            changed |= Seen(Words, _resume.bizManButton);
            changed |= Seen(Words + 1, _resume.taxiTravelButton);
            changed |= Seen(Words + 2, _resume.setDestinationButton);
            changed |= Seen(Words + 3, _resume.changeSignButton);
            int kids = Kids(_resume.options) + Kids(_resume.setDestinationButton != null ? _resume.setDestinationButton.transform.parent : null) * 64;
            if (kids != _seenKids)
            {
                _seenKids = kids;   // another mod's button put in or taken out (Voogle Route's beside set destination)
                changed = true;
            }
            return changed;
        }

        // The pieces a container shows now.
        private static int Kids(Transform t)
        {
            int n = 0;
            for (int i = 0; t != null && i < t.childCount; i++)
            {
                n += t.GetChild(i).gameObject.activeSelf ? 1 : 0;
            }
            return n;
        }

        private bool Seen(int slot, Button b) => Seen(slot, b != null && b.gameObject.activeInHierarchy && b.interactable);

        private bool Seen(int slot, bool on)
        {
            if (_seenOn[slot] == on)
            {
                return false;
            }
            _seenOn[slot] = on;
            return true;
        }

        private string Seen(int i) => WorldHost.Plain(_seen[i]);

        // The box's words into the card's and the panel's models.
        private void Read()
        {
            string address = Seen(0), name = Seen(1), place = After(Seen(2)), kind = Seen(3), open = Seen(4);
            string traffic = Seen(5), capacity = Seen(6), area = Seen(7), owner = Seen(8), business = Seen(9);
            var openText = _labels[4] as TMP_Text;
            UiRole tone = openText != null && !string.IsNullOrEmpty(open) ? WorldHost.Tone(openText.color) : UiRole.Text2;
            string firstLine = FirstLine(open), state = Join(firstLine, Rest(open));
            string facts = Join(kind, place);
            bool business_ = !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(kind);
            // At the door: the business by its name, what it is and where, whether it is open, and the game's hints.
            _cbc = _resume.CityBuildingController;
            BuildingRegistration reg = _cbc != null ? _cbc.buildingRegistration : null;
            BizWorth worth = BizFacts.Read(_cbc);
            _card.Title = business_ ? name : address;
            _card.Sub = DoorLines(facts, traffic, capacity, area, business, owner, worth);
            _card.State = firstLine;
            _card.StateTone = tone;
            _card.Icon = business_ ? UiIcon.Store : UiIcon.Building;
            int hints = 0;
            if (!string.IsNullOrEmpty(Seen(10)))
            {
                _card.Actions[hints++] = new UiPromptAction { Label = Seen(10), Hint = true };
            }
            if (!string.IsNullOrEmpty(Seen(11)))
            {
                _card.Actions[hints++] = new UiPromptAction { Label = Seen(11), Hint = true };
            }
            _card.ActionCount = hints;
            // Picked: the info panel, its strip the address, its head the business, its rows the building's and the business's.
            _info.Title = address ?? name;
            _info.Icon = UiIcon.Building;
            _info.Tabs = business_ ? new[] { UiIcon.Building, UiIcon.Store, UiIcon.Person } : new[] { UiIcon.Building, UiIcon.Person };
            _info.TabTints = new UiRole[0];
            _tab = Math.Min(_tab, _info.Tabs.Length - 1);
            _info.Tab = _tab;
            _info.Lead = business_ ? name : null;
            _info.Mark = business_ ? Initials(name) : null;
            _info.MarkTone = UiTokens.Chart((int)(UiColour.HueOf(name ?? string.Empty) / 45f));
            _info.Name = null;
            _info.Facts = facts;
            _info.Status = state;
            _info.StatusIcon = UiIcon.Clock;
            _info.StatusTone = tone == UiRole.OkText ? UiRole.Ok : tone == UiRole.DangerText ? UiRole.Danger : tone == UiRole.WarnText ? UiRole.Warn : UiRole.None;
            // The building's rows, then the business's; an empty building's kind and traffic are the building's own.
            _ownerRival = Rival(reg != null ? reg.buildingOwnerRivalId : null, owner);
            _businessRival = reg != null && !reg.RentedByPlayer ? Rival(reg.businessOwnerRivalId, business) : null;
            var building = new Rows();
            Owner(building, owner, _ownerRival, LinkOwner);   // "Owner: Hudson Realty", or the game's own sentence ("Owned by you")
            building.Add("bizman_market_value", worth.Market, 0);   // the value beside the owner it belongs to
            building.Add("reallife_ui_world_neighbourhood", place, LinkMap);
            building.Add("reallife_ui_world_area", area, 0);
            building.Add("reallife_ui_world_capacity", capacity, 0);
            Rows trade = business_ ? new Rows() : building;
            trade.Add("reallife_ui_world_kind", kind, 0);
            if (business_)
            {
                trade.Add("reallife_ui_world_open", state, 0);
            }
            trade.Add("reallife_ui_world_traffic", traffic, 0);
            Customers(trade, reg);
            if (business_ && _businessRival != null)
            {
                Owner(trade, business, _businessRival, LinkBusinessOwner);   // a rival's business: who runs it, a link to them
            }
            else if (business_ && !Plain(reg, business))
            {
                trade.Add(null, business, 0);   // the game's line said otherwise than its own words (another mod's): as it is
            }
            if (business_)
            {
                Valuation(trade, worth);
            }
            // The tabs: the building with the business under it, the business alone, its owners.
            var sections = new List<UiInfoSection>(2);
            _linkAt.Clear();
            _linkKind.Clear();
            int tab = business_ ? _tab : (_tab == 1 ? 2 : 0);
            if (tab == 0)
            {
                Section(sections, UiText.T("reallife_ui_world_building"), BuildingState(reg), building);
            }
            if (business_ && tab < 2)
            {
                Section(sections, UiText.T("reallife_ui_world_business"), BusinessState(reg), trade);
            }
            if (tab == 2)
            {
                var owners = new Rows();
                Owner(owners, owner, _ownerRival, LinkOwner);
                owners.Add("bizman_market_value", worth.Market, 0);
                if (_businessRival != null)
                {
                    Owner(owners, business, _businessRival, LinkBusinessOwner);
                }
                else
                {
                    owners.Add(null, business, 0);
                }
                Valuation(owners, worth);
                Section(sections, UiText.T("reallife_ui_world_owner"), null, owners);
            }
            _info.Sections = sections.ToArray();
            Footer();
        }

        // The business's valuation under its owner's row: a rival's by the game's own sum, the player's by what a sale of it would ask.
        private static void Valuation(Rows rows, BizWorth worth)
        {
            if (worth.Valuation != null)
            {
                rows.Add(new UiInfoRow { Label = BizFacts.ValuationLabel(worth), Value = worth.Valuation });
            }
        }

        // The door card's lines, each of the game's box's facts once: the kind and place, the traffic and the room for customers, the floor area, the business's owner with its
        // valuation, the building's owner with its market value.
        private static string DoorLines(string facts, string traffic, string capacity, string area, string business, string owner, BizWorth worth) => BizFactsRules.Lines(facts,
            BizFactsRules.Commas(BizFactsRules.Pair(UiText.T("reallife_ui_world_traffic"), traffic), BizFactsRules.Pair(UiText.T("reallife_ui_world_capacity"), capacity)),
            BizFactsRules.Pair(UiText.T("reallife_ui_world_area"), area), business, BizFacts.ValuationSentence(worth), owner, BizFacts.MarketSentence(worth));

        // A section's rows and what each row's link opens, gathered before the section's place in the panel is known.
        private sealed class Rows
        {
            public readonly List<UiInfoRow> List = new List<UiInfoRow>(6);
            public readonly List<int> Links = new List<int>(6);

            // A row when the game shows it; a line that is its own sentence (no key: "Owned by you") is the row's words alone.
            public void Add(string key, string value, int link)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }
                List.Add(key == null ? new UiInfoRow { Label = value } : new UiInfoRow { Label = UiText.T(key), Value = value, Link = link != 0 });
                Links.Add(key == null ? 0 : link);
            }

            public void Add(UiInfoRow row)
            {
                List.Add(row);
                Links.Add(0);
            }
        }

        private void Section(List<UiInfoSection> sections, string label, string value, Rows rows)
        {
            int index = sections.Count;
            for (int r = 0; r < rows.Links.Count; r++)
            {
                if (rows.Links[r] != 0)
                {
                    _linkAt.Add(index * 16 + r);
                    _linkKind.Add(rows.Links[r]);
                }
            }
            sections.Add(new UiInfoSection { Label = label, Value = value, Rows = rows.List.ToArray() });
        }

        // An owner's row: "Owner: Viktor Hale" as a link to the rival's page when the game's own line names that rival; the
        // game's own sentence otherwise ("Owned by you", the city's, a government building's, another mod's words).
        private static void Owner(Rows rows, string sentence, string rival, int link)
        {
            string name = rival != null ? rival.GetRivalName() : null;
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(sentence))
            {
                rows.Add("reallife_ui_world_owner_one", name, link);
                return;
            }
            rows.Add(null, sentence, 0);
        }

        // A rival's id when the game's line shows and names them and the game still knows them; else null.
        private static string Rival(string id, string sentence)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(sentence) || RivalsHelper.GetRivalData(id) == null)
            {
                return null;
            }
            string name = id.GetRivalName();
            return !string.IsNullOrEmpty(name) && sentence.IndexOf(name, StringComparison.Ordinal) >= 0 ? id : null;
        }

        // True when the game's business line is its own words for this business (the player's "Rented by you"), so its state
        // word says it all.
        private static bool Plain(BuildingRegistration reg, string business)
        {
            return string.IsNullOrEmpty(business) || reg != null && reg.RentedByPlayer && business == UiText.T("buildingresume_rented_by_you");
        }

        // The building's word over its rows: the player's own, rented by them, for rent, a rival's, or let to somebody's business.
        private static string BuildingState(BuildingRegistration reg)
        {
            if (reg == null)
            {
                return null;
            }
            bool tenant = !string.IsNullOrEmpty(reg.BusinessName) && !reg.RentedByPlayer;   // somebody else's business in it
            if (reg.BuildingOwnedByPlayer)
            {
                return UiText.T(tenant ? "reallife_ui_world_state_rented_out" : "buildingresume_owned_by_you");
            }
            if (reg.RentedByPlayer)
            {
                return UiText.T("buildingresume_rented_by_you");
            }
            if (reg.AvailableForRent)
            {
                return UiText.T("reallife_ui_world_state_for_rent");
            }
            if (!string.IsNullOrEmpty(reg.buildingOwnerRivalId))
            {
                return UiText.T("reallife_ui_world_state_rival");
            }
            return tenant ? UiText.T("reallife_ui_world_state_rented_out") : null;
        }

        // The business's word over its rows: the player's, or a rival's; nothing for a business of the city's own.
        private static string BusinessState(BuildingRegistration reg)
        {
            if (reg == null || string.IsNullOrEmpty(reg.BusinessName))
            {
                return null;
            }
            if (reg.RentedByPlayer)
            {
                return UiText.T("reallife_ui_world_state_yours");
            }
            return !string.IsNullOrEmpty(reg.businessOwnerRivalId) ? UiText.T("reallife_ui_world_state_rival") : null;
        }

        // Customers this hour as the game counts them for the business (its calculator for the kind, at the hour and the day,
        // none while it is shut), against the room the box gives the building: "36 of 48" on a meter. None for a building with
        // no walk-in trade, or when the game cannot say.
        private void Customers(Rows rows, BuildingRegistration reg)
        {
            if (reg == null || reg.AvailableForRent || string.IsNullOrEmpty(reg.BusinessName) || _cbc == null || _cbc.building == null)
            {
                return;
            }
            int most, now;
            try
            {
                most = _cbc.building.GetCustomerCapacity;
                if (most <= 0)
                {
                    return;
                }
                CustomerEntriesCalculator calculator = CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg);
                float initial = calculator.GetInitialCustomers();
                if (initial <= 0f)
                {
                    return;
                }
                int hour = TimeHelper.CurrentHour;
                now = BusinessHelper.IsBusinessOpen(reg, hour) ? Math.Max(0, calculator.GetCustomersByHour(hour, TimeHelper.GetDayOfWeek(), initial)) : 0;
            }
            catch (Exception)
            {
                return;   // a business the game's calculator does not count
            }
            now = Math.Min(now, most);   // never more in an hour than the room for them, as the game's insight says
            rows.Add(new UiInfoRow
            {
                Label = UiText.T("reallife_ui_world_customers"), Meter = true, Fill = (float)now / most,
                Value = UiText.T("reallife_ui_world_customers_of").Replace("{count}", now.ToString()).Replace("{most}", most.ToString()),
            });
        }

        // The game's buttons in the footer, in its order: the taxi in taxi mode or BizMan as the wide one, then the rest as icons.
        private void Footer()
        {
            _footer.Clear();
            var icons = new List<UiIcon>(5);
            _main = Shows(_resume.taxiTravelButton) ? _resume.taxiTravelButton : Shows(_resume.bizManButton) ? _resume.bizManButton : null;
            _info.FooterLabel = _main == _resume.taxiTravelButton && _main != null ? Seen(12) : _main != null ? UiWidgets.Label(_main) : null;
            _info.FooterLabelOff = false;
            Add(icons, _resume.bizManButton, UiIcon.Briefcase);
            Add(icons, _resume.setDestinationButton, UiIcon.Navigation);
            Add(icons, _resume.changeSignButton, UiIcon.PaintBrush);
            // Another mod's buttons beside the game's (Voogle Route clones set destination beside it): each pressed as it is.
            _foreign.Clear();
            Foreign(_resume.setDestinationButton != null ? _resume.setDestinationButton.transform.parent : null);
            Foreign(_resume.options);
            for (int i = 0; i < _foreign.Count && icons.Count < 5; i++)
            {
                Button b = _foreign[i].GetComponentInChildren<Button>(false);
                if (b != null && b.gameObject.activeInHierarchy && !_footer.Contains(b))
                {
                    _footer.Add(b);
                    icons.Add(UiIcon.MapTrifold);
                }
            }
            // The address's link: the camera to the building on the city map, as the game focuses one from its own lists.
            _locate = -1;
            if (_cbc != null && icons.Count < 6)
            {
                _locate = icons.Count;
                _footer.Add(null);
                icons.Add(UiIcon.Locate);
            }
            _info.Footer = icons.ToArray();
            _info.FooterTints = new UiRole[0];
        }

        // A container's pieces that hold none of the game's four buttons and are not the mod's own: another mod's.
        private void Foreign(Transform container)
        {
            for (int i = 0; container != null && i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (!child.gameObject.activeSelf || child.name.StartsWith(UiHostRules.ModPrefix, StringComparison.Ordinal) || Holds(child, _resume.bizManButton)
                    || Holds(child, _resume.taxiTravelButton) || Holds(child, _resume.setDestinationButton) || Holds(child, _resume.changeSignButton) || _foreign.Contains(child))
                {
                    continue;
                }
                if (child.GetComponentInChildren<Button>(false) != null)
                {
                    _foreign.Add(child);
                }
            }
        }

        private static bool Holds(Transform t, Button b) => b != null && (b.transform == t || b.transform.IsChildOf(t));

        private void Add(List<UiIcon> icons, Button b, UiIcon icon)
        {
            if (b != null && b != _main && b.gameObject.activeInHierarchy)
            {
                _footer.Add(b);
                icons.Add(icon);
            }
        }

        private static bool Shows(Button b) => b != null && b.gameObject.activeInHierarchy;

        private void Lay(UiFrame frame)
        {
            float w = UiPromptCard.Width(_card), h = UiPromptCard.Height(_card);
            _cardScene.Begin(WorldKits.BuildingId, w, h, true);
            _cardScene.Floats = true;
            _cardScene.Pointer = true;
            UiPromptCard.Lay(_cardScene, UiAt.Screen, _card, 0f, 0f);
            _cardView.Write(_cardScene, false);
            _at = new Vector2(float.NaN, float.NaN);
            if (!_docked)
            {
                return;
            }
            HudHost hud = HudHost.Instance;
            if (hud != null && HudStatusGame.Shows && !CityMap.IsOpen)
            {
                return;   // the HUD lays it out in its left column (Dock)
            }
            float dw = UiInfoPanel.OuterWidth(_info), dh = UiInfoPanel.Height(_info);
            _dockW = dw + UiTokens.S2;
            _dockH = dh;
            _dockScene.Begin(WorldKits.BuildingId, dw + UiTokens.S2, dh, true);
            _dockScene.Floats = true;
            _dockScene.Pointer = true;
            UiInfoPanel.Draw(_dockScene, _info, 0f, 0f);
            _dockView.Write(_dockScene, false);
        }

        // Picked: in the HUD's left column while the kit's HUD shows (its objectives go under it), else at the top left here.
        private void Dock(UiFrame frame)
        {
            HudHost hud = HudHost.Instance;
            if (hud != null && HudStatusGame.Shows && !CityMap.IsOpen)
            {
                _dockView.Root.gameObject.SetActive(false);   // on the city map the dock holds the left, so the card stays at its building
                HudScreen screen = hud.Screen;
                if (!ReferenceEquals(screen.Model.Info, _info) || !_inHud)
                {
                    screen.Model.Info = _info;
                    screen.Dirty = true;
                    _inHud = true;
                }
                if (!ReferenceEquals(screen.PiecePressed, _chain))
                {
                    _was = screen.PiecePressed;   // whoever put a piece up before keeps their presses
                    screen.PiecePressed = _chain;
                }
                return;
            }
            if (_inHud)
            {
                Undock();
                Lay(frame);
            }
            // No HUD of the kit's to hold it (its switch is off, the game hid its top bar, or it has not stood up): the panel
            // stands where the game shows its own options, at the door, on the world's canvas. Docked at the top left here it
            // stood under the game's HUD canvas (its tasks, the minimap's card), so a right click showed nothing at all.
            if (!_awaySaid)
            {
                _awaySaid = true;
                UiDirector.Logger?.Info("UI: a picked building's panel stands at its door on the world's canvas: the kit's HUD is "
                                        + (hud == null ? "not up (" + HudLive.State + ")" : "hidden") + ", so its left column cannot hold it.");
            }
            _dockView.Root.gameObject.SetActive(true);
            Vector2 at = Door(frame, _dockW, _dockH);
            _dockView.Root.anchoredPosition = new Vector2(at.x * _scale, -at.y * _scale);
        }

        private void Undock()
        {
            _dockView.Root.gameObject.SetActive(false);
            if (!_inHud)
            {
                return;
            }
            _inHud = false;
            HudHost hud = HudHost.Instance;
            if (hud == null)
            {
                return;
            }
            HudScreen screen = hud.Screen;
            if (ReferenceEquals(screen.Model.Info, _info))
            {
                screen.Model.Info = null;
                screen.Dirty = true;
            }
            if (ReferenceEquals(screen.PiecePressed, _chain))
            {
                screen.PiecePressed = _was;
            }
            _was = null;
        }

        // Where a piece w by h stands at the door, px of the layout: its top left at the game's box's, kept inside the screen.
        private Vector2 Door(UiFrame frame, float w, float h)
        {
            _panel.GetWorldCorners(_corners);
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            Vector2 topLeft = WorldHost.ToLayout(RectTransformUtility.WorldToScreenPoint(cam, _corners[1]), frame);
            float x = topLeft.x, y = topLeft.y;
            UiHudRules.Clamp(ref x, ref y, w, h, frame.Width, frame.BandY);
            return new Vector2(Mathf.Round(x), Mathf.Round(y));
        }

        // At the door: the card where the game's box stands, its top left at the box's, kept inside the screen.
        private void Place(UiFrame frame)
        {
            Vector2 door = Door(frame, UiPromptCard.Width(_card), UiPromptCard.Height(_card));
            var at = new Vector2(door.x * _scale, -door.y * _scale);
            if (at != _at)
            {
                _at = at;
                _cardView.Root.anchoredPosition = at;
            }
        }

        private void DockPressed(int node)
        {
            if (node < 0 || node >= _dockScene.NodeCount)
            {
                return;
            }
            ref UiNode n = ref _dockScene.Nodes[node];
            UiRefusals.Begin();
            bool done = !n.Disabled && Press(n.Command, n.Arg);
            if (done)
            {
                _dockView.Press(Time.unscaledTime);
            }
            else
            {
                UiRefusalLog.Say("the building's card", _dockScene, node, n.Disabled);
            }
            UiGame.Instance.Play(done ? UiCue.Confirm : UiCue.Error);
        }

        // The panel's own presses, from the HUD or the world's canvas; another piece's go to whoever put it up.
        private bool Press(int command, int arg)
        {
            if (!_docked || _resume == null)
            {
                return _was != null && _was(command, arg);
            }
            switch (command)
            {
                case UiInfoPanel.TabCommand:
                    if (arg < 0 || arg >= _info.Tabs.Length)
                    {
                        return false;
                    }
                    _tab = arg;
                    _dirty = true;
                    return true;
                case UiInfoPanel.FooterCommand:
                    bool locate = arg >= 0 && arg == _locate;   // the address's camera move, the one footer button that is not the game's
                    Button b = locate ? null : arg < 0 ? _main : arg < _footer.Count ? _footer[arg] : null;
                    return locate ? Locate() : b != null && UiWidgets.Press(b);
                case UiInfoPanel.LinkCommand:
                    int link = _linkAt.IndexOf(arg);
                    return link >= 0 && Link(_linkKind[link]);
                case UiDraw.CloseCommand:
                    _resume.Close();
                    return true;
            }
            return _was != null && _was(command, arg);
        }

        // An owner's link opens the rival's page in the game's rivals app, as its contacts app opens one (ContactsApp's rival
        // button: the rival's leaderboard line, the app, the rival in it); the neighbourhood's opens the city map as the phone's
        // map app does.
        private bool Link(int kind)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.smartphoneUI == null)
            {
                return false;
            }
            if (kind == LinkMap)
            {
                if (!CityMap.CanOpenMap())
                {
                    return false;
                }
                uis.smartphoneUI.OpenApp(AppName.VoogleMaps);
                return true;
            }
            string id = kind == LinkOwner ? _ownerRival : kind == LinkBusinessOwner ? _businessRival : null;
            RivalData rival = id != null ? RivalsHelper.GetRivalData(id) : null;
            RivalLeaderboardData data = rival != null ? RivalLeaderboard.GetRivalLeaderboardData(rival) : null;
            if (data == null)
            {
                return false;
            }
            uis.smartphoneUI.OpenApp("Rivals");
            RivalsApp app = InstanceBehavior<RivalsApp>.Instance;
            app?.ShowRival(data);
            return app != null;
        }

        // The address's camera move: the city map opened on the building, as the game focuses one (CityMap.FocusOnBuilding).
        private bool Locate()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController cbc = _cbc;
            if (city == null || city.cityMap == null || cbc == null || !CityMap.CanOpenMap())
            {
                return false;
            }
            city.cityMap.FocusOnBuilding(cbc);
            return true;
        }

        // ------------------------------------------------------------------------------------------------- words

        // The words after a label's own name ("Neighborhood: Murray Hill" is Murray Hill), in any language's colon.
        private static string After(string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return null;
            }
            int at = Math.Max(words.LastIndexOf(':'), words.LastIndexOf('：'));
            return at >= 0 && at + 1 < words.Length ? words.Substring(at + 1).Trim() : words;
        }

        private static string FirstLine(string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return null;
            }
            int at = words.IndexOf('\n');
            return at < 0 ? words : words.Substring(0, at).Trim();
        }

        private static string Rest(string words)
        {
            int at = string.IsNullOrEmpty(words) ? -1 : words.IndexOf('\n');
            return at < 0 ? null : words.Substring(at + 1).Replace('\n', ' ').Trim();
        }

        private static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + ", " + b;

        // The business owner's line cut to its state, for a section's header ("Rented by you").
        private static string Short(string words) => string.IsNullOrEmpty(words) ? null : FirstLine(words);

        // A business's initials, as its sign would read: the first letters of its first two words.
        internal static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            var sb = new System.Text.StringBuilder(2);
            bool start = true;
            for (int i = 0; i < name.Length && sb.Length < 2; i++)
            {
                char c = name[i];
                if (char.IsLetterOrDigit(c))
                {
                    if (start)
                    {
                        sb.Append(char.ToUpperInvariant(c));
                    }
                    start = false;
                }
                else
                {
                    start = true;
                }
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }
    }
}
