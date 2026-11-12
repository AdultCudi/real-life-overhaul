using System;
using Entities;
using Entities.Employee.JobDemands;
using Helpers;
using Items.SpecialItems;
using Localizor;
using Player.HUD.ItemInfoOverlays;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A person at their station, clicked: the info panel docked at the top left (in the HUD's left column while the kit's HUD
    /// shows) with their face in their mood's ring, the shift they are on and what they have worked of it, their mood and best
    /// skill, their wage and what the week has earned, and the buttons the game's own box had for their page and their
    /// schedule, with the map moved to their workplace, a route to it, and the camera that follows them. Read when the person or
    /// the minute changes; laid out when what it says changed.
    /// </summary>
    internal sealed partial class OverlayCardsLive
    {
        private const int Locate = 0, Route = 1, Follow = 2, Schedule = 3;   // the footer's icon buttons, in order

        private readonly UiScene _personScene = new UiScene();
        private readonly UiInfoModel _personInfo = new UiInfoModel();
        private UiView _personView;
        private bool _personUp, _inHud;
        private int _personId, _personKey = int.MinValue;
        private float _personDue;
        private EmployeeStationController _station;
        private EmployeeInstance _who;
        private Func<int, int, bool> _chain, _was;
        private UiInfoModel _before;

        /// <summary>True while a person's panel stands.</summary>
        public bool EmployeeShowing => _personUp;

        /// <summary>The panel's model, for the tests.</summary>
        public UiInfoModel PersonInfo => _personInfo;

        private void BuildEmployee(RectTransform rect, float scale)
        {
            _personView = new UiView(rect, "Person") { Scale = scale, Ring = false };
            _personView.PointerPressed = Guard.Wrap<int>(PersonPressed);
            _personView.PointerEntered = Guard.Wrap<int>(node => _personView.SetHover(node));
            _personView.PointerExited = Guard.Wrap<int>(node => _personView.SetHover(-1));
            _personView.Root.gameObject.SetActive(false);
            _chain = PersonPress;
        }

        private void RescaleEmployee(float scale)
        {
            if (_personView != null)
            {
                _personView.Scale = scale;
            }
            _personKey = int.MinValue;
        }

        private void GiveEmployee()
        {
            EmployeeLeave();
            EmployeeFollowLive.End(null);
        }

        private void EmployeeLeave()
        {
            if (_personView != null)
            {
                _personView.Root.gameObject.SetActive(false);
            }
            Undock();
            _personUp = false;
            _station = null;
            _who = null;
            _personKey = int.MinValue;
        }

        /// <summary>The person's panel for the large box over a station with a person at it, while the card's form is the click's. False when there is none to show.</summary>
        private bool EmployeeFrame(UiFrame frame, bool want)
        {
            if (!want || _person == null || !_person.gameObject.activeSelf)
            {
                EmployeeLeave();
                return false;
            }
            var station = _detailed.relevantController as EmployeeStationController;
            EmployeeInstance who = station != null ? station.employeeInstance : null;
            if (who == null || who.characterData == null || string.IsNullOrEmpty(who.characterData.name))
            {
                EmployeeLeave();
                return false;
            }
            float now = Time.unscaledTime;
            int id = who.GetHashCode();
            if (id != _personId || !_personUp)
            {
                _personId = id;
                _personDue = 0f;
                _personKey = int.MinValue;
            }
            _station = station;
            _who = who;
            if (now >= _personDue)
            {
                _personDue = now + ReadEvery * 2f;
                int key = PersonKey(who);
                if (key != _personKey)
                {
                    _personKey = key;
                    ReadPerson(station, who);
                    LayPerson();
                }
            }
            Dock();
            _personUp = true;
            return true;
        }

        // What the panel says changes with the minute, the mood, the week's hours, the pay (the wage paid, Core/PaidWage) and whether they are followed.
        private int PersonKey(EmployeeInstance who)
        {
            unchecked
            {
                int k = who.GetHashCode();
                k = k * 31 + TimeHelper.CurrentHour * 60 + (int)TimeHelper.CurrentMinute;
                k = k * 31 + (int)who.satisfaction;
                k = k * 31 + who.workedHoursThisWeek;
                k = k * 31 + WageShown.PaidCents(who);
                k = k * 31 + (EmployeeFollowLive.Following(who) ? 1 : 0);
                k = k * 31 + (who.isAbsent ? 1 : 0);
                return k;
            }
        }

        private void ReadPerson(EmployeeStationController station, EmployeeInstance who)
        {
            UiInfoModel m = _personInfo;
            string name = who.characterData.name;
            OverlayMood mood = OverlayRules.Mood(who.satisfaction);
            bool shift = OverlayFactsLive.Shift(who, out int from, out int until, out bool on);
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(who.assignedAddress);
            m.Title = UiText.T("reallife_ui_overlay_employee");
            m.Icon = UiIcon.Person;
            m.Tabs = new UiIcon[0];
            m.Name = name;
            m.Hue = HueOf(name);
            m.Ring = OverlayRules.MoodTone(mood);
            m.Status = UiText.T(who.isAbsent ? "reallife_ui_overlay_absent" : on ? "reallife_ui_overlay_on_shift" : "reallife_ui_overlay_off_shift");
            m.StatusIcon = on ? UiIcon.Check : UiIcon.Clock;
            m.StatusTone = on ? UiRole.Ok : UiRole.Notice;
            string place = reg != null ? (string.IsNullOrEmpty(reg.BusinessName) ? who.assignedAddress.ToFormattedString() : reg.BusinessName + ", " + who.assignedAddress.ToFormattedString()) : null;
            string where = Plain(_headerOfDetailed.GetValue(_detailed) as TMPro.TMP_Text);
            m.Facts = string.IsNullOrEmpty(place) ? where : string.IsNullOrEmpty(where) ? place : where + ", " + place;

            var sections = new UiInfoSection[who.demands != null && who.demands.Count > 0 ? 4 : 3];
            var today = new UiInfoRow[2];
            if (shift && from >= 0 && until >= 0)
            {
                today[0] = new UiInfoRow
                {
                    Label = UiText.T(on ? "reallife_ui_overlay_hours" : "reallife_ui_overlay_next_shift"),
                    Value = UiText.Fill("reallife_ui_overlay_range", "from", from.GetFormattedTime(), "to", until.GetFormattedTime()),
                };
                if (on)
                {
                    int worked = OverlayRules.WorkedMinutes(from, TimeHelper.CurrentHour, (int)TimeHelper.CurrentMinute);
                    OverlayRules.Split(worked, out int h, out int rest);
                    today[1] = new UiInfoRow
                    {
                        Label = UiText.T("reallife_ui_overlay_worked"), Value = h > 0 ? UiText.Fill("reallife_ui_overlay_h_min", "h", h.ToString(), "min",
                            rest.ToString()) : UiText.Fill("reallife_ui_overlay_min", "min", rest.ToString()),
                        Meter = true, Fill = OverlayRules.ShiftShare(from, until, TimeHelper.CurrentHour, (int)TimeHelper.CurrentMinute) / 100f,
                    };
                }
                else
                {
                    today = new[] { today[0] };
                }
            }
            else
            {
                today = new[] { new UiInfoRow { Label = UiText.T("reallife_ui_overlay_hours"), Value = UiText.T("reallife_ui_overlay_no_shift") } };
            }
            sections[0] = new UiInfoSection { Label = UiText.T("reallife_ui_overlay_today_shift"), Rows = today };

            var how = new System.Collections.Generic.List<UiInfoRow>(2)
            {
                new UiInfoRow
                {
                    Label = UiText.T("reallife_ui_overlay_mood"), Value = MoodWords(mood, who.satisfaction),
                        Ink = mood == OverlayMood.Low ? UiRole.DangerText : mood == OverlayMood.Middle ? UiRole.WarnText : UiRole.OkText,
                    Meter = true, Fill = OverlayRules.Percent(who.satisfaction) / 100f,
                }
            };
            if (OverlayFactsLive.TopSkill(who, out string skill, out float level))
            {
                how.Add(new UiInfoRow { Label = skill, Value = OverlayRules.Percent(level) + "%", Meter = true, Fill = OverlayRules.SkillShare(level) / 100f });
            }
            sections[1] = new UiInfoSection { Label = UiText.T("reallife_ui_overlay_how"), Rows = how.ToArray() };

            float paid = WageShown.PaidMoney(who);   // what the employee costs an hour at the city's prices with any raise agreed, as My Employees and the Staff page name it
            sections[2] = new UiInfoSection
            {
                Label = UiText.T("reallife_ui_overlay_pay"),
                Rows = new[]
                {
                    new UiInfoRow { Label = UiText.T("reallife_ui_overlay_wage"), Value = UiText.Fill("reallife_ui_overlay_per_hour_money", "amount", UiText.Money(paid)) },
                    new UiInfoRow { Label = UiText.T("reallife_ui_overlay_earned"), Value = UiText.Money(OverlayRules.Earned(who.workedHoursThisWeek, paid)) },
                    new UiInfoRow { Label = UiText.T("reallife_ui_overlay_page"), Value = UiText.Fill("reallife_ui_overlay_open_page", "name", OverlayRules.Short(name)), Link = true },
                },
            };
            if (sections.Length > 3)
            {
                var wants = new UiInfoRow[Math.Min(4, who.demands.Count)];
                for (int i = 0; i < wants.Length; i++)
                {
                    bool met = JobDemandHelper.GetByName(who.demands[i]).Fulfilled(who, null);
                    wants[i] = new UiInfoRow
                    {
                        Label = Plain(who.demands[i].GetLocalization()), Value = UiText.T(met ? "reallife_ui_overlay_met" : "reallife_ui_overlay_unmet"),
                        Ink = met ? UiRole.OkText : UiRole.WarnText,
                    };
                }
                sections[3] = new UiInfoSection { Label = UiText.T("reallife_ui_overlay_wants"), Rows = wants };
            }
            m.Sections = sections;
            m.FooterLabel = UiText.T("reallife_ui_overlay_open");
            m.Footer = new[] { UiIcon.Locate, UiIcon.Navigation, EmployeeFollowLive.Following(who) ? UiIcon.EyeSlash : UiIcon.Eye, UiIcon.Calendar };
            m.FooterTints = new UiRole[m.Footer.Length];
            m.Prompts = new UiPrompt[0];
            m.PromptCount = 0;
        }

        private void LayPerson()
        {
            if (_inHud)
            {
                HudHost.Instance.Screen.Dirty = true;   // the HUD lays its left column out again with the new rows
                return;
            }
            _personScene.Begin(UiOverlays.PersonId, UiInfoPanel.OuterWidth(_personInfo) + UiTokens.S2, UiInfoPanel.Height(_personInfo), true);
            _personScene.Floats = true;
            _personScene.Pointer = true;
            UiInfoPanel.Draw(_personScene, _personInfo, 0f, 0f);
            _personView.Write(_personScene, false);
        }

        // In the HUD's left column while the kit's HUD shows; else at the top left of the world's canvas. Whatever panel stood in the
        // column before is put back when this one goes.
        private void Dock()
        {
            HudHost hud = HudHost.Instance;
            if (hud != null && HudStatusGame.Shows)
            {
                _personView.Root.gameObject.SetActive(false);
                HudScreen screen = hud.Screen;
                if (!ReferenceEquals(screen.Model.Info, _personInfo))
                {
                    _before = screen.Model.Info;
                    screen.Model.Info = _personInfo;
                    screen.Dirty = true;
                    _inHud = true;
                    _was = screen.PiecePressed;
                    screen.PiecePressed = _chain;
                    LayPerson();
                }
                return;
            }
            if (_inHud)
            {
                Undock();
                LayPerson();
            }
            _personView.Root.gameObject.SetActive(true);
            _personView.Root.anchoredPosition = new Vector2(UiTokens.S2 * _scale, -UiTokens.S4 * _scale);
        }

        private void Undock()
        {
            if (!_inHud)
            {
                return;
            }
            _inHud = false;
            HudHost hud = HudHost.Instance;
            if (hud != null)
            {
                HudScreen screen = hud.Screen;
                if (ReferenceEquals(screen.Model.Info, _personInfo))
                {
                    screen.Model.Info = _before;
                    screen.Dirty = true;
                }
                if (ReferenceEquals(screen.PiecePressed, _chain))
                {
                    screen.PiecePressed = _was;
                }
            }
            _before = null;
            _was = null;
        }

        private void PersonPressed(int node)
        {
            if (node < 0 || node >= _personScene.NodeCount)
            {
                return;
            }
            ref UiNode n = ref _personScene.Nodes[node];
            UiRefusals.Begin();
            bool done = !n.Disabled && PersonPress(n.Command, n.Arg);
            if (done)
            {
                _personView.Press(Time.unscaledTime);
            }
            else
            {
                UiRefusalLog.Say("a person's card", _personScene, node, n.Disabled);
            }
            UiGame.Instance.Play(done ? UiCue.Confirm : UiCue.Error);
        }

        // The panel's own presses, from the HUD or the world's canvas; another piece's go to whoever put it up.
        private bool PersonPress(int command, int arg)
        {
            if (!_personUp || _person == null || _who == null)
            {
                return _was != null && _was(command, arg);
            }
            switch (command)
            {
                case UiInfoPanel.FooterCommand:
                    if (arg < 0)
                    {
                        return Run(_person.OnManageEmployeeClicked);   // the game's own button for their page
                    }
                    if (arg == Schedule)
                    {
                        return Run(_person.OnManageScheduleClicked);
                    }
                    if (arg == Locate)
                    {
                        return LocateWork();
                    }
                    if (arg == Route)
                    {
                        return RouteWork();
                    }
                    if (arg == Follow)
                    {
                        return FollowPerson();
                    }
                    return false;
                case UiInfoPanel.LinkCommand:
                    return Run(_person.OnManageEmployeeClicked);
                case UiDraw.CloseCommand:
                    _manager.HideDetailedOverlay();
                    return true;
            }
            return _was != null && _was(command, arg);
        }

        // The city map opened on the workplace, as the game focuses a building.
        private bool LocateWork()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController cbc = city != null && _who.assignedAddress != null ? city.FindCityBuildingController(_who.assignedAddress) : null;
            if (city == null || city.cityMap == null || cbc == null || !CityMap.CanOpenMap())
            {
                return false;
            }
            city.cityMap.FocusOnBuilding(cbc);
            return true;
        }

        // The game's destination set to the workplace, with the game's own notice, as BizMan's Set destination does it.
        private bool RouteWork()
        {
            Address address = _who.assignedAddress;
            if (address == null || address.IsUndefined() || SaveGameManager.Current == null)
            {
                return false;
            }
            SaveGameManager.Current.customDestination = address;
            Notifications.Show(NotificationType.Success, "notification_destination_set", new System.Collections.Generic.Dictionary<string, string> { { "address", address.ToFormattedString() } });
            UI.Guiders.GuidersManager.SetGuiderTarget(address, UI.Guiders.DirectionGuiderType.Destination);
            return true;
        }

        // The camera on the person, or off them again.
        private bool FollowPerson()
        {
            if (EmployeeFollowLive.Following(_who))
            {
                EmployeeFollowLive.End("pressed again");
                _personKey = int.MinValue;
                return true;
            }
            bool began = EmployeeFollowLive.Begin(_station);
            if (began)
            {
                _manager.HideDetailedOverlay();   // the game's own: the box has done its work, the camera is on the person
            }
            return began;
        }
    }
}
