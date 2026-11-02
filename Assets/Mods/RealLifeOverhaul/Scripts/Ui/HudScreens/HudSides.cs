using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Extensions;
using Helpers;
using Streets;
using TMPro;
using UI;
using UI.CurrentBuilding;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The side panels at the top right, mirrored from what the game and the mod show there: the mod's shift cards (every job,
    /// Civic canvassing, Family, community service, Leisure, the practical test, Social: each card's headline, lines in their
    /// colours and its bar), the game's current job (its title, business, address, today and tomorrow in the game's
    /// colours, Set destination and Quit job) and the current building (its address, name and kind, the open state in the
    /// game's colour, today's opening hours, parking, the temporarily closed switch, BizMan, the interior designer and the
    /// walls). At most two stand open, the shift first, then the job, then the building; the rest show their strip, and a
    /// panel the player folds or opens stays so for the session. The game's panels stay up, hidden by leaf, so whatever
    /// reads them (Going Public's owner actions, VoogleRoute) reads them as before; every button presses the game's own.
    /// Cost: four reads a second of a few dozen fields while the HUD shows.
    /// </summary>
    internal static class HudSides
    {
        private const int Shift = 0, Job = 1, Building = 2;
        private const int MostPanels = 6;

        private static readonly UiConceal Hidden = new UiConceal();
        private static readonly List<ShiftCard> Cards = new List<ShiftCard>(4);
        private static readonly UiSidePanelModel[] Pool = new UiSidePanelModel[MostPanels];
        private static readonly int[] Kinds = new int[MostPanels];
        private static readonly UiSidePanelModel[][] Stacks = new UiSidePanelModel[MostPanels + 1][];
        private static readonly Dictionary<int, bool> Folded = new Dictionary<int, bool>();   // the player's own fold per kind, this session
        private static readonly UiInfoRow[] Rows = new UiInfoRow[UiSidePanelModel.MostRows];
        private static readonly UiActionModel[] Acts = new UiActionModel[UiSidePanelModel.MostActions];

        private static UI.CurrentJob.CurrentJob _job;
        private static CurrentBuildingUI _building;
        private static Transform _buildingPanel;
        private static Button _destination, _quit, _bizman, _walls, _designer;
        private static Toggle _closed;
        private static int _count;
        private static bool _on;

        static HudSides()
        {
            for (int i = 0; i < MostPanels; i++)
            {
                Pool[i] = new UiSidePanelModel();
            }
            for (int n = 0; n <= MostPanels; n++)
            {
                Stacks[n] = new UiSidePanelModel[n];
            }
        }

        /// <summary>The game's job and building panels hidden by leaf and the mod's cards unseen; false while they are not built.</summary>
        public static bool Begin(UIs uis)
        {
            UI.PlayerHUD.PlayerHUD hud = uis != null ? uis.playerHUD : null;
            if (hud == null || hud.currentJobUI == null || hud.currentBuildingUI == null)
            {
                return false;
            }
            _job = hud.currentJobUI;
            _building = hud.currentBuildingUI;
            _buildingPanel = _building.transform.Find("Panel");
            Transform jobPanel = _job.panel != null ? _job.panel.transform : null;
            _destination = Child<Button>(jobPanel, "SetDestinationButton");
            _quit = Child<Button>(jobPanel, "QuitJobButton");
            _bizman = Child<Button>(_buildingPanel, "OpenBizman/BuildingButton");
            _walls = Child<Button>(_buildingPanel, "ToggleWalls/ToggleWallsButton");
            _designer = Child<Button>(_buildingPanel, "InteriorDesigner/InteriorDesignerButton");
            _closed = Child<Toggle>(_buildingPanel, "TemporaryClosed/TemporarilyClosed/Toggle");
            Hidden.Add(jobPanel);
            Hidden.Add(_buildingPanel);
            Hidden.Hide();
            ShiftCard.Unseen(true);
            _on = true;
            return true;
        }

        /// <summary>The game's panels and the mod's cards given back as found.</summary>
        public static void End()
        {
            if (!_on)
            {
                return;
            }
            _on = false;
            Hidden.Clear();
            ShiftCard.Unseen(false);
            _count = 0;
        }

        /// <summary>A new city: the panels are looked for again and the folds forgotten.</summary>
        public static void Rearm()
        {
            End();
            _job = null;
            _building = null;
            _buildingPanel = null;
            _destination = _quit = _bizman = _walls = _designer = null;
            _closed = null;
            Folded.Clear();
        }

        /// <summary>The panels read again into the HUD's model; the signature of what shows.</summary>
        public static int Look(UiHudModel model)
        {
            if (!_on)
            {
                return 0;
            }
            _count = 0;
            int sig = 0;
            unchecked
            {
                ShiftCard.Count(Cards);
                for (int i = 0; i < Cards.Count && _count < MostPanels; i++)
                {
                    if (Cards[i].Shown)
                    {
                        sig = sig * 31 + ShiftPanel(Next(Shift), Cards[i]);
                    }
                }
                if (_job != null && _job.panel != null && _job.panel.activeInHierarchy && _count < MostPanels)
                {
                    sig = sig * 31 + Guarded(JobPanel, Next(Job));
                }
                if (_buildingPanel != null && _buildingPanel.gameObject.activeInHierarchy && _count < MostPanels)
                {
                    sig = sig * 31 + Guarded(BuildingPanel, Next(Building));
                }
                // At most two open, by the order they were added; the player's own fold keeps for the session.
                int open = 0;
                for (int i = 0; i < _count; i++)
                {
                    bool folded = Folded.TryGetValue(Kinds[i], out bool f) ? f : open >= 2;
                    Pool[i].Collapsed = folded;
                    if (!folded)
                    {
                        open++;
                    }
                    sig = sig * 31 + (folded ? 1 : 2);
                }
            }
            UiSidePanelModel[] stack = Stacks[_count];
            for (int i = 0; i < _count; i++)
            {
                stack[i] = Pool[i];
            }
            model.Side = stack;
            return sig * 31 + _count;
        }

        private static UiSidePanelModel Next(int kind)
        {
            Kinds[_count] = kind;
            UiSidePanelModel m = Pool[_count++];
            m.Title = m.Meta = m.HeadName = m.HeadSub = m.HeadChip = m.SwitchLabel = m.ProgressUnder = null;
            m.HeadTone = UiRole.Ok;
            m.Icon = UiIcon.Briefcase;
            m.HasProgress = m.HasTiming = false;
            m.SwitchOn = m.SwitchDisabled = m.ToolOn = false;
            m.Tool = UiIcon.None;
            return m;
        }

        // ------------------------------------------------------------------------------------------------- the shift

        // A shift card: "Your shift" over the card's own headline, its lines in their colours, its bar as a meter or a gauge.
        private static int ShiftPanel(UiSidePanelModel m, ShiftCard card)
        {
            m.Title = UiText.T("reallife_ui_hud_shift");
            string headline = card.TitleWords;
            int rows = 0, sig = Hash(headline);
            if (!string.IsNullOrEmpty(headline) && UiTextRules.Width(headline, UiStyle.Small) + UiTokens.S1 > UiSidePanel.Width * 0.5f)
            {
                Rows[rows++] = new UiInfoRow { Label = headline };   // too long for the strip's quiet word: the card's first line
            }
            else
            {
                m.Meta = headline;
            }
            for (int line = 0; line < ShiftCard.Lines && rows < UiSidePanelModel.MostRows; line++)
            {
                string words = card.Words(line);
                if (string.IsNullOrEmpty(words))
                {
                    continue;
                }
                Color32 c = card.Colour(line);
                Rows[rows++] = new UiInfoRow { Label = words, Ink = Ink(c) };
                unchecked
                {
                    sig = sig * 31 + words.GetHashCode() + c.r * 3 + c.g * 5 + c.b * 7;
                }
            }
            SetRows(m, rows);
            SetActions(m, 0);
            GameUi.GameBar bar = card.MadeBar;
            if (bar != null && bar.On)
            {
                if (bar.Timing)
                {
                    bar.Zones(out float from, out float to, out float bestFrom, out float bestTo);
                    m.HasTiming = true;
                    m.Timing = new UiTimingModel { ZoneFrom = from, ZoneTo = to, Marker = bar.Value };
                    unchecked
                    {
                        sig = sig * 31 + (int)(bar.Value * 200f) + (int)(from * 100f) * 7 + (int)(to * 100f) * 13;
                    }
                }
                else
                {
                    m.HasProgress = true;
                    m.Progress = new UiProgressModel { Share = bar.Value };
                    unchecked
                    {
                        sig = sig * 31 + (int)(bar.Value * 200f);
                    }
                }
            }
            return sig;
        }

        // ------------------------------------------------------------------------------------------------- the job

        // The game's job panel as it wrote itself: its title over the business and address, today and tomorrow in its colours.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int JobPanel(UiSidePanelModel m)
        {
            string title = Words(_job.title != null ? _job.title.TextContainer : null);
            m.Title = UiText.T("reallife_ui_hud_job");
            m.Meta = title;
            m.Icon = UiIcon.Briefcase;
            m.HeadName = _job.businessName != null ? UiWidgets.Text(_job.businessName) : null;
            m.HeadSub = Words(_job.address != null ? _job.address.TextContainer : null);
            m.HeadChip = null;
            int rows = 0;
            int sig = Hash(title) * 7 + Hash(m.HeadName) * 3 + Hash(m.HeadSub);
            sig = Day(_job.todayWorkStatus != null ? _job.todayWorkStatus.TextContainer : null, ref rows, sig);
            sig = Day(_job.tomorrowWorkStatus != null ? _job.tomorrowWorkStatus.TextContainer : null, ref rows, sig);
            SetRows(m, rows);
            int actions = 0;
            if (UiWidgets.There(_destination))
            {
                Acts[actions++] = new UiActionModel
                {
                    Label = UiWidgets.Label(_destination), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Count,
                    Disabled = !_destination.interactable,
                };
            }
            if (UiWidgets.There(_quit))
            {
                Acts[actions++] = new UiActionModel { Label = UiWidgets.Label(_quit), Kind = UiButtonKind.Destructive, Key = UiAction.Count, Disabled = !_quit.interactable };
            }
            SetActions(m, actions);
            for (int a = 0; a < actions; a++)
            {
                sig = unchecked(sig * 31 + Hash(Acts[a].Label) + (Acts[a].Disabled ? 1 : 0));
            }
            return unchecked(sig * 31 + actions);
        }

        // A day's line ("Today: 09:00 - 17:00") as a label and its value, in the game's colour for it.
        private static int Day(TMP_Text text, ref int rows, int sig)
        {
            string words = Words(text);
            if (string.IsNullOrEmpty(words) || rows >= UiSidePanelModel.MostRows)
            {
                return sig;
            }
            int colon = words.IndexOf(':');
            bool split = colon > 0 && colon < words.Length - 1 && words[colon + 1] == ' ';
            Rows[rows++] = split
                ? new UiInfoRow { Label = words.Substring(0, colon), Value = words.Substring(colon + 2), Ink = Ink(text.color) }
                : new UiInfoRow { Label = words, Ink = Ink(text.color) };
            Color c = text.color;
            return unchecked(sig * 31 + words.GetHashCode() + (int)(c.r * 255f) * 3 + (int)(c.g * 255f) * 5);
        }

        // ------------------------------------------------------------------------------------------------- the building

        // The game's building panel as it wrote itself (the name or address, the open state in its colour, the parking,
        // the switch and the buttons the game left on, Going Public's owner actions among them), with the address, the
        // kind and today's opening hours from the building's own registration.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int BuildingPanel(UiSidePanelModel m)
        {
            string headline = Words(Child<TMP_Text>(_buildingPanel, "Headline/Label"));
            if (InParking())
            {
                return ParkingPanel(m, headline);
            }
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            BuildingRegistration reg = manager != null ? manager.buildingRegistration : null;
            string address = reg != null && reg.Address != null ? reg.Address.ToFormattedString() : headline;
            bool owned = manager != null && manager.IsPlayerOwnedBusiness;
            m.Title = address;
            m.Meta = owned ? UiText.T("reallife_ui_hud_your_business") : null;
            m.Icon = owned ? UiIcon.Store : UiIcon.Building;
            m.HeadName = reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : headline != address ? headline : null;
            string kind = manager != null && manager.businessType != null && !string.IsNullOrEmpty(manager.businessType.businessTypeName)
                && manager.businessType.businessTypeName != "ba:businesstype_empty" ? UiText.T(manager.businessType.businessTypeName) : null;
            string district = HudLive.LastBar != null ? HudLive.LastBar.District : null;
            m.HeadSub = kind != null && district != null ? kind + ", " + district : kind ?? district;
            if (m.HeadName == null)
            {
                m.HeadName = m.HeadSub;   // a building with no business: its kind and district stand as its name
                m.HeadSub = null;
            }
            TMP_Text state = Child<TMP_Text>(_buildingPanel, "OpenState/OpenState");
            bool stateShows = state != null && state.gameObject.activeInHierarchy && !string.IsNullOrEmpty(state.text);
            m.HeadChip = stateShows ? Words(state) : null;
            m.HeadTone = stateShows ? Tone(state.color, UiRole.None) : UiRole.Ok;
            int rows = 0;
            int sig = Hash(m.Title) * 7 + Hash(m.HeadName) * 5 + Hash(m.HeadChip) * 3 + (owned ? 1 : 0);
            if (reg != null && kind != null && rows < UiSidePanelModel.MostRows)
            {
                string hours = Hours(reg);
                if (hours != null)
                {
                    Rows[rows++] = new UiInfoRow { Label = UiText.T("bizman_schedule_opening_hours"), Value = hours };
                    sig = unchecked(sig * 31 + hours.GetHashCode());
                }
            }
            Transform parking = _buildingPanel.Find("ParkingInfo");
            if (parking != null && parking.gameObject.activeInHierarchy)
            {
                sig = Line(Child<TMP_Text>(parking, "Price"), ref rows, sig);
                sig = Line(Child<TMP_Text>(parking, "ParkedVehicles"), ref rows, sig);
            }
            SetRows(m, rows);
            if (_closed != null && _closed.gameObject.activeInHierarchy)
            {
                m.SwitchLabel = Words(Child<TMP_Text>(_buildingPanel, "TemporaryClosed/TemporarilyClosed/Label"));
                m.SwitchOn = _closed.isOn;
                m.SwitchDisabled = !_closed.interactable;
                sig = unchecked(sig * 31 + (m.SwitchOn ? 3 : 1) + Hash(m.SwitchLabel));
            }
            int actions = 0;
            if (_bizman != null && _bizman.gameObject.activeInHierarchy)
            {
                Acts[actions++] = new UiActionModel { Label = UiWidgets.Label(_bizman), Kind = UiButtonKind.Primary, Key = UiAction.Count, Disabled = !_bizman.interactable };
            }
            if (_designer != null && _designer.gameObject.activeInHierarchy)
            {
                Acts[actions++] = new UiActionModel { Label = UiWidgets.Label(_designer), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Disabled = !_designer.interactable };
            }
            SetActions(m, actions);
            for (int a = 0; a < actions; a++)
            {
                sig = unchecked(sig * 31 + Hash(Acts[a].Label) + (Acts[a].Disabled ? 1 : 0));
            }
            if (actions > 0 && _walls != null && _walls.gameObject.activeInHierarchy)
            {
                m.Tool = UiIcon.Grid;
                m.ToolOn = WallsLowered();
            }
            return unchecked(sig * 31 + actions * 4 + (m.Tool != UiIcon.None ? 2 : 0) + (m.ToolOn ? 1 : 0));
        }

        // An underground parking as the game's panel writes it: "Parking (its address)" over the price an hour and the cars
        // parked there; nothing of the building above it, no switch, no buttons.
        private static int ParkingPanel(UiSidePanelModel m, string headline)
        {
            m.Title = headline;
            m.Icon = UiIcon.Car;
            int rows = 0, sig = Hash(headline) * 7 + 5;
            Transform parking = _buildingPanel.Find("ParkingInfo");
            if (parking != null && parking.gameObject.activeInHierarchy)
            {
                sig = Line(Child<TMP_Text>(parking, "Price"), ref rows, sig);
                sig = Line(Child<TMP_Text>(parking, "ParkedVehicles"), ref rows, sig);
            }
            SetRows(m, rows);
            SetActions(m, 0);
            return unchecked(sig * 31 + rows);
        }

        // The player is in an underground car park; false on a game without the car park's manager.
        private static bool InParking()
        {
            try
            {
                return Parked();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the HUD's car park panel");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Parked() => Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking;

        // Today's opening hours as the game's own contact card writes them; null for none kept.
        private static string Hours(BuildingRegistration reg)
        {
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            if (today == null)
            {
                return null;
            }
            if (!today.isOpen || today.openingHourSlots == null || today.openingHourSlots.Count == 0)
            {
                return UiText.T("contacts_today_opening_hours_closed");
            }
            int from = 24, to = 0;
            for (int i = 0; i < today.openingHourSlots.Count; i++)
            {
                from = Math.Min(from, today.openingHourSlots[i].startingHour);
                to = Math.Max(to, today.openingHourSlots[i].endingHour);
            }
            return UiText.T("contacts_today_opening_hours_open").Replace("{startingHour}", from.GetFormattedTime()).Replace("{endingHour}", to.GetFormattedTime());
        }

        // The walls' state as the game's own walls button shows it: its picture is the walls standing only while they all stand.
        private static bool WallsLowered() => _building.wallsButtonImage != null && _building.wallsButtonImage.sprite != _building.wallsVisibleSprite;

        // A line of the game's own words as a row of its own, its label and value apart where the game wrote "Label: value".
        private static int Line(TMP_Text text, ref int rows, int sig)
        {
            if (text == null || !text.gameObject.activeInHierarchy || rows >= UiSidePanelModel.MostRows)
            {
                return sig;
            }
            string words = Words(text);
            if (string.IsNullOrEmpty(words))
            {
                return sig;
            }
            int colon = words.IndexOf(": ", StringComparison.Ordinal);
            Rows[rows++] = colon > 0 && colon < words.Length - 2
                ? new UiInfoRow { Label = words.Substring(0, colon), Value = words.Substring(colon + 2) }
                : new UiInfoRow { Label = words };
            return unchecked(sig * 31 + words.GetHashCode());
        }

        // ------------------------------------------------------------------------------------------------- presses

        /// <summary>A side panel's command: its fold (the player's choice), its buttons, its tool and its switch, each the game's own.</summary>
        public static bool Press(int command, int arg)
        {
            int panel = command == UiSidePanel.LinkCommand ? arg / 8 : command == UiSidePanel.ActionCommand ? arg / 4 : arg;
            if (panel < 0 || panel >= _count)
            {
                return false;
            }
            int kind = Kinds[panel];
            switch (command)
            {
                case UiSidePanel.FoldCommand:
                    Folded[kind] = !Pool[panel].Collapsed;
                    return true;
                case UiSidePanel.ActionCommand:
                    return Action(kind, arg % 4);
                case UiSidePanel.ToolCommand:
                    return kind == Building && UiWidgets.Press(_walls);
                case UiSidePanel.SwitchCommand:
                    return kind == Building && _closed != null && UiWidgets.Set(_closed, !_closed.isOn);
            }
            return false;
        }

        // The action-th button the panel drew, in the order it drew them: the same buttons, read again as they are now.
        private static bool Action(int kind, int index)
        {
            if (kind == Job)
            {
                Button[] order = { UiWidgets.There(_destination) ? _destination : null, UiWidgets.There(_quit) ? _quit : null };
                return UiWidgets.Press(Nth(order, index));
            }
            if (kind == Building)
            {
                Button[] order =
                {
                    _bizman != null && _bizman.gameObject.activeInHierarchy ? _bizman : null,
                    _designer != null && _designer.gameObject.activeInHierarchy ? _designer : null
                };
                return UiWidgets.Press(Nth(order, index));
            }
            return false;
        }

        private static Button Nth(Button[] order, int index)
        {
            int seen = 0;
            for (int i = 0; i < order.Length; i++)
            {
                if (order[i] != null && seen++ == index)
                {
                    return order[i];
                }
            }
            return null;
        }

        // ------------------------------------------------------------------------------------------------- shared

        /// <summary>A colour the game writes a state in, as the kit's tone: its green good, its red bad, its orange and yellow a warning, its grey quiet.</summary>
        internal static UiRole Tone(Color c, UiRole plain)
        {
            float max = Math.Max(c.r, Math.Max(c.g, c.b)), min = Math.Min(c.r, Math.Min(c.g, c.b));
            if (max - min < 0.12f)
            {
                return max < 0.75f ? UiRole.Muted : plain;   // white is the words' own, a grey is quiet
            }
            if (c.g >= c.r && c.g >= c.b)
            {
                return UiRole.Ok;
            }
            if (c.r >= c.g && c.r >= c.b)
            {
                return c.g > 0.45f ? UiRole.Warn : UiRole.Danger;
            }
            return plain;
        }

        /// <summary>A colour the game writes words in, as the kit's ink for words: its green, red and orange in the kit's text tones, its grey quiet.</summary>
        internal static UiRole Ink(Color c)
        {
            switch (Tone(c, UiRole.None))
            {
                case UiRole.Ok: return UiRole.OkText;
                case UiRole.Warn: return UiRole.WarnText;
                case UiRole.Danger: return UiRole.DangerText;
                case UiRole.Muted: return UiRole.Muted;
            }
            return UiRole.None;
        }

        private static void SetRows(UiSidePanelModel m, int rows)
        {
            if (m.Rows.Length != rows)
            {
                m.Rows = new UiInfoRow[rows];
            }
            Array.Copy(Rows, m.Rows, rows);
        }

        private static void SetActions(UiSidePanelModel m, int actions)
        {
            if (m.Actions.Length != actions)
            {
                m.Actions = new UiActionModel[actions];
            }
            Array.Copy(Acts, m.Actions, actions);
        }

        private static string Words(TMP_Text t) => t != null && t.gameObject.activeInHierarchy ? UiWidgets.Text(t) : null;

        private static int Hash(string s) => s != null ? s.GetHashCode() : 0;

        private static T Child<T>(Transform parent, string path) where T : Component
        {
            Transform t = parent != null ? parent.Find(path) : null;
            return t != null ? t.GetComponent<T>() : null;
        }

        private static int Guarded(Func<UiSidePanelModel, int> read, UiSidePanelModel m)
        {
            try
            {
                return read(m);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the HUD's job and building panels");
                _count--;   // the panel is left out; the game's own reading of it is unchanged
                return 0;
            }
        }
    }
}
