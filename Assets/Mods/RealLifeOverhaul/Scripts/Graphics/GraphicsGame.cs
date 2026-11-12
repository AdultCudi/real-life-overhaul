using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Buildings.Indoors.InteriorDesign;
using Parking.UndergroundParking;
using UI.InteriorDesigner;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Gfx
{
    /// <summary>
    /// The only file of the picture that touches the engine and the game. It owns one global volume of the render
    /// pipeline, over every volume of the game's, on a layer that only the player's camera is given: the game's own
    /// profiles are never edited, so switching the volume off is the game's picture exactly, and the game's map, its
    /// previews and its screenshots never see it. A look's settings are written into that volume's own settings, and the
    /// sun's and the moon's colours and contact shadows onto the game's two lights, which are given back as found.
    /// Another mod's global volume that sets the same pipeline setting is left to it, setting by setting, said once.
    /// COST: nothing per frame of its own. A write happens when the director asks (a few float writes); the search for
    /// other volumes copies the pipeline's own list, on a door, the map or an option only.
    /// </summary>
    internal sealed class GraphicsGame
    {
        public const int VolumeLayer = 31;     // a layer none of the game's cameras reads volumes on, so only the player's camera, given it here, sees the look
        public const float Priority = 800f;    // over every volume of the game's (its menu's is 100), under the mod's own picture sliders (1000)
        private const int LayerBit = unchecked((int)0x80000000);   // VolumeLayer as a mask
        private const string HostName = "RealLifeOverhaul Graphics";
        private const string OursPrefix = "RealLifeOverhaul";      // the mod's own objects, by their root's name
        private const float Close = 0.0005f;   // a slider is written again only when it has moved by more than this
        private const float RadiusMost = 5f, StrengthMost = 4f;    // the corner shading's own limits

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly IModLogger _logger;
        private readonly int _count = (int)GraphicsKnob.Count;
        private readonly VolumeParameter<float>[] _sliders;
        private readonly VolumeParameter<Color>[] _colours;
        private readonly VolumeParameter<bool>[] _switches;
        private readonly float[] _written;
        private readonly bool[] _on;
        private readonly List<VolumeComponent> _mine = new List<VolumeComponent>(12);
        private readonly List<string> _takenBy = new List<string>(12);
        private readonly HashSet<string> _said = new HashSet<string>(StringComparer.Ordinal);

        private GameObject _host;
        private Volume _volume;
        private VolumeProfile _profile;
        private Tonemapping _tone;
        private Exposure _exposure;
        private WhiteBalance _balance;
        private ChannelMixer _mixer;
        private SplitToning _toning;
        private Bloom _bloom;
        private Fog _fog;
        private IndirectLightingController _indirect;
        private ContactShadows _contact;
        private MicroShadowing _micro;
        private VolumeComponent _occlusion;                    // the game's own corner shading (its HBAO), a setting of ours for it
        private VolumeParameter<float> _radius, _strength;     // ... its two sliders
        private VolumeComponent _gameOcclusion;                // the game's own, in the street's profile
        private VolumeParameter<float> _gameRadius, _gameStrength;
        private bool _seenActive;
        private float _seenRadius, _seenStrength;
        private VolumeParameter<TonemappingMode> _curve;
        private VolumeParameter<int> _level;

        private Camera _camera;
        private HDAdditionalCameraData _cameraData;
        private int _cameraMask;
        private bool _cameraHeld;
        private HDAdditionalCameraData _previewData;           // the staff uniform preview's camera, which reads every layer
        private int _previewMask;
        private bool _previewHeld;

        private Action _changed, _onChange, _onDesignerOpen, _onDesignerShut;
        private Action<bool> _onMap;
        private Action<Address> _onDoor;
        private Action _designerOpenSeen, _designerShutSeen, _parkingInSeen, _parkingOutSeen;
        private Action<bool> _mapSeen;
        private Action<Address> _enterSeen, _exitSeen;

        private Light _sun, _moon;
        private HDAdditionalLightData _sunData, _moonData;
        private Color _sunWas, _moonWas;
        private bool _sunContactOverride, _sunContactValue, _moonContactOverride, _moonContactValue;
        private bool _lightsHeld, _sunContactOn, _moonContactOn;

        /// <summary>How many settings the last write changed, for the check line.</summary>
        public int Wrote { get; private set; }

        public GraphicsGame(Transform root, IModLogger logger)
        {
            _logger = logger;
            _sliders = new VolumeParameter<float>[_count];
            _colours = new VolumeParameter<Color>[_count];
            _switches = new VolumeParameter<bool>[_count];
            _written = new float[_count];
            _on = new bool[_count];
            Build(root);
        }

        // ---------------------------------------------------------------- the game's state, read

        /// <summary>True once a save is loaded and nothing is loading: the clock, the lights and the camera can be asked for.</summary>
        public static bool Ready
        {
            get
            {
                GameManager game = InstanceBehavior<GameManager>.IsInitialized ? InstanceBehavior<GameManager>.Instance : null;
                return game != null && game.timeOfDayController != null && SaveGameManager.Current != null && !LoadScene.isLoading && ModState.Loaded;
            }
        }

        // What the look must leave as the game draws it: the city map (and the mod's build mode, which is the map), the
        // interior designer, whose colours the player is choosing, and the blueprint maker.

        public static bool MapOpen => CityMap.IsOpen;

        public static bool DesignerOpen => InteriorDesignerUI.IsOpen;

        public static bool Blueprints => InteriorDesignerHelper.BlueprintCreatorMode;

        public static int Place => BuildingManager.IsInsideBuilding || UndergroundParkingManager.IsInsideParking ? GraphicsPlace.Indoors : GraphicsPlace.City;

        public static float Hour => SaveGameManager.Current != null ? SaveGameManager.Current.Hour + SaveGameManager.Current.Minute / 60f : 12f;

        /// <summary>How far the game's sky has gone to its rain look, 0 to 1: its own figure, which it fades over the half hour before the drops.</summary>
        public static float Rain => RainHelper.isRaining ? Mathf.Clamp01(RainHelper.environmentTransitionProgress) : 0f;

        public static bool InSession => GoingPublic.InSession;

        public static bool Paused => Time.timeScale <= 0f;

        public static bool Focused => Application.isFocused;

        public static int ScreenWidth => Screen.width;

        public static int ScreenHeight => Screen.height;

        /// <summary>What caps the frame rate, for the measurement's lines: with a cap on, a frame's length says nothing of its cost.</summary>
        public static string FrameCap()
        {
            int sync = QualitySettings.vSyncCount, target = Application.targetFrameRate;
            return sync > 0 ? "v-sync is on" : target > 0 ? "the frame rate is capped at " + target.ToString(Inv) : null;
        }

        /// <summary>A notification of the picture's tests, never kept in the save.</summary>
        public static void Tell(string key, float seconds, bool warning = false)
        {
            Notifications.Show(warning ? NotificationType.Warning : NotificationType.Info, key, null, seconds, null, null, notificationSound: false, trackOnSaveGame: false);
        }

        // ---------------------------------------------------------------- the game's events

        /// <summary>
        /// The director's handlers for what changes the picture at once, each made once behind the guard: the first look
        /// when the game says the save is loaded (also in a save that loads paused), then a door, the car park, the city
        /// map, and the interior designer's two calls.
        /// </summary>
        public void Hook(Action loaded, Action changed, Action designerOpened, Action designerShut)
        {
            _changed = changed;
            _onChange = Guard.Wrap(changed);
            _onDesignerOpen = Guard.Wrap(designerOpened);
            _onDesignerShut = Guard.Wrap(designerShut);
            _onMap = Guard.Wrap<bool>(MapToggled);
            _onDoor = Guard.Wrap<Address>(DoorUsed);
            GlobalEvents.RegisterOnGameLoadedLateCallback(Guard.Wrap(loaded));
        }

        /// <summary>
        /// The game empties its events on every load: each handler is put back when its event has changed since it was
        /// last seen. Seven reference comparisons once all are on.
        /// </summary>
        public void KeepEvents()
        {
            EventHold.Keep(ref GlobalEvents.onCityMapToggle, _onMap, ref _mapSeen);
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onDoor, ref _enterSeen);
            EventHold.Keep(ref GlobalEvents.onExitBuilding, _onDoor, ref _exitSeen);
            EventHold.Keep(ref InteriorDesignerUI.onOpenInteriorDesigner, _onDesignerOpen, ref _designerOpenSeen);
            EventHold.Keep(ref InteriorDesignerUI.onCloseInteriorDesigner, _onDesignerShut, ref _designerShutSeen);
            EventHold.Keep(ref UndergroundParkingManager.onEnter, _onChange, ref _parkingInSeen);
            EventHold.Keep(ref UndergroundParkingManager.onExit, _onChange, ref _parkingOutSeen);
        }

        private void DropEvents()
        {
            GlobalEvents.onCityMapToggle -= _onMap;
            GlobalEvents.onEnterBuilding -= _onDoor;
            GlobalEvents.onExitBuilding -= _onDoor;
            InteriorDesignerUI.onOpenInteriorDesigner -= _onDesignerOpen;
            InteriorDesignerUI.onCloseInteriorDesigner -= _onDesignerShut;
            UndergroundParkingManager.onEnter -= _onChange;
            UndergroundParkingManager.onExit -= _onChange;
        }

        private void MapToggled(bool open) => _changed?.Invoke();

        private void DoorUsed(Address address) => _changed?.Invoke();

        // ---------------------------------------------------------------- the volume

        private void Build(Transform root)
        {
            _host = new GameObject(HostName);
            _host.transform.SetParent(root, false);
            _host.layer = VolumeLayer;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = HostName;
            _tone = _profile.Add<Tonemapping>();
            _exposure = _profile.Add<Exposure>();
            _balance = _profile.Add<WhiteBalance>();
            _mixer = _profile.Add<ChannelMixer>();
            _toning = _profile.Add<SplitToning>();
            _bloom = _profile.Add<Bloom>();
            _fog = _profile.Add<Fog>();
            _indirect = _profile.Add<IndirectLightingController>();
            Mine(_tone, _exposure, _balance, _mixer, _toning, _bloom, _fog, _indirect);

            _curve = _tone.mode;
            Bind(GraphicsKnob.ToeStrength, _tone.toeStrength);
            Bind(GraphicsKnob.ToeLength, _tone.toeLength);
            Bind(GraphicsKnob.ShoulderStrength, _tone.shoulderStrength);
            Bind(GraphicsKnob.ShoulderLength, _tone.shoulderLength);
            Bind(GraphicsKnob.ShoulderAngle, _tone.shoulderAngle);
            Bind(GraphicsKnob.Compensation, _exposure.compensation);
            Bind(GraphicsKnob.Temperature, _balance.temperature);
            Bind(GraphicsKnob.MixRedRed, _mixer.redOutRedIn);
            Bind(GraphicsKnob.MixRedGreen, _mixer.redOutGreenIn);
            Bind(GraphicsKnob.MixRedBlue, _mixer.redOutBlueIn);
            Bind(GraphicsKnob.MixGreenRed, _mixer.greenOutRedIn);
            Bind(GraphicsKnob.MixGreenGreen, _mixer.greenOutGreenIn);
            Bind(GraphicsKnob.MixGreenBlue, _mixer.greenOutBlueIn);
            Bind(GraphicsKnob.MixBlueRed, _mixer.blueOutRedIn);
            Bind(GraphicsKnob.MixBlueGreen, _mixer.blueOutGreenIn);
            Bind(GraphicsKnob.MixBlueBlue, _mixer.blueOutBlueIn);
            BindColour(GraphicsKnob.ShadowsRed, _toning.shadows);
            BindColour(GraphicsKnob.HighlightsRed, _toning.highlights);
            Bind(GraphicsKnob.BloomThreshold, _bloom.threshold);
            Bind(GraphicsKnob.BloomScatter, _bloom.scatter);
            Bind(GraphicsKnob.FogReach, _fog.meanFreePath);
            Bind(GraphicsKnob.FogHeight, _fog.maximumHeight);
            Bind(GraphicsKnob.FogBase, _fog.baseHeight);
            BindColour(GraphicsKnob.FogTintRed, _fog.tint);
            Bind(GraphicsKnob.Ambient, _indirect.indirectDiffuseLightingMultiplier);
            Bind(GraphicsKnob.Reflections, _indirect.reflectionLightingMultiplier);

            _volume = _host.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = Priority;
            _volume.sharedProfile = _profile;
            _volume.profile = _profile;   // its own already: another mod asking the volume for its profile gets this one, not a copy that would leave ours unread
            _volume.enabled = false;
        }

        // The settings of the two costly choices, made the first time one is asked for: the Free picture never carries them.
        private void BuildExtras()
        {
            if (_contact != null)
            {
                return;
            }
            _contact = _profile.Add<ContactShadows>();
            _micro = _profile.Add<MicroShadowing>();
            Mine(_contact, _micro);
            BindSwitch(GraphicsKnob.ContactShadows, _contact.enable);
            Bind(GraphicsKnob.ContactLength, _contact.length);
            Bind(GraphicsKnob.ContactOpacity, _contact.opacity);
            Bind(GraphicsKnob.ContactScale, _contact.distanceScaleFactor);
            Bind(GraphicsKnob.ContactReach, _contact.maxDistance);
            Bind(GraphicsKnob.ContactFade, _contact.fadeDistance);
            _level = _contact.quality;
            BindSwitch(GraphicsKnob.MicroShadows, _micro.enable);
            Bind(GraphicsKnob.MicroOpacity, _micro.opacity);
            BuildOcclusion();
        }

        // The game's corner shading is another maker's plug-in, reached by name: without it the rest of the picture is the same.
        private void BuildOcclusion()
        {
            try
            {
                Type kind = GameContract.GraphicsShade.Off ? null : GameContract.OcclusionKind.Type;
                if (kind == null || GameContract.OcclusionRadius.Field == null || GameContract.OcclusionStrength.Field == null)
                {
                    Say("shade", "Realistic graphics: the game's corner shading is not as this version knows it, so the Balanced and High choices leave it as the game has it.");
                    return;
                }
                _occlusion = _profile.Add(kind, false);
                _radius = GameContract.OcclusionRadius.Field.GetValue(_occlusion) as VolumeParameter<float>;
                _strength = GameContract.OcclusionStrength.Field.GetValue(_occlusion) as VolumeParameter<float>;
                if (_radius == null || _strength == null)
                {
                    _occlusion.active = false;
                    _occlusion = null;
                    return;
                }
                Mine(_occlusion);
            }
            catch (Exception e)
            {
                _occlusion = null;
                _logger?.Error(e);
            }
        }

        private void Mine(params VolumeComponent[] settings)
        {
            foreach (VolumeComponent setting in settings)
            {
                _mine.Add(setting);
                _takenBy.Add(null);
            }
        }

        private void Bind(GraphicsKnob knob, VolumeParameter<float> slider)
        {
            _sliders[(int)knob] = slider;
        }

        private void BindColour(GraphicsKnob red, VolumeParameter<Color> colour)
        {
            _colours[(int)red] = colour;
        }

        private void BindSwitch(GraphicsKnob knob, VolumeParameter<bool> toggle)
        {
            _switches[(int)knob] = toggle;
        }

        // ---------------------------------------------------------------- the camera and the lights

        /// <summary>
        /// Finds the player's camera and the game's sun and moon, by the game's own fields, and gives the camera the look's
        /// layer; the staff uniform preview's camera, which reads every layer, is kept off it. True once all is found.
        /// </summary>
        public bool Bind()
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            if (camera != _camera || _cameraData == null)
            {
                GiveBackCamera();
                _camera = camera;
                _cameraData = camera.GetComponent<HDAdditionalCameraData>();
            }
            if (_cameraData == null)
            {
                return false;
            }
            if (!_cameraHeld)
            {
                _cameraMask = _cameraData.volumeLayerMask;
                _cameraData.volumeLayerMask = _cameraMask | LayerBit;
                _cameraHeld = true;
            }
            if (!_previewHeld)
            {
                HoldPreview();
            }
            if (_sun == null || _moon == null)
            {
                FindLights();
            }
            return _sun != null && _moon != null;
        }

        /// <summary>True when the game has made another camera its main one since the last look: one reference comparison.</summary>
        public bool CameraMoved => _camera == null || GameManager.GetMainCamera() != _camera;

        /// <summary>True when the game's own corner shading has been switched or resized since ours was last written.</summary>
        public bool ShadeMoved => _occlusion != null && _gameOcclusion != null && (_gameOcclusion.active != _seenActive || _gameRadius.value != _seenRadius || _gameStrength.value != _seenStrength);

        private void HoldPreview()
        {
            GameManager game = InstanceBehavior<GameManager>.Instance;
            EmployeeUniformPreview preview = game != null ? game.employeeUniformPreview : null;
            HDAdditionalCameraData data = preview != null ? preview.GetComponentInChildren<HDAdditionalCameraData>(true) : null;
            if (data == null)
            {
                return;
            }
            int mask = data.volumeLayerMask;
            if ((mask & LayerBit) != 0)
            {
                _previewData = data;
                _previewMask = mask;
                data.volumeLayerMask = mask & ~LayerBit;
                _previewHeld = true;
            }
        }

        private void FindLights()
        {
            TimeOfDayController clock = Clock();
            if (clock == null || clock.sunLight == null || clock.moonLight == null)
            {
                return;
            }
            _sun = clock.sunLight;
            _moon = clock.moonLight;
            _sunData = _sun.GetComponent<HDAdditionalLightData>();
            _moonData = _moon.GetComponent<HDAdditionalLightData>();
        }

        private static TimeOfDayController Clock()
        {
            GameManager game = InstanceBehavior<GameManager>.IsInitialized ? InstanceBehavior<GameManager>.Instance : null;
            return game != null ? game.timeOfDayController : null;
        }

        private void HoldLights()
        {
            if (_lightsHeld || _sun == null || _moon == null)
            {
                return;
            }
            _sunWas = _sun.color;
            _moonWas = _moon.color;
            if (_sunData != null)
            {
                _sunContactOverride = _sunData.useContactShadow.useOverride;
                _sunContactValue = _sunData.useContactShadow.@override;
            }
            if (_moonData != null)
            {
                _moonContactOverride = _moonData.useContactShadow.useOverride;
                _moonContactValue = _moonData.useContactShadow.@override;
            }
            _lightsHeld = true;
        }

        private void GiveBackLights()
        {
            if (!_lightsHeld)
            {
                return;
            }
            _lightsHeld = false;
            if (_sun != null)
            {
                _sun.color = _sunWas;
            }
            if (_moon != null)
            {
                _moon.color = _moonWas;
            }
            if (_sunData != null)
            {
                _sunData.useContactShadow.useOverride = _sunContactOverride;
                _sunData.useContactShadow.@override = _sunContactValue;
            }
            if (_moonData != null)
            {
                _moonData.useContactShadow.useOverride = _moonContactOverride;
                _moonData.useContactShadow.@override = _moonContactValue;
            }
            _sunContactOn = _moonContactOn = false;
            int sun = (int)GraphicsKnob.SunRed, moon = (int)GraphicsKnob.MoonRed;
            _on[sun] = _on[sun + 1] = _on[sun + 2] = _on[moon] = _on[moon + 1] = _on[moon + 2] = false;
        }

        private void GiveBackCamera()
        {
            if (_cameraHeld && _cameraData != null)
            {
                _cameraData.volumeLayerMask = _cameraMask;
            }
            _cameraHeld = false;
            if (_previewHeld && _previewData != null)
            {
                _previewData.volumeLayerMask = _previewMask;
            }
            _previewHeld = false;
            _previewData = null;
        }

        // ---------------------------------------------------------------- other mods' volumes

        /// <summary>
        /// Looks through the pipeline's own list of volumes for another mod's global volume that sets a pipeline setting
        /// the look sets too: that setting is then left to it (ours switched off), each said once. A volume of the game's
        /// came with its scene; the mod's own carry its name. It also finds the game's own corner shading in a street's
        /// profile, to scale it. COST: the list is copied by the pipeline, so on a door, the map or an option only.
        /// </summary>
        public void LookForOthers()
        {
            for (int i = 0; i < _takenBy.Count; i++)
            {
                _takenBy[i] = null;
            }
            Type shade = _occlusion != null ? _occlusion.GetType() : null;
            VolumeComponent street = null;
            Volume[] volumes = VolumeManager.instance.GetVolumes(~0);
            for (int v = 0; v < volumes.Length; v++)
            {
                Volume volume = volumes[v];
                if (volume == null || volume == _volume)
                {
                    continue;
                }
                VolumeProfile profile = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
                if (profile == null)
                {
                    continue;
                }
                bool games = volume.GetInstanceID() > 0;   // loaded with a scene of the game's; one made by code has a number under 0
                if (games)
                {
                    if (shade != null && street == null && !volume.isGlobal && profile.TryGet(shade, out VolumeComponent found))
                    {
                        street = found;
                    }
                    continue;
                }
                if (!volume.isGlobal || volume.weight <= 0f || volume.transform.root.name.StartsWith(OursPrefix, StringComparison.Ordinal))
                {
                    continue;
                }
                Taken(profile, volume.name);
            }
            for (int i = 0; i < _mine.Count; i++)
            {
                _mine[i].active = _takenBy[i] == null;
                if (_takenBy[i] != null)
                {
                    Say(_mine[i].GetType().Name + "|" + _takenBy[i], "Realistic graphics: another mod's volume '" + _takenBy[i] + "' sets " + _mine[i].GetType().Name
                        + ", so the look leaves that setting to it.");
                }
            }
            if (street != _gameOcclusion)
            {
                _gameOcclusion = street;
                _gameRadius = street != null ? GameContract.OcclusionRadius.Field?.GetValue(street) as VolumeParameter<float> : null;
                _gameStrength = street != null ? GameContract.OcclusionStrength.Field?.GetValue(street) as VolumeParameter<float> : null;
                if (_gameRadius == null || _gameStrength == null)
                {
                    _gameOcclusion = null;
                }
            }
        }

        private void Taken(VolumeProfile profile, string by)
        {
            List<VolumeComponent> theirs = profile.components;
            for (int c = 0; c < theirs.Count; c++)
            {
                VolumeComponent setting = theirs[c];
                if (setting == null || !setting.active || !Overrides(setting))
                {
                    continue;
                }
                for (int i = 0; i < _mine.Count; i++)
                {
                    if (_takenBy[i] == null && _mine[i].GetType() == setting.GetType())
                    {
                        _takenBy[i] = by;
                    }
                }
            }
        }

        private static bool Overrides(VolumeComponent setting)
        {
            for (int p = 0; p < setting.parameters.Count; p++)
            {
                if (setting.parameters[p].overrideState)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The settings left to another mod's volume now, by name, or null.</summary>
        public string LeftToOthers()
        {
            StringBuilder text = null;
            for (int i = 0; i < _mine.Count; i++)
            {
                if (_takenBy[i] != null)
                {
                    (text ??= new StringBuilder(64)).Append(text.Length > 0 ? ", " : string.Empty).Append(_mine[i].GetType().Name);
                }
            }
            return text?.ToString();
        }

        // ---------------------------------------------------------------- writing a look

        /// <summary>
        /// Writes a look: each knob's value where it has moved, and whether it is set at all. The volume is switched on by
        /// it. Nothing is allocated; a look a game minute on from the last moves twenty sliders at most.
        /// </summary>
        public void Apply(float[] values, bool[] set)
        {
            Wrote = 0;
            if (set[(int)GraphicsKnob.ContactShadows] || set[(int)GraphicsKnob.MicroShadows])
            {
                BuildExtras();
            }
            for (int k = 0; k < _count; k++)
            {
                if (_sliders[k] != null)
                {
                    Slide(k, values[k], set[k]);
                }
                else if (_colours[k] != null)
                {
                    Colour(k, values, set[k]);
                }
                else if (_switches[k] != null)
                {
                    Toggle(k, set[k] && values[k] > 0.5f);
                }
            }
            Curve(set[(int)GraphicsKnob.ToneCurve]);
            Level(values[(int)GraphicsKnob.ContactQuality], set[(int)GraphicsKnob.ContactQuality]);
            Shade(values, set);
            Lights(values, set);
            if (!_volume.enabled)
            {
                _volume.enabled = true;
            }
        }

        private void Slide(int k, float value, bool on)
        {
            VolumeParameter<float> slider = _sliders[k];
            if (on && (!_on[k] || Mathf.Abs(_written[k] - value) > Close))
            {
                slider.value = value;
                _written[k] = value;
                Wrote++;
            }
            if (on != _on[k])
            {
                slider.overrideState = on;
                _on[k] = on;
            }
        }

        private void Colour(int red, float[] values, bool on)
        {
            VolumeParameter<Color> colour = _colours[red];
            bool moved = Mathf.Abs(_written[red] - values[red]) > Close || Mathf.Abs(_written[red + 1] - values[red + 1]) > Close || Mathf.Abs(_written[red + 2] - values[red + 2]) > Close;
            if (on && (!_on[red] || moved))
            {
                colour.value = new Color(values[red], values[red + 1], values[red + 2], 1f);
                _written[red] = values[red];
                _written[red + 1] = values[red + 1];
                _written[red + 2] = values[red + 2];
                Wrote++;
            }
            if (on != _on[red])
            {
                colour.overrideState = on;
                _on[red] = on;
            }
        }

        private void Toggle(int k, bool on)
        {
            if (on != _on[k])
            {
                _switches[k].value = on;
                _switches[k].overrideState = on;
                _on[k] = on;
                Wrote++;
            }
        }

        // The look's own curve in place of the game's: the same work for the graphics card, another shape.
        private void Curve(bool on)
        {
            int k = (int)GraphicsKnob.ToneCurve;
            if (on != _on[k])
            {
                _curve.value = on ? TonemappingMode.Custom : TonemappingMode.None;
                _curve.overrideState = on;
                _on[k] = on;
                Wrote++;
            }
        }

        private void Level(float level, bool on)
        {
            int k = (int)GraphicsKnob.ContactQuality;
            if (_level == null)
            {
                return;
            }
            if (on && (!_on[k] || Mathf.Abs(_written[k] - level) > Close))
            {
                _level.value = Mathf.RoundToInt(level);
                _written[k] = level;
                Wrote++;
            }
            if (on != _on[k])
            {
                _level.overrideState = on;
                _on[k] = on;
            }
        }

        // The game's own corner shading made wider and a little stronger, only while the player has it switched on in the
        // game's options: its strength is what starts the pass, so it is never written while the game's is off.
        private void Shade(float[] values, bool[] set)
        {
            if (_occlusion == null)
            {
                return;
            }
            int radius = (int)GraphicsKnob.OcclusionRadius, strength = (int)GraphicsKnob.OcclusionStrength;
            bool games = _gameOcclusion != null && _gameOcclusion.active && _gameStrength.overrideState && _gameStrength.value > 0f;
            bool on = set[radius] && set[strength] && games;
            if (_gameOcclusion != null)
            {
                _seenActive = _gameOcclusion.active;
                _seenRadius = _gameRadius.value;
                _seenStrength = _gameStrength.value;
            }
            if (on)
            {
                float wide = Mathf.Min(RadiusMost, _seenRadius * values[radius]), strong = Mathf.Min(StrengthMost, _seenStrength * values[strength]);
                if (!_on[radius] || Mathf.Abs(_written[radius] - wide) > Close || Mathf.Abs(_written[strength] - strong) > Close)
                {
                    _radius.value = wide;
                    _strength.value = strong;
                    _written[radius] = wide;
                    _written[strength] = strong;
                    Wrote++;
                }
            }
            if (on != _on[radius])
            {
                _radius.overrideState = on;
                _strength.overrideState = on;
                _on[radius] = _on[strength] = on;
            }
        }

        // The sun's and the moon's colour as a filter over the game's own, and their contact shadows. The game sets their
        // strength and the sun's colour temperature every frame and never their colour, so the filter stays as written.
        private void Lights(float[] values, bool[] set)
        {
            if (_sun == null || _moon == null)
            {
                return;
            }
            HoldLights();
            Filter(_sun, _sunWas, (int)GraphicsKnob.SunRed, values, set);
            Filter(_moon, _moonWas, (int)GraphicsKnob.MoonRed, values, set);
            Contact(_sunData, ref _sunContactOn, set[(int)GraphicsKnob.SunContact], _sunContactOverride, _sunContactValue);
            Contact(_moonData, ref _moonContactOn, set[(int)GraphicsKnob.MoonContact], _moonContactOverride, _moonContactValue);
        }

        private void Filter(Light light, Color was, int red, float[] values, bool[] set)
        {
            bool on = set[red];
            bool moved = Mathf.Abs(_written[red] - values[red]) > Close || Mathf.Abs(_written[red + 1] - values[red + 1]) > Close || Mathf.Abs(_written[red + 2] - values[red + 2]) > Close;
            if (on && (!_on[red] || moved))
            {
                // A light's colour is kept in the screen's encoding: the filter is worked in linear light and encoded back.
                Color linear = was.linear;
                light.color = new Color(linear.r * values[red], linear.g * values[red + 1], linear.b * values[red + 2], was.a).gamma;
                _written[red] = values[red];
                _written[red + 1] = values[red + 1];
                _written[red + 2] = values[red + 2];
                Wrote++;
            }
            else if (!on && _on[red])
            {
                light.color = was;
            }
            _on[red] = on;
        }

        private void Contact(HDAdditionalLightData data, ref bool held, bool on, bool wasOverride, bool wasValue)
        {
            if (data == null || on == held)
            {
                return;
            }
            data.useContactShadow.useOverride = on || wasOverride;
            data.useContactShadow.@override = on || wasValue;
            held = on;
            Wrote++;
        }

        /// <summary>
        /// The game's own picture back: the volume off and the two lights as found. With <paramref name="all"/> the
        /// cameras' layers too (the option switched off, or the city unloading). Safe at any time.
        /// </summary>
        public void PutBack(bool all)
        {
            if (_volume != null && _volume.enabled)
            {
                _volume.enabled = false;
            }
            GiveBackLights();
            if (all)
            {
                GiveBackCamera();
            }
        }

        /// <summary>True while the volume is on.</summary>
        public bool Shown => _volume != null && _volume.enabled;

        /// <summary>Everything given back and the volume, its profile and its settings destroyed. Safe at any time.</summary>
        public void Dispose()
        {
            DropEvents();
            PutBack(true);
            CodeAssets.DropProfile(_profile);   // the settings added to it with it
            _profile = null;
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
            }
            _host = null;
            _volume = null;
        }

        // ---------------------------------------------------------------- the hours' test

        /// <summary>
        /// The game's own clock tool switched on, as its interior designer's slider does: the game then lights the city for
        /// the hour it is told and leaves the save's clock alone. False if the game's clock is not there.
        /// </summary>
        public static bool HoursBegin()
        {
            TimeOfDayController clock = Clock();
            if (clock == null)
            {
                return false;
            }
            clock.usingSliderDebugTool = true;
            return true;
        }

        public static void HoursShow(float hour)
        {
            TimeOfDayController clock = Clock();
            if (clock == null)
            {
                return;
            }
            InGameTimeOfDayPanel.selectedTimeInHours = hour;
            clock.SetEnvironmentSettings(hour);
            clock.UpdateHourlyValues(hour);
        }

        /// <summary>The game's light back on the save's own hour. Safe at any time.</summary>
        public static void HoursEnd()
        {
            TimeOfDayController clock = Clock();
            if (clock == null)
            {
                return;
            }
            clock.usingSliderDebugTool = false;
            if (SaveGameManager.Current != null)
            {
                clock.UpdateHourlyValues(SaveGameManager.Current.Hour);
            }
        }

        // ---------------------------------------------------------------- words for the log

        /// <summary>What is on the picture now, for a check line: the camera, the main sliders and the lights.</summary>
        public string Describe()
        {
            var text = new StringBuilder(256);
            text.Append(_cameraHeld ? "the player's camera reads the look's layer" : "no camera has the look's layer");
            text.Append(_previewHeld ? ", the staff preview's camera is kept off it" : string.Empty);
            text.Append("; exposure ").Append(Said(GraphicsKnob.Compensation, "+0.00;-0.00")).Append(" stops, white balance ").Append(Said(GraphicsKnob.Temperature, "+0.0;-0.0"));
            text.Append(", colour ").Append(Said(GraphicsKnob.MixRedRed, "0")).Append("% red in red, glow from ").Append(Said(GraphicsKnob.BloomThreshold, "0.00"));
            text.Append(", haze ").Append(Said(GraphicsKnob.FogReach, "0")).Append(" m reaching ").Append(Said(GraphicsKnob.FogHeight, "0")).Append(" m up");
            text.Append(", sky light ").Append(Said(GraphicsKnob.Ambient, "0.00"));
            int sun = (int)GraphicsKnob.SunRed;
            text.Append("; the sun's filter ");
            text.Append(_on[sun] ? _written[sun].ToString("0.00", Inv) + " " + _written[sun + 1].ToString("0.00", Inv) + " " + _written[sun + 2].ToString("0.00", Inv) : "none");
            text.Append(_sunContactOn ? ", contact shadows on the sun" : string.Empty).Append(_moonContactOn ? " and the moon" : string.Empty);
            text.Append(_on[(int)GraphicsKnob.OcclusionRadius] ? ", corner shading " + _written[(int)GraphicsKnob.OcclusionRadius].ToString("0.00", Inv) + " m wide" : string.Empty);
            string others = LeftToOthers();
            text.Append(others != null ? "; left to another mod's volume: " + others : string.Empty);
            return text.ToString();
        }

        private string Said(GraphicsKnob knob, string format)
        {
            int k = (int)knob;
            return _on[k] ? _written[k].ToString(format, Inv) : "the game's";
        }

        private void Say(string key, string line)
        {
            if (_said.Add(key))
            {
                _logger?.Warn(line);
            }
        }
    }
}
