using System;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// Which of the game's highlights the 2.5D city map has taken into its blocks, and when the game's buildings are looked at again to
    /// find out. The game tells nothing when it highlights a building (it moves it to a layer and writes a colour), so the picture
    /// reads them, but only after something that changes them: the map opening, the pointer coming to another building, the card of one opening
    /// or shutting, the game's filters having work or having worked on a building (a toggle queues every building and the game works them a few at a frame, so a short
    /// one is over before the picture looks: <see cref="NavMapWork"/> counts them), and a look the mod itself changed. Each of
    /// those starts one pass over all the buildings, <see cref="Slice"/> of them a frame; a pass starts another while the filters still have work, and
    /// one more when it is done. A still, open map costs the comparisons that ask. Game-free; EconomyCheck runs it.
    /// </summary>
    public sealed class NavMapLights
    {
        public const int Slice = 128;       // buildings read in one frame of a pass

        private bool[] _lit = new bool[0];
        private int _hulls, _cursor;
        private bool _sweeping, _queued, _held;
        private int _hover = int.MinValue, _selected = int.MinValue;

        /// <summary>Buildings now taken as lit.</summary>
        public int Lit { get; private set; }

        /// <summary>The buildings the book is for.</summary>
        public int Hulls => _hulls;

        /// <summary>Passes started since the city was set (for the log and the checks).</summary>
        public int Passes { get; private set; }

        /// <summary>True while a pass is under way.</summary>
        public bool Sweeping => _sweeping;

        /// <summary>A city of <paramref name="hulls"/> buildings, none lit, no pass.</summary>
        public void Reset(int hulls)
        {
            if (_lit.Length != hulls)
            {
                _lit = new bool[hulls];
            }
            else
            {
                Array.Clear(_lit, 0, hulls);
            }
            _hulls = hulls;
            _cursor = 0;
            _sweeping = _queued = _held = false;
            _hover = _selected = int.MinValue;
            Lit = 0;
            Passes = 0;
        }

        /// <summary>Something changed that moves a highlight: one pass over every building (after the one under way, if there is one).</summary>
        public void Poke()
        {
            if (_hulls == 0)
            {
                return;
            }
            if (_sweeping)
            {
                _queued = true;
                return;
            }
            _sweeping = true;
            _cursor = 0;
            Passes++;
        }

        /// <summary>
        /// Once a frame, while the map is open: the game's filters have work or not, which building the pointer is on and which one's card is open
        /// (any number that is the same while they are the same: an object's instance id). True, with the buildings from <paramref name="from"/> up to
        /// but not including <paramref name="to"/> to be read now, while a pass is under way; false, doing nothing, when nothing changed.
        /// </summary>
        public bool Step(bool filtersWorking, int hover, int selected, out int from, out int to)
        {
            from = to = 0;
            if (_hulls == 0)
            {
                return false;
            }
            if (filtersWorking)
            {
                _held = true;
                Poke();
            }
            else if (_held)
            {
                _held = false;
                Poke();   // the filters' work is done: once more, for the last of it
            }
            if (_hover == int.MinValue)
            {
                _hover = hover;   // the first look at the pointer: the pass that opened the map reads whatever it is on
                _selected = selected;
            }
            else if (hover != _hover || selected != _selected)
            {
                _hover = hover;
                _selected = selected;
                Poke();
            }
            if (!_sweeping)
            {
                return false;
            }
            from = _cursor;
            to = Math.Min(_hulls, from + Slice);
            if (to >= _hulls)
            {
                _cursor = 0;
                _sweeping = _queued || _held;
                if (_sweeping)
                {
                    Passes++;
                }
                _queued = false;
            }
            else
            {
                _cursor = to;
            }
            return true;
        }

        public bool IsLit(int hull)
        {
            return hull >= 0 && hull < _hulls && _lit[hull];
        }

        /// <summary>Takes a building as lit or not; true when that was a change.</summary>
        public bool Set(int hull, bool on)
        {
            if (hull < 0 || hull >= _hulls || _lit[hull] == on)
            {
                return false;
            }
            _lit[hull] = on;
            Lit += on ? 1 : -1;
            return true;
        }

        /// <summary>The next lit building from <paramref name="cursor"/> on (taking it as not lit), for taking every tint away as the map shuts; -1 when there is none left.</summary>
        public int TakeNext(ref int cursor)
        {
            while (cursor < _hulls)
            {
                int hull = cursor++;
                if (_lit[hull])
                {
                    _lit[hull] = false;
                    Lit--;
                    return hull;
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// Whether a picture drew nothing, kept for the session: the first draw of a picture that comes out black says the colour shader does not draw
    /// here (or the camera's matrices put the whole city out of view), and once that is said the map is never again covered by a picture that is
    /// black. A picture's own option or a new city does not clear it: it is the machine's answer, and the picture that asked is not asked again. The first
    /// word of it is told to the log once. Game-free.
    /// </summary>
    public static class NavMapLatch
    {
        public const int Full = 0, Minimap = 1;

        private static readonly bool[] Black = new bool[2];

        /// <summary>Whether the picture of that kind drew nothing earlier in this session.</summary>
        public static bool Drew(int kind)
        {
            return kind >= 0 && kind < Black.Length && Black[kind];
        }

        /// <summary>The picture of that kind came out black: true the first time (the caller says so to the log), false after.</summary>
        public static bool Mark(int kind)
        {
            if (kind < 0 || kind >= Black.Length || Black[kind])
            {
                return false;
            }
            Black[kind] = true;
            return true;
        }

        /// <summary>For the checks only.</summary>
        public static void Forget()
        {
            Black[0] = Black[1] = false;
        }
    }

    /// <summary>
    /// Where the pictures stand among the children of the game's marker layer: the 2.5D city picture first, under everything; the mod's own route
    /// line next (so it lies over the picture and under every pin of the game's); nothing else of the mod's moves. One place decides it
    /// (<see cref="Indices"/>), called when either object is made or the map opens, so which of them was made last never matters. Game-free.
    /// </summary>
    public static class NavMapLayers
    {
        /// <summary>The sibling index each takes: -1 for one that is not there.</summary>
        public static void Indices(bool picture, bool route, out int pictureIndex, out int routeIndex)
        {
            pictureIndex = picture ? 0 : -1;
            routeIndex = route ? (picture ? 1 : 0) : -1;
        }
    }

    /// <summary>
    /// Whether the picture is drawn again this frame: something it shows changed (the route, a tint, a street's name, the player, a zone), its meshes are still being
    /// sent, or the camera's matrices are not the ones it was last drawn with. A still camera with nothing changed draws nothing, which is the whole cost of an
    /// open, still map. The decision is made after everything that can change what the picture shows has been looked at, the game's zones included: a zone the district view
    /// tints or a neighbourhood line the filters toggle moves no camera. Game-free; EconomyCheck runs it.
    /// </summary>
    public static class NavMapMoves
    {
        /// <summary>The camera's matrices are sixteen floats each, as the engine keeps them.</summary>
        public static bool Needed(bool changed, int meshesPending, float[] view, float[] lastView, float[] projection, float[] lastProjection)
        {
            if (changed || meshesPending > 0)
            {
                return true;
            }
            for (int i = 0; i < 16; i++)
            {
                if (view[i] != lastView[i] || projection[i] != lastProjection[i])
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// The game's look at its buildings, counted. The game keeps no word of when it has highlighted one: its filters queue every building and work them off inside its own
    /// update, a few a frame up to two milliseconds, so a toggle whose whole work fits in the frame it began in is over before the picture looks, and an empty queue says
    /// nothing. Each building the queue works on is counted by a step wrapped round the game's own (<see cref="Wrap"/>, put on the queue by NavMapFollow), and
    /// <see cref="Took"/> says whether any was worked since it was last asked, whatever the queue holds now. A game whose queue could not be wrapped keeps the queue's own word
    /// and <see cref="Following"/> false. Game-free; EconomyCheck runs it.
    /// </summary>
    public static class NavMapWork
    {
        private static int _items;

        /// <summary>True once the game's step is wrapped, so that every building it works on is counted.</summary>
        public static bool Following { get; internal set; }

        /// <summary>Buildings the game's queue has worked on since the step was wrapped.</summary>
        public static int Items => _items;

        /// <summary>The game's step with a count added in front of it: the game's own always runs.</summary>
        public static Action<T> Wrap<T>(Action<T> game)
        {
            return item =>
            {
                _items++;
                game(item);
            };
        }

        /// <summary>True when the queue worked on a building since <paramref name="seen"/> was taken; <paramref name="seen"/> is brought up to now.</summary>
        public static bool Took(ref int seen)
        {
            int now = _items;
            bool took = now != seen;
            seen = now;
            return took;
        }
    }
}
