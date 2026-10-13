using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BAModAPI;
using RealLifeOverhaul;
using UnityEngine;

[assembly: RegisterModClass(typeof(CharacterQualityCity))]
[assembly: RegisterModClass(typeof(CharacterQualityIntro))]

namespace RealLifeOverhaul
{
    /// <summary>City: the high-detail player, plus hair, textures and lighting for the shop preview character.</summary>
    [ModEntryOnCityLoad]
    public class CharacterQualityCity : IModBigAmbitions
    {
        private const string RootName = "RealLifeOverhaul Character";
        private static string _running;   // the id of the copy that made the root
        private GameObject _root;

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            if (_running != context.ModId && GameObject.Find(RootName) != null)
            {
                return Task.CompletedTask;   // a second copy of the mod is switched on: the first one's character work stands (Core/RealLifeOverhaulMod says so in the log)
            }
            CharacterQuality.Logger = context.Logger;
            HairLook.Logger = context.Logger;
            PlayerDouble.Logger = context.Logger;
            ModAssets.Logger = context.Logger;
            ModLooks.Logger = context.Logger;
            CharacterCatalogue.Logger = context.Logger;
            ModAssets.Begin(context);

            _running = context.ModId;
            _root = new GameObject(RootName);
            _root.AddComponent<CharacterQuality>();   // first: the double hands its character to it
            _root.AddComponent<PlayerDouble>();
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            if (_root != null)
            {
                _root.name = RootName + " (closing)";   // gone at the end of the frame: a city loaded before then must not take it for a second copy
                UnityEngine.Object.Destroy(_root);
                _root = null;
                _running = null;
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>Character creation: hair, textures and lighting for the character being made.</summary>
    [ModEntryOnIntroLoad]
    public class CharacterQualityIntro : IModBigAmbitions
    {
        private GameObject _root;

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            CharacterQuality.Logger = context.Logger;
            HairLook.Logger = context.Logger;
            ModAssets.Logger = context.Logger;
            ModLooks.Logger = context.Logger;
            CharacterCatalogue.Logger = context.Logger;
            ModAssets.Begin(context);
            CharacterCatalogue.BeginCreator();   // a new character: no look of any other game

            _root = new GameObject("RealLifeOverhaul Character (creation)");
            // The entry can run before the creation scene is the active one; this keeps the object out of a scene that is about to go.
            UnityEngine.Object.DontDestroyOnLoad(_root);
            _root.AddComponent<CharacterQuality>().CreationScene = true;
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            CharacterCatalogue.LeaveCreator();   // the picks are in the new game's data; nobody else gets them
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Looks after the detailed characters on show: fuller hair (<see cref="HairLook"/>), the mod's
    /// own hair, beards and clothes (<see cref="ModLooks"/>), sharper textures and, for the screens
    /// that present one character, better lighting (<see cref="CharacterLighting"/>).
    ///
    /// All of the work is done when a character's look changes (the game announces that, and the
    /// handler runs inside that announcement) or when an option is switched. The Update here only
    /// compares a few options with what was last applied and, for the first seconds of a scene,
    /// looks for the characters on a half-second timer.
    /// </summary>
    public class CharacterQuality : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("character", "CharacterQuality", typeof(CharacterQuality));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float SearchSeconds = 0.5f;
        private const int CreationSearchAttempts = 60;   // each one walks the (small) creation scene
        private const int CitySearchAttempts = 600;      // each one is a single lookup; the city can take minutes to load

        private sealed class Managed
        {
            public AppearanceSetter Character;
            public Action Handler;
            public bool SharedPreview;   // the shop preview also shows employees; the player's looks go on it only while it shows the player
        }

        public static IModLogger Logger;
        public static CharacterQuality Instance { get; private set; }

        /// <summary>Set by the character-creation entry; otherwise this is the city.</summary>
        public bool CreationScene;

        private readonly List<Managed> _characters = new List<Managed>(3);
        private CharacterLighting _lighting;
        private GridLooks _creatorLooks;
        private bool _hairOn;
        private bool _hairSuspended;
        private bool _hairFailed;
        private bool _skinOn;
        private bool _skinFailed;
        private bool _stopped;
        private bool _looksOn;
        private bool _looksReady;
        private int _looksVersion = -1;
        private int _searchesLeft;
        private float _nextSearch;

        public int ManagedCharacters => _characters.Count;
        internal CharacterLighting Lighting => _lighting;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _searchesLeft = CreationScene ? CreationSearchAttempts : CitySearchAttempts;
            SkinLook.Logger = Logger;
        }

        private void OnDestroy()
        {
            Stop();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
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
            try
            {
                if (_searchesLeft > 0 && Time.unscaledTime >= _nextSearch)
                {
                    _nextSearch = Time.unscaledTime + SearchSeconds;
                    _searchesLeft--;
                    if (Search())
                    {
                        _searchesLeft = 0;
                    }
                    else if (_searchesLeft == 0)
                    {
                        Logger?.Warn("Character quality: no character on show was found in this scene; hair and lighting are left as the game has them.");
                    }
                }

                // The finish waits for the bundle (its strand maps); if the bundle fails it goes ahead without them.
                // (HairTests.FinishOff: the test entry that switches it off for a look at the game's own hair.)
                bool hair = Settings.CharacterHair && !HairTests.FinishOff && !_hairFailed && ModAssets.Settled;
                if (hair != _hairOn)
                {
                    _hairOn = hair;
                    if (hair)
                    {
                        ApplyHairToAll();
                    }
                    else
                    {
                        HairLook.RestoreAll();
                    }
                }

                // The skin finish needs nothing from the bundle: on and off with its option.
                bool skin = Settings.CharacterSkin && !_skinFailed;
                if (skin != _skinOn)
                {
                    _skinOn = skin;
                    ApplySkinToAll();
                }

                // The player's picks of the mod's own looks: applied when one changes, and once the assets have loaded.
                if (Settings.CharacterNewLooks != _looksOn || ModAssets.Ready != _looksReady || CharacterCatalogue.Version != _looksVersion)
                {
                    _looksOn = Settings.CharacterNewLooks;
                    _looksReady = ModAssets.Ready;
                    _looksVersion = CharacterCatalogue.Version;
                    ApplyLooksToAll();
                    _looksVersion = CharacterCatalogue.Version;   // applying may itself take a look off (the game's piece under it changed)
                    _creatorLooks?.Refresh();
                }

                if (_lighting != null && Settings.CharacterLighting != _lighting.Applied)
                {
                    if (Settings.CharacterLighting)
                    {
                        _lighting.Apply();
                    }
                    else
                    {
                        _lighting.Restore();
                    }
                }
            }
            catch (Exception e)
            {
                // Put everything back and stop, rather than fault again next frame.
                Logger?.Error(e);
                Stop();
                enabled = false;
            }
        }

        // ---------------------------------------------------------------- finding the characters on show

        private bool Search()
        {
            return CreationScene ? SearchCreation() : SearchCity();
        }

        private bool SearchCreation()
        {
            // The game's creator says which character it edits; without it, the detailed one in the scene.
            var customizer = FindObjectOfType<Intro.IntroCharacterCustomizer>();
            AppearanceSetter shown = customizer != null && CharacterTools.IsHighDetail(customizer.appearanceSetter) ? customizer.appearanceSetter : null;
            if (shown == null)
            {
                foreach (AppearanceSetter candidate in FindObjectsOfType<AppearanceSetter>())
                {
                    if (CharacterTools.IsHighDetail(candidate))
                    {
                        shown = candidate;
                        break;
                    }
                }
            }
            if (shown == null)
            {
                return false;
            }
            Camera camera = Camera.main;
            _lighting = new CharacterLighting("character creation", FindObjectsOfType<Light>(), shown.transform,
                camera != null ? camera.transform : null, transform, Logger);
            CharacterCatalogue.SetCreator(shown);   // before it is looked after: its pieces are what the picks are made over
            Manage(shown);
            _creatorLooks = GridLooks.Attach(customizer, shown, Logger, GridMode.Creator);   // the mod's starter garments in the game's own grids
            return true;
        }

        private bool SearchCity()
        {
            EmployeeUniformPreview preview = CharacterTools.FindPreview();
            if (preview == null)
            {
                return false;   // the game is still loading
            }
            AppearanceSetter shown = preview.appearanceSetter;
            if (shown == null)
            {
                Logger?.Warn("Character quality: the shop preview has no character; its hair and lighting are left alone.");
                return true;
            }

            // The preview's lights sit under an object the game switches on and off with the screen.
            var setUp = CharacterTools.ReadField<GameObject>(preview, "setUp");
            var camera = CharacterTools.ReadField<Camera>(preview, "previewCamera");
            Transform parent = setUp != null ? setUp.transform : null;
            Light[] lights = setUp != null ? setUp.GetComponentsInChildren<Light>(true) : Array.Empty<Light>();
            if (parent == null || lights.Length == 0)
            {
                Logger?.Info("Character quality: the shop preview's lights were not found; its lighting is left alone.");
            }
            else
            {
                _lighting = new CharacterLighting("shop preview", lights, shown.transform, camera != null ? camera.transform : null, parent, Logger);
            }

            if (CharacterTools.IsHighDetail(shown))
            {
                Manage(shown, true);
            }
            else
            {
                Logger?.Info("Character quality: the shop preview character is not the detailed set; its hair is left alone.");
            }
            return true;
        }

        // ---------------------------------------------------------------- characters

        /// <summary>Hair and textures for this character, now and whenever the game changes its look.</summary>
        public void Manage(AppearanceSetter character, bool sharedPreview = false)
        {
            if (character == null || _stopped)
            {
                return;
            }
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].Character == character)
                {
                    return;
                }
            }
            HairLook.Prune();
            SkinLook.Prune();
            var managed = new Managed { Character = character, SharedPreview = sharedPreview };
            managed.Handler = () => Refresh(managed);
            character.visualsUpdated += managed.Handler;
            _characters.Add(managed);
            Refresh(managed);
        }

        public void Forget(AppearanceSetter character)
        {
            for (int i = _characters.Count - 1; i >= 0; i--)
            {
                if (_characters[i].Character == character)
                {
                    if (character != null)
                    {
                        character.visualsUpdated -= _characters[i].Handler;
                    }
                    _characters.RemoveAt(i);
                }
            }
            ModLooks.Forget(character);
            try
            {
                SkinLook.Apply(character, false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Runs inside the game's own appearance update, so nothing may escape from it.
        private void Refresh(Managed managed)
        {
            bool hairChanged = false;
            try
            {
                AppearanceSetter character = managed.Character;
                if (character == null || _stopped)
                {
                    return;
                }
                if (_hairOn && !_hairSuspended)
                {
                    hairChanged = HairLook.Apply(character);
                }
                CharacterTools.SharpenTextures(character);
            }
            catch (Exception e)
            {
                DisableHair(e);
            }
            // Fails safe on its own: nothing escapes from it.
            ModLooks.Apply(managed.Character, !_stopped && !_hairSuspended && ShowsPlayer(managed));
            ApplySkin(managed);
            SayHair(managed.Character, hairChanged);
        }

        // The self-check line of the hair finish: one per character per change of its hair, with diagnostics on, after
        // the scalp underlay is on too (an underlay still on its way in from the bundle is said to be none).
        private static void SayHair(AppearanceSetter character, bool changed)
        {
            if (!changed || character == null || !Meta.Dev.Verbose)
            {
                return;
            }
            try
            {
                Logger?.Info(HairLook.Describe(character));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The skin finish, for the characters that show the player; the game's own maps for anyone else on the shop preview.
        // Nothing escapes from it: on any surprise the finish is switched off for the session and the game's maps are back.
        private void ApplySkin(Managed managed)
        {
            try
            {
                if (managed.Character != null)
                {
                    SkinLook.Apply(managed.Character, _skinOn && !_stopped && !_hairSuspended && ShowsPlayer(managed));
                }
            }
            catch (Exception e)
            {
                if (!_skinFailed)
                {
                    _skinFailed = true;
                    Logger?.Warn("Character quality: the skin finish failed and is switched off for this session; the game's skin maps are back.");
                    Logger?.Error(e);
                }
                _skinOn = false;
                try
                {
                    SkinLook.RestoreAll();
                }
                catch (Exception inner)
                {
                    Logger?.Error(inner);
                }
            }
        }

        private void ApplySkinToAll()
        {
            for (int i = 0; i < _characters.Count && !_skinFailed; i++)
            {
                ApplySkin(_characters[i]);
            }
        }

        private static bool ShowsPlayer(Managed managed)
        {
            if (!managed.SharedPreview)
            {
                return true;   // character creation, or the player's double
            }
            try
            {
                var save = SaveGameManager.Current;
                CharacterData shown = managed.Character != null ? managed.Character.data : null;
                return save != null && shown != null && save.charactersData != null && save.charactersData.Count > 0
                       && !string.IsNullOrEmpty(shown.name) && shown.name == save.charactersData[0].name;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ApplyLooksToAll()
        {
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].Character != null)
                {
                    ModLooks.Apply(_characters[i].Character, !_stopped && !_hairSuspended && ShowsPlayer(_characters[i]));
                }
            }
        }

        private void ApplyHairToAll()
        {
            try
            {
                for (int i = 0; i < _characters.Count; i++)
                {
                    if (_characters[i].Character != null)
                    {
                        SayHair(_characters[i].Character, HairLook.Apply(_characters[i].Character));
                    }
                }
            }
            catch (Exception e)
            {
                DisableHair(e);
            }
        }

        private void DisableHair(Exception error)
        {
            if (!_hairFailed)
            {
                _hairFailed = true;
                Logger?.Warn("Character quality: hair styling failed and is switched off for this session; the game's hair materials are back.");
                Logger?.Error(error);
            }
            _hairOn = false;
            try
            {
                HairLook.RestoreAll();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Hair back to the game's materials, and the mod's own pieces off, while a character is being copied.</summary>
        public void SuspendHair()
        {
            _hairSuspended = true;
            HairLook.RestoreAll();
            ModLooks.RestoreAll();
            SkinLook.RestoreAll();
        }

        public void ResumeHair()
        {
            _hairSuspended = false;
            ApplyLooksToAll();
            if (_hairOn)
            {
                ApplyHairToAll();
            }
            if (_skinOn)
            {
                ApplySkinToAll();
            }
        }

        // ---------------------------------------------------------------- stop

        private void Stop()
        {
            if (_stopped)
            {
                return;
            }
            _stopped = true;
            _searchesLeft = 0;
            for (int i = 0; i < _characters.Count; i++)
            {
                try
                {
                    if (_characters[i].Character != null)
                    {
                        _characters[i].Character.visualsUpdated -= _characters[i].Handler;
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            _characters.Clear();
            try
            {
                _creatorLooks?.Dispose();
                _creatorLooks = null;
                HairLook.Shutdown();
                HairTests.Forget();
                ModLooks.Shutdown();
                SkinLook.Shutdown();
                CharacterTools.RestoreTextures();
                if (_lighting != null)
                {
                    _lighting.Restore();
                    _lighting = null;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _hairOn = false;
            _skinOn = false;
        }
    }
}
