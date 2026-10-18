using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The rules of "traffic sees people in the road" in plain numbers: stopping distances, the strip ahead of a car, how hard
    /// the driver brakes and when the horn sounds, when somebody waits at the kerb, when a held car's person is moved on, a
    /// bump against a hit, the grid of people and who gets a stand-in. Game-free, so NavCheck's SolidCheck runs every rule.
    /// </summary>
    public static class SolidRules
    {
        // When and where it looks.
        public const float TickSeconds = 0.125f;     // the street is looked at eight times a second
        public const float IdleSeconds = 0.5f;       // ...and twice a second while there is nothing to look at (indoors, the option off)
        public const float Near = 90f;               // metres from the player inside which cars and people are looked at (the game puts neither further out than that)
        public const int MostPeople = 160;           // people the grid holds; the game has about 60 out
        public const int MostCars = 96;              // cars looked at in one tick

        // The driver.
        public const float Reaction = 0.4f;          // seconds before a driver's foot is on the brake (a tick and a physics step are inside it)
        public const float Comfort = 3.5f;           // m/s2 of an ordinary stop: the strip ahead is as long as that takes
        public const float EaseFrom = 2.2f;          // m/s2 needed to stop short of them at which the driver begins to brake
        public const float HardFrom = 5f;            // ...above which it is a hard brake, with the horn (StreetRules.TrafficBraking)
        public const float HardMost = 9f;            // m/s2: all a car's tyres give. Needing more than this, a driver cannot stop
        public const float LastResort = 60f;         // m/s2 a careful car may be stopped with when nothing is owed (the game's own stop for an obstacle is harsher: 50 km/h in 1.25 m)
        public const float Gap = 2.5f;               // metres left between the nose and the person
        public const float GapLeast = 0.8f;          // ...and the least that is ever aimed for
        public const float GapMost = 8f;             // ...and the most, for a car whose sensor reaches a long way
        public const float SensorOverlap = 0.5f;     // metres of a stand-in that are inside the sensor where the car comes to rest
        public const float Crawl = 1f;               // m/s below which nothing a driver does is a hard brake: the last of an ordinary stop
        public const float Overrun = 1000f;          // m/s2 "needed" by a car that is inside its gap already: more than any limit, so it is braked with all it may be
        public const float SightMost = 60f;          // metres: no strip is longer
        public const float SightLeast = 4.5f;        // ...or shorter (a car that stands still has somebody 4 m ahead in its way)

        // The strip and the people in it.
        public const float StripMargin = 0.5f;       // metres beyond the car's side that are still in its way
        public const float StripWidest = 1.5f;       // metres from the car's line at which that margin ends (a lane's kerb is a little further out)
        public const float StripLeast = 0.15f;       // ...but always this much beyond the side of a vehicle that is wider than that
        public const float MovingFrom = 0.3f;        // m/s from which somebody is on the move
        public const float PersonRadius = 0.3f;      // metres
        public const float StepBand = 1.2f;          // metres outside the strip from which somebody walking in is about to step out
        public const float StepSpeed = 0.5f;         // m/s across the car's line that makes it a step out and not a drift along the kerb
        public const float StepWithin = 1f;          // seconds inside which they would be in the strip
        public const float WaitMost = 4f;            // seconds somebody waits at the kerb for a car at the most
        public const float SafetyStep = 0.5f;        // metres added to what the car needs before somebody may step out

        // Stand-ins, brakes, watched cars.
        public const int Proxies = 16;               // stand-ins the cars' sensors read, at the most
        public const float ProxyRadius = 0.6f;       // metres: a little more than the strip's margin, so that whoever is in the strip is in the sensor
        public const float ProxyHeight = 1.8f;       // metres
        public const int Brakes = 8;                 // cars braked for somebody at once
        public const int Watched = 6;                // cars whose bodies are compared with people every other physics step
        public const float WatchWithin = 1f;         // seconds from a person at which a car is watched

        // The horn and the hold.
        public const float HornEvery = 4f;           // seconds between two horns from one car (the game's own least is 2)
        public const float DawdleAfter = 3f;         // seconds a car stands behind somebody before it sounds its horn
        public const float HeldBeyondGap = 3.5f;     // metres beyond its gap inside which somebody ahead of a standing car is holding it up
        public const float HeldSpeed = 0.5f;         // m/s below which a car stands
        public const float HoldMost = 12f;           // seconds a car is held before the person is moved on
        public const float RemoveAfter = 16f;        // ...and before somebody nobody can see is put somewhere else by the game
        public const int PutAwayMost = 8;            // people sent back to the game's pool after one look; a car past these waits for the next

        // Contact.
        public const float BumpFrom = 0.6f;          // m/s: slower than this the car has only come to rest against them
        public const float StrikeFrom = 2.2f;        // m/s, 8 km/h: from here it is a hit; below it a bump, and nobody falls

        /// <summary>What a driver makes of somebody ahead.</summary>
        public enum Driver { Clear, Ease, Hard, Late }

        /// <summary>What a car's body meeting a person is.</summary>
        public enum Contact { None, Bump, Strike }

        /// <summary>Metres a car at that speed covers while it stops with that braking.</summary>
        public static float StoppingDistance(float speed, float braking)
        {
            if (speed <= 0f)
            {
                return 0f;
            }
            return speed * speed / (2f * (braking < 0.1f ? 0.1f : braking));
        }

        /// <summary>How far ahead of its nose a car looks: its reaction, an ordinary stop with a third to spare, and the gap; 4.5 to 60 m.</summary>
        public static float Sight(float speed)
        {
            if (speed < 0f)
            {
                speed = 0f;
            }
            float sight = speed * Reaction + StoppingDistance(speed, Comfort) * 1.3f + Gap + 2f;
            return sight < SightLeast ? SightLeast : sight > SightMost ? SightMost : sight;
        }

        /// <summary>The gap a car aims to leave when the game's sensor is to hold it: its sensor half a metre into the stand-in. 0.8 to 8 m.</summary>
        public static float GapFor(float sensorBeyondNose)
        {
            float gap = sensorBeyondNose + ProxyRadius - SensorOverlap;
            return gap > GapMost ? GapMost : gap < GapLeast ? GapLeast : gap;
        }

        /// <summary>How far ahead of its nose a car with that gap looks at that speed: Sight, and whatever its gap is over the usual one.</summary>
        public static float SightFor(float speed, float gap)
        {
            return Sight(speed) + (gap > Gap ? gap - Gap : 0f);
        }

        /// <summary>The braking (m/s2) that stops a car gap short of somebody distance metres ahead; inside its gap and moving, Overrun.</summary>
        public static float Need(float speed, float distance, float gap)
        {
            if (speed <= 0f)
            {
                return 0f;
            }
            float room = distance - gap;
            if (room <= 0.05f)
            {
                return Overrun;
            }
            float need = speed * speed / (2f * room);
            return need > Overrun ? Overrun : need;
        }

        /// <summary>What the driver makes of them: nothing yet, an ordinary stop, a hard one, or too late; at a crawl never more than ordinary.</summary>
        public static Driver Judge(float speed, float distance, float gap)
        {
            float need = Need(speed, distance, gap);
            if (need < EaseFrom)
            {
                return Driver.Clear;
            }
            return need < HardFrom || speed < Crawl ? Driver.Ease : need <= HardMost ? Driver.Hard : Driver.Late;
        }

        /// <summary>The braking to apply now, m/s2: none until needed, then what stops the car at the gap; only what tyres give when an accident is owed.</summary>
        /// <param name="braking">The car was being braked for this person at the last look.</param>
        public static float Brake(float speed, float distance, float gap, bool braking, bool owed)
        {
            if (speed <= 0f)
            {
                return 0f;
            }
            float need = Need(speed, distance, gap);
            if (need < EaseFrom && !braking)
            {
                return 0f;
            }
            float most = owed ? HardMost : LastResort;
            return need > most ? most : need;
        }

        /// <summary>How far ahead of a car's nose somebody must be to step into its way: a tick and a reaction, a firm stop, the least gap, a step.</summary>
        public static float SafeToStep(float speed)
        {
            if (speed < 0f)
            {
                speed = 0f;
            }
            return speed * (Reaction + TickSeconds) + StoppingDistance(speed, HardFrom) + GapLeast + PersonRadius + SafetyStep;
        }

        /// <summary>True if somebody that far ahead of the nose of a car at that speed must not step out now.</summary>
        public static bool MustWait(float speed, float distance)
        {
            return speed > HeldSpeed && distance < SafeToStep(speed);
        }

        /// <summary>Seconds they wait for the car to go by: until it has passed and a second more, four at the most.</summary>
        public static float WaitFor(float speed, float distance)
        {
            float wait = (distance < 0f ? 0f : distance) / (speed < 1f ? 1f : speed) + 1f;
            return wait > WaitMost ? WaitMost : wait;
        }

        /// <summary>True if the driver cannot stop and an accident is owed: the one case in which a car reaches somebody still moving.</summary>
        public static bool MayStrike(bool owed, float speed, float distance, float gap)
        {
            return owed && Judge(speed, distance, gap) == Driver.Late;
        }

        /// <summary>Where a point is from a car: metres ahead of its middle along the way it faces (a flat unit vector), and to its right.</summary>
        public static void Local(float px, float pz, float cx, float cz, float fx, float fz, out float ahead, out float side)
        {
            float dx = px - cx;
            float dz = pz - cz;
            ahead = dx * fx + dz * fz;
            side = dx * fz - dz * fx;
        }

        /// <summary>Half the strip's width ahead of a vehicle: half a metre beyond its side, at most 1.5 m from its line, always a hand beyond a wider one.</summary>
        public static float HalfStrip(float halfWidth)
        {
            float wide = halfWidth + StripMargin;
            if (wide > StripWidest)
            {
                wide = StripWidest;
            }
            return wide < halfWidth + StripLeast ? halfWidth + StripLeast : wide;
        }

        /// <summary>True if somebody that far to the side is in the car's way: anywhere in the strip if moving, only where its body would touch if not.</summary>
        public static bool InWay(float side, float halfWidth, bool moving)
        {
            float reach = moving ? HalfStrip(halfWidth) : halfWidth + PersonRadius;
            return side <= reach && side >= -reach;
        }

        /// <summary>True if that place is in the strip: between <paramref name="from"/> and <paramref name="to"/> metres ahead, and within the half width either side. The edges count.</summary>
        public static bool InStrip(float ahead, float side, float from, float to, float halfWidth)
        {
            return ahead >= from && ahead <= to && side >= -halfWidth && side <= halfWidth;
        }

        /// <summary>True if somebody beside the strip is about to step into it: within a step of its edge, walking in, inside it within a second.</summary>
        /// <param name="side">Metres to the right of the car's line.</param>
        /// <param name="sideSpeed">Their speed towards positive side, in m/s.</param>
        /// <param name="halfStrip">Half the strip's width (the car's half width and the margin).</param>
        public static bool StepsIn(float side, float sideSpeed, float halfStrip)
        {
            float aside = Math.Abs(side);
            if (aside <= halfStrip || aside > halfStrip + StepBand)
            {
                return false;
            }
            float inwards = side > 0f ? -sideSpeed : sideSpeed;
            return inwards >= StepSpeed && aside - inwards * StepWithin <= halfStrip;
        }

        /// <summary>True if the car sounds its horn at somebody who has just stepped out close: on the look at which it became a hard brake, and not within four seconds of its last.</summary>
        public static bool HornDue(Driver now, Driver before, float sinceLastHorn)
        {
            bool hard = now == Driver.Hard || now == Driver.Late;
            bool was = before == Driver.Hard || before == Driver.Late;
            return hard && !was && sinceLastHorn >= HornEvery;
        }

        /// <summary>True if a car that has stood behind somebody that long sounds its horn at them (again).</summary>
        public static bool DawdleHorn(float heldFor, float sinceLastHorn)
        {
            return heldFor >= DawdleAfter && sinceLastHorn >= HornEvery;
        }

        /// <summary>True if a car is being held up: standing, with somebody no further ahead of its nose than its gap and a few metres.</summary>
        public static bool Held(float speed, float distance, float gap)
        {
            return speed < HeldSpeed && distance <= gap + HeldBeyondGap;
        }

        /// <summary>True once a car has been held so long that the person is moved on.</summary>
        public static bool MoveOn(float heldFor) => heldFor >= HoldMost;

        /// <summary>True once somebody who would not move, and whom nobody can see, is put somewhere else by the game.</summary>
        public static bool Remove(float heldFor, bool inView) => heldFor >= RemoveAfter && !inView;

        /// <summary>
        /// Adds somebody to this look's list of people to send back to the game's pool, once however many cars they hold.
        /// False if the list is full: the car is left as it is and tries again at the next look.
        /// </summary>
        public static bool Queue<T>(T[] queue, ref int count, T item) where T : class
        {
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(queue[i], item))
                {
                    return true;
                }
            }
            if (count >= queue.Length)
            {
                return false;
            }
            queue[count++] = item;
            return true;
        }

        /// <summary>What the car's body meeting a person at that speed is.</summary>
        public static Contact Touch(float speed)
        {
            return speed < BumpFrom ? Contact.None : speed < StrikeFrom ? Contact.Bump : Contact.Strike;
        }

        /// <summary>True if a car that near somebody (metres ahead of its nose) at that speed is to be watched for contact: within a second of them.</summary>
        public static bool Watch(float speed, float distance)
        {
            return speed >= BumpFrom && distance <= speed * WatchWithin + 1f;
        }

        // Stepping out at a chosen moment.

        public const float KerbToStrip = 1.5f;       // metres from a lane's middle at which somebody walking in is in a car's strip
        public const float NoseFromMiddle = 2.3f;    // metres from a car's middle to its nose, near enough
        public const float CloseBraking = 6.5f;      // m/s2 a driver is left to need by somebody who steps out close
        public const float SeenAfter = 0.3f;         // seconds of the car's travel allowed for their being seen

        /// <summary>How far up the lane the car should be as somebody at the kerb sets off, to leave the driver a hard stop (or, late, none).</summary>
        public static float StepOutAt(float carSpeed, float metresToLane, bool late, float walkPace = StreetRules.WalkPace)
        {
            if (carSpeed < 0f)
            {
                carSpeed = 0f;
            }
            float walk = (metresToLane - KerbToStrip < 0f ? 0f : metresToLane - KerbToStrip) / (walkPace < 0.5f ? 0.5f : walkPace);
            float left = late ? carSpeed * SeenAfter + 1f : carSpeed * SeenAfter + StoppingDistance(carSpeed, CloseBraking) + Gap;
            return carSpeed * walk + left + NoseFromMiddle;
        }

        /// <summary>People hit by ordinary traffic per hour at the most: the reckless drivers' bank, as a hit happens only while one is owed.</summary>
        public static float StrikesPerHourMost(int frequency, float weather)
        {
            return StreetRules.PerHour(frequency, weather) * StreetRules.PeopleShare;
        }

        /// <summary>The people near the player in a grid of 8 m squares, built once a tick by a counting sort, nothing allocated after the constructor.</summary>
        public sealed class PeopleGrid
        {
            public const float Cell = 8f;            // metres
            public const int Side = 24;              // squares each way: 192 m, the window round the player
            private const float Slack = 0.05f;       // metres the squares looked at reach beyond the strip's corners

            private readonly float[] _x;
            private readonly float[] _z;
            private readonly int[] _cell;
            private readonly int[] _order;           // people, sorted by square
            private readonly int[] _start = new int[Side * Side + 1];
            private readonly int[] _fill = new int[Side * Side];
            private float _minX;
            private float _minZ;

            public int Count { get; private set; }

            public PeopleGrid(int capacity)
            {
                _x = new float[capacity];
                _z = new float[capacity];
                _cell = new int[capacity];
                _order = new int[capacity];
            }

            public float X(int person) => _x[person];
            public float Z(int person) => _z[person];

            /// <summary>Empties it and centres its window on a place.</summary>
            public void Begin(float centreX, float centreZ)
            {
                Count = 0;
                _minX = centreX - Cell * Side * 0.5f;
                _minZ = centreZ - Cell * Side * 0.5f;
            }

            /// <summary>Adds somebody; their number, or -1 if they are outside the window or it is full.</summary>
            public int Add(float x, float z)
            {
                if (Count >= _x.Length)
                {
                    return -1;
                }
                int column = (int)Math.Floor((x - _minX) / Cell);
                int row = (int)Math.Floor((z - _minZ) / Cell);
                if (column < 0 || column >= Side || row < 0 || row >= Side)
                {
                    return -1;
                }
                _x[Count] = x;
                _z[Count] = z;
                _cell[Count] = row * Side + column;
                return Count++;
            }

            /// <summary>Sorts everybody added into their squares. Call once, after the last Add.</summary>
            public void Build()
            {
                int cells = Side * Side;
                for (int i = 0; i <= cells; i++)
                {
                    _start[i] = 0;
                }
                for (int i = 0; i < Count; i++)
                {
                    _start[_cell[i] + 1]++;
                }
                for (int i = 0; i < cells; i++)
                {
                    _start[i + 1] += _start[i];
                    _fill[i] = _start[i];
                }
                for (int i = 0; i < Count; i++)
                {
                    _order[_fill[_cell[i]]++] = i;
                }
            }

            /// <summary>Everybody in a strip ahead of a car into found, with how far ahead and aside; only the squares its corners span are looked at.</summary>
            /// <returns>How many were found (never more than the arrays hold).</returns>
            public int Strip(float cx, float cz, float fx, float fz, float from, float to, float halfWidth, int[] found, float[] ahead, float[] side)
            {
                // The strip's four corners: its bounds are theirs.
                float rx = fz;
                float rz = -fx;
                float ax = cx + fx * from, az = cz + fz * from;
                float bx = cx + fx * to, bz = cz + fz * to;
                float wx = Math.Abs(rx) * halfWidth, wz = Math.Abs(rz) * halfWidth;
                // A hand's width more each way: the squares are picked by other arithmetic than the test below, and somebody
                // exactly on the strip's edge must not fall between the two.
                float minX = (ax < bx ? ax : bx) - wx - Slack, maxX = (ax > bx ? ax : bx) + wx + Slack;
                float minZ = (az < bz ? az : bz) - wz - Slack, maxZ = (az > bz ? az : bz) + wz + Slack;
                int c0 = (int)Math.Floor((minX - _minX) / Cell), c1 = (int)Math.Floor((maxX - _minX) / Cell);
                int r0 = (int)Math.Floor((minZ - _minZ) / Cell), r1 = (int)Math.Floor((maxZ - _minZ) / Cell);
                if (c0 < 0) { c0 = 0; }
                if (r0 < 0) { r0 = 0; }
                if (c1 >= Side) { c1 = Side - 1; }
                if (r1 >= Side) { r1 = Side - 1; }
                int count = 0;
                for (int row = r0; row <= r1; row++)
                {
                    for (int column = c0; column <= c1; column++)
                    {
                        int cell = row * Side + column;
                        for (int k = _start[cell]; k < _start[cell + 1]; k++)
                        {
                            int person = _order[k];
                            float dx = _x[person] - cx;
                            float dz = _z[person] - cz;
                            float along = dx * fx + dz * fz;
                            float across = dx * fz - dz * fx;
                            if (along >= from && along <= to && across >= -halfWidth && across <= halfWidth && count < found.Length)
                            {
                                found[count] = person;
                                ahead[count] = along;
                                side[count] = across;
                                count++;
                            }
                        }
                    }
                }
                return count;
            }
        }

        /// <summary>The few stand-ins shared among the people in some car's way, the most pressing first; a holder that still needs one keeps it.</summary>
        public sealed class ProxyPool
        {
            private readonly int[] _holder;          // who has each stand-in; 0 for nobody
            private readonly int[] _wanted;
            private readonly float[] _urgency;
            private readonly int[] _tag;
            private readonly bool[] _kept;
            private int _wantedCount;

            public int Size => _holder.Length;

            /// <summary>Stand-ins in use after the last Share.</summary>
            public int InUse { get; private set; }

            public ProxyPool(int size)
            {
                _holder = new int[size];
                _wanted = new int[size];
                _urgency = new float[size];
                _tag = new int[size];
                _kept = new bool[size];
            }

            /// <summary>Who has that stand-in (their id), or 0.</summary>
            public int Holder(int slot) => _holder[slot];

            /// <summary>What was given with the id of whoever has that stand-in at the last Share (the caller's own number for them).</summary>
            public int Tag(int slot) => _tag[slot];

            /// <summary>Starts a new look: nobody is wanted yet.</summary>
            public void Begin()
            {
                _wantedCount = 0;
            }

            /// <summary>Somebody needs a stand-in: id is theirs from look to look, never 0; urgency the seconds before a car reaches them.</summary>
            public void Want(int id, float urgency, int tag)
            {
                if (id == 0)
                {
                    return;
                }
                for (int i = 0; i < _wantedCount; i++)
                {
                    if (_wanted[i] == id)
                    {
                        if (urgency >= _urgency[i])
                        {
                            return;
                        }
                        // More pressing than was thought: taken out and put in again at its new place.
                        for (int k = i; k < _wantedCount - 1; k++)
                        {
                            _wanted[k] = _wanted[k + 1];
                            _urgency[k] = _urgency[k + 1];
                            _tag[k] = _tag[k + 1];
                        }
                        _wantedCount--;
                        break;
                    }
                }
                int place = _wantedCount;
                while (place > 0 && _urgency[place - 1] > urgency)
                {
                    place--;
                }
                if (place >= _wanted.Length)
                {
                    return;
                }
                int last = _wantedCount < _wanted.Length ? _wantedCount : _wanted.Length - 1;
                for (int i = last; i > place; i--)
                {
                    _wanted[i] = _wanted[i - 1];
                    _urgency[i] = _urgency[i - 1];
                    _tag[i] = _tag[i - 1];
                }
                _wanted[place] = id;
                _urgency[place] = urgency;
                _tag[place] = tag;
                if (_wantedCount < _wanted.Length)
                {
                    _wantedCount++;
                }
            }

            /// <summary>Shares the stand-ins out: freed is set for each taken from somebody, given for each with a new holder.</summary>
            public void Share(bool[] freed, bool[] given, int[] tags)
            {
                int size = _holder.Length;
                for (int slot = 0; slot < size; slot++)
                {
                    freed[slot] = false;
                    given[slot] = false;
                    _kept[slot] = false;
                }
                // Whoever still needs theirs keeps it.
                for (int slot = 0; slot < size; slot++)
                {
                    int holder = _holder[slot];
                    if (holder == 0)
                    {
                        continue;
                    }
                    int at = -1;
                    for (int i = 0; i < _wantedCount; i++)
                    {
                        if (_wanted[i] == holder)
                        {
                            at = i;
                            break;
                        }
                    }
                    if (at < 0)
                    {
                        _holder[slot] = 0;
                        freed[slot] = true;
                    }
                    else
                    {
                        _kept[at] = true;
                        tags[slot] = _tag[at];
                    }
                }
                // The rest take the free ones, the most pressing first.
                int free = 0;
                for (int i = 0; i < _wantedCount; i++)
                {
                    if (_kept[i])
                    {
                        continue;
                    }
                    while (free < size && _holder[free] != 0)
                    {
                        free++;
                    }
                    if (free >= size)
                    {
                        break;
                    }
                    _holder[free] = _wanted[i];
                    tags[free] = _tag[i];
                    given[free] = true;
                }
                int used = 0;
                for (int slot = 0; slot < size; slot++)
                {
                    if (_holder[slot] != 0)
                    {
                        used++;
                    }
                }
                InUse = used;
            }

            /// <summary>Takes every stand-in back at once. <paramref name="freed"/> is set for each that somebody had.</summary>
            public void Clear(bool[] freed)
            {
                for (int slot = 0; slot < _holder.Length; slot++)
                {
                    freed[slot] = _holder[slot] != 0;
                    _holder[slot] = 0;
                }
                _wantedCount = 0;
                InUse = 0;
            }
        }
    }
}
