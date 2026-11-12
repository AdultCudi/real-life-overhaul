namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What a made-up name is made for: one of the six kinds of software a firm sells, or a game of one of the twelve genres
    /// (<see cref="Games"/> plus the genre). Each has word lists of its own, so a spreadsheet tool never sounds like a horror game.
    /// </summary>
    public static class TitleStyle
    {
        public const int SaasConsumer = 0, SaasBusiness = 1, SaasEnterprise = 2, App = 3, Licence = 4, Model = 5, Games = 6;
        public const int Count = Games + (int)Genre.Count;
    }

    /// <summary>
    /// The words product and game names are made of (<see cref="TitleRules"/>), all invented: a first and a second list for each
    /// <see cref="TitleStyle"/>, sixteen words each, no second word shared between two styles, so two styles never make the same
    /// name. Product names are proper names and are not translated. Game-free; cost: arrays read when a name is made.
    /// </summary>
    public static class TitleCatalogue
    {
        public const int Most = 40;    // characters: the longest name a player may type, and no made-up name is longer
        public const int Words = 16;   // words in each list: an even number, since each side takes every second pairing (TitleRules.Name)

        /// <summary>True for a style whose two words are written as one ("Brightsheet"), false for two words ("Hollow Manor").</summary>
        public static readonly bool[] Joined =
        {
            true, true, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false
        };

        public static readonly string[][] First =
        {
            new[] { "Satchel", "Sunny", "Maple", "Pebble", "Cosy", "Lark", "Clover", "Poppy", "Nimble", "Mellow", "Wren", "Plum", "Biscuit", "Tulip", "Hazel", "Dapple" },
            new[] { "Bright", "Quill", "Harbour", "Lantern", "Copper", "Tide", "Juniper", "Meadow", "Willow", "Ridge", "Arbor", "Marble", "Kestrel", "Thistle", "Barley", "Anvil" },
            new[]
            {
                "Northmere", "Ravensmoor", "Stonefield", "Highmoor", "Westmarch", "Greywater", "Oakhaven", "Castlereach", "Longmere", "Farrowdale", "Aldergate", "Brackenridge",
                "Halewood", "Marlowe", "Penrose", "Quarrybank"
            },
            new[] { "Zippy", "Tap", "Dash", "Hop", "Ping", "Snug", "Fizz", "Jot", "Bop", "Zest", "Dot", "Pip", "Whiz", "Nip", "Jolly", "Skip" },
            new[]
            {
                "Draftwright", "Platewright", "Gearline", "Truss", "Kiln", "Treadle", "Mason", "Caliper", "Plumbline", "Sextant", "Lathe", "Trowel", "Bevel", "Rivet", "Spindle", "Gantry"
            },
            new[] { "Lumen", "Tessera", "Marrow", "Quorum", "Sable", "Halcyon", "Verity", "Orrery", "Cinder", "Beacon", "Larkspur", "Osprey", "Meridian", "Solace", "Tallow", "Fathom" },
            // the games, in the order of Genre: action, adventure, role-playing, strategy, simulation, sports, racing, puzzle, horror, shooter, casual, family
            new[]
            {
                "Iron", "Crimson", "Thunder", "Savage", "Burning", "Rogue", "Steel", "Reckless", "Furious", "Broken", "Rising", "Scarlet", "Storm", "Blazing", "Vengeful", "Midnight"
            },
            new[] { "Lost", "Hidden", "Wandering", "Distant", "Forgotten", "Jade", "Amber", "Curious", "Far", "Secret", "Drifting", "Gilded", "Winding", "Misty", "Faraway", "Little" },
            new[] { "Elder", "Ashen", "Sundered", "Hallowed", "Ancient", "Fabled", "Ember", "Twilight", "Sworn", "Rune", "Silver", "Briar", "Dawn", "Wyrm", "Raven", "Mythic" },
            new[]
            {
                "Grand", "Imperial", "Sovereign", "Northern", "Bronze", "Marching", "Rival", "Banner", "Border", "Granite", "Frontier", "Citadel", "Warden", "Old", "Divided", "Restless"
            },
            new[] { "Busy", "Tiny", "Happy", "Tidy", "Thriving", "Bustling", "Homely", "Green", "Humble", "Model", "Pocket", "Lively", "Cosy", "Open", "Seaside", "Hometown" },
            new[] { "Champion", "Final", "Rowdy", "Local", "Sunday", "Underdog", "Home", "Derby", "Varsity", "Golden", "Roaring", "Full", "Last", "Proud", "Muddy", "Late" },
            new[]
            {
                "Turbo", "Apex", "Redline", "Nitro", "Dusty", "Coastal", "Hairpin", "Rapid", "Gleaming", "Screaming", "Downhill", "Gravel", "Sideways", "Throttle", "Tarmac", "Flying"
            },
            new[] { "Tangled", "Clever", "Twisty", "Paper", "Clockwork", "Dainty", "Puzzling", "Folded", "Shifting", "Mirror", "Quiet", "Painted", "Stacked", "Nested", "Woven", "Tilted" },
            new[] { "Hollow", "Silent", "Pale", "Withered", "Dread", "Sunken", "Whispering", "Forsaken", "Bleak", "Rotten", "Shrouded", "Unquiet", "Black", "Cold", "Lonely", "Grim" },
            new[] { "Ghost", "Zero", "Hostile", "Delta", "Viper", "Scorched", "Frontline", "Night", "Kilo", "Echo", "Outpost", "Tracer", "Burnt", "Ricochet", "Lone", "Sabre" },
            new[] { "Bubbly", "Sweet", "Jelly", "Fluffy", "Sparkly", "Lucky", "Bouncy", "Juicy", "Silly", "Fizzy", "Sugar", "Wobbly", "Peppy", "Giggly", "Twinkly", "Snappy" },
            new[] { "Merry", "Jaunty", "Rainbow", "Friendly", "Cuddly", "Playful", "Fuzzy", "Brave", "Kind", "Wonder", "Cheerful", "Starry", "Gentle", "Giddy", "Breezy", "Dandy" }
        };

        public static readonly string[][] Second =
        {
            new[] { "nest", "pal", "nook", "jar", "list", "trail", "plate", "diary", "pantry", "album", "stroll", "patch", "kettle", "porch", "locker", "basket" },
            // none of the second words products were named with before names could be typed (desk, ledger, flow and the rest): no made-up name is an older save's
            new[] { "sheet", "audit", "tally", "roster", "hub", "file", "plan", "task", "count", "dock", "lane", "mark", "slate", "port", "till", "yard" },
            new[]
            {
                "Suite", "Core", "Fabric", "Nexus", "Registry", "Console", "Gateway", "Directory", "Pipeline", "Vault", "Conductor", "Controller", "Ledgerworks", "Exchange", "Insight",
                "Foundry"
            },
            new[] { "mate", "ling", "bee", "roo", "pop", "hive", "dart", "wink", "nudge", "sprout", "puddle", "wiggle", "bubble", "doodle", "pocket", "button" },
            new[]
            {
                "Pro", "Studio", "Workbench", "Engineer", "Designer", "Planner", "Architect", "Drafter", "Modeller", "Surveyor", "Estimator", "Toolkit", "Works", "Bench", "Blueprint",
                "Station"
            },
            new[] { "Mind", "Reason", "Scholar", "Sage", "Thinker", "Mentor", "Scribe", "Savant", "Muse", "Cortex", "Logic", "Tutor", "Witness", "Counsel", "Voice", "Pilot" },
            new[]
            {
                "Brawlers", "Gauntlet", "Vendetta", "Rampage", "Havoc", "Reckoning", "Onslaught", "Uprising", "Renegades", "Showdown", "Knuckles", "Mayhem", "Bruisers", "Skirmish",
                "Payback", "Retribution"
            },
            new[]
            {
                "Voyage", "Compass", "Isle", "Excursion", "Trailhead", "Lagoon", "Passage", "Lighthouse", "Atlas", "Caravan", "Wayfarer", "Cove", "Footpath", "Archipelago", "Keepsake",
                "Postcard"
            },
            new[]
            {
                "Crown", "Saga", "Chronicle", "Covenant", "Throne", "Realm", "Vanguard", "Legacy", "Pilgrim", "Sigil", "Grimoire", "Heirloom", "Relic", "Dominion", "Prophecy", "Tapestry"
            },
            new[]
            {
                "Empires", "Conquest", "Legions", "Campaign", "Tactics", "Realms", "Stratagem", "Generals", "Provinces", "Strongholds", "Garrison", "Armada", "Dynasties", "Standards",
                "Bulwark", "Ramparts"
            },
            new[]
            {
                "Farmstead", "Township", "Tycoon", "Bakery", "Harbourside", "Orchard", "Railway", "Airfield", "Market", "Workshop", "Homestead", "Vineyard", "Depot", "Boulevard",
                "Allotment", "Shipyard"
            },
            new[]
            {
                "Eleven", "Whistle", "Kickoff", "Fixture", "Pennant", "Playoffs", "Touchline", "Scoreboard", "Trophy", "Seasons", "Crossbar", "Sidelines", "Tiebreak", "Podium", "Stadium",
                "Clubhouse"
            },
            new[]
            {
                "Circuit", "Rally", "Speedway", "Raceway", "Drifters", "Chicane", "Pitlane", "Hillclimb", "Roadsters", "Paddock", "Backroads", "Switchbacks", "Qualifier", "Chequers",
                "Autocross", "Straightaway"
            },
            new[]
            {
                "Blocks", "Maze", "Riddles", "Tiles", "Jigsaw", "Cubes", "Knots", "Lanterns", "Patterns", "Marbles", "Gears", "Mosaic", "Labyrinth", "Tangrams", "Dominoes", "Conundrum"
            },
            new[]
            {
                "Manor", "Vigil", "Cellar", "Wake", "Asylum", "Parish", "Seance", "Marsh", "Chapel", "Lullaby", "Burial", "Corridor", "Attic", "Sanatorium", "Nightfall", "Mourning"
            },
            new[]
            {
                "Squadron", "Operatives", "Breach", "Perimeter", "Standoff", "Platoon", "Incursion", "Extraction", "Ceasefire", "Dropzone", "Marksmen", "Barrage", "Salvo", "Recoil",
                "Airstrike", "Detachment"
            },
            new[]
            {
                "Poppers", "Swirl", "Jumble", "Scoops", "Sprinkles", "Bounce", "Tumble", "Gumdrops", "Shuffle", "Confetti", "Doodles", "Marshmallows", "Pinwheel", "Ribbons", "Bonbons",
                "Hopscotch"
            },
            new[]
            {
                "Picnic", "Parade", "Playtime", "Funfair", "Treehouse", "Carnival", "Campout", "Sleepover", "Menagerie", "Playground", "Jamboree", "Sandcastle", "Puppets", "Storytime",
                "Hayride", "Toybox"
            }
        };

        /// <summary>What an add-on is called after its game's name and a colon: "Ember Crown: Aftermath".</summary>
        public static readonly string[] AddOns =
        {
            "Aftermath", "New Shores", "The Long Winter", "Encore", "Second Wind", "Deep Waters", "Homecoming", "High Season", "Winding Roads", "The Far Shore", "Old Rivals",
            "Midsummer", "The Hidden Wing", "Loose Ends", "Fresh Tracks", "Borrowed Time"
        };

        /// <summary>
        /// Famous real products, games and companies, in the form names are compared in (<see cref="TitleRules.Key"/>): no made-up
        /// name is one of them, and none holds one as a word. A player's own typed name is not held to it.
        /// </summary>
        public static readonly string[] Denied =
        {
            "windows", "office", "excel", "outlook", "photoshop", "acrobat", "slack", "zoom", "teams", "notion", "dropbox", "salesforce", "oracle", "sap", "chatgpt", "gemini",
            "copilot", "siri", "alexa", "cortana", "google", "gmail", "chrome", "android", "youtube", "microsoft", "azure", "apple", "iphone", "ipad", "macos", "safari", "amazon",
            "kindle", "meta", "facebook", "instagram", "whatsapp", "messenger", "tiktok", "twitter", "snapchat", "netflix", "spotify", "uber", "airbnb", "paypal", "stripe",
            "shopify", "adobe", "nvidia", "intel", "ibm", "openai", "tesla", "samsung", "sony", "playstation", "xbox", "nintendo", "steam", "linux", "unity", "unreal", "minecraft",
            "fortnite", "roblox", "zelda", "mario", "pokemon", "tetris", "pacman", "sonic", "doom", "quake", "halo", "fifa", "madden", "callofduty", "battlefield", "counterstrike",
            "overwatch", "valorant", "apexlegends", "grandtheftauto", "gta", "reddeadredemption", "skyrim", "elderscrolls", "fallout", "eldenring", "darksouls", "residentevil",
            "silenthill", "finalfantasy", "dragonquest", "thesims", "simcity", "civilization", "ageofempires", "starcraft", "warcraft", "diablo", "leagueoflegends", "dota",
            "candycrush", "angrybirds", "amongus", "granturismo", "forza", "needforspeed", "mariokart", "streetfighter", "mortalkombat", "tekken", "assassinscreed", "farcry",
            "thewitcher", "cyberpunk", "halflife", "portal", "destiny", "animalcrossing", "stardewvalley", "rollercoastertycoon", "footballmanager", "topeleven", "ironfist",
            "goldencompass", "lostodyssey", "harvestmoon", "ghostprotocol", "midnightclub", "blackops", "deltaforce", "ghostrecon", "crossfire", "killzone", "zendesk", "freshdesk",
            "evernote", "basecamp", "workday", "trello", "asana", "jira", "github", "wordpress", "quickbooks", "autocad", "solidworks", "matlab", "tableau", "snowflake", "palantir",
            "skype", "discord", "twitch", "pinterest", "linkedin", "ebay", "alibaba", "wechat", "baidu", "tencent", "huawei", "xiaomi", "oculus", "bing", "yahoo", "firefox", "opera"
        };
    }
}
