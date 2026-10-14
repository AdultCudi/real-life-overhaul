using System;

namespace RealLifeOverhaul
{
    /// <summary>What the running game's build is to this version of the mod.</summary>
    public enum BuildKind
    {
        Unknown,   // the game did not say
        Same,      // the build the mod is made for
        Older,     // a build before it: members the mod was compiled against may be missing
        Newer      // a later build: the game may have changed since
    }

    /// <summary>
    /// The build of Big Ambitions this version of the mod is made for, and what follows from the build the running game
    /// says it is: GameVersion.buildNumber (the game's resource Versioning/Current), the number its main menu shows and
    /// Player.log gives as "Loaded Big Ambitions (Build N)".
    ///
    /// A game older than that can lack members the mod was compiled against, and Mono cannot compile a method that names
    /// one: it throws at every call instead. Build 3675 lacked five (Helpers.FuneralHelper, Building.locatedInsidePrivateFenceIndex,
    /// BuildingResume.TargetController, SubwayStation.GetNameKey and HudConfirm.Show with isNegative), and on it the mod's
    /// apps did not open while the camera, careers and more failed every frame. Core/GameMembers and Core/GameConfirm
    /// name those five in small methods of their own; they and every other piece of the game the mod relies on are lines
    /// of the game contract (Core/GameContract), and Core/Guard says what this game lacks and switches off only what
    /// stands on it; the notice in GameBuild.Notice.cs tells the player what to do.
    ///
    /// Game-free, checked offline (ContractCheck), which also holds <see cref="MadeFor"/> to the build of the game
    /// installed for building the mod.
    /// </summary>
    public static partial class GameBuild
    {
        /// <summary>
        /// The build the mod is compiled against and checked in, the one build number of the mod. ContractCheck reads the
        /// build of the installed game the mod is built against and fails while it is another one: after a game update,
        /// build and check the mod against it (README, "After a game update"), then change this number.
        /// </summary>
        public const int MadeFor = 3682;

        /// <summary>The way to bring the game up to date in Steam, as Player.log gives it (the notice says it in the player's language).</summary>
        public const string SteamSteps = "in Steam, Library, Big Ambitions, Properties, Betas set to None, then Installed Files, Verify integrity of game files";

        public static BuildKind Of(int running)
        {
            if (running <= 0)
            {
                return BuildKind.Unknown;
            }
            return running == MadeFor ? BuildKind.Same : running < MadeFor ? BuildKind.Older : BuildKind.Newer;
        }

        /// <summary>The words "This game is Build N; Real Life Overhaul V is made for Build M." that end every line of the build check.</summary>
        public static string Builds(int running, string version)
        {
            return (running > 0 ? "This game is Build " + running : "This game's build could not be read") + "; Real Life Overhaul " + version
                   + " is made for Build " + MadeFor + ".";
        }

        /// <summary>
        /// The line for Player.log at game start: a warning for an older or a newer game and for one whose build could
        /// not be read; null for the build the mod is made for (the check line of the developer diagnostics says that).
        /// </summary>
        public static string Line(int running, string version)
        {
            switch (Of(running))
            {
                case BuildKind.Older:
                    return "Game build: this game is Build " + running + ", older than Build " + MadeFor + " that Real Life Overhaul " + version
                           + " is made for. Parts of the mod that need what this game lacks stand down, and some apps and features will not work. To update the game, "
                           + SteamSteps + ".";
                case BuildKind.Newer:
                    return "Game build: this game is Build " + running + ", newer than Build " + MadeFor + " that Real Life Overhaul " + version + " is made for.";
                case BuildKind.Unknown:
                    return "Game build: the game's build could not be read; Real Life Overhaul " + version + " is made for Build " + MadeFor + ".";
                default:
                    return null;
            }
        }

        /// <summary>
        /// The member a missing member's exception names, as Mono writes them: "Method not found: void .HudConfirm.Show(string,bool)"
        /// gives "HudConfirm.Show(string,bool)", "...get_TargetController()" the property's name, "Field not found: int
        /// Buildings.Building.locatedInsidePrivateFenceIndex Due to: ..." the field's, and "Could not resolve type with token 010002a5
        /// from typeref (expected class 'Helpers.FuneralHelper' in assembly ...)" the type's. Any other message is given as it is,
        /// cut to a line.
        /// </summary>
        public static string MemberOf(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "a member it does not name";
            }
            string quoted = Between(message, "expected class '", "'") ?? Between(message, "Could not load type '", "'");
            if (quoted != null)
            {
                return quoted;
            }
            string rest = After(message, "Method not found: ") ?? After(message, "Field not found: ");
            if (rest != null)
            {
                int due = rest.IndexOf(" Due to:", StringComparison.Ordinal);
                if (due >= 0)
                {
                    rest = rest.Substring(0, due);
                }
                rest = rest.Trim().TrimEnd('.').Trim('\'');
                int bracket = rest.IndexOf('(');
                int space = bracket >= 0 ? rest.LastIndexOf(' ', bracket) : rest.LastIndexOf(' ');
                string member = (space >= 0 ? rest.Substring(space + 1) : rest).TrimStart('.');
                // A property's getter or setter is named as the property.
                int get = member.IndexOf(".get_", StringComparison.Ordinal);
                int set = member.IndexOf(".set_", StringComparison.Ordinal);
                int accessor = get >= 0 ? get : set;
                if (accessor >= 0 && member.EndsWith(")", StringComparison.Ordinal))
                {
                    int open = member.IndexOf('(', accessor);
                    member = member.Substring(0, accessor + 1) + member.Substring(accessor + 5, open - accessor - 5);
                }
                return member.Length > 0 ? member : rest;
            }
            string line = message.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return line.Length > 160 ? line.Substring(0, 160) : line;
        }

        private static string Between(string text, string start, string end)
        {
            int from = text.IndexOf(start, StringComparison.Ordinal);
            if (from < 0)
            {
                return null;
            }
            from += start.Length;
            int to = text.IndexOf(end, from, StringComparison.Ordinal);
            return to > from ? text.Substring(from, to - from) : null;
        }

        private static string After(string text, string start)
        {
            int from = text.IndexOf(start, StringComparison.Ordinal);
            return from < 0 ? null : text.Substring(from + start.Length);
        }
    }
}
