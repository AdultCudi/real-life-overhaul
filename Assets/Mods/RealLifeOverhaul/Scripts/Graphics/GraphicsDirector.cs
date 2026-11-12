using System;
using System.Collections;
using System.Globalization;
using System.Text;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Gfx
{
    /// <summary>
    /// Realistic graphics (docs/features/graphics.md): a look for the whole picture, chosen on the options page, that
    /// follows the hour, the weather and the season. It owns the state and the timing: what the picture was last worked
    /// out for, and when to work it out again. The rules (<see cref="GraphicsRules"/>) say what every setting is for a
    /// moment; <see cref="GraphicsGame"/> writes them into a volume of the mod's own and onto the game's sun and moon.
    /// The picture is written when something changes and never otherwise: the clock's minute or the weather's twentieth
    /// moving (looked at once a second of game time), a door, the city map, the interior designer, the option.
    /// On the city map, in the mod's build mode (which is the map), in the interior designer and in the blueprint maker
    /// the volume is off and the lights are the game's. No Update. COST: nothing per frame of the mod's own; once a
    /// second of game time a few comparisons, a look worked out and the sliders that moved written, twenty at most.
    /// </summary>
    public sealed partial class GraphicsDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static GraphicsDirector Instance { get; private set; }

        private const float Every = 1f;   // seconds of game time between looks at the hour and the weather: in a pause the clock stands still, and so does the picture

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly float[] _values = new float[(int)GraphicsKnob.Count];
        private readonly bool[] _set = new bool[(int)GraphicsKnob.Count];
        private GraphicsGame _game;
        private GraphicsMoment _moment;      // what the picture on the screen was worked out for
        private bool _shown;                 // a look is on the picture
        private bool _bound;                 // the camera and the lights were found in this city
        private bool _broken;
        private bool _designer;              // the interior designer is open, by its own two calls
        private int _forcedLook = -1, _forcedCost = -1;   // the measurement's own look and cost while it runs
        private float _shownHour = -1f;      // the hours' test: the hour it shows, in place of the clock's
        private int _saidLook = -1, _saidCost = -1, _saidPlace = -1;

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            root.AddComponent<GraphicsDirector>();
        }

        /// <summary>The look in force: the option's, or the measurement's own while it runs.</summary>
        public int Look => _forcedLook >= 0 ? _forcedLook : GraphicsRules.LookOf(Settings.GraphicsLook);

        /// <summary>The cost setting in force.</summary>
        public int Cost => _forcedCost >= 0 ? _forcedCost : GraphicsRules.CostOf(Settings.GraphicsCost);

        /// <summary>True while a look is on the picture (false on the map, in the designer and with the option off).</summary>
        public bool Shown => _shown;

        private void Awake()
        {
            Instance = this;
            _game = new GraphicsGame(transform, Logger);
            _game.Hook(Loaded, Changed, DesignerOpened, DesignerShut);
            MapParts.Add("the picture's look", MapLook);   // asked after the map shut, with Developer diagnostics on (Diagnostics/SessionWatch.Map)
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            try
            {
                StopTests();
                _game?.Dispose();   // the lights and the cameras as the game had them, the events let go, the volume gone
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _game = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSeconds(Every);
            while (true)
            {
                yield return wait;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    EachSecond();
                }
                catch (Exception e)
                {
                    Fault(e);
                }
            }
        }

        private void Fault(Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            Logger?.Warn("Realistic graphics: switched off for this session after the error above; the picture is the game's own again.");
            try
            {
                _forcedLook = _forcedCost = -1;
                _shownHour = -1f;
                _game?.PutBack(true);
                _shown = false;
            }
            catch (Exception again)
            {
                Logger?.Error(again);
            }
        }

        private void EachSecond()
        {
            if (!GraphicsGame.Ready)
            {
                return;
            }
            _game.KeepEvents();
            _designer = GraphicsGame.DesignerOpen;   // the two calls set it at once; this puts right one that was missed
            Refresh(false);
        }

        // The game's late call once a save is loaded: the first look, behind the loading screen, also in a save that loads
        // paused (the timer above waits on the game's clock).
        private void Loaded()
        {
            if (!_broken && GraphicsGame.Ready)
            {
                _game.KeepEvents();
                Refresh(true);
            }
        }

        private void DesignerOpened()
        {
            _designer = true;
            Changed();
        }

        private void DesignerShut()
        {
            _designer = false;   // the game clears its own flag after this call: the look comes back with the room, not a second later
            Changed();
        }

        // Something that changes the picture at once: a door, the car park, the map, the designer. Other mods' volumes are
        // looked for again here.
        private void Changed()
        {
            if (!_broken && _game != null && GraphicsGame.Ready)
            {
                Refresh(true);
            }
        }

        /// <summary>The option was moved: the picture follows at once, also in the pause the options page is opened in. Never throws.</summary>
        public static void OptionChanged()
        {
            GraphicsDirector director = Instance;
            if (director == null || director._broken || director._game == null)
            {
                return;
            }
            try
            {
                if (GraphicsGame.Ready)
                {
                    director.Refresh(true);
                }
            }
            catch (Exception e)
            {
                director.Fault(e);
            }
        }

        /// <summary>
        /// The options page's clean-up: the tests stopped and the game's own picture back. The look returns with the next
        /// second, as the option says. Safe when nothing is on.
        /// </summary>
        public void PutAway()
        {
            StopTests();
            _game?.PutBack(true);
            _shown = false;
            _bound = false;
        }

        // The picture brought in line with the moment. With look: other mods' volumes are looked for again (a door, the map, an option).
        private void Refresh(bool look)
        {
            int wanted = Look;
            bool off = wanted == GraphicsLook.Off;
            if (off || GraphicsGame.MapOpen || _designer || GraphicsGame.Blueprints)
            {
                if (_shown || (off && _bound))
                {
                    _game.PutBack(off);   // off by the option: the cameras' layers back too
                    _bound = _bound && !off;
                    if (_shown)
                    {
                        Said(off ? "switched off" : GraphicsGame.MapOpen ? "the city map is open" : _designer ? "the interior designer is open" : "the blueprint maker is open");
                    }
                    _shown = false;
                }
                return;
            }
            if (!_bound || _game.CameraMoved)
            {
                _bound = _game.Bind();
                if (!_bound)
                {
                    return;   // no camera or no sun yet: asked again in a second
                }
                look = true;
            }
            GraphicsMoment now = Now(wanted);
            if (_shown && !look && now.Same(_moment) && !_game.ShadeMoved)
            {
                return;
            }
            if (look)
            {
                _game.LookForOthers();
            }
            _moment = now;
            GraphicsRules.Evaluate(in now, _values, _set);
            _game.Apply(_values, _set);
            bool first = !_shown;
            _shown = true;
            if (first || now.Look != _saidLook || now.Cost != _saidCost || now.Place != _saidPlace)
            {
                Check(now);
            }
        }

        private GraphicsMoment Now(int look)
        {
            Func<int> season = GraphicsBridges.Season;
            Func<float> falling = GraphicsBridges.SnowFalling, cover = GraphicsBridges.SnowCover;
            return GraphicsRules.Moment(look, Cost, GraphicsGame.Place, _shownHour >= 0f ? _shownHour : GraphicsGame.Hour, GraphicsGame.Rain, falling != null ? falling() : 0f,
                cover != null ? cover() : 0f, season != null ? season() : -1);
        }

        // One line when the look, the cost or the place changes, with Developer diagnostics on: what was written against what the rules set.
        private void Check(in GraphicsMoment now)
        {
            _saidLook = now.Look;
            _saidCost = now.Cost;
            _saidPlace = now.Place;
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            int set = 0, costly = 0;
            for (int k = 0; k < _set.Length; k++)
            {
                set += _set[k] ? 1 : 0;
                costly += _set[k] && GraphicsRules.Knobs[k].Cost > now.Cost ? 1 : 0;
            }
            Logger?.Info("Graphics check (" + GraphicsRules.LookName(now.Look) + ", " + GraphicsRules.CostName(now.Cost) + ", " + (now.Place == GraphicsPlace.Indoors ? "indoors" : "the city")
                         + ", " + Clock(now.Hour) + "): " + set + " settings set, " + _game.Wrote + " written, " + costly + " beyond the chosen cost (wanted: none); " + _game.Describe() + ": "
                         + (costly == 0 && _game.Shown ? "PASS" : "FAIL") + ".");
        }

        private void Said(string why)
        {
            _saidLook = _saidCost = _saidPlace = -1;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Graphics: the game's own picture (" + why + ").");
            }
        }

        // The map check after the map shut (Diagnostics/SessionWatch.Map): the look taken off for the map is on the picture
        // again. A few comparisons and nothing searched; its words only when it is given a line.
        private int MapLook(StringBuilder line)
        {
            if (_game == null || _broken || Look == GraphicsLook.Off)
            {
                line?.Append("the picture's look: none is chosen");
                return 0;
            }
            if (GraphicsGame.MapOpen || _designer || GraphicsGame.Blueprints)
            {
                line?.Append("the picture's look: held off where the game's own picture is wanted");
                return 0;
            }
            bool back = _shown && _game.Shown;
            line?.Append(back ? "the picture's look: on again" : "the picture's look: not on again");
            return back ? 0 : 1;
        }

        private static string Clock(float hour)
        {
            int minutes = Mathf.RoundToInt(hour * 60f) % (24 * 60);
            return (minutes / 60).ToString("00", Inv) + ":" + (minutes % 60).ToString("00", Inv);
        }
    }
}
