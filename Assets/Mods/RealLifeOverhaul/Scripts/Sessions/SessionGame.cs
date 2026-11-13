using System;
using System.Collections.Generic;
using System.Reflection;
using Controllers;
using Entities;
using Helpers;
using Player.HUD.ItemInfoOverlays;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>One of the player's businesses where some kind of session is held, as read from the save once an hour.</summary>
    public sealed class SessionFirm
    {
        public string Place = string.Empty, Name = string.Empty, Type = string.Empty, Street = string.Empty;
        public int Number;
    }

    /// <summary>
    /// Everything the desk sessions read from or do to the game: the player's firms, the office they stand in and its
    /// computer workstations, the owner's own desk among them, the click on it (an entry at the front of the game's calls
    /// to action, as the careers' desk has), notices and the destination marker. The walk, the chair and the panel are the
    /// careers' own desk mechanics (Careers/DeskGame), the same the game's work uses.
    /// </summary>
    internal static class SessionGame
    {
        private static readonly List<EmployeeStationController> Found = new List<EmployeeStationController>(16);
        private static readonly HashSet<string> Rostered = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Comparison<EmployeeStationController> ById = CompareById;

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The player's businesses with a kind of session held at their type, in the save's order. One pass over the buildings.</summary>
        public static void Firms(List<SessionFirm> into, List<SessionFirm> pool)
        {
            for (int i = 0; i < into.Count; i++)
            {
                pool.Add(into[i]);
            }
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasValidAddress || !reg.HasEstablishedBusiness || !SessionRegistry.Any(reg.businessTypeName) || !GameRegistrations.Found(reg))
                {
                    continue;
                }
                SessionFirm f = pool.Count > 0 ? pool[pool.Count - 1] : new SessionFirm();
                if (pool.Count > 0)
                {
                    pool.RemoveAt(pool.Count - 1);
                }
                Fill(f, reg);
                into.Add(f);
            }
        }

        private static void Fill(SessionFirm f, BuildingRegistration reg)
        {
            f.Street = reg.StreetName ?? string.Empty;
            f.Number = reg.StreetNumber;
            f.Place = PlaceRules.Key(reg.StreetName, reg.StreetNumber);
            f.Name = TalentGame.NameOf(reg);
            f.Type = reg.businessTypeName ?? string.Empty;
        }

        /// <summary>The player's own business the player stands in now, with a kind of session held at its type; false elsewhere or while going in or out.</summary>
        public static bool InFirm(SessionFirm into, out BuildingManager manager, out BuildingRegistration reg)
        {
            if (!DeskGame.Inside(out manager, out reg) || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || !SessionRegistry.Any(reg.businessTypeName))
            {
                return false;
            }
            Fill(into, reg);
            return true;
        }

        /// <summary>
        /// The office's computer workstations with their chairs (the stations the game seats its people at), how many of them
        /// nobody is rostered at in the business's week, and the owner's desk: the first of those by the layout's ids, the
        /// same every visit and on every machine; null with none. One pass over the building's items and its week.
        /// </summary>
        public static EmployeeStationController OwnDesk(BuildingManager manager, BuildingRegistration reg, out int desks, out int free)
        {
            desks = free = 0;
            Rostered.Clear();
            List<ScheduleDay> week = reg != null ? reg.scheduleDays : null;
            for (int d = 0; week != null && d < week.Count; d++)
            {
                List<WorkShift> shifts = week[d] != null ? week[d].workShifts : null;
                for (int s = 0; shifts != null && s < shifts.Count; s++)
                {
                    if (shifts[s] != null && !string.IsNullOrEmpty(shifts[s].employeeId) && !string.IsNullOrEmpty(shifts[s].itemInstanceId))
                    {
                        Rostered.Add(shifts[s].itemInstanceId);
                    }
                }
            }
            Found.Clear();
            List<ItemController> items = manager != null ? manager.allItemControllers : null;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] is EmployeeStationController station && Desk(station))
                {
                    desks++;
                    if (station.ItemInstance != null && !Rostered.Contains(station.ItemInstance.id))
                    {
                        Found.Add(station);
                    }
                }
            }
            free = Found.Count;
            Found.Sort(ById);
            EmployeeStationController own = Found.Count > 0 ? Found[0] : null;
            Found.Clear();
            Rostered.Clear();
            return own;
        }

        /// <summary>The building's items counted, so a desk put in or taken out asks for the owner's desk again.</summary>
        public static int Items(BuildingManager manager)
        {
            return manager != null && manager.allItemControllers != null ? manager.allItemControllers.Count : 0;
        }

        /// <summary>
        /// A number that changes when somebody is put on or taken off a workstation in the week's rota. Taking a person off the last
        /// free desk changes no item, so without it the owner's desk was never looked for again until the player left and came back.
        /// One pass over the week's shifts, once a second at most.
        /// </summary>
        public static int Roster(BuildingRegistration reg)
        {
            int sig = 17;
            List<ScheduleDay> week = reg != null ? reg.scheduleDays : null;
            for (int d = 0; week != null && d < week.Count; d++)
            {
                List<WorkShift> shifts = week[d] != null ? week[d].workShifts : null;
                for (int s = 0; shifts != null && s < shifts.Count; s++)
                {
                    if (shifts[s] != null && !string.IsNullOrEmpty(shifts[s].employeeId) && !string.IsNullOrEmpty(shifts[s].itemInstanceId))
                    {
                        sig = unchecked(sig * 31 + shifts[s].itemInstanceId.GetHashCode());
                    }
                }
            }
            return sig;
        }

        /// <summary>True if the player stands within a few metres of the desk, flat on the floor.</summary>
        public static bool Near(EmployeeStationController desk)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || desk == null)
            {
                return false;
            }
            Vector3 d = desk.transform.position - player.transform.position;
            return d.x * d.x + d.z * d.z < NearMeters * NearMeters;
        }

        private const float NearMeters = 3f;   // m: close enough that the desk's marker is no use

        /// <summary>
        /// The game's panel drawn again from the activity's own labels and buttons. The game reads them only when the panel opens and
        /// when the time begins, so a page changed in between (Next session, Begin, a choice) stayed as it was on screen while the
        /// buttons behind it went on answering for the new page.
        /// </summary>
        public static void Redraw(PlayerActivity.IPlayerActivity activity)
        {
            try
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                if (uis != null && uis.playerActivityUI != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, activity))
                {
                    uis.playerActivityUI.UpdateActivityDisplay();
                }
            }
            catch (Exception e)
            {
                SessionDirector.Logger?.Error(e);
            }
        }

        // A computer workstation the game would seat somebody at: standing, bought, assignable, with its chair.
        private static bool Desk(EmployeeStationController station)
        {
            return station != null && station.gameObject.activeInHierarchy && !(station.playerItemPurchaserSettings != null && station.playerItemPurchaserSettings.enabled)
                   && station.Item != null && station.Item.assignable && DeskGame.KindOf(station) == StationKind.Desk;
        }

        private static int CompareById(EmployeeStationController a, EmployeeStationController b)
        {
            string x = a.ItemInstance != null ? a.ItemInstance.id : null, y = b.ItemInstance != null ? b.ItemInstance.id : null;
            int byId = string.CompareOrdinal(x ?? string.Empty, y ?? string.Empty);
            if (byId != 0)
            {
                return byId;
            }
            Vector3 p = a.transform.position, q = b.transform.position;
            int byX = p.x.CompareTo(q.x);
            return byX != 0 ? byX : p.z.CompareTo(q.z);
        }

        /// <summary>True if somebody of the game's is at the desk now (one sent there out of the week, by the game's own work).</summary>
        public static bool Taken(EmployeeStationController station) => DeskGame.Taken(station);

        /// <summary>The nearest of the player's firms to the player, or null; looked for on a press.</summary>
        public static SessionFirm Nearest(List<SessionFirm> firms)
        {
            PlayerController player = PlayerHelper.PlayerController;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (player == null || city == null)
            {
                return firms.Count > 0 ? firms[0] : null;
            }
            Vector3 at = player.transform.position;
            SessionFirm best = null;
            float near = float.MaxValue;
            for (int i = 0; i < firms.Count; i++)
            {
                CityBuildingController c = city.FindCityBuildingController(new Address(firms[i].Street, firms[i].Number));
                float d = c != null ? (c.transform.position - at).sqrMagnitude : float.MaxValue / 2;
                if (best == null || d < near)
                {
                    best = firms[i];
                    near = d;
                }
            }
            return best;
        }

        /// <summary>The game's destination marker on a firm.</summary>
        public static bool Route(SessionFirm firm) => firm != null && DeskGame.Route(firm.Street, firm.Number);

        /// <summary>A notice of the game's, in its own words and colours.</summary>
        public static void Tell(NotificationType type, string key, Dictionary<string, string> data, float seconds = 10f)
        {
            Notifications.Show(type, key, data, seconds);
        }

        /// <summary>The pairs of a session's outcome as the game's notice data.</summary>
        public static Dictionary<string, string> Data(string[] pairs)
        {
            var data = new Dictionary<string, string>(4);
            for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return data;
        }

        public static int Day => Save != null ? Save.Day : 0;

        public static int Hour => TimeHelper.CurrentHour;

        // The game's own CanStartActivity says yes whenever the player is not in a vehicle, panel open or not, and notices every ask
        // in a vehicle (and another mod patches it): the work waits on the panel itself being open.
        public static bool PanelBusy => DeskGame.Busy;

        private sealed class Behaviour : ICtaBehavior
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("living", "SessionGame.Behaviour", typeof(Behaviour));   // the game's calls of it (Core/Guard)

            // asked by the game for whatever the pointer is on: a fault here would break its pointer, so none leaves
            public override bool ShouldShow(EntityController entityController)
            {
                if (FrameGuard.Off)
                {
                    return false;
                }
                try
                {
                    return entityController != null && ReferenceEquals(entityController, Target);
                }
                catch (Exception e)
                {
                    return Guard.Failed(FrameGuard, e, false);
                }
            }

            public override (string, Action) GetCta(EntityController entityController)
            {
                if (FrameGuard.Off)
                {
                    return (string.Empty, null);
                }
                try
                {
                    if (entityController == null || !ReferenceEquals(entityController, Target))
                    {
                        return (string.Empty, null);
                    }
                    return ("reallife_session_cta", Click);
                }
                catch (Exception e)
                {
                    return Guard.Failed(FrameGuard, e, (string.Empty, (Action)null));
                }
            }
        }

        private static readonly Action Click = Clicked;
        private static Behaviour _behaviour;
        private static List<ICtaBehavior> _list;
        private static bool _failed;

        /// <summary>The owner's desk the player can click for a session now, set by the director; null for none.</summary>
        public static EmployeeStationController Target;

        /// <summary>What a click on the owner's desk does (the director's).</summary>
        public static Action Clicks;

        /// <summary>Adds the entry, once. False if the game's list could not be reached (said once): the desk then cannot be clicked.</summary>
        public static bool Install()
        {
            if (_behaviour != null)
            {
                return true;
            }
            if (_failed)
            {
                return false;
            }
            try
            {
                FieldInfo field = GameContract.CtaBehaviours.Field;   // resolved once (Core/GameContract)
                _list = field != null ? field.GetValue(null) as List<ICtaBehavior> : null;
                if (_list == null)
                {
                    _failed = true;
                    SessionDirector.Logger?.Warn("Sessions: the game's list of click actions was not found, so a desk cannot be clicked for a session.");
                    return false;
                }
                _behaviour = new Behaviour();
                _list.Insert(0, _behaviour);   // asked first: it answers only for the owner's desk the director set
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                _behaviour = null;
                SessionDirector.Logger?.Error(e);
                return false;
            }
        }

        public static bool Clickable => _behaviour != null;

        /// <summary>Takes the entry out of the game's list again.</summary>
        public static void Remove()
        {
            try
            {
                if (_list != null && _behaviour != null)
                {
                    _list.Remove(_behaviour);
                }
            }
            catch (Exception e)
            {
                SessionDirector.Logger?.Error(e);
            }
            _behaviour = null;
            _list = null;
            _failed = false;
            Target = null;
        }

        private static void Clicked()
        {
            try
            {
                Clicks?.Invoke();
            }
            catch (Exception e)
            {
                SessionDirector.Logger?.Error(e);
            }
        }
    }
}
