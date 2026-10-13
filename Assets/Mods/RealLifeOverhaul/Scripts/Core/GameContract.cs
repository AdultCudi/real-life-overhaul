namespace RealLifeOverhaul
{
    /// <summary>
    /// THE GAME CONTRACT: every piece of the game this mod reaches by name instead of through the game's modding
    /// interface, which parts of the mod stand on each, and the type the mod expects it to have. Resolved once when the
    /// mod starts (Core/Guard calls <see cref="ContractBook.Resolve"/>); the code that uses a piece reads it from here,
    /// never by name again. After a game update a piece that is missing or has changed switches off only its parts
    /// (<see cref="GamePart.Off"/>), each said once in Player.log, on screen and on the options page, until the mod is
    /// updated; every other part runs as before.
    ///
    /// Before a release (and at once after a game update) the owner runs RealLifeOverhaul.Build/ContractCheck, which
    /// reads this file as it ships and checks every line against the game's assemblies on disk: what is missing or
    /// changed, and which parts that hits. Then <see cref="ContractBook.CheckedBuild"/> is set to the game's build.
    ///
    /// TO ADD A PIECE: a part (P) if it serves something new, then one line: Field, StaticField, Prop, Method,
    /// StaticMethod or TypeOf, with the assembly's simple name, the type's full name (a nested type after '+'), the
    /// member's name and its type as ContractBook.TypeText writes it ("Dictionary&lt;String,Toggle&gt;"). Optional: the
    /// code has another way when it is gone, so it is only said in the log. Use the member as GameContract.X.Field
    /// (or .Property, .Setter, .Method, .Type): null when it is not there. No Unity or game types in this file.
    /// </summary>
    public static class GameContract
    {
        private const string Game = "BigAmbitions";
        private const string Plugins = "ExternalPlugins";
        private const string Sky = "DayNightCycle";
        private const string UnityUi = "UnityEngine.UI";

        static GameContract()
        {
        }

        /// <summary>Makes sure every line below is registered (the class's initialiser runs on its first use).</summary>
        public static void Touch()
        {
        }

        // ---------------------------------------------------------------- the parts (id, feature of Meta/FeatureList, what it is)

        public static readonly GamePart MarkersBehind = ContractBook.Part("markers_behind", "camera", "the game's place markers put right when what they mark is behind the camera");
        public static readonly GamePart LookBehind = ContractBook.Part("look_behind", "camera", "looking behind with the right mouse button held");
        public static readonly GamePart CharacterDetail = ContractBook.Part("character_detail", "character", "the detailed character copied for the player's looks");
        public static readonly GamePart ShopPreview = ContractBook.Part("shop_preview", "character", "the lighting of the clothes shop's preview");
        public static readonly GamePart LooksGrid = ContractBook.Part("looks_grid", "character", "the mod's garments in the game's character screens");
        public static readonly GamePart WornPieces = ContractBook.Part("worn_pieces", "character", "the mod's own hair and clothes on the player");
        public static readonly GamePart ClothesShop = ContractBook.Part("clothes_shop", "leisure", "the clothes shops' screen");
        public static readonly GamePart AppTables = ContractBook.Part("app_tables", "phone", "the tables of the mod's phone apps");
        public static readonly GamePart AppButtons = ContractBook.Part("app_buttons", "phone", "the buttons of the mod's phone apps");
        public static readonly GamePart ContactPictures = ContractBook.Part("contact_pictures", "phone", "the pictures of the mod's contacts");
        public static readonly GamePart ContactSearch = ContractBook.Part("contact_search", "phone", "the search box of the Contacts app");
        public static readonly GamePart PhoneSkin = ContractBook.Part("phone_skin", "phone", "the phone's skin");
        public static readonly GamePart MapPicker = ContractBook.Part("map_picker", "navigation", "clicking the markers when choosing a place on the city map");
        public static readonly GamePart MapPlaces = ContractBook.Part("map_places", "navigation", "the mod's places in the city map's filters");
        public static readonly GamePart NavMarks = ContractBook.Part("nav_marks", "navigation", "the game's look for the markers on the minimap");
        public static readonly GamePart PanelBeside = ContractBook.Part("panel_beside", null, "the mod's cards beside the game's building card");
        public static readonly GamePart CardFlag = ContractBook.Part("card_flag", null, "the mod's cards over things in the street");
        public static readonly GamePart CardGuard = ContractBook.Part("card_guard", null, "putting away the game's card over a thing that was removed");
        public static readonly GamePart KeyClash = ContractBook.Part("key_clash", "keys", "the warning when a key of the mod is one of the game's");
        public static readonly GamePart Moods = ContractBook.Part("moods", "life", "the mod's moods");
        public static readonly GamePart Thunder = ContractBook.Part("thunder", "events", "thunderstorms in the heaviest showers");
        public static readonly GamePart ClickHome = ContractBook.Part("click_home", "home", "clicking a cooker or a coffee machine at home");
        public static readonly GamePart ClickLedger = ContractBook.Part("click_ledger", "ledger", "clicking the City Ledger at home");
        public static readonly GamePart ClickBusStop = ContractBook.Part("click_bus_stop", "transport", "clicking a bus stop");
        public static readonly GamePart Mop = ContractBook.Part("mop", "jobs", "the cleaner's mop");
        public static readonly GamePart SchoolCar = ContractBook.Part("school_car", "licences", "the driving school's car");
        public static readonly GamePart SaveCheck = ContractBook.Part("save_check", "savetools", "the save tools' test of whether the game may save here");
        public static readonly GamePart BenchSitters = ContractBook.Part("bench_sitters", "citylife", "people sitting on the game's benches");
        public static readonly GamePart StreetLamps = ContractBook.Part("street_lamps", "performance", "the savings on street lamps");
        public static readonly GamePart FarDistricts = ContractBook.Part("far_districts", "performance", "the far districts trial of the performance benchmark");
        public static readonly GamePart UiProbe = ContractBook.Part("ui_probe", "diagnostics", "the interface part of the performance probe");
        public static readonly GamePart BodyFade = ContractBook.Part("body_fade", "ragdoll", "showing a body the game faded beside a car");
        public static readonly GamePart BusStops = ContractBook.Part("bus_stops", "transport", "bus stops kept clear of parked cars");

        // ---------------------------------------------------------------- the pieces

        // Camera/PoiDirection: without them every marker is looked at, and a direction marker uses the whole screen
        public static readonly GameMember PermanentPois = Optional(StaticField(MarkersBehind, Game, "UI.PermanentPointsOfInterest", "PermanentPois", "List<PointOfInterest>"));
        public static readonly GameMember PoiDeadZone = Optional(Field(MarkersBehind, Game, "PointOfInterest", "deadZone", "Vector4"));
        public static readonly GameMember PoiPointer = Optional(Field(MarkersBehind, Game, "PointOfInterest", "pointerRectTransform", "RectTransform"));

        // Camera/ThirdPersonCamera: without it the game's own right click also fires when a look behind ends
        public static readonly GameMember RightButtonHeld = Optional(StaticField(LookBehind, Game, "MouseController", "RightButtonHeld", "Boolean"));

        // Character/CharacterTools (one of three places a detailed character is copied from), Phone/Contacts/ContactPainter (the game's public call otherwise)
        public static readonly GameMember PortraitInstance = Optional(StaticField(On(CharacterDetail, ContactPictures), Game, "Character.Customization.PortraitGenerator", "_instance", "PortraitGenerator"));
        public static readonly GameMember PortraitCharacter = Optional(Field(On(CharacterDetail, ContactPictures), Game, "Character.Customization.PortraitGenerator", "portraitAppearanceSetter", "AppearanceSetter"));

        // Character/CharacterQuality (through CharacterTools.ReadField)
        public static readonly GameMember PreviewSetUp = Field(ShopPreview, Game, "EmployeeUniformPreview", "setUp", "GameObject");
        public static readonly GameMember PreviewCamera = Field(ShopPreview, Game, "EmployeeUniformPreview", "previewCamera", "Camera");

        // Character/GridLooks
        public static readonly GameMember CustomizerPicker = Field(LooksGrid, Game, "Character.Customization.CharacterCustomizer", "elementPicker", "ElementPicker");
        public static readonly GameMember CustomizerShown = Field(LooksGrid, Game, "Character.Customization.CharacterCustomizer", "onSubCategoryChanged", "UnityEvent<AppearanceElementType>");
        public static readonly GameMember PickerTemplate = Field(LooksGrid, Game, "Character.Customization.ElementPicker", "entryTemplate", "Transform");
        public static readonly GameMember PickerOutline = Field(LooksGrid, Game, "Character.Customization.ElementPicker", "_currentSelectedOutline", "GameObject");

        // Character/ModLooks
        public static readonly GameMember WornRenderers = Field(WornPieces, Game, "AppearanceSetter", "_selectedMeshRenderers", "Dictionary<AppearanceElementType,SkinnedMeshRenderer>");

        // Leisure/Shops/ClothesScreen
        public static readonly GameMember CustomizerTags = Field(ClothesShop, Game, "Character.Customization.CharacterCustomizer", "tags", "AppearanceTag[]");
        public static readonly GameMember ClothesSaveButton = Field(ClothesShop, Game, "ChangeCharacterClothesUI", "saveClothesChangesButton", "Button");

        // Core/GameUi/AppShell: the row of the game's employee table, and the colour of a chosen row
        public static readonly GameMember EmployeeTable = TypeOf(AppTables, Game, "UI.Smartphone.Apps.MyEmployees.EmployeeScrollerController");
        public static readonly GameMember EmployeeTableRow = Field(AppTables, Game, "UI.Smartphone.Apps.MyEmployees.EmployeeScrollerController", "prefab", "EmployeeCellView");
        public static readonly GameMember RowChosenColour = Optional(Field(AppTables, Game, "BaTable.BaTableCellView`1", "selectedBackgroundColor", "Color"));

        // Core/GameUi/PhoneApps, PhoneGridPager: each has another way when the field is gone
        public static readonly GameMember HudAppTemplate = Optional(Field(AppButtons, Game, "SmartphoneUI", "appButtonTemplate", "Transform"));
        public static readonly GameMember MenuAppTemplate = Optional(Field(AppButtons, Game, "UI.Smartphone.FullMenu", "appButtonTemplate", "Transform"));
        public static readonly GameMember AppButtonIcon = Optional(Field(AppButtons, Game, "AppButton", "icon", "Image"));

        // Core/GameUi/WorldPanel: the building box is found another way, the flag is only put right as a city unloads, the
        // lines are made again with their buttons: each has another way
        public static readonly GameMember ResumePanel = Optional(Field(PanelBeside, Game, "UI.InGameUI.BuildingResume", "panelRectTransform", "RectTransform"));
        public static readonly GameMember OverlayVisible = Optional(NeedsSetter(Prop(CardFlag, Game, "UI.Overlays.OverlayUI", "IsVisible", "Boolean", true)));
        public static readonly GameMember OverlayHeader = Optional(Method(CardFlag, Game, "UI.Overlays.OverlayUI", "SetUpHeader", "Void"));

        // Core/GameUi/MapPicker, Core/MapPlaces, Navigation/NavMarks: the game's marker
        public static readonly GameMember PoiBlob = Field(On(MapPicker, MapPlaces, NavMarks), Game, "PointOfInterest", "blobImage", "Image");
        public static readonly GameMember PoiButton = Field(On(MapPicker, MapPlaces), Game, "PointOfInterest", "button", "Button");
        public static readonly GameMember PoiIcon = Field(NavMarks, Game, "PointOfInterest", "buildingIcon", "BuildingIcon");
        public static readonly GameMember PoiText = Field(NavMarks, Game, "PointOfInterest", "_text", "String");
        public static readonly GameMember BuildingIconImage = Field(NavMarks, Game, "BuildingIcon", "image", "Image");

        // Core/MapPlaces: the city map's filters
        public static readonly GameMember FilterToggles = Field(MapPlaces, Game, "CityMapFilters", "_filterToggles", "Dictionary<String,Toggle>");
        public static readonly GameMember FilterEntries = Field(MapPlaces, Game, "CityMapFilters", "_filterEntries", "Dictionary<String,CityMapFilter>");
        public static readonly GameMember FilterToggleAll = Field(MapPlaces, Game, "CityMapFilters", "_toggleAll", "Toggle");
        public static readonly GameMember FilterCreate = Method(MapPlaces, Game, "CityMapFilters", "CreateFilter", "CityMapFilter",
            "String", "Sprite", "CityMapFilterData", "CityMapFilterCategory", "LanguageChangeEventDataHolder", "Nullable<Vector3>", "Func<Nullable<Vector3>>");
        public static readonly GameMember FilterCreateLine = Method(MapPlaces, Game, "CityMapFilters", "CreateLine", "CityMapFilterCategory", "String");

        // Core/OverlayGuard
        public static readonly GameMember OverlaySimple = Field(CardGuard, Game, "Player.HUD.ItemInfoOverlays.OverlayManager", "simpleOverlay", "SimpleOverlay");
        public static readonly GameMember OverlayDetailed = Field(CardGuard, Game, "Player.HUD.ItemInfoOverlays.OverlayManager", "detailedOverlay", "DetailedOverlay");

        // Core/Keys: the player's bindings as the game has them now; without them the game's defaults are warned about
        public static readonly GameMember PlayerInput = Optional(StaticField(KeyClash, Game, "Helpers.InputHelper", "playerInput", "PlayerInput"));
        public static readonly GameMember VehicleInput = Optional(StaticField(KeyClash, Plugins, "NWH.VehiclePhysics2.Input.InputSystemVehicleInputProvider", "vehicleInputActions", "VehicleInputActions"));

        // Core/ModState, Family, Travel, Meta/SaveTools: the game's register of mood types (the mod's own are added to it)
        public static readonly GameMember HappinessRegistry = StaticField(Moods, Game, "Helpers.HappinessHelper", "Modifiers", "Dictionary<String,HappinessModifier>");

        // Events/Weather
        public static readonly GameMember RainIntensity = StaticField(Thunder, Sky, "BigAmbitions.DayNightCycle.RainHelper", "NextRainMaxIntensity", "Single");

        // Home/HomeOffers, Ledger/LedgerPointer, Transport/TransitPointer: the game's list of what a click does
        public static readonly GameMember CtaBehaviours = StaticField(On(ClickHome, ClickLedger, ClickBusStop), Game, "Player.HUD.ItemInfoOverlays.CtaManager", "CtaBehaviors", "List<ICtaBehavior>");

        // Jobs/JobMop
        public static readonly GameMember MopClick = Optional(Method(Mop, Game, "MopController", "OnFloorCellClick", "Void", "DirtSpotObject"));   // (else the cell under the player)

        // Licences/Driving/TestCar
        public static readonly GameMember NavTargets = Field(SchoolCar, Game, "EntityController", "navMeshTargets", "Transform[]");
        public static readonly GameMember NavBlockers = Field(SchoolCar, Game, "PlayerController", "_activeNavigationBlockers", "HashSet<NavigationBlocker>");

        // Meta/SaveTools ("yes" when it cannot be read)
        public static readonly GameMember CanSave = Optional(StaticMethod(SaveCheck, Game, "SaveGameManager", "CanSave", "Boolean"));

        // People/Seating
        public static readonly GameMember BenchSitter = Field(BenchSitters, Game, "OutsideBenchController", "_npc", "BaseHuman");

        // Performance/PerfGame
        public static readonly GameMember LampLights = Field(StreetLamps, Game, "StreetLamp", "lights", "Light[]");
        public static readonly GameMember LampData = Field(StreetLamps, Game, "StreetLamp", "additionalLightDatas", "HDAdditionalLightData[]");
        public static readonly GameMember LampGlow = Field(StreetLamps, Game, "StreetLamp", "volumetricLightObject", "GameObject");
        public static readonly GameMember LampIsOn = Field(StreetLamps, Game, "StreetLamp", "_isOn", "Boolean");

        // Performance/PerfProbe (Unity's interface)
        public static readonly GameMember LayoutQueue = Field(UiProbe, UnityUi, "UnityEngine.UI.CanvasUpdateRegistry", "m_LayoutRebuildQueue", null);
        public static readonly GameMember GraphicQueue = Field(UiProbe, UnityUi, "UnityEngine.UI.CanvasUpdateRegistry", "m_GraphicRebuildQueue", null);

        // Phone/Contacts
        public static readonly GameMember ContactIcons = Optional(StaticField(ContactPictures, Game, "UI.Smartphone.Apps.Contacts.ContactsApp", "PredefinedContactIconSprites", "Dictionary<String,Sprite>"));   // (else from the next load)
        public static readonly GameMember ContactsMarkAll = Field(ContactSearch, Game, "UI.Smartphone.Apps.Contacts.ContactsApp", "markAllAsReadButton", "Button");
        public static readonly GameMember TextInputField = Field(ContactSearch, Game, "TextInputUI", "inputField", "TMP_InputField");

        // Phone/PhoneSkin
        public static readonly GameMember FrameData = Field(PhoneSkin, Game, "Player.HUD.SmartphoneUI.SmartPhoneFrame", "frameData", "List<SmartPhoneFrame.FrameData>");
        public static readonly GameMember FrameImage = Field(PhoneSkin, Game, "Player.HUD.SmartphoneUI.SmartPhoneFrame", "frameImage", "Image");
        public static readonly GameMember FrameMask = Field(PhoneSkin, Game, "Player.HUD.SmartphoneUI.SmartPhoneFrame", "maskImage", "Image");

        // Ragdoll/Ragdoll
        public static readonly GameMember NearItemToHide = Method(BodyFade, Game, "HumanHider", "SetIsNearItemToHide", "Void", "Boolean");

        // Transport/BusZones: a parking lane's private settings, and the scenery it may switch off with a bus stop's kerb
        public static readonly GameMember LaneSpotCount = Field(BusStops, Game, "ParkingLaneGenerator", "spotCountOverride", "Int32");
        public static readonly GameMember LaneSideBySide = Field(BusStops, Game, "ParkingLaneGenerator", "sideBySideParkingSpots", "Boolean");
        public static readonly GameMember LaneCurved = Field(BusStops, Game, "ParkingLaneGenerator", "curvedLane", "Boolean");
        public static readonly GameMember LaneVehicles = Field(BusStops, Game, "ParkingLaneGenerator", "specificVehicles", "String[]");
        public static readonly GameMember LaneSpotDistance = Field(BusStops, Game, "ParkingLaneGenerator", "parkingSpotDistance", "Single");
        // (found by name on the scenery: a name that changes is a script no longer switched off with its kerb)
        public static readonly GameMember SceneryTree = Optional(TypeOf(BusStops, Game, "TreeController"));
        public static readonly GameMember SceneryMapHider = Optional(TypeOf(BusStops, Game, "CityMapHider"));
        public static readonly GameMember SceneryDirt = Optional(TypeOf(BusStops, Game, "DirtSpotObject"));
        public static readonly GameMember SceneryNavModifier = Optional(TypeOf(BusStops, "Unity.AI.Navigation", "Unity.AI.Navigation.NavMeshModifier"));
        public static readonly GameMember SceneryNavObstacle = Optional(TypeOf(BusStops, "UnityEngine.AIModule", "UnityEngine.AI.NavMeshObstacle"));
        public static readonly GameMember SceneryLight = Optional(TypeOf(BusStops, "Unity.RenderPipelines.HighDefinition.Runtime", "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData"));

        // Performance/Tweaks/FarDistricts (its benchmark trial by Renderer.enabled): the game's components whose own code
        // enables and disables renderers, by reading the game's code (file and line of one such write each). Without every
        // one of them the trial cannot be safe, so it stays off.
        public static readonly GameMember[] Switchers =
        {
            TypeOf(FarDistricts, Game, "CityBuildingController"),        // CityBuildingController.cs 241, 718
            TypeOf(FarDistricts, Game, "CityHamptonsHouseController"),   // CityHamptonsHouseController.cs 86-116
            TypeOf(FarDistricts, Game, "HamptonsHouse"),                 // HamptonsHouse.cs 121-252
            TypeOf(FarDistricts, Game, "BuildingSignController"),        // through CityBuildingController.cs 718
            TypeOf(FarDistricts, Game, "BuildingLogoSignController"),    // through CityBuildingController.cs 718
            TypeOf(FarDistricts, Game, "BillboardAd"),                   // BillboardAd.cs 101, 106
            TypeOf(FarDistricts, Game, "DirtSpotObject"),                // DirtSpotObject.cs 63, 79
            TypeOf(FarDistricts, Game, "GasStationTrigger"),             // GasStationTrigger.cs 39-49
            TypeOf(FarDistricts, Game, "Buildings.BuildingTypes.Special.PrivateDriverService.PrivateDriverGarageTrigger"),   // PrivateDriverGarageTrigger.cs 68-82
            TypeOf(FarDistricts, Game, "AutoParkSpot"),                  // AutoParkSpot.cs 14, 20; VehicleParkingHelper.cs 65-190
            TypeOf(FarDistricts, Game, "FenceDoor"),                     // FenceDoor.cs 352, 357
            TypeOf(FarDistricts, Game, "EntityController"),              // EntityController.cs 145, 160 (ViewBlockingEntity and its kin derive from it: ViewBlockingEntity.cs 248)
            TypeOf(FarDistricts, Game, "ItemController"),                // ItemController.cs 461, 475
            TypeOf(FarDistricts, Game, "BaseHuman"),                     // BaseHuman.cs 297; HumanHider.cs 117, 126
            TypeOf(FarDistricts, Game, "CarFeatures"),                   // CarFeatures.cs 114, 130
            TypeOf(FarDistricts, Game, "CarController"),                 // CarFeatures.cs 114, 130
            TypeOf(FarDistricts, Game, "Buildings.TaxiDestinationData"), // Buildings/TaxiDestinationData.cs 49
            TypeOf(FarDistricts, Game, "Controllers.CoatCheckController"),   // Controllers/CoatCheckController.cs 143
            TypeOf(FarDistricts, Game, "CarnivalPedestrian"),            // CarnivalPedestrian.cs 48; CarnivalPedestrianPool.cs 44
            TypeOf(FarDistricts, Game, "Controllers.AttractionViewBlockingEntity"),    // Controllers/AttractionViewBlockingEntity.cs 49
            TypeOf(FarDistricts, Game, "Controllers.FerrisWheelViewBlockingEntity"),   // Controllers/FerrisWheelViewBlockingEntity.cs 38-58
            TypeOf(FarDistricts, Game, "ParkingBuildingWithDifferentHeightsManager"),  // (22 writes)
            TypeOf(FarDistricts, Game, "PlayerActivity.GolfCourse"),     // PlayerActivity/GolfCourse.cs 232-498
            TypeOf(FarDistricts, Game, "SewerSteam"),                    // SewerSteam.cs 54, 59
            TypeOf(FarDistricts, Game, "PlayerActivity.Tennis.TennisPlayer"),
            TypeOf(FarDistricts, Game, "PlayerActivity.Tennis.TennisBall"),
            TypeOf(FarDistricts, Game, "PlayerActivity.Tennis.TennisScoreLine"),
            TypeOf(FarDistricts, Game, "PlayerActivity.WorkoutActivity"),
            // (The list before the contract also named "InteriorElement", which the game's assembly has no type of: it was
            // never found, and is left out.)
        };

        // Core/GameUi/InfoBox: the game's hover effect taken off a copied drop-down (left on when it is not found);
        // Core/GameFurniture: a light of the game's pipeline is kept on a copy of furniture. Both found by name.
        public static readonly GameMember HoverEffect = Optional(TypeOf(null, Game, "UI.HoverEffect"));
        public static readonly GameMember HdLight = Optional(TypeOf(null, "Unity.RenderPipelines.HighDefinition.Runtime", "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData"));

        // ---------------------------------------------------------------- the game's layers, by name

        /// <summary>
        /// The game's physics and rendering layers the mod names (LayerMask.GetMask, NameToLayer: raycasts for the ground,
        /// the roads, buildings and props, what is a parked or driven vehicle, a person, the interface). They are the game's
        /// project settings, not in its assemblies, so ContractCheck cannot read them: Core/Guard looks each up as the mod
        /// starts and names any that is gone in Player.log (what is looked for on it is then not found).
        /// </summary>
        public static readonly string[] Layers =
        {
            "AiVehicles", "AutoParkSpots", "BuildingWalls", "Buildings", "BuildingsOutlined", "Default", "Ground", "GroundUnplacable", "Human",
            "Ignore Raycast", "InteractiveItems", "InteractiveItemsOutlined", "ParkedVehicles", "ParkingArea", "Player", "PlayerVehicles", "Roads",
            "StreetProps", "TransparentFX", "UI", "Vehicles", "Water",
        };

        // ---------------------------------------------------------------- how a line is made

        private static GamePart[] On(params GamePart[] parts)
        {
            return parts;
        }

        private static GamePart[] One(GamePart part)
        {
            return part != null ? new[] { part } : new GamePart[0];
        }

        private static GameMember Field(GamePart part, string assembly, string type, string name, string valueType)
        {
            return new GameMember(GameMemberKind.Field, assembly, type, name, false, valueType, null, false, One(part));
        }

        private static GameMember Field(GamePart[] parts, string assembly, string type, string name, string valueType)
        {
            return new GameMember(GameMemberKind.Field, assembly, type, name, false, valueType, null, false, parts);
        }

        private static GameMember StaticField(GamePart part, string assembly, string type, string name, string valueType)
        {
            return new GameMember(GameMemberKind.Field, assembly, type, name, true, valueType, null, false, One(part));
        }

        private static GameMember StaticField(GamePart[] parts, string assembly, string type, string name, string valueType)
        {
            return new GameMember(GameMemberKind.Field, assembly, type, name, true, valueType, null, false, parts);
        }

        private static GameMember Prop(GamePart part, string assembly, string type, string name, string valueType, bool isStatic)
        {
            return new GameMember(GameMemberKind.Property, assembly, type, name, isStatic, valueType, null, false, One(part));
        }

        private static GameMember Method(GamePart part, string assembly, string type, string name, string returns, params string[] parameters)
        {
            return new GameMember(GameMemberKind.Method, assembly, type, name, false, returns, parameters, false, One(part));
        }

        private static GameMember StaticMethod(GamePart part, string assembly, string type, string name, string returns, params string[] parameters)
        {
            return new GameMember(GameMemberKind.Method, assembly, type, name, true, returns, parameters, false, One(part));
        }

        private static GameMember TypeOf(GamePart part, string assembly, string type)
        {
            return new GameMember(GameMemberKind.Type, assembly, type, null, false, null, null, false, One(part));
        }

        private static GameMember Optional(GameMember member)
        {
            member.MakeOptional();
            return member;
        }

        private static GameMember NeedsSetter(GameMember member)
        {
            member.NeedsSetter = true;
            return member;
        }
    }
}
