using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>One kind of step a plan may name: its words, the arguments it takes and how long it may run.</summary>
    public sealed class SelfTestKind
    {
        public readonly string Name;
        public readonly int Limit;                // seconds of real time the step may take when the plan names no limit
        public readonly string[] Required;        // "name:type" with type s (words), i (whole number), n (number) or b (true or false)
        public readonly string[] Optional;
        public readonly string Words;             // what it does, in English, for the README and the results

        public SelfTestKind(string name, int limit, string words, string[] required, string[] optional)
        {
            Name = name;
            Limit = limit;
            Words = words;
            Required = required ?? Array.Empty<string>();
            Optional = optional ?? Array.Empty<string>();
        }

        /// <summary>The type letter of an argument this kind takes, or null when it takes none by that name.</summary>
        public string TypeOf(string arg)
        {
            for (int i = 0; i < Required.Length; i++)
            {
                if (Matches(Required[i], arg))
                {
                    return Required[i].Substring(arg.Length + 1);
                }
            }
            for (int i = 0; i < Optional.Length; i++)
            {
                if (Matches(Optional[i], arg))
                {
                    return Optional[i].Substring(arg.Length + 1);
                }
            }
            return null;
        }

        private static bool Matches(string spec, string arg) => spec.Length > arg.Length + 1 && spec[arg.Length] == ':' && string.CompareOrdinal(spec, 0, arg, 0, arg.Length) == 0;
    }

    /// <summary>
    /// The one table of the self-test's steps, game-free so the offline checks can read a plan against it. The game side
    /// (SelfTestSteps) has a class for each name here, and a check holds the two lists together. A developer diagnostic: it
    /// is read only when a plan file exists. COST: one array, made when first asked.
    /// </summary>
    public static class SelfTestKinds
    {
        private static readonly string[] Settle = { "settle:n" };

        public static readonly SelfTestKind[] All =
        {
            // Waiting and the clock
            new SelfTestKind("wait_menu", 240, "Waits until the main menu stands and nothing is loading.", null, null),
            new SelfTestKind("wait_city", 900, "Waits until a city stands: a save is loaded, nothing is loading, the mod's own load chain has finished, then the settle seconds more.", null, Settle),
            new SelfTestKind("wait_seconds", 60, "Waits real seconds (the limit grows with them).", new[] { "seconds:n" }, null),
            new SelfTestKind("wait_minutes", 600, "Waits until that many game minutes have passed at the game's own speed.", new[] { "minutes:n" }, null),
            new SelfTestKind("skip_time", 900, "Skips game time by the game's own time machine: a span such as 1d, 3h or 2d5h30m.", new[] { "span:s" }, null),
            new SelfTestKind("set_clock", 900,
                "Skips forward to a time of day by the game's own time machine (today, or tomorrow if it has passed; or the day named).",
                new[] { "hour:i" }, new[] { "minute:n", "day:i" }),

            // Saves and the main menu
            new SelfTestKind("load_save", 600, "Loads a save from the main menu the way the load screen does: by its name, or the newest one.", null, new[] { "save:s", "character:s" }),
            new SelfTestKind("new_game", 600, "Starts a new game from the main menu with a fixed character and seed, the tutorial off, and goes on into the city.", null,
                new[] { "name:s", "money:i", "age:i", "seed:s", "tutorial:b" }),
            new SelfTestKind("save_game", 180, "Saves the running game under that name through the game's own save, and waits until it is on disk.", new[] { "name:s" }, null),
            new SelfTestKind("return_menu", 600, "Leaves the city for the main menu as the game's funeral and pause menu do, and waits until the menu stands.", null, null),

            // The mod's options
            new SelfTestKind("set_option", 30, "Sets a mod option by its id (a toggle as 1 or 0, a slider or choice as its number); the plan's end puts it back.", new[] { "id:s", "value:s" }, null),
            new SelfTestKind("assert_option", 30, "Asserts a mod option's value.", new[] { "id:s", "value:s" }, null),

            // The interface
            new SelfTestKind("open_phone", 60, "Opens the phone's full menu.", null, null),
            new SelfTestKind("close_screen", 60, "Closes what shows: the kit screen's Back, then the full menu.", null, null),
            new SelfTestKind("open_app", 90,
                "Opens a phone app by the game's name (Contacts, BizMan, EconoView, MyEmployees, Persona, Rivals, MarketInsider, VoogleMaps) or the mod's app id.",
                new[] { "app:s" },
                new[] { "settle:n", "shot:b" }),
            new SelfTestKind("open_all_apps", 900, "Opens every app of the phone in turn, the game's and the mod's, each with a picture and the fault check, closing each.", null,
                new[] { "settle:n", "shots:b", "skip:s", "only:s" }),
            new SelfTestKind("open_screen", 120, "Opens a screen of the kit by its registry id, through the app or the way its game screen opens, and asserts the kit shows it.", new[] { "id:s" },
                new[] { "settle:n", "shot:b" }),
            new SelfTestKind("open_every_screen", 3600,
                "Walks the whole registry: opens each screen that can be opened from a running city, a picture and the fault check for each; the others are listed as skipped, with why.",
                null,
                new[] { "settle:n", "shots:b", "skip:s", "only:s" }),
            new SelfTestKind("kit_press", 60, "Presses a command of the showing kit screen with an argument, the way a click or the pad does.", new[] { "command:i" }, new[] { "arg:i", "screen:s" }),
            new SelfTestKind("kit_type", 60, "Types a text into the showing kit screen's field, the way the keyboard does.", new[] { "text:s" }, new[] { "screen:s", "submit:b" }),
            new SelfTestKind("kit_scroll", 60, "Turns the wheel over a scroll region of the showing kit screen.", new[] { "clip:i", "notches:n" }, new[] { "screen:s" }),
            new SelfTestKind("press_button", 120,
                "Presses a button of the mod's options page (Options, Tests) by the key of its label (the button argument); run an event, a gig, a session or an incident the mod has a test for.",
                new[] { "button:s" }, new[] { "settle:n" }),
            new SelfTestKind("list_buttons", 30, "Writes the keys of every button of the mod's options page into the results.", null, null),

            // The game's own debug code paths
            new SelfTestKind("console", 120,
                "Runs a command of the game's own debug console (skiptime, SetMoney, tpb, EnterBuilding, courses and the rest).",
                new[] { "command:s" }, new[] { "settle:n" }),
            new SelfTestKind("set_money", 30, "Sets the player's money through the game's own SetMoney command.", new[] { "amount:i" }, null),
            new SelfTestKind("teleport", 60,
                "Puts the player at an address (12 5th Avenue), at a business type's first building, or inside the building.",
                null, new[] { "address:s", "business:s", "inside:b" }),
            new SelfTestKind("found_business", 120, "Rents a building and starts a business of a type in it as the game's start screen does (a tech firm is a type of the mod's).", new[] { "type:s" },
                new[] { "name:s", "address:s", "settle:n" }),

            // Looking and asserting
            new SelfTestKind("screenshot", 60, "Takes a screenshot into the output folder, named by the step.", null, new[] { "name:s" }),
            new SelfTestKind("assert_no_faults", 30,
                "Asserts the mod's guards recorded no fault since the last step, no feature went off, and no kit piece fell back to the game's own screen.",
                null, new[] { "since:s" }),
            new SelfTestKind("assert_no_log_errors", 30, "Asserts the game's log took no error or exception line since the last step (which: rlo, the default, or any).", null, new[] { "which:s" }),
            new SelfTestKind("assert_kit_showing", 60, "Asserts the kit screen of that id is the one showing (id none: no kit screen shows).", new[] { "id:s" }, new[] { "wait:n" }),
            new SelfTestKind("assert_text", 60, "Asserts words are on the showing kit screen: the words of a text key, or plain words.", null, new[] { "key:s", "words:s", "screen:s" }),
            new SelfTestKind("assert_mod_loaded", 30,
                "Asserts a mod is loaded, by a part of its name as the game reports it; fails with the list when it is absent.",
                new[] { "name:s" }, new[] { "skip_if_absent:b" }),
            new SelfTestKind("assert_log_has", 90,
                "Asserts a line of the game's log written since the last step that is not an assert (or since the step of the label named by from) holds the text; "
                + "absent true asserts it does not. It waits up to wait seconds for the line.",
                new[] { "text:s" }, new[] { "from:s", "absent:b", "wait:n" }),
            new SelfTestKind("measure", 30,
                "Notes a named moment (mark) and/or records the seconds since a named moment (since) into the results under a name (as), so two builds can be compared.",
                null, new[] { "mark:s", "since:s", "as:s" }),
            new SelfTestKind("monologue", 60,
                "Puts a monologue of the game's own on screen (its speech card, the kit's monologue screen) from a text key of the game's, then waits settle seconds.",
                null, new[] { "key:s", "settle:n" }),
            new SelfTestKind("game_open", 60,
                "Opens a window of the game's own by its own call: help (the F1 help), blueprints (the pause menu's Blueprints), phone (the full menu).",
                new[] { "what:s" }, new[] { "settle:n" }),
            new SelfTestKind("assert_money", 30,
                "Asserts the player's money: atleast, atmost, and/or changed_by (within a tolerance, default 1) since the last action or the step labelled by from.",
                null, new[] { "atleast:n", "atmost:n", "changed_by:n", "within:n", "from:s" }),
            new SelfTestKind("assert_owns", 30,
                "Asserts the building at an address is the player's (owned or rented), optionally with that business name; absent true asserts it is not.",
                new[] { "address:s" }, new[] { "business:s", "absent:b" }),
            new SelfTestKind("assert_bank_line", 30,
                "Asserts the bank's lines hold a transaction whose key contains the text since the last action (or the step labelled by from): at least one, exactly count, or none with absent.",
                new[] { "key:s" }, new[] { "count:i", "absent:b", "from:s" }),
            new SelfTestKind("assert_notice", 60,
                "Asserts a notice whose text key contains the text was shown since the last action (or from the labelled step), waiting up to wait seconds; absent asserts none was. "
                + "Only notices the game keeps in the save are seen.",
                new[] { "key:s" }, new[] { "from:s", "absent:b", "wait:n" }),
            new SelfTestKind("write_wav", 30,
                "Writes a short generated tone (a wav) into a folder under RealLifeOverhaul/GigMusic of the game's data folder and nowhere else; "
                + "delete true removes it, bad true writes a file that is not a wav.",
                new[] { "path:s" }, new[] { "seconds:n", "hz:n", "delete:b", "bad:b" }),
            new SelfTestKind("set_year", 10, "Sets the length of the game's year in days for the running game (a long year, for the terms that read it).", new[] { "days:i" }, null),
            new SelfTestKind("note", 10, "Writes a line into the results.", new[] { "text:s" }, null),
        };

        private static Dictionary<string, SelfTestKind> _byName;

        /// <summary>A kind by its name, or null.</summary>
        public static SelfTestKind Find(string name)
        {
            if (_byName == null)
            {
                var map = new Dictionary<string, SelfTestKind>(All.Length, StringComparer.Ordinal);
                for (int i = 0; i < All.Length; i++)
                {
                    map[All[i].Name] = All[i];
                }
                _byName = map;
            }
            return name != null && _byName.TryGetValue(name, out SelfTestKind kind) ? kind : null;
        }
    }
}
