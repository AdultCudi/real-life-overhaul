using System;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    // Places and prompts: the courthouse door, a lawyer's desk, where somebody is let out, and the Legal file.
    public sealed partial class JusticeDirector
    {
        private const string ButtonHearing = "reallife_justice_button_hearing";
        private const string ButtonSurrender = "reallife_justice_button_surrender";
        private const string ButtonCommunity = "reallife_justice_button_community";
        private const string ButtonLawyer = "reallife_justice_button_lawyer";

        private int _promptKind;            // 0 none, 1 hearing, 2 surrender, 3 community service, 4 a lawyer's desk
        private int _promptShownKind;
        private bool _markCourt;
        private string _courtLabel;
        private DoorPanel _door;            // the game's own panel over the courthouse door or the lawyer's desk
        private Action _onUse;
        private Func<string> _doorWhat;
        private GuideMark _courtMark = new GuideMark("the court date");

        public string CourtPlaceName()
        {
            return JusticeSettings.Courthouse && _court.Usable
                ? JusticeText.T("reallife_justice_place_courthouse", "address", _court.Name)
                : JusticeText.T("reallife_justice_place_video");
        }

        /// <summary>Where somebody let out is put down: the Law bridge's precinct, else the courthouse front. False if neither.</summary>
        public bool ReleaseSpot(out Vector3 spot)
        {
            return ReleaseSpot(out spot, out _);
        }

        // The one decision of where somebody is put down, and its words: the station the Law bridge gives, else the courthouse.
        private bool ReleaseSpot(out Vector3 spot, out string place)
        {
            place = null;
            try
            {
                if (Justice.Bridges.Law.ReleaseSpot(out spot, out string name))
                {
                    place = JusticeText.T("reallife_justice_place_station", "name", string.IsNullOrEmpty(name) ? JusticeText.T("reallife_justice_place_station_plain") : name);
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            spot = _court.Front;
            if (_court.Usable)
            {
                place = JusticeText.T("reallife_justice_place_courthouse", "address", _court.Name);
            }
            return _court.Usable;
        }

        /// <summary>Where they are let out, in words: always the place <see cref="ReleaseSpot(out Vector3)"/> puts them down.</summary>
        public string ReleasePlaceName()
        {
            ReleaseSpot(out _, out string place);
            return place ?? JusticeText.T("reallife_justice_place_street");
        }

        private void Prompts(PlayerController player)
        {
            int kind = 0;
            bool mark = false;
            if (player != null && !PlayerHelper.IsUsingVehicle && !_routine && !JusticePanel.IsOpen && !_community.Active)
            {
                if (!BuildingManager.IsInsideBuilding)
                {
                    if (JusticeSettings.Courthouse && _court.Usable)
                    {
                        JusticeCase due = DueNow();
                        mark = due != null;
                        if (_court.InReach(player.transform.position))
                        {
                            int hour = TimeHelper.CurrentHour;
                            kind = due != null ? (due.Stage == CaseStage.Warrant ? 2 : 1)
                                : JusticeState.CommunityHours > 0 && JusticeSettings.Community && hour >= 7 && hour < 20 ? 3 : 0;
                        }
                    }
                }
                else if (JusticeState.OpenCount > 0 && DialogController.current == null)
                {
                    BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance != null ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
                    if (here != null && here.businessTypeName == LegalCounsel.LawFirmType && (JusticeSettings.Lawyers || LegalCounsel.IsRetained(here)))
                    {
                        kind = 4;
                    }
                }
            }
            _promptKind = kind;
            if (mark)
            {
                // The game's destination marker: a job or an outing that has it keeps it, and it is tried again next second.
                _markCourt = true;
                if (_courtLabel == null)
                {
                    _courtLabel = JusticeText.T("reallife_justice_marker_court");
                }
                _courtMark.Show(_court.Front, _courtLabel);
            }
            else if (_markCourt)
            {
                _markCourt = false;
                _courtMark.Hide();
            }
            if (kind == 0)
            {
                if (_promptShownKind != 0)
                {
                    _promptShownKind = 0;
                    _door.Hide("nothing to offer here now");
                }
                return;
            }
            WorldPanel panel = _door.Panel;
            if (kind != _promptShownKind)
            {
                _promptShownKind = kind;
                panel.Lines(JusticeText.T(kind == 4 ? "reallife_justice_door_lawfirm" : "reallife_justice_door_courthouse"),
                    JusticeText.T(kind == 1 ? "reallife_justice_door_hearing" : kind == 2 ? "reallife_justice_door_warrant"
                        : kind == 3 ? "reallife_justice_door_community" : "reallife_justice_door_lawyer"), null, WorldPanel.White());
                panel.Button(0, kind == 1 ? ButtonHearing : kind == 2 ? ButtonSurrender : kind == 3 ? ButtonCommunity : ButtonLawyer, _onUse, true, true);
                panel.Button(1, null, null, false, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
            }
            Vector3 standing = player.transform.position;
            _door.Show(kind == 4 ? standing : _court.Front, standing, kind == 4 ? 2.1f : 2.4f, _doorWhat);
        }

        private string DoorWhat()
        {
            return _promptKind == 1 ? "hearing" : _promptKind == 2 ? "surrender" : _promptKind == 3 ? "community service" : "lawyer's desk";
        }

        private void UseDoor()
        {
            if (_promptKind == 0 || ThirdPersonCamera.IsUiOpen())
            {
                return;
            }
            try
            {
                UsePrompt();
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void HidePrompt()
        {
            _promptKind = 0;
            _promptShownKind = 0;
            _markCourt = false;
            _door.Hide("another screen has the player");
            _courtMark.Hide();
        }

        private void UsePrompt()
        {
            int kind = _promptKind;
            if (kind == 4)
            {
                BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance != null ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
                if (here != null && DialogController.current == null && LifeEvents.IsSafeForEvent())
                {
                    HidePrompt();
                    LegalPhone.OpenInPerson(LegalCounsel.From(here));
                }
                return;
            }
            if (kind == 3)
            {
                HidePrompt();
                StartCommunity(announceRefusal: true);
                return;
            }
            JusticeCase due = DueNow();
            if (due == null)
            {
                return;
            }
            if (!Safe())
            {
                JusticeText.Toast(NotificationType.Warning, "reallife_justice_toast_not_now", 6f);
                return;
            }
            BeginHearing(due, remote: false);
        }

        /// <summary>The Legal file opens as soon as the phone is away and nothing else has the player.</summary>
        public void QueueFile()
        {
            _fileQueued = true;
        }

        private void OpenFile()
        {
            HidePrompt();
            _hold.Pause();
            _hold.Block();
            _fileOpen = true;
            _file.ShowMain();
        }

        public void CloseFile()
        {
            _fileOpen = false;
            if (JusticePanel.Exists)
            {
                JusticePanel.Instance.Close();
            }
            _hold.ReleaseAll(this);
        }

        /// <summary>Begins a community service shift where the player stands. Returns why not, and says so if asked to.</summary>
        public JusticeRefusal StartCommunity(bool announceRefusal)
        {
            JusticeRefusal why = !JusticeSettings.Community ? JusticeRefusal.PartOff
                : PoliceAttention.IsBusy ? JusticeRefusal.PoliceBusy
                : _routine || _hearing != null || JobDirector.OnShift ? JusticeRefusal.Busy
                : TimeHelper.CurrentHour >= 21 || TimeHelper.CurrentHour < 6 ? JusticeRefusal.Night
                : _community.Start(PlayerHelper.PlayerController);
            if (why != JusticeRefusal.None && announceRefusal)
            {
                JusticeRefusals.Tell(Logger, "Community service", why);
            }
            return why;
        }
    }
}
