using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The inside of the city bus as a rider walks it, and the few rules of a ride that are not the game's. Bus-local
    /// metres: x towards the kerb side (where the doors are), z forward, the floor <see cref="Floor"/> over the road; a
    /// facing is degrees about the vertical, 0 forward and 90 towards the doors.
    ///
    /// The places are the clips' own measurements laid on the bus: board_step leaves a rider standing on the step
    /// <see cref="StepIn"/> in from where it began; pay_tap holds the card to a reader 0.50 m ahead and 0.975 m up, which
    /// is the validator by the driver; sit_bench sits on a seat 0.46 m up whose middle is 0.30 m behind the feet; strap_hang
    /// holds a strap 1.82 m over the feet, 0.28 m to the right and 0.16 m ahead. The bus's inside is modelled to these
    /// numbers (PropAssets/scripts/props_service.py, bus_inside), and PropAssets/scripts/checks_service.py reads every
    /// <c>new Spot</c> of this file and measures it on the mesh: the floor under each place of a path, nothing over it, the
    /// seats, the reader and the strap where the clips want them. EconomyCheck (BusInsideChecks.cs) walks every path
    /// against the furniture listed here.
    ///
    /// Game-free: nothing here knows of Unity.
    /// </summary>
    public static class BusInside
    {
        /// <summary>A place on the floor, in the bus's own metres.</summary>
        public struct Spot
        {
            public readonly float X;
            public readonly float Z;

            public Spot(float x, float z)
            {
                X = x;
                Z = z;
            }

            public float To(Spot other)
            {
                float dx = other.X - X, dz = other.Z - Z;
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }

            /// <summary>The facing that looks from here towards another place (0 forward, 90 towards the doors).</summary>
            public float Facing(Spot other)
            {
                return (float)(Math.Atan2(other.X - X, other.Z - Z) * 180.0 / Math.PI);
            }
        }

        /// <summary>An obstacle on the floor, as a box seen from above: seats, housings, the cab.</summary>
        public struct Box
        {
            public readonly float X0, X1, Z0, Z1;

            public Box(float x0, float x1, float z0, float z1)
            {
                X0 = x0;
                X1 = x1;
                Z0 = z0;
                Z1 = z1;
            }

            /// <summary>Metres from a place to the box's edge; 0 or less inside it.</summary>
            public float Clearance(Spot at)
            {
                float dx = Math.Max(Math.Max(X0 - at.X, at.X - X1), 0f);
                float dz = Math.Max(Math.Max(Z0 - at.Z, at.Z - Z1), 0f);
                if (dx > 0f || dz > 0f)
                {
                    return (float)Math.Sqrt(dx * dx + dz * dz);
                }
                return -Math.Min(Math.Min(at.X - X0, X1 - at.X), Math.Min(at.Z - Z0, Z1 - at.Z));
            }
        }

        public enum Place { Aboard, Reader, Seat, WindowSeat, Strap, MiddleAboard, MiddleReader }

        // ---------------------------------------------------------------- the bus and the clips

        public const float Floor = 0.33f;                         // BusVehicle.Floor: the doors' threshold and the whole floor
        public const float HalfWidth = BusBay.BusSideHalf;        // the side the doors are in
        public const float Wall = 1.15f;                          // the inner face of the walls
        public const float FrontDoor = 4.45f;                     // BusVehicle.FrontDoorAhead: the middle of the front door
        public const float MiddleDoor = -0.55f;                   // BusVehicle.MiddleDoorAhead
        public const float Outside = HalfWidth + 0.30f;           // BusVehicle.DoorOut: where a rider stands on the road to step in (board_step), and steps down to
        public const float StepIn = 0.44f;                        // board_step ends standing on the step this far in from where it began
        public const float StepUp = 0.30f;                        // ...and this far up: the floor is Floor, so the root is put 3 cm higher than the pose
        public const float SeatUp = 0.46f;                        // sit_bench: the seat's top over the feet
        public const float SeatFeet = 0.30f;                      // ...and its middle this far behind them
        public const float StrapUp = 1.82f;                       // strap_hang: the strap's loop over the feet
        public const float StrapRight = 0.28f;                    // ...this far to the right
        public const float StrapAhead = 0.16f;                    // ...and this far ahead
        public const float StrapX = 0.30f;                        // the rail along the aisle the straps hang from
        public const float RiderStrap = -0.55f;                   // the strap opposite the middle door that a standing rider holds
        public const float ReaderAhead = 0.50f;                   // pay_tap: the reader ahead of the feet
        public const float ReaderUp = 0.975f;                     // ...and over them
        public const float ReaderX = 0.482f;                      // the validator's lens by the driver (its face, towards the door)
        public const float ReaderZ = 4.94f;
        public const float ReaderY = 1.31f;                       // ...the middle of the lens over the road
        public const float MiddleReaderX = 0.96f;                 // the second validator, inside the middle door on its rear pole: its lens (facing forward, into the doorway)
        public const float MiddleReaderZ = -0.948f;               // (props_service.py: on the bracket to the pole at y0 + 0.08; ReaderY over the road as the first)
        public const float Walk = 1.05f;                          // metres a second inside: the game's walk, unhurried
        public const float StepDown = 0.55f;                      // seconds to walk down from the threshold onto the road

        // ---------------------------------------------------------------- the places

        /// <summary>On the road at the front door, facing in: where board_step begins.</summary>
        public static readonly Spot BoardFrom = new Spot(1.50f, 4.45f);

        /// <summary>Where board_step leaves the rider: on the floor StepIn in from BoardFrom.</summary>
        public static readonly Spot Aboard = new Spot(1.06f, 4.45f);

        /// <summary>
        /// Before the validator, turned to it (<see cref="ReaderFacing"/>): its lens ReaderAhead away and ReaderUp over the floor.
        /// Not square in front of it: that is where the door's post stands. From a little behind and to the side, clear of both posts.
        /// </summary>
        public static readonly Spot Reader = new Spot(0.90f, 4.66f);

        public static float ReaderFacing => Reader.Facing(new Spot(ReaderX, ReaderZ));

        /// <summary>Before the middle door's validator, turned back to it: its lens ReaderAhead away, in the doorway behind the step.</summary>
        public static readonly Spot MiddleReader = new Spot(0.96f, -0.448f);

        public static float MiddleReaderFacing => MiddleReader.Facing(new Spot(MiddleReaderX, MiddleReaderZ));

        /// <summary>The two seats a rider sits on, opposite the middle door with open floor ahead of them: their middles.</summary>
        public static readonly Spot[] Seats = { new Spot(-0.45f, -0.52f), new Spot(-0.86f, -0.52f) };

        /// <summary>Where a standing rider stands, facing forward, the strap at RiderStrap in the right hand.</summary>
        public static readonly Spot Strap = new Spot(0.02f, -0.71f);

        /// <summary>Inside the middle door at its threshold, and on the road beyond it: the way out.</summary>
        public static readonly Spot Exit = new Spot(0.98f, -0.55f);
        public static readonly Spot ExitTo = new Spot(1.50f, -0.55f);

        /// <summary>Inside the front door, for a rider who gets off again before the bus leaves while still on its platform.</summary>
        public static readonly Spot FrontExit = new Spot(1.00f, 4.45f);
        public static readonly Spot FrontExitTo = new Spot(1.50f, 4.45f);

        /// <summary>On the road at the middle door, facing in, and where board_step leaves the rider there: the way in when the front door cannot be reached.</summary>
        public static readonly Spot MiddleBoardFrom = new Spot(1.50f, -0.55f);
        public static readonly Spot MiddleAboard = new Spot(1.06f, -0.55f);

        // From the middle door's step up the aisle to the validator (the way in by the middle door), and back down it.
        private static readonly Spot DoorIn = new Spot(0.60f, -0.40f);
        private static readonly Spot[] AisleUp = { new Spot(0.15f, -0.20f), new Spot(0.15f, 0.25f), new Spot(0.15f, 3.40f), new Spot(0.45f, 4.30f) };

        // Along the aisle from the validator: past the platform's corner, between the front housings, to the space opposite the middle door.
        private static readonly Spot[] Aisle = { new Spot(0.45f, 4.30f), new Spot(0.15f, 3.40f), new Spot(0.15f, 0.25f) };
        private static readonly Spot SeatTurn = new Spot(-0.30f, -0.05f);       // short of the seats: they turn round there
        private static readonly Spot OffSeat = new Spot(-0.10f, -0.22f);        // up off a seat, towards the door
        private static readonly Spot WindowOut = new Spot(-0.45f, -0.08f);      // out from the window seat past the aisle one
        private static readonly Spot BackToAisle = new Spot(0.15f, -0.20f);     // the aisle's end opposite the middle door

        /// <summary>Where the feet stand to sit on a seat, facing forward.</summary>
        public static Spot SeatFeetAt(int seat)
        {
            Spot middle = Seats[Math.Max(0, Math.Min(Seats.Length - 1, seat))];
            return new Spot(middle.X, middle.Z + SeatFeet);
        }

        /// <summary>Where a place puts the feet.</summary>
        public static Spot At(Place place)
        {
            switch (place)
            {
                case Place.Aboard: return Aboard;
                case Place.Reader: return Reader;
                case Place.Seat: return SeatFeetAt(0);
                case Place.WindowSeat: return SeatFeetAt(1);
                case Place.MiddleAboard: return MiddleAboard;
                case Place.MiddleReader: return MiddleReader;
                default: return Strap;
            }
        }

        /// <summary>The way a rider faces at a place once there.</summary>
        public static float FacingAt(Place place)
        {
            return place == Place.Reader ? ReaderFacing : place == Place.MiddleReader ? MiddleReaderFacing : place == Place.Aboard || place == Place.MiddleAboard ? -90f : 0f;
        }

        // ---------------------------------------------------------------- the furniture a path must keep clear of

        /// <summary>
        /// What stands on the floor, seen from above (props_service.py: SEATS, the housings, the cab, the poles): every
        /// seat's cushion and back, the wheel housings, the cab and its partition, the validator, the poles.
        /// </summary>
        public static readonly Box[] Furniture = BuildFurniture();

        private static Box[] BuildFurniture()
        {
            var list = new List<Box>(24);
            float[,] seats =
            {
                { -0.45f, -0.52f }, { -0.86f, -0.52f }, { -0.45f, 0.95f }, { -0.86f, 0.95f }, { -0.45f, 1.70f }, { -0.86f, 1.70f },
                { -0.45f, -1.75f }, { -0.86f, -1.75f }, { -0.45f, -4.15f }, { -0.86f, -4.15f }, { 0.86f, 0.80f }, { 0.86f, 1.55f },
                { 0.86f, -1.75f }, { 0.86f, -4.15f }
            };
            for (int i = 0; i < seats.GetLength(0); i++)
            {
                // The cushion 0.40 square, and its back behind it.
                list.Add(new Box(seats[i, 0] - 0.20f, seats[i, 0] + 0.20f, seats[i, 1] - 0.27f, seats[i, 1] + 0.20f));
            }
            list.Add(new Box(-Wall, -0.55f, 2.28f, 3.52f));            // the front housings: over the axle, 0.55 in from the middle
            list.Add(new Box(0.55f, Wall, 2.28f, 3.52f));
            list.Add(new Box(-Wall, -0.60f, -3.52f, -2.28f));          // the rear ones, 0.60
            list.Add(new Box(0.60f, Wall, -3.52f, -2.28f));
            list.Add(new Box(-Wall, -0.40f, 3.84f, 5.30f));            // the cab and its partition
            list.Add(new Box(-0.40f, Wall, 5.04f, 5.30f));             // the wall under the windscreen beyond the platform
            list.Add(new Box(0.36f, 0.48f, 4.89f, 4.99f));             // the validator on its post
            list.Add(new Box(0.90f, 1.07f, -1.05f, -0.95f));           // the middle door's validator, on its bracket to the rear pole
            list.Add(new Box(-Wall, Wall, -5.30f, -4.84f));            // the back seat
            float[,] poles = { { -0.25f, 1.33f }, { 0.66f, 1.18f }, { -0.25f, -1.36f }, { 0.66f, -1.36f }, { 1.08f, 3.94f }, { 1.08f, 4.96f }, { 1.08f, -1.06f }, { 1.08f, -0.04f } };
            for (int i = 0; i < poles.GetLength(0); i++)
            {
                list.Add(new Box(poles[i, 0] - 0.02f, poles[i, 0] + 0.02f, poles[i, 1] - 0.02f, poles[i, 1] + 0.02f));
            }
            return list.ToArray();
        }

        // ---------------------------------------------------------------- the paths

        /// <summary>
        /// The way from one place to another, the first place included, written into <paramref name="into"/> (cleared first).
        /// Inwards: Aboard to Reader, and Reader to a seat or the strap. Anything else is a path of the first place alone.
        /// </summary>
        public static void Path(Place from, Place to, List<Spot> into)
        {
            into.Clear();
            into.Add(At(from));
            if (from == Place.Aboard && to == Place.Reader)
            {
                into.Add(Reader);
                return;
            }
            if (from == Place.MiddleAboard && to == Place.MiddleReader)
            {
                into.Add(MiddleReader);   // (in by the middle door: its own validator, a step inside)
                return;
            }
            if (from == Place.MiddleReader && (to == Place.Seat || to == Place.WindowSeat || to == Place.Strap))
            {
                // From the middle door's validator into the bus: round the pole, to the seats opposite or the strap.
                into.Add(DoorIn);
                if (to == Place.Strap)
                {
                    into.Add(Strap);
                    return;
                }
                into.Add(BackToAisle);
                into.Add(SeatTurn);
                if (to == Place.WindowSeat)
                {
                    into.Add(WindowOut);
                }
                into.Add(At(to));
                return;
            }
            if (from == Place.MiddleAboard && to == Place.Reader)
            {
                // In by the middle door: up the aisle to the validator by the driver, to pay as at the front.
                into.Add(DoorIn);
                for (int i = 0; i < AisleUp.Length; i++)
                {
                    into.Add(AisleUp[i]);
                }
                into.Add(Reader);
                return;
            }
            if (from != Place.Reader || (to != Place.Seat && to != Place.WindowSeat && to != Place.Strap))
            {
                return;
            }
            for (int i = 0; i < Aisle.Length; i++)
            {
                into.Add(Aisle[i]);
            }
            if (to == Place.Strap)
            {
                into.Add(Strap);
                return;
            }
            into.Add(SeatTurn);
            if (to == Place.WindowSeat)
            {
                into.Add(WindowOut);
            }
            into.Add(At(to));
        }

        /// <summary>
        /// The way off the bus from wherever a rider stands on its floor: by the front door from its platform, else by the
        /// middle door (the aisle first, if they are up it). The last place is the threshold (Exit or FrontExit); stepping
        /// down from there onto the road (ExitTo, FrontExitTo) is the caller's. True for the front door.
        /// </summary>
        public static bool WayOut(Spot at, List<Spot> into)
        {
            into.Clear();
            into.Add(at);
            if (at.Z > 3.70f)
            {
                into.Add(FrontExit);
                return true;
            }
            if (at.X < -0.30f && at.Z < 0.10f && at.Z > -0.40f)
            {
                // At a seat: out past the aisle seat if it is the window one, then towards the door.
                if (at.X < -0.65f)
                {
                    into.Add(WindowOut);
                }
                into.Add(OffSeat);
            }
            else if (at.Z > 0.30f)
            {
                // Up the aisle: down it to its end opposite the door.
                into.Add(new Spot(0.15f, Math.Min(at.Z, 3.40f)));
                into.Add(BackToAisle);
            }
            into.Add(Exit);
            return false;
        }

        /// <summary>
        /// The way off by a door chosen for the rider (the nearest one's step down may be blocked: <see cref="OffTries"/>): from
        /// wherever they stand on the floor to that door's threshold, along the aisle when the other door is nearer. The last
        /// place is Exit or FrontExit.
        /// </summary>
        public static void WayOutBy(Spot at, bool front, List<Spot> into)
        {
            bool nearFront = WayOut(at, into);
            if (nearFront == front)
            {
                return;
            }
            into.RemoveAt(into.Count - 1);   // (the nearer door's threshold)
            if (front)
            {
                if (at.Z > 0.30f)
                {
                    // Up the aisle already: on up it to the front platform.
                    into.Clear();
                    into.Add(at);
                    into.Add(new Spot(0.15f, Math.Min(at.Z, 3.40f)));
                    if (at.Z < 3.40f)
                    {
                        into.Add(AisleUp[2]);
                    }
                    into.Add(AisleUp[3]);
                    into.Add(FrontExit);
                    return;
                }
                // From the seats, the strap or the middle door (WayOut has brought them to the aisle's end): up it to the front platform.
                for (int i = 0; i < AisleUp.Length; i++)
                {
                    into.Add(AisleUp[i]);
                }
                into.Add(FrontExit);
                return;
            }
            // From the front platform down the aisle to the middle door.
            for (int i = AisleUp.Length - 1; i >= 0; i--)
            {
                into.Add(AisleUp[i]);
            }
            into.Add(DoorIn);
            into.Add(Exit);
        }

        // ---------------------------------------------------------------- reaching the doors from the street

        /// <summary>A place on the road by one of the doors, a little along the side from its middle if need be.</summary>
        public struct Approach
        {
            public readonly bool Middle;
            public readonly float Along;

            public Approach(bool middle, float along)
            {
                Middle = middle;
                Along = along;
            }

            /// <summary>Where it is, in the bus's metres, on the road where one steps in and down.</summary>
            public Spot At => new Spot(Outside, (Middle ? MiddleDoor : FrontDoor) + Along);
        }

        /// <summary>The most a place to walk to may be along the side from its door's middle: the rest is walked by hand along the bus.</summary>
        public const float AlongMost = 1.0f;

        /// <summary>
        /// Where a rider is walked to, to get on, in the order tried: the front door's own place, then a little further
        /// along the kerb each way (something on the road there, or no way to it on the walkable ground), and only then
        /// the middle door, where they step in and walk up the aisle to the validator to pay.
        /// </summary>
        public static readonly Approach[] BoardTries =
        {
            new Approach(false, 0f), new Approach(false, 0.5f), new Approach(false, -0.5f), new Approach(false, AlongMost), new Approach(false, -AlongMost),
            new Approach(true, 0f), new Approach(true, 0.5f), new Approach(true, -0.5f)
        };

        /// <summary>
        /// Where a rider steps down to, to get off, in the order tried: beside the middle door, a little along the side each
        /// way, then the front door. From the front platform the front door comes first.
        /// </summary>
        public static readonly Approach[] OffTries =
        {
            new Approach(true, 0f), new Approach(true, 0.4f), new Approach(true, -0.4f), new Approach(false, 0f), new Approach(false, -0.4f)
        };

        public static readonly Approach[] OffTriesFront =
        {
            new Approach(false, 0f), new Approach(false, -0.4f), new Approach(true, 0f), new Approach(true, 0.4f), new Approach(true, -0.4f)
        };

        /// <summary>
        /// Where a rider is walked to, to get on, when the middle door is the nearer: its own place first, a little along each
        /// way, then the front door's. Many city buses are boarded by any door: there is a validator by each.
        /// </summary>
        public static readonly Approach[] BoardTriesMiddle =
        {
            new Approach(true, 0f), new Approach(true, 0.5f), new Approach(true, -0.5f),
            new Approach(false, 0f), new Approach(false, 0.5f), new Approach(false, -0.5f), new Approach(false, AlongMost), new Approach(false, -AlongMost)
        };

        /// <summary>
        /// Which door a rider boards by: the one nearer to where they stand as the bus pulls in (metres to the front door's place
        /// and to the middle door's on the road), the front one when it is as near as makes no difference.
        /// </summary>
        public static bool ByMiddleDoor(float toFront, float toMiddle)
        {
            return toMiddle + 0.75f < toFront;
        }

        /// <summary>The first of the tries that will do, by the caller's test (a path on the walkable ground, nothing standing there); -1 if none.</summary>
        public static int First(Approach[] tries, Func<Approach, bool> will)
        {
            for (int i = 0; tries != null && will != null && i < tries.Length; i++)
            {
                if (will(tries[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        // ---------------------------------------------------------------- the boarding strip at a stop

        /// <summary>
        /// A strip of the street at a stop that is kept clear of the game's street furniture (poles, bins, bollards, signs,
        /// hydrants, benches): from one point to another, in metres along the stop's lane from the pole and aside from the
        /// lane's line towards the kerb, and half as wide again either side.
        /// </summary>
        public struct Strip
        {
            public readonly float Along0, Aside0, Along1, Aside1, Half;

            public Strip(float along0, float aside0, float along1, float aside1, float half)
            {
                Along0 = along0;
                Aside0 = aside0;
                Along1 = along1;
                Aside1 = aside1;
                Half = half;
            }

            public float Length => (float)Math.Sqrt((Along1 - Along0) * (Along1 - Along0) + (Aside1 - Aside0) * (Aside1 - Aside0));

            /// <summary>Whether a point (along, aside) lies on the strip.</summary>
            public bool Holds(float along, float aside)
            {
                float da = Along1 - Along0, ds = Aside1 - Aside0;
                float length2 = da * da + ds * ds;
                float k = length2 > 1e-6f ? Math.Max(0f, Math.Min(1f, ((along - Along0) * da + (aside - Aside0) * ds) / length2)) : 0f;
                float pa = Along0 + da * k - along, ps = Aside0 + ds * k - aside;
                return pa * pa + ps * ps <= Half * Half;
            }

            /// <summary>Metres from a point (along, aside) to the strip's middle line: within <see cref="Half"/> of it, the point is on the strip.</summary>
            public float Distance(float along, float aside)
            {
                float da = Along1 - Along0, ds = Aside1 - Aside0;
                float length2 = da * da + ds * ds;
                float k = length2 > 1e-6f ? Math.Max(0f, Math.Min(1f, ((along - Along0) * da + (aside - Aside0) * ds) / length2)) : 0f;
                float pa = Along0 + da * k - along, ps = Aside0 + ds * k - aside;
                return (float)Math.Sqrt(pa * pa + ps * ps);
            }
        }

        public const float WalkwayHalf = 0.75f;   // the walkway from the waiting place to a door: 1.5 m wide
        public const float KerbBeyond = 1.0f;     // the kerb's strip runs this far beyond each door
        public const float KerbHalf = 0.8f;       // ...and is 1.6 m wide, astride the kerb's edge
        public const float KerbIn = 0.2f;         // ...its middle this far onto the pavement

        /// <summary>
        /// The strips kept clear at a stop, for a bus standing with its middle beside the pole (as it stands in the lane or its
        /// bay): the kerb from beyond the middle door to beyond the front door, and a walkway from the waiting place to each
        /// door. <paramref name="kerb"/> is metres from the lane's line to the kerb's edge; the waiting place is given the same way.
        /// <paramref name="stand"/> is metres the bus stands on from the pole (<see cref="StandShift"/>): its doors are that much further on.
        /// </summary>
        public static Strip[] BoardingStrips(float kerb, float waitAlong, float waitAside, float stand = 0f)
        {
            float edge = kerb + KerbIn;
            return new[]
            {
                new Strip(MiddleDoor + stand - KerbBeyond, edge, FrontDoor + stand + KerbBeyond, edge, KerbHalf),
                new Strip(waitAlong, waitAside, FrontDoor + stand, kerb, WalkwayHalf),
                new Strip(waitAlong, waitAside, MiddleDoor + stand, kerb, WalkwayHalf)
            };
        }

        // ---------------------------------------------------------------- the stop's pole, and where the bus stands by it

        /// <summary>What of the stop's pole stands out from its middle at a walker's height: the pole and its timetable plate (the flag is over head height).</summary>
        public const float PoleReach = 0.35f;

        /// <summary>Metres a walker needs between the pole and the way to a door: the navigation mesh's walker radius and a hand more.</summary>
        public const float PoleClear = 0.30f;

        /// <summary>Half a door's opening along the bus (<see cref="InDoor"/>).</summary>
        public const float DoorHalf = 0.59f;

        /// <summary>Metres onto the pavement from the kerb's edge that a door's step and the people at it take up.</summary>
        public const float DoorStep = 1.2f;

        /// <summary>Metres between the edge of a door's opening and anything standing on the kerb beside it, at the least.</summary>
        public const float DoorBy = 0.25f;

        /// <summary>
        /// The metres the bus may stand on from the pole (negative: short of it), in the order tried: beside the pole first.
        /// In a bay only those from 0 to <see cref="BayStandMost"/> (TransitMap.LayBay leaves that much slack ahead and none behind).
        /// </summary>
        public static readonly float[] StandShifts = { 0f, 0.5f, -0.5f, 1.0f, -1.0f };

        /// <summary>The most a bus may stand on from the pole in its bay: half the slack its bay has ahead (TransitMap.BayAheadSlack).</summary>
        public const float BayStandMost = 0.5f;

        /// <summary>
        /// The places the pole may stand, in the order tried: X metres onto the pavement from the kerb's edge, Z metres along
        /// from where the bus's middle stands. Beside the shelter's far end, set back from the kerb; set back behind the
        /// waiting place; at the bus's rear end; ahead of its nose. Every one is clear of the boarding strips and of both
        /// doors however the bus stands (<see cref="StandShifts"/>): the economy check says so.
        /// </summary>
        public static readonly Spot[] SignPlaces =
        {
            new Spot(2.0f, 5.5f),
            new Spot(1.6f, -3.2f),
            new Spot(0.5f, -(BusBay.BusLength * 0.5f + 1.0f)),
            new Spot(0.5f, FrontDoor + KerbBeyond + 2.7f)
        };

        /// <summary>The one of <see cref="SignPlaces"/> that is only taken where the stop has a shelter (it stands by its end panel).</summary>
        public const int SignByShelter = 0;

        /// <summary>
        /// Whether the pole may stand at (along, aside): clear of every boarding strip and of both doors' openings and their
        /// steps by <see cref="PoleReach"/> and <see cref="PoleClear"/>, with the bus standing <paramref name="stand"/> on.
        /// </summary>
        public static bool SignClear(float along, float aside, float kerb, float waitAlong, float waitAside, float stand)
        {
            float reach = PoleReach + PoleClear;
            Strip[] strips = BoardingStrips(kerb, waitAlong, waitAside, stand);
            for (int i = 0; i < strips.Length; i++)
            {
                if (strips[i].Distance(along, aside) < strips[i].Half + reach)
                {
                    return false;
                }
            }
            return !InDoorWay(along, aside, kerb, stand, reach);
        }

        /// <summary>The same, for every way the bus may stand (<see cref="StandShifts"/>).</summary>
        public static bool SignClearAlways(float along, float aside, float kerb, float waitAlong, float waitAside)
        {
            for (int i = 0; i < StandShifts.Length; i++)
            {
                if (!SignClear(along, aside, kerb, waitAlong, waitAside, StandShifts[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Whether something reaching <paramref name="reach"/> from (along, aside) stands in a door's opening or at its step.</summary>
        public static bool InDoorWay(float along, float aside, float kerb, float stand, float reach)
        {
            if (aside > kerb + DoorStep + reach)
            {
                return false;
            }
            return Math.Abs(along - (FrontDoor + stand)) < DoorHalf + reach || Math.Abs(along - (MiddleDoor + stand)) < DoorHalf + reach;
        }

        /// <summary>
        /// Where the bus stands, in metres on from the pole: the first of <see cref="StandShifts"/> from
        /// <paramref name="least"/> to <paramref name="most"/> at which neither door lines up with anything standing on the
        /// kerb, else the first at which the front door (the way in) does not, else beside the pole. The things are given
        /// by where their middles are along the lane (<paramref name="at"/>) and how far each reaches along it either way
        /// (<paramref name="half"/>); the first <paramref name="count"/> count.
        /// </summary>
        public static float StandShift(float[] at, float[] half, int count, float least, float most)
        {
            float frontOnly = float.NaN;
            for (int i = 0; i < StandShifts.Length; i++)
            {
                float shift = StandShifts[i];
                if (shift < least - 1e-4f || shift > most + 1e-4f)
                {
                    continue;
                }
                bool front = DoorFree(FrontDoor + shift, at, half, count);
                if (front && DoorFree(MiddleDoor + shift, at, half, count))
                {
                    return shift;
                }
                if (front && float.IsNaN(frontOnly))
                {
                    frontOnly = shift;
                }
            }
            return float.IsNaN(frontOnly) ? 0f : frontOnly;
        }

        /// <summary>Whether a door whose middle is <paramref name="door"/> metres along has nothing on the kerb within <see cref="DoorBy"/> of its opening.</summary>
        public static bool DoorFree(float door, float[] at, float[] half, int count)
        {
            for (int i = 0; at != null && half != null && i < count && i < at.Length && i < half.Length; i++)
            {
                if (Math.Abs(at[i] - door) < DoorHalf + DoorBy + half[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Whether something standing at (along, aside) is in the way of boarding, by the strips.</summary>
        public static bool InStrips(Strip[] strips, float along, float aside)
        {
            for (int i = 0; strips != null && i < strips.Length; i++)
            {
                if (strips[i].Holds(along, aside))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Metres along a path.</summary>
        public static float Length(List<Spot> path)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                length += path[i - 1].To(path[i]);
            }
            return length;
        }

        /// <summary>The place a given distance along a path, and the way it heads there.</summary>
        public static Spot Along(List<Spot> path, float metres, out float facing)
        {
            facing = 0f;
            if (path.Count == 0)
            {
                return default(Spot);
            }
            for (int i = 1; i < path.Count; i++)
            {
                float leg = path[i - 1].To(path[i]);
                facing = path[i - 1].Facing(path[i]);
                if (metres <= leg || i == path.Count - 1)
                {
                    float k = leg > 1e-4f ? Math.Max(0f, Math.Min(1f, metres / leg)) : 1f;
                    return new Spot(path[i - 1].X + (path[i].X - path[i - 1].X) * k, path[i - 1].Z + (path[i].Z - path[i - 1].Z) * k);
                }
                metres -= leg;
            }
            return path[path.Count - 1];
        }

        /// <summary>The clearance of a place from the furniture (the least, in metres), leaving out one box (-1 for none).</summary>
        public static float Clearance(Spot at, int except = -1)
        {
            float least = float.MaxValue;
            for (int i = 0; i < Furniture.Length; i++)
            {
                if (i != except)
                {
                    least = Math.Min(least, Furniture[i].Clearance(at));
                }
            }
            // The walls, but not where a door is open in the side: there the floor goes on to the step and the road.
            least = Math.Min(least, at.X > 0f && InDoor(at.Z) ? Outside - at.X : Wall - Math.Abs(at.X));
            return least;
        }

        /// <summary>Whether a place along the bus is within one of its doors.</summary>
        public static bool InDoor(float z)
        {
            return Math.Abs(z - FrontDoor) < 0.59f || Math.Abs(z - MiddleDoor) < 0.59f;
        }

        // ---------------------------------------------------------------- the ride's own rules

        /// <summary>How far a rider has got with a ride, for what an end to it means.</summary>
        public enum Stage { None, Walking, Boarding, Aboard, Leaving, Away, Arriving, Riding }

        public const float ShowMetres = 30f;      // the bus is seen to pull away this far before the screen darkens
        public const float ShowMoving = 7f;       // ...or for this many seconds on the move
        public const float ShowStill = 8f;        // ...or, held in its bay with its doors shut, this long
        public const float ShowMost = 16f;        // ...and never longer than this after the doors shut

        /// <summary>Whether the bus has been seen leaving for long enough and the time can be skipped.</summary>
        public static bool FadeNow(float shutFor, float movingFor, float metres)
        {
            return metres >= ShowMetres || movingFor >= ShowMoving || (movingFor <= 0f && shutFor >= ShowStill) || shutFor >= ShowMost;
        }

        /// <summary>
        /// Whether a ride that ends here, not at the stop it was for, gives the fare back. It does once paid until the bus
        /// has carried the rider away (the doors shut on them, the bus left with them); never once it has.
        /// </summary>
        public static bool FareBackOnCancel(Stage stage)
        {
            return stage == Stage.Boarding || stage == Stage.Aboard;
        }

        /// <summary>
        /// Whether a save written now puts the rider back at the stop they boarded at with the fare given back (true), or
        /// at the stop they are getting off at (false). Until the time of the ride has passed, it has not happened.
        /// </summary>
        public static bool SaveAtStart(Stage stage)
        {
            return stage == Stage.Boarding || stage == Stage.Aboard || stage == Stage.Leaving || stage == Stage.Away || stage == Stage.Riding;
        }

        /// <summary>
        /// Whether a save written now gives the fare back when it is loaded. Riding the bus through the city
        /// (<see cref="Stage.Riding"/>, or its rest skipped under the fade) a save has the rider at the last stop the bus called
        /// at, or at the one they boarded at before it has called at any: the fare is given back only in that case, as for a
        /// save written as the bus pulls away, since the ride has then not happened.
        /// </summary>
        public static bool SaveFareBack(Stage stage, int stopsPassed)
        {
            return stopsPassed <= 0 && SaveAtStart(stage);
        }
    }
}
