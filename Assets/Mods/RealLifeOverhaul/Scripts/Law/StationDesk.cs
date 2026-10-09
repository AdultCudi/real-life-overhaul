using System;
using System.Text;
using BAModAPI;
using Extensions;
using Helpers;
using Localizor;
using TMPro;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The front desk of a police station: what the desk sergeant can do for somebody who walks
    /// in (or, at a precinct that cannot be walked into, for somebody at its door).
    ///   report a crime     the same as a 911 call about what is going on nearby (the crime
    ///                      scene's own report, a traffic accident, a fire), with a little extra
    ///                      standing with the police for coming in; and never a "wasted call"
    ///   pay fines          whatever is still owed (Core/ModState's pending fine), now, before it
    ///                      is taken by itself
    ///   the record         both reputations and the record, line by line
    ///   the pound          a car the police are holding after an arrest is brought to the kerb
    ///                      outside for the impound fee
    ///   licences           handed to the Licences feature, if that is there (Bridges.Licensing)
    ///   a police report    after a break-in or a crash: a reference number, and whatever the
    ///                      paperwork bridge makes of it (Bridges.PoliceReport)
    ///   hand yourself in   for somebody who got away after hurting someone: half the time
    ///   visiting hours     a word about how the station works
    /// One panel, built the first time it is wanted; while it is up the player stands still and
    /// the cursor is free. Nothing here runs unless LawDirector calls it.
    /// </summary>
    internal sealed class StationDesk
    {
        public const int PaperFee = 15;
        public const int TurnInDays = 7;             // how long after getting away somebody can still hand themselves in

        private const NavigationBlocker Blocker = NavigationBlocker.DoctorOperation;   // (as Custody: any blocker stops the player; nothing else on a street or in an empty unit sets this one)

        private enum Service { None, Report, Fines, Record, Impound, Licence, Paper, TurnIn, Hours, Leave, NextEntry, Back }

        private readonly IModLogger _logger;
        private readonly Transform _root;
        private readonly Precincts _precincts;
        private readonly Service[] _slots = new Service[DeskPanel.Options];
        private readonly StringBuilder _text = new StringBuilder(256);
        private DeskPanel _panel;
        private Action<int> _onClick;
        private int _clicked = -1;
        private Precinct _precinct;
        private bool _inside;
        private bool _held;
        private PlayerController _player;
        private int _recordLine = -1;        // the line of the record being read out, or -1
        private string _bringOnExit;         // a car asked for at a desk indoors: brought when the player steps outside
        private Precinct _bringTo;
        private string _prompt;
        private string _promptShown;
        private int _promptKeys = -1;

        public StationDesk(Transform root, Precincts precincts, IModLogger logger)
        {
            _root = root;
            _precincts = precincts;
            _logger = logger;
            _onClick = index => _clicked = index;
        }

        /// <summary>True while the desk's panel is on screen.</summary>
        public bool IsOpen => DeskPanel.IsOpen;

        // ---------------------------------------------------------------- the prompt

        /// <summary>The line at the foot of the screen that says what the interaction key does here ("talk to the desk sergeant"). Null takes it away.</summary>
        public void Prompt(string key, string precinct)
        {
            if (key == null)
            {
                if (_promptShown != null)
                {
                    _promptShown = null;
                    if (_panel != null)
                    {
                        _panel.HidePrompt();
                    }
                }
                return;
            }
            // Built only when it changes (the place, or a key binding).
            if (key != _prompt || _promptKeys != Keys.Version || _promptShown == null)
            {
                _prompt = key;
                _promptKeys = Keys.Version;
                _promptShown = key.Tr().Replace("{precinct}", precinct ?? string.Empty);
                Panel().ShowPrompt(_promptShown);
            }
        }

        private DeskPanel Panel()
        {
            if (_panel == null)
            {
                _panel = DeskPanel.Create(_root, _precincts.Icon);
            }
            return _panel;
        }

        // ---------------------------------------------------------------- opening and closing

        /// <summary>Opens the desk of a precinct. <paramref name="inside"/>: the player is standing in it (otherwise at its door, or anywhere for the test button).</summary>
        public void Open(Precinct precinct, bool inside)
        {
            if (precinct == null || IsOpen)
            {
                return;
            }
            _precinct = precinct;
            _inside = inside;
            _recordLine = -1;
            _clicked = -1;
            Prompt(null, null);
            _player = PlayerHelper.PlayerController;
            if (_player != null && !_held)
            {
                _held = true;
                _player.ResetNavigation();
                _player.SetNavigationBlocker(Blocker);
            }
            Panel().Open(precinct.Name, !inside, _onClick);
            _panel.SetLine(Greeting());
            Refresh();
            _logger?.Info("Law: front desk opened at " + precinct.Name + (inside ? " (inside)." : " (at the door)."));
        }

        /// <summary>Takes the desk off the screen and lets the player walk. Safe at any time.</summary>
        public void Close()
        {
            if (_panel != null)
            {
                _panel.Close();
            }
            if (_held)
            {
                _held = false;
                try
                {
                    if (_player != null)
                    {
                        _player.UnsetNavigationBlocker(Blocker);
                    }
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }
            _precinct = null;
            _recordLine = -1;
        }

        public void Dispose()
        {
            Close();
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel.gameObject);
                _panel = null;
            }
        }

        /// <summary>Every frame while the panel is up: what was clicked is done; the interaction key leaves the desk.</summary>
        public void Frame()
        {
            if (!IsOpen)
            {
                return;
            }
            if (SaveGameManager.Current == null || _precinct == null || Custody.Active)
            {
                Close();
                return;
            }
            int clicked = _clicked;
            _clicked = -1;
            if (clicked >= 0 && clicked < _slots.Length)
            {
                Do(_slots[clicked]);
            }
            else if (JobKeys.InteractDown())
            {
                Close();
            }
        }

        /// <summary>The player has stepped out into the street: a car asked for at the desk is brought round now.</summary>
        public void OnStreet()
        {
            if (_bringOnExit == null)
            {
                return;
            }
            string id = _bringOnExit;
            Precinct to = _bringTo;
            _bringOnExit = null;
            _bringTo = null;
            if (id == LawState.ImpoundVehicleId)
            {
                Bring(id, to);
            }
        }

        // ---------------------------------------------------------------- what is offered

        private string Greeting()
        {
            string key = Wanted ? "reallife_law_desk_greeting_wanted"
                : Settings.LawReputation && Reputation.Police >= 75 ? "reallife_law_desk_greeting_friend"
                : Settings.LawReputation && Reputation.Police < 35 ? "reallife_law_desk_greeting_known"
                : "reallife_law_desk_greeting";
            return key.Tr().Replace("{precinct}", _precinct.Name);
        }

        private static bool Wanted =>
            LawState.Loaded && LawState.WantedCharges != 0 && SaveGameManager.Current != null && TimeHelper.CurrentDay - LawState.WantedDay <= TurnInDays;

        // The buttons, as things stand now. A service that cannot be had is shown greyed with the reason in its own words; one
        // whose bridge is not there is not shown at all.
        private void Refresh()
        {
            int slot = 0;
            if (_recordLine >= 0)
            {
                Put(ref slot, _recordLine > 0 ? Service.NextEntry : Service.None, "reallife_records_next".Tr(), _recordLine > 0);
                Put(ref slot, Service.Back, "reallife_law_desk_back".Tr(), true);
            }
            else
            {
                Put(ref slot, Service.Report, "reallife_law_desk_report".Tr(), true);
                int owed = ModState.PendingFine;
                Put(ref slot, Service.Fines, owed > 0 ? Custody.Fill("reallife_law_desk_fines", "amount", ((float)owed).ToCurrencyFormat()) : "reallife_law_desk_fines_none".Tr(), owed > 0);
                Put(ref slot, Service.Record, "reallife_law_desk_record".Tr(), true);
                bool held = LawState.ImpoundVehicleId != null;
                Put(ref slot, Service.Impound, held ? Custody.Fill("reallife_law_desk_impound", "amount", ((float)LawBook.ImpoundFee).ToCurrencyFormat()) : "reallife_law_desk_impound_none".Tr(), held);
                if (Bridges.Licensing != null)
                {
                    Put(ref slot, Service.Licence, (string.IsNullOrEmpty(Bridges.LicensingLabelKey) ? "reallife_law_desk_licence" : Bridges.LicensingLabelKey).Tr(), true);
                }
                bool incident = Bridges.HasIncident;
                Put(ref slot, Service.Paper, incident ? Custody.Fill("reallife_law_desk_paper", "amount", ((float)PaperFee).ToCurrencyFormat()).Replace("{what}", IncidentName()) : "reallife_law_desk_paper_none".Tr(), incident);
                if (Wanted)
                {
                    Put(ref slot, Service.TurnIn, "reallife_law_desk_turnin".Tr(), Custody.Ready);
                }
                Put(ref slot, Service.Hours, "reallife_law_desk_hours".Tr(), true);
                Put(ref slot, Service.Leave, "reallife_law_desk_leave".Tr(), true);
            }
            for (; slot < _slots.Length; slot++)
            {
                _slots[slot] = Service.None;
                _panel.SetOption(slot, null, false);
            }
        }

        private void Put(ref int slot, Service service, string label, bool available)
        {
            if (slot >= _slots.Length || service == Service.None)
            {
                return;
            }
            _slots[slot] = available ? service : Service.None;
            _panel.SetOption(slot, label, available);
            slot++;
        }

        private static string IncidentName()
        {
            switch (LawState.IncidentKind)
            {
                case Bridges.BreakIn: return "reallife_law_paper_breakin".Tr();
                case Bridges.CarBreakIn: return "reallife_law_paper_car".Tr();
                case Bridges.Accident: return "reallife_law_paper_accident".Tr();
                default: return "reallife_law_paper_other".Tr();
            }
        }

        // ---------------------------------------------------------------- what is done

        private void Do(Service service)
        {
            try
            {
                switch (service)
                {
                    case Service.Report: Report(); break;
                    case Service.Fines: PayFines(); break;
                    case Service.Record: _recordLine = Record.Count; Say(Summary()); break;
                    case Service.NextEntry: NextEntry(); break;
                    case Service.Back: _recordLine = -1; Say(Greeting()); break;
                    case Service.Impound: Impound(); break;
                    case Service.Licence: Licence(); return;
                    case Service.Paper: Paper(); break;
                    case Service.TurnIn: TurnIn(); return;
                    case Service.Hours: Say("reallife_law_desk_hours_line".Tr()); break;
                    case Service.Leave: Close(); return;
                    default: return;
                }
                if (IsOpen)
                {
                    Refresh();
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                Close();
            }
        }

        private void Say(string line)
        {
            _panel.SetLine(line);
        }

        // What a 911 call would do about whatever is going on near the player, said across a counter; with nothing going
        // on it is a word and no more (nobody is fined for asking in person).
        private void Report()
        {
            if (Events.EventDirector.FireBurning)
            {
                Say((Events.EventDirector.Report911() ? "reallife_911_dispatched_fire" : "reallife_911_noted").Tr());
                return;
            }
            ReportKind kind = CrimeDirector.Reportable();
            bool taken;
            if (kind == ReportKind.Accident)
            {
                taken = TrafficAccidents.Instance != null && TrafficAccidents.Instance.ReportAccident();
                if (taken)
                {
                    ModState.AddModifier(ModState.Helped);
                }
            }
            else
            {
                taken = kind != ReportKind.None && CrimeDirector.ReportScene();
            }
            if (taken)
            {
                Reputation.AtTheDesk(2);   // for coming in with it
                Say("reallife_law_desk_report_taken".Tr());
                _logger?.Info("Law: a report (" + kind + ") was taken at the front desk of " + _precinct.Name + ".");
            }
            else
            {
                Say((CrimeDirector.IsActive || PoliceAttention.IsBusy || kind != ReportKind.None ? "reallife_law_desk_report_known" : "reallife_law_desk_report_nothing").Tr());
            }
        }

        private void PayFines()
        {
            int owed = ModState.PendingFine;
            if (owed <= 0)
            {
                Say("reallife_law_desk_fines_none".Tr());
                return;
            }
            if (!Crime.Pay(owed, "reallife_transaction_fine"))
            {
                Say("reallife_law_desk_fines_short".Tr());
                return;
            }
            ModState.ClearPendingFine();
            Reputation.AtTheDesk(1);
            Say(Custody.Fill("reallife_law_desk_fines_paid", "amount", ((float)owed).ToCurrencyFormat()));
            _logger?.Info($"Law: outstanding fines of {owed} paid at the front desk.");
        }

        private static string Summary()
        {
            int since = Record.DaysSinceLastOffence;
            string last = since < 0 ? "reallife_records_since_never".GetLocalization()
                : since == 0 ? "reallife_records_since_today".GetLocalization()
                : "reallife_records_since_days".GetLocalization().Replace("{days}", since.ToString());
            return "reallife_records_summary".GetLocalization()
                .Replace("{police}", Reputation.PoliceWord.GetLocalization()).Replace("{policeScore}", Reputation.Police.ToString())
                .Replace("{people}", Reputation.PublicWord.GetLocalization()).Replace("{peopleScore}", Reputation.Public.ToString())
                .Replace("{count}", Record.Count.ToString()).Replace("{since}", last)
                .Replace("{watch}", (Reputation.NoWarnings ? "reallife_records_watch_high" : Reputation.ScrutinyLevel > 0 ? "reallife_records_watch_some" : "reallife_records_watch_none").GetLocalization());
        }

        // The record, newest first, a line at a time.
        private void NextEntry()
        {
            int index = Mathf.Min(_recordLine, Record.Count) - 1;
            if (index < 0)
            {
                _recordLine = 0;
                Say((Record.Count == 0 ? "reallife_records_clean" : "reallife_records_end").GetLocalization());
                return;
            }
            _recordLine = index;
            RecordEntry line = Record.Entry(index);
            string outcome;
            switch (line.Outcome)
            {
                case Outcome.Held: outcome = "reallife_records_outcome_held".GetLocalization(); break;
                case Outcome.Served: outcome = "reallife_records_outcome_served".GetLocalization().Replace("{hours}", line.Hours.ToString()); break;
                case Outcome.Bailed: outcome = "reallife_records_outcome_bailed".GetLocalization().Replace("{hours}", line.Hours.ToString()); break;
                case Outcome.Escaped: outcome = "reallife_records_outcome_escaped".GetLocalization(); break;
                default: outcome = "reallife_records_outcome_cited".GetLocalization(); break;
            }
            Say("reallife_records_line".GetLocalization()
                .Replace("{number}", (Record.Count - index).ToString()).Replace("{count}", Record.Count.ToString()).Replace("{day}", line.Day.ToString())
                .Replace("{charges}", Custody.ChargeNames(line.Charges, ", ")).Replace("{outcome}", outcome)
                .Replace("{amount}", ((float)line.Fines).ToCurrencyFormat()));
        }

        // A car at the pound: brought to the kerb outside. Asked for indoors, it is there when the player steps out (the fee
        // is charged when it is brought, with the message that says where it is).
        private void Impound()
        {
            string id = LawState.ImpoundVehicleId;
            if (id == null)
            {
                Say("reallife_law_desk_impound_none".Tr());
                return;
            }
            if (Crime.Cash < LawBook.ImpoundFee)
            {
                Say(Custody.Fill("reallife_law_desk_impound_short", "amount", ((float)LawBook.ImpoundFee).ToCurrencyFormat()));
                return;
            }
            if (_inside || BuildingManager.IsInsideBuilding)
            {
                _bringOnExit = id;
                _bringTo = _precinct;
                Say("reallife_law_desk_impound_outside".Tr());
                return;
            }
            Bring(id, _precinct);
            Say("reallife_law_desk_impound_done".Tr());
        }

        private void Bring(string id, Precinct to)
        {
            LawState.ImpoundVehicleId = null;
            try
            {
                Vector3 near = to != null && Precincts.Front(to, out Vector3 front, out _) ? front : to != null ? to.Door : PlayerHelper.GetCityPosition();
                foreach (VehicleController vehicle in VehicleHelper.AllPlayerVehicles)
                {
                    if (vehicle != null && vehicle.vehicleInstance != null && vehicle.vehicleInstance.id == id)
                    {
                        if (JobSpots.Flat(vehicle.transform.position, near) < 70f)
                        {
                            Notifications.Show(NotificationType.Info, "reallife_law_desk_impound_here", null, 8f);   // it is already here: nothing to pay
                            return;
                        }
                        VehicleTow.BringNear(vehicle.vehicleInstance, near, LawBook.ImpoundFee, "reallife_law_car_impound", "reallife_law_car_impound_shop",
                            "reallife_law_car_impound_left", "reallife_transaction_impound");
                        _logger?.Info("Law: the car at the pound was asked for at the front desk and brought round.");
                        return;
                    }
                }
                Notifications.Show(NotificationType.Info, "reallife_law_desk_impound_gone", null, 8f);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void Licence()
        {
            Action open = Bridges.Licensing;
            Close();
            try
            {
                open?.Invoke();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void Paper()
        {
            if (!Bridges.HasIncident)
            {
                Say("reallife_law_desk_paper_none".Tr());
                return;
            }
            if (!Crime.Pay(PaperFee, "reallife_transaction_police_report"))
            {
                Say(Custody.Fill("reallife_law_desk_paper_short", "amount", ((float)PaperFee).ToCurrencyFormat()));
                return;
            }
            string kind = LawState.IncidentKind;
            LawState.ReportsGiven++;
            _text.Length = 0;
            _text.Append("PR-").Append((_precinct.Index + 1).ToString("00")).Append('-').Append(LawState.IncidentDay.ToString("0000")).Append('-').Append(LawState.ReportsGiven.ToString("000"));
            string reference = _text.ToString();
            LawState.IncidentKind = null;
            LawState.IncidentDay = -1;
            try
            {
                Bridges.PoliceReport?.Invoke(kind, reference);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            Say(Custody.Fill("reallife_law_desk_paper_done", "reference", reference));
            _logger?.Info("Law: police report " + reference + " (" + kind + ") handed over at the front desk.");
        }

        private void TurnIn()
        {
            Precinct precinct = _precinct;
            Close();
            if (!Custody.TurnIn(LawState.WantedCharges, precinct))
            {
                Notifications.Show(NotificationType.Warning, "reallife_law_desk_turnin_refused", null, 8f);
            }
        }
    }

    /// <summary>
    /// The front desk as the player sees it: a plate with the precinct's name, what the sergeant
    /// says, and a button for each thing that can be asked for. At a precinct's door (where there
    /// is no room to walk into) it has the front office drawn behind it. It also owns the one
    /// line at the foot of the screen that says what the interaction key does at a station.
    /// Built the first time it is wanted and kept switched off between; a text is only set when
    /// what it says has changed.
    /// </summary>
    internal sealed class DeskPanel : MonoBehaviour
    {
        public const int Options = 10;

        private static readonly Color Dim = new Color(0.02f, 0.03f, 0.05f, 0.45f);
        private static readonly Color Office = new Color(0.045f, 0.06f, 0.10f, 0.97f);
        private static readonly Color Plate = new Color(0.085f, 0.10f, 0.135f, 0.97f);
        private static readonly Color Accent = new Color(0.20f, 0.42f, 0.95f, 1f);
        private static readonly Color Soft = new Color(0.66f, 0.72f, 0.82f);
        private static readonly Color ButtonOn = new Color(0.16f, 0.20f, 0.28f, 1f);
        private static readonly Color ButtonOff = new Color(0.10f, 0.11f, 0.13f, 1f);

        /// <summary>True while the desk is on screen: the mouse cursor is needed (CustodyPanel.IsOpen answers for both).</summary>
        public static bool IsOpen { get; private set; }

        private GameObject _root;
        private GameObject _office;
        private GameObject _prompt;
        private Image _backdrop;
        private TMP_FontAsset _font;
        private TextMeshProUGUI _precinct;
        private TextMeshProUGUI _line;
        private TextMeshProUGUI _promptText;
        private readonly TextMeshProUGUI[] _optionText = new TextMeshProUGUI[Options];
        private readonly Image[] _optionPlate = new Image[Options];
        private readonly Button[] _optionButton = new Button[Options];
        private readonly bool[] _optionOn = new bool[Options];
        private Action<int> _chosen;
        private Sprite _badge;

        public static DeskPanel Create(Transform parent, Sprite badge)
        {
            var go = new GameObject("RealLifeOverhaul Front Desk");
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<DeskPanel>();
            panel.Build(badge);
            return panel;
        }

        private void Build(Sprite badge)
        {
            _badge = badge;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29000;   // over the game's own screens, under the holding cell
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            _font = Hud.Font;

            _root = new GameObject("Desk", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var all = (RectTransform)_root.transform;
            Stretch(all);
            _backdrop = _root.AddComponent<Image>();
            _backdrop.color = Dim;
            _backdrop.raycastTarget = true;   // nothing under the desk can be clicked

            // The front office, drawn: a wall with a blue band, the badge, a counter. Only at a precinct with no room to walk into.
            _office = new GameObject("Office", typeof(RectTransform));
            _office.transform.SetParent(all, false);
            var office = (RectTransform)_office.transform;
            Stretch(office);
            Box("Band", office, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(2400f, 54f), new Color(0.06f, 0.17f, 0.45f, 0.9f));
            Box("Rule", office, new Vector2(0.5f, 1f), new Vector2(0f, -182f), new Vector2(2400f, 4f), new Color(0.9f, 0.9f, 0.95f, 0.5f));
            Box("Counter", office, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(2400f, 180f), new Color(0.05f, 0.11f, 0.26f, 1f));
            Box("Counter Top", office, new Vector2(0.5f, 0f), new Vector2(0f, 186f), new Vector2(2400f, 14f), new Color(0.62f, 0.64f, 0.68f, 1f));
            if (badge != null)
            {
                RectTransform big = Box("Badge", office, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(760f, 760f), new Color(1f, 1f, 1f, 0.045f));
                big.GetComponent<Image>().sprite = badge;
            }

            RectTransform plate = Box("Plate", all, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1100f, 690f), Plate);
            Box("Stripe", plate, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(1100f, 8f), Accent);
            RectTransform disc = Box("Disc", plate, new Vector2(0f, 1f), new Vector2(70f, -62f), new Vector2(72f, 72f), Accent);
            if (badge != null)
            {
                RectTransform star = Box("Star", disc, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f), Color.white);
                star.GetComponent<Image>().sprite = badge;
            }
            TextMeshProUGUI title = Text("Title", plate, 21f, Soft, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(40f, -26f), new Vector2(900f, 28f));
            title.SetText("reallife_law_desk_title".Tr());
            _precinct = Text("Precinct", plate, 38f, Color.white, TextAlignmentOptions.Left);
            Place(_precinct.rectTransform, new Vector2(40f, -54f), new Vector2(900f, 50f));

            _line = Text("Line", plate, 24f, Color.white, TextAlignmentOptions.TopLeft);
            Place(_line.rectTransform, new Vector2(0f, -128f), new Vector2(1010f, 150f));
#pragma warning disable CS0618
            _line.enableWordWrapping = true;
#pragma warning restore CS0618
            _line.fontStyle = FontStyles.Normal;
            _line.enableAutoSizing = true;
            _line.fontSizeMin = 16f;
            _line.fontSizeMax = 24f;

            for (int i = 0; i < Options; i++)
            {
                int index = i;
                RectTransform button = Box("Option", plate, new Vector2(0.5f, 1f), new Vector2((i % 2 == 0 ? -1 : 1) * 258f, -326f - (i / 2) * 68f), new Vector2(500f, 60f), ButtonOn);
                _optionPlate[i] = button.GetComponent<Image>();
                _optionPlate[i].raycastTarget = true;
                _optionButton[i] = button.gameObject.AddComponent<Button>();
                _optionButton[i].targetGraphic = _optionPlate[i];
                _optionButton[i].onClick.AddListener(() => Click(index));
                Box("Tab", button, new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(6f, 44f), Accent);
                _optionText[i] = Text("Label", button, 20f, Color.white, TextAlignmentOptions.Left);
                _optionText[i].rectTransform.anchorMin = new Vector2(0f, 0f);
                _optionText[i].rectTransform.anchorMax = new Vector2(1f, 1f);
                _optionText[i].rectTransform.offsetMin = new Vector2(24f, 4f);
                _optionText[i].rectTransform.offsetMax = new Vector2(-10f, -4f);
#pragma warning disable CS0618
                _optionText[i].enableWordWrapping = true;
#pragma warning restore CS0618
                _optionText[i].enableAutoSizing = true;
                _optionText[i].fontSizeMin = 13f;
                _optionText[i].fontSizeMax = 20f;
            }
            TextMeshProUGUI hint = Text("Hint", plate, 18f, Soft, TextAlignmentOptions.Center);
            hint.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            hint.rectTransform.sizeDelta = new Vector2(1000f, 24f);
            hint.SetText("reallife_law_desk_hint".Tr());
            _root.SetActive(false);

            _prompt = new GameObject("Prompt", typeof(RectTransform));
            _prompt.transform.SetParent(transform, false);
            var prompt = (RectTransform)_prompt.transform;
            prompt.anchorMin = new Vector2(0.5f, 0f);
            prompt.anchorMax = new Vector2(0.5f, 0f);
            prompt.pivot = new Vector2(0.5f, 0f);
            prompt.anchoredPosition = new Vector2(0f, 168f);
            prompt.sizeDelta = new Vector2(760f, 50f);
            var promptPlate = _prompt.AddComponent<Image>();
            promptPlate.color = new Color(0.05f, 0.07f, 0.11f, 0.88f);
            promptPlate.raycastTarget = false;
            Box("Tab", prompt, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, 50f), Accent);
            _promptText = Text("Text", prompt, 22f, Color.white, TextAlignmentOptions.Center);
            Stretch(_promptText.rectTransform);
            _promptText.enableAutoSizing = true;
            _promptText.fontSizeMin = 14f;
            _promptText.fontSizeMax = 22f;
            _prompt.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_root != null && _root.activeSelf)
            {
                IsOpen = false;
            }
        }

        private void Click(int index)
        {
            if (index >= 0 && index < Options && _optionOn[index])
            {
                _chosen?.Invoke(index);
            }
        }

        /// <summary>Puts the desk on screen. <paramref name="chosen"/> is told which button was clicked.</summary>
        /// <param name="office">True at a precinct's door: the front office is drawn behind the plate. False inside one: the room is seen behind it.</param>
        public void Open(string precinct, bool office, Action<int> chosen)
        {
            _chosen = chosen;
            _precinct.SetText(precinct);
            _office.SetActive(office);
            _backdrop.color = office ? Office : Dim;
            _prompt.SetActive(false);
            _root.SetActive(true);
            IsOpen = true;
        }

        public void Close()
        {
            _chosen = null;
            if (_root != null && _root.activeSelf)
            {
                _root.SetActive(false);
            }
            IsOpen = false;
        }

        public void SetLine(string text)
        {
            _line.SetText(text ?? string.Empty);
        }

        /// <summary>What a button says, and whether it can be pressed. A null label takes the button away.</summary>
        public void SetOption(int index, string label, bool available)
        {
            if (index < 0 || index >= Options)
            {
                return;
            }
            bool shown = label != null;
            if (_optionButton[index].gameObject.activeSelf != shown)
            {
                _optionButton[index].gameObject.SetActive(shown);
            }
            _optionOn[index] = shown && available;
            if (!shown)
            {
                return;
            }
            _optionText[index].SetText(label);
            _optionText[index].color = available ? Color.white : Soft;
            _optionPlate[index].color = available ? ButtonOn : ButtonOff;
            _optionButton[index].interactable = available;
        }

        public void ShowPrompt(string text)
        {
            _promptText.SetText(text ?? string.Empty);
            if (!_prompt.activeSelf && !IsOpen)
            {
                _prompt.SetActive(true);
            }
        }

        public void HidePrompt()
        {
            if (_prompt != null && _prompt.activeSelf)
            {
                _prompt.SetActive(false);
            }
        }

        private static RectTransform Box(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color colour)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        private TextMeshProUGUI Text(string objectName, Transform parent, float size, Color colour, TextAlignmentOptions alignment)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = colour;
            // The SDK's TextMeshPro is older than the game's: this property is the one both versions have.
#pragma warning disable CS0618
            text.enableWordWrapping = false;
#pragma warning restore CS0618
            if (_font != null)
            {
                text.font = _font;
            }
            return text;
        }

        // Anchored to the top centre of the parent, hanging down from the given offset.
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
