using System;
using System.Collections;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Floors
{
    /// <summary>
    /// The game's own panel at a held building's door and at its lift (GameUi/DoorPanel, a WorldPanel each): the building's name,
    /// its holder's line, and a button a floor named after its place, three to a page with "more floors" beyond four. A page is
    /// turned on the press itself. Shown from the one-second timer; nothing a frame but the near-door watch's one flag.
    /// </summary>
    public sealed partial class FloorsDirector
    {
        private static bool _watching;        // the game's own click on a building is watched for, a frame at a time
        private static FloorHost _watchHost;  // ...at this building's door

        // Once a second: the player on a floor (its lift, its place's tick), found inside a held building on none, or at a door.
        private void Floors()
        {
            if (FloorsGame.Manager == null || !FloorsGame.PlayerAt(out Vector3 at, out _))
            {
                return;
            }
            string inside = FloorsGame.InsideKey();
            FloorVisit v = _visit;
            bool moving = FloorsGame.Entering || FloorsGame.Exiting || _lifting;
            if (v != null && !moving && !string.Equals(inside, v.Key, StringComparison.Ordinal))
            {
                // out of the building with no exit seen (a load, another mod's move): the visit ends all the same, as a way out
                FloorDo d = Loan.Exited(v.Key);
                if ((d & FloorDo.Leave) != 0)
                {
                    EndVisit("the player is no longer in the building", false);
                }
                v = null;
            }
            if (v != null)
            {
                HideDoors("inside");
                _watching = false;
                if (!moving)
                {
                    try
                    {
                        v.Place.Tick(v);
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e);
                    }
                    FitTick(v);
                    LiftPanel(v, at);
                }
                return;
            }
            for (int i = 0; i < Hosts.Count; i++)
            {
                Hosts[i].LiftPanel.Hide("not on a floor");
            }
            if (inside != null)
            {
                HideDoors("inside another building");
                _watching = false;
                FloorHost host = HostByKey(inside);
                if (host != null && !moving && !Loan.Pending && !Loan.Borrowed)
                {
                    Found(host, false);
                }
                return;
            }
            DoorPanels(at);
            Watch(at);
        }

        private static void HideDoors(string why)
        {
            for (int i = 0; i < Hosts.Count; i++)
            {
                Hosts[i].DoorPanel.Hide(why);
            }
        }

        // The panel at the nearest held building's door within reach, the others down.
        private static void DoorPanels(Vector3 at)
        {
            bool busy = FloorsGame.InVehicle || FloorsGame.Busy();
            FloorHost near = null;
            float nearest = DoorReach;
            for (int i = 0; i < Hosts.Count; i++)
            {
                FloorHost host = Hosts[i];
                if (host.Floors == 0 || !host.ShowsDoor(host.Open()))
                {
                    continue;
                }
                Vector3 offset = host.Door - at;
                float flat = new Vector2(offset.x, offset.z).magnitude;
                if (flat <= nearest && Mathf.Abs(offset.y) <= DoorRise)
                {
                    near = host;
                    nearest = flat;
                }
            }
            for (int i = 0; i < Hosts.Count; i++)
            {
                if (!ReferenceEquals(Hosts[i], near))
                {
                    Hosts[i].DoorPanel.Hide("away from its door");
                }
            }
            if (near == null)
            {
                return;
            }
            if (busy)
            {
                near.DoorPanel.Hide("busy");
                return;
            }
            DoorPanel(near, at);
        }

        private static void DoorPanel(FloorHost host, Vector3 at)
        {
            Prepare(host);
            bool open = host.Open();
            WorldPanel panel = host.DoorPanel.Panel;
            host.DoorLine(open, out string left, out string right, out Color32 colour);
            panel.Lines(host.Name, left, right, colour);
            Buttons(host, panel, host.DoorPress, host.DoorFloors, host.DoorPage, 0, true);
            bool up = host.DoorPanel.Show(host.Door, at, 2.2f, host.DoorWords(open));
            host.DoorUp(up, open);
        }

        // The lift by the entrance: the building's other floors.
        private static void LiftPanel(FloorVisit v, Vector3 at)
        {
            FloorHost host = v.Host;
            Vector3 offset = v.Spawn - at;
            if (FloorsGame.Busy() || v.Place.Busy() || host.Floors < 2 || new Vector2(offset.x, offset.z).magnitude > LiftReach || Mathf.Abs(offset.y) > LiftRise)
            {
                host.LiftPanel.Hide("away from the lift");
                return;
            }
            Prepare(host);
            WorldPanel panel = host.LiftPanel.Panel;
            panel.Lines(host.Name, v.Place.LiftLine(v.Floor, v.Part), null, WorldPanel.White());
            Buttons(host, panel, host.LiftPress, host.LiftFloors, host.LiftPage, v.Floor, false);
            host.LiftPanel.Show(v.Spawn, at, 2.1f, host.LiftWords(v.Floor));
        }

        // Up to four buttons: the floors on this page (leaving out the one the player is on), or three and "more floors" when they do not fit.
        private static void Buttons(FloorHost host, WorldPanel panel, Action[] press, int[] floors, int page, int skip, bool door)
        {
            FloorsRules.Page(host.Floors, skip, page, floors);
            for (int slot = 0; slot < floors.Length; slot++)
            {
                int floor = floors[slot];
                if (floor < 0)
                {
                    panel.Button(slot, "reallife_campus_floor_more", press[slot], true, false);   // the words the university's and the towers' panels had
                    continue;
                }
                FloorPlace place = floor > 0 ? host.PlaceOn(floor) : null;
                if (place == null)
                {
                    floors[slot] = 0;
                    panel.Button(slot, null, null, false, false);
                    continue;
                }
                place.Button(floor, host.PartOf(floor), out string key, out object args, out bool marked);
                panel.Button(slot, key, args, press[slot], !door || place.Open(), marked || slot == 0);
            }
        }

        // The panels' button actions of a building, made once (each marks the frame its key ran on its own panel).
        private static void Prepare(FloorHost host)
        {
            if (host.DoorPress != null)
            {
                return;
            }
            host.DoorPress = new Action[FloorsRules.Slots];
            host.LiftPress = new Action[FloorsRules.Slots];
            for (int i = 0; i < FloorsRules.Slots; i++)
            {
                int slot = i;
                host.DoorPress[i] = host.DoorPanel.Press(() => DoorPressed(host, slot));
                host.LiftPress[i] = host.LiftPanel.Press(() => LiftPressed(host, slot));
            }
        }

        private static void DoorPressed(FloorHost host, int slot)
        {
            int floor = host.DoorFloors[slot];
            if (floor < 0)
            {
                host.DoorPage = FloorsRules.NextPage(host.DoorPage, host.Floors, 0, FloorsRules.Slots);
                if (FloorsGame.PlayerAt(out Vector3 at, out _))
                {
                    DoorPanel(host, at);   // the next page at once, on the press
                }
                return;
            }
            FloorPlace place = floor > 0 ? host.PlaceOn(floor) : null;
            if (place == null || !place.Open())
            {
                return;
            }
            place.Pressed(host, floor);
            if (Loan.Pending)
            {
                host.DoorPanel.Hide("going in");
            }
        }

        private static void LiftPressed(FloorHost host, int slot)
        {
            int floor = host.LiftFloors[slot];
            FloorVisit v = _visit;
            if (v == null || !ReferenceEquals(v.Host, host))
            {
                return;
            }
            if (floor < 0)
            {
                host.LiftPage = FloorsRules.NextPage(host.LiftPage, host.Floors, v.Floor, FloorsRules.Slots);
                if (FloorsGame.PlayerAt(out Vector3 at, out _))
                {
                    LiftPanel(v, at);   // the next page at once, on the press
                }
                return;
            }
            if (floor > 0 && floor != v.Floor)
            {
                host.LiftPanel.Hide("going to another floor");
                Lift(floor, "the lift");
            }
        }

        // While the player is outdoors near the open door of a building whose door's floor stands on another structure: the
        // game's own click on the building is seen before it loads, so the building reads as that structure in time.
        private void Watch(Vector3 at)
        {
            FloorHost want = null;
            float nearest = WatchReach;
            for (int i = 0; i < Hosts.Count; i++)
            {
                FloorHost host = Hosts[i];
                if (host.Floors == 0 || !host.Open())
                {
                    continue;
                }
                Vector3 offset = host.Door - at;
                float flat = new Vector2(offset.x, offset.z).magnitude;
                if (flat < nearest && NeedsBorrow(host, host.PlaceOn(FloorsRules.DoorFloor(host.Floors, host.DefaultFloor(false)))))
                {
                    want = host;
                    nearest = flat;
                }
            }
            _watchHost = want;
            if (want != null && !_watching && isActiveAndEnabled)
            {
                _watching = true;
                StartCoroutine(Guard.Routine(WatchClick()));
            }
            else if (want == null)
            {
                _watching = false;
            }
        }

        private static IEnumerator WatchClick()
        {
            while (_watching)
            {
                FloorHost host = _watchHost;
                if (host != null && FloorsGame.Entering && !Loan.Pending && !Loan.Borrowed && !FloorsGame.InsideBuilding)
                {
                    _watching = false;
                    Instance?.GameWayIn(host);
                }
                yield return null;
            }
        }
    }
}
