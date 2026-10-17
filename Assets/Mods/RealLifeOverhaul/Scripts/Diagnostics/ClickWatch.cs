using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using Helpers;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul
{
    /// <summary>
    /// With "Developer diagnostics" on: one line in the log for a left click in the city, saying what the click met.
    /// It answers "I clicked a building and nothing happened": whether an interface element lay under the pointer
    /// (the game then ignores the world), what the ray into the world hit first, what the game had as its target,
    /// and whether the player's walking was blocked. With diagnostics off it does one flag test a frame and nothing more.
    /// </summary>
    public sealed class ClickWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("diagnostics", "ClickWatch", typeof(ClickWatch));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public static IModLogger Logger;

        private static readonly List<RaycastResult> Under = new List<RaycastResult>(8);
        private static readonly RaycastHit[] Hits = new RaycastHit[6];
        private readonly StringBuilder _text = new StringBuilder(512);
        private PointerEventData _pointer;
        private float _next;

        private static readonly int HotUpdate = HotSpots.Slot("Diagnostics: ClickWatch.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (!Meta.Dev.Verbose || !Input.GetMouseButtonUp(0) || Time.unscaledTime < _next)
            {
                return;
            }
            _next = Time.unscaledTime + 0.5f;
            try
            {
                Write();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                enabled = false;
            }
        }

        private void Write()
        {
            var game = InstanceBehavior<GameManager>.Instance;
            Camera camera = GameManager.GetMainCamera();
            EventSystem events = EventSystem.current;
            if (game == null || game.playerController == null || camera == null || events == null)
            {
                return;
            }
            Vector2 at = (Vector2)Input.mousePosition;
            _text.Length = 0;
            _text.Append("Click check: cursor ").Append(Cursor.lockState).Append(" at (").Append(Mathf.RoundToInt(at.x)).Append(", ").Append(Mathf.RoundToInt(at.y))
                 .Append(") of ").Append(Screen.width).Append(" x ").Append(Screen.height).Append("; over the interface: ").Append(events.IsPointerOverGameObject() ? "YES" : "no");
            if (_pointer == null)
            {
                _pointer = new PointerEventData(events);
            }
            _pointer.position = at;
            Under.Clear();
            events.RaycastAll(_pointer, Under);
            for (int i = 0; i < Under.Count && i < 3; i++)
            {
                _text.Append(i == 0 ? " (" : "; ").Append(Path(Under[i].gameObject.transform));
            }
            if (Under.Count > 0)
            {
                _text.Append(')');
            }
            Under.Clear();
            Ray ray = camera.ScreenPointToRay(at);
            int count = Physics.RaycastNonAlloc(ray, Hits, 600f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(Hits, 0, count, ByDistance.Instance);
            _text.Append("; the ray meets ");
            if (count == 0)
            {
                _text.Append("nothing");
            }
            for (int i = 0; i < count && i < 4; i++)
            {
                Collider c = Hits[i].collider;
                _text.Append(i == 0 ? string.Empty : ", then ").Append('\'').Append(c.name).Append("' on ").Append(LayerMask.LayerToName(c.gameObject.layer))
                     .Append(c.isTrigger ? " (trigger)" : string.Empty).Append(" at ").Append(Hits[i].distance.ToString("F1", CultureInfo.InvariantCulture)).Append(" m");
                EntityController entity = c.GetComponentInParent<EntityController>();
                if (entity != null)
                {
                    _text.Append(" [").Append(entity.GetType().Name).Append(']');
                }
            }
            EntityController target = MouseController.currentTargetEntity;
            _text.Append("; the game's target: ").Append(target != null ? target.GetType().Name + " '" + target.name + "'" : "none")
                 .Append("; walking blocked: ").Append(game.playerController.NavigationDisabled ? "YES" : "no")
                 .Append("; indoors: ").Append(BuildingManager.IsInsideBuilding ? "yes" : "no")
                 .Append("; in a vehicle: ").Append(PlayerHelper.IsUsingVehicle ? "yes" : "no")
                 .Append("; menu: ").Append(FullMenu.IsOpen ? "yes" : "no").Append("; map: ").Append(CityMap.IsOpen ? "yes" : "no")
                 .Append("; paused: ").Append(Time.timeScale == 0f ? "yes" : "no").Append('.');
            Logger?.Info(_text.ToString());
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private sealed class ByDistance : IComparer<RaycastHit>
        {
            public static readonly ByDistance Instance = new ByDistance();

            public int Compare(RaycastHit a, RaycastHit b)
            {
                return a.distance.CompareTo(b.distance);
            }
        }
    }
}
