using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Player.HUD.ItemWarningIcons;
using UnityEngine;
using UnityEngine.Pool;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The game's own mark over each classroom door of the university, for the room's course (CampusRules.MarkOf). The
    /// game's icon manager would mark a classroom door for the game's course its custom value names, so the doors are told
    /// not to be marked (alwaysShowWarningIcon, which it reads first) and the mod puts in the game's own icon from its pool:
    /// the green "i" for a class or exam within the hour, the plain "i" while enrolled, the tick for a course passed. The
    /// game moves and clears them as its own. Its private pool and pictures are looked up once; without them the doors go
    /// unmarked. The cost: once a second on a floor, five lookups; an icon taken or let go only when a mark changes.
    /// </summary>
    internal sealed class CampusMarks
    {
        private static FieldInfo _poolField, _activeField;
        private static MethodInfo _visuals;
        private static bool _resolved, _works;
        private static readonly Dictionary<WarningIconType, (Sprite icon, Sprite background, bool pointer)> Pictures = new Dictionary<WarningIconType, (Sprite, Sprite, bool)>(4);

        private readonly IModLogger _logger;
        private readonly ItemController[] _doors = new ItemController[CampusRules.RoomsPerFloor];
        private readonly ItemWarningIcon[] _icons = new ItemWarningIcon[CampusRules.RoomsPerFloor];
        private readonly WarningIconType[] _types = new WarningIconType[CampusRules.RoomsPerFloor];

        public CampusMarks(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>The game's icon for a mark of a room.</summary>
        public static WarningIconType IconOf(RoomMark mark)
        {
            switch (mark)
            {
                case RoomMark.Soon: return WarningIconType.Action;
                case RoomMark.Enrolled: return WarningIconType.Info;
                case RoomMark.Completed: return WarningIconType.Completed;
                default: return WarningIconType.None;
            }
        }

        /// <summary>Puts a room's mark over its door (from 0), or takes it away; nothing if it is already so.</summary>
        public void Set(int room, ItemController door, RoomMark mark)
        {
            if (room < 0 || room >= _doors.Length || door == null)
            {
                return;
            }
            ItemWarningIconManager manager = InstanceBehavior<ItemWarningIconManager>.Instance;
            if (manager == null)
            {
                return;
            }
            if (!ReferenceEquals(_doors[room], door))
            {
                _doors[room] = door;
                _icons[room] = null;
                _types[room] = WarningIconType.None;
                door.alwaysShowWarningIcon = false;   // the game's mark of its own course is not put back (its manager reads this first)
                manager.HideWarningIcon(door);
            }
            WarningIconType type = IconOf(mark);
            Dictionary<int, ItemWarningIcon> active = Active(manager);
            int key = door.GetHashCode();
            bool ours = active != null && _icons[room] != null && active.TryGetValue(key, out ItemWarningIcon there) && ReferenceEquals(there, _icons[room]);
            if (ours && _types[room] == type)
            {
                return;
            }
            if (ours || (active != null && active.ContainsKey(key)))
            {
                manager.HideWarningIcon(door);   // the game's own way: back to its pool
            }
            _icons[room] = null;
            _types[room] = type;
            if (type == WarningIconType.None || active == null)
            {
                return;
            }
            ObjectPool<ItemWarningIcon> pool = Pool(manager);
            if (pool == null || !Picture(manager, type, out (Sprite icon, Sprite background, bool pointer) look))
            {
                return;
            }
            ItemWarningIcon icon = pool.Get();
            icon.linkedItemController = door;
            icon.icon.sprite = look.icon;
            icon.background.sprite = look.background;
            icon.Pointer.SetActive(look.pointer);
            icon.currentIconType = type;
            active[key] = icon;
            _icons[room] = icon;
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Careers check (university room mark): room " + (room + 1) + " marked " + mark + " (the game's " + type + " icon): PASS");
            }
        }

        /// <summary>Every room's mark taken away (the doors are kept by the game until it leaves the building).</summary>
        public void Clear()
        {
            ItemWarningIconManager manager = InstanceBehavior<ItemWarningIconManager>.Instance;
            for (int r = 0; r < _doors.Length; r++)
            {
                if (_icons[r] != null && manager != null && _doors[r] != null)
                {
                    manager.HideWarningIcon(_doors[r]);
                }
                _doors[r] = null;
                _icons[r] = null;
                _types[r] = WarningIconType.None;
            }
        }

        /// <summary>The player has left: the game has let every icon go with the building.</summary>
        public void Forget()
        {
            Array.Clear(_doors, 0, _doors.Length);
            Array.Clear(_icons, 0, _icons.Length);
            Array.Clear(_types, 0, _types.Length);
        }

        private bool Resolve()
        {
            if (_resolved)
            {
                return _works;
            }
            _resolved = true;
            try
            {
                _poolField = GameContract.IconPool.Field;   // the game's private pieces, resolved once (Core/GameContract)
                _activeField = GameContract.IconsActive.Field;
                _visuals = GameContract.IconVisuals.Method;
                _works = _poolField != null && _activeField != null && _visuals != null
                         && typeof(ObjectPool<ItemWarningIcon>).IsAssignableFrom(_poolField.FieldType) && typeof(Dictionary<int, ItemWarningIcon>).IsAssignableFrom(_activeField.FieldType);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _works = false;
            }
            if (!_works)
            {
                _logger?.Warn("Careers: the game's icons over items are not as this version expects; the university's classroom doors go without a mark.");
            }
            return _works;
        }

        private Dictionary<int, ItemWarningIcon> Active(ItemWarningIconManager manager)
        {
            return Resolve() ? _activeField.GetValue(manager) as Dictionary<int, ItemWarningIcon> : null;
        }

        private ObjectPool<ItemWarningIcon> Pool(ItemWarningIconManager manager)
        {
            return Resolve() ? _poolField.GetValue(manager) as ObjectPool<ItemWarningIcon> : null;
        }

        // The game's own pictures for an icon type, asked of its manager once a type.
        private bool Picture(ItemWarningIconManager manager, WarningIconType type, out (Sprite icon, Sprite background, bool pointer) look)
        {
            if (Pictures.TryGetValue(type, out look) && look.icon != null && look.background != null)
            {
                return true;
            }
            try
            {
                object made = _visuals.Invoke(manager, new object[] { type });
                if (made is ValueTuple<Sprite, Sprite, bool> tuple && tuple.Item1 != null && tuple.Item2 != null)
                {
                    look = (tuple.Item1, tuple.Item2, tuple.Item3);
                    Pictures[type] = look;
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            look = default;
            return false;
        }
    }
}
