using BigAmbitions.InputSystem;
using Entities;
using Items.SpecialItems;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The camera on a live person at their station, riding on the mod's third person camera (its Focus: the point the camera
    /// watches in place of the player, as it watches a player knocked down). It ends on any movement key, on Escape, on any
    /// panel that needs the camera (ThirdPersonCamera.IsUiOpen), when the person is gone, when the player leaves the building
    /// or the camera is switched off; it never starts over another thing's hold on the camera. Rivals' people at their own
    /// stations are followed the same way. Cost: nothing while idle; while following, a few reads of the game's input a frame.
    /// </summary>
    internal static class EmployeeFollowLive
    {
        private static EmployeeInstance _who;
        private static Transform _target;
        private static bool _inside;

        /// <summary>True while the camera is on someone.</summary>
        public static bool Active => _target != null;

        /// <summary>True while the camera is on this person.</summary>
        public static bool Following(EmployeeInstance who) => _target != null && ReferenceEquals(_who, who);

        /// <summary>The camera on the person at a station; false, with a notice that says why, when it cannot be.</summary>
        public static bool Begin(EmployeeStationController station)
        {
            Transform person = station != null && station.employee != null && station.employee.employeeTpc != null ? station.employee.employeeTpc.transform : null;
            if (person == null)
            {
                return Refuse("reallife_ui_overlay_follow_nobody");
            }
            if (!Settings.CameraEnabled)
            {
                return Refuse("reallife_ui_overlay_follow_camera");
            }
            if (ThirdPersonCamera.Focus != null && ThirdPersonCamera.Focus != _target)
            {
                return Refuse("reallife_ui_overlay_follow_busy");   // another piece holds the camera (a fall): it keeps it
            }
            _who = station.employeeInstance;
            _target = person;
            _inside = BuildingManager.IsInsideBuilding;
            ThirdPersonCamera.Focus = person;
            Notifications.Show(NotificationType.Info, "reallife_ui_overlay_following", new System.Collections.Generic.Dictionary<string, string> { { "name",
                OverlayRules.Short(_who.characterData.name) } }, 8f);
            return true;
        }

        /// <summary>The camera back on the player, with a line in the log saying why when asked to.</summary>
        public static void End(string why)
        {
            if (ReferenceEquals(_target, null))
            {
                return;
            }
            if (ThirdPersonCamera.Focus == _target)
            {
                ThirdPersonCamera.Focus = null;
            }
            _target = null;
            _who = null;
            if (why != null && Meta.Dev.Verbose)
            {
                UiDirector.Logger?.Info("UI: the camera is back on the player (" + why + ").");
            }
        }

        /// <summary>A frame while following: the camera ends on anything that is the player taking the camera back.</summary>
        public static void Tick()
        {
            if (ReferenceEquals(_target, null))
            {
                return;
            }
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                End("the person is gone");
            }
            else if (!Settings.CameraEnabled)
            {
                End("the third person camera was switched off");
            }
            else if (BuildingManager.IsInsideBuilding != _inside)
            {
                End("the player changed place");
            }
            else if (PlayerAction.Cancel.Pressed() || (PlayerAction.Move.Vector().sqrMagnitude >= 0.05f && !TypingNow.Now) || ThirdPersonCamera.IsUiOpen())
            {
                End("the player took the camera back");
            }
        }

        private static bool Refuse(string key)
        {
            Notifications.Show(NotificationType.Warning, key, null, 8f);
            return false;
        }
    }
}
