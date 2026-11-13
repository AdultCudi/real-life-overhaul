using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>The notice's sentences in the player's language, each as its key reads, with its placeholders still in.</summary>
    public struct BuildNoticeWords
    {
        public string OldBody;    // {version} {made} {build}: an older game, and how to update it in Steam
        public string OldOff;     // {version} {made} {build}: the same where the mod is as good as off
        public string OldShort;   // {version} {made} {build}: the same in one line, for a box with little room
        public string OldParts;   // {parts}: what is off or works without on an older game
        public string Update;     // {parts}: a newer game
        public string Parts;      // {parts}: pieces missing on the build the mod is made for, or on one that could not be read
        public string Short;      // {count}: parts off, in one line
        public string More;       // {names} {more} {count}: a few names and how many more
        public string Log;        // where every one of them is named
    }

    /// <summary>The notice as it is shown.</summary>
    public struct BuildNotice
    {
        public string Body;
        public int Named;   // the parts it names
        public int Form;    // 0 the fullest, then each shorter one
    }

    /// <summary>
    /// What the notice of the game's build says (Core/GameBuild.Notice, Core/Guard.Status). It used to name every part of the
    /// mod that was off in one sentence. On Build 3670 that was every feature and some twenty smaller parts: the game's
    /// question box grew far past the screen with its heading and buttons, and the player could not use the main menu. So a
    /// list names a few and counts the rest, the notice is the fullest of a few forms that the box has room for
    /// (Core/BoxFitRules), and where nearly every feature is off it says the mod is switched off instead of listing anything.
    /// Player.log names every part, and the options page marks each feature.
    /// Game-free, checked by EconomyCheck (OldBuildChecks.cs). COST: a few short strings when the notice is shown.
    /// </summary>
    public static class BuildNoticeRules
    {
        public const int MostNamed = 3;          // parts a list names before it counts the rest
        public const float NameMost = 18f;       // font sizes: a name wider than this (32 letters) is counted, not named
        public const float OffShare = 0.75f;     // of the features: from here on the mod is said to be switched off

        /// <summary>True when so many of the features are off that the mod is as good as off: nothing is listed then.</summary>
        public static bool AsGoodAsOff(int featuresOff, int features)
        {
            return features > 0 && featuresOff >= features * OffShare;
        }

        /// <summary>
        /// The names for a sentence: all of them when they are few and short, else the first <see cref="MostNamed"/> short ones
        /// and how many more there are (<paramref name="more"/>: {names}, {more}, {count}). Where none is short, the narrowest
        /// one is named. <paramref name="named"/> is how many it names; <paramref name="cut"/>, whether any was left out.
        /// </summary>
        public static string Listed(IList<string> names, string more, out int named, out bool cut)
        {
            named = 0;
            cut = false;
            if (names == null || names.Count == 0)
            {
                return string.Empty;
            }
            var text = new StringBuilder(96);
            int narrowest = 0;
            float least = float.MaxValue;
            for (int i = 0; i < names.Count; i++)
            {
                float wide = BoxFitRules.Wide(names[i]);
                if (wide < least)
                {
                    least = wide;
                    narrowest = i;
                }
                if (wide <= NameMost && named < MostNamed)
                {
                    text.Append(named > 0 ? ", " : string.Empty).Append(names[i]);
                    named++;
                }
            }
            if (named == 0)
            {
                text.Append(names[narrowest]);
                named = 1;
            }
            if (named == names.Count)
            {
                return text.ToString();
            }
            cut = true;
            return (more ?? "{names}").Replace("{names}", text.ToString()).Replace("{more}", (names.Count - named).ToString())
                .Replace("{count}", names.Count.ToString());
        }

        /// <summary>
        /// The notice for a game of this build: the fullest form the question box has room for at this UI zoom.
        /// <paramref name="names"/> are the parts that are off, in the player's language, the most telling first;
        /// <paramref name="menuToo"/> asks for room in the full menu's box as well (in a city).
        /// </summary>
        public static BuildNotice Compose(BuildKind kind, int running, string version, IList<string> names, int featuresOff, int features,
            in BuildNoticeWords words, float zoom, bool menuToo)
        {
            int count = names != null ? names.Count : 0;
            string parts = Listed(names, words.More, out int named, out bool cut);
            var forms = new List<string>(4);
            var naming = new List<bool>(4);
            if (kind == BuildKind.Older)
            {
                string body = Builds(words.OldBody, running, version);
                if (AsGoodAsOff(featuresOff, features))
                {
                    forms.Add(Builds(words.OldOff, running, version));
                    naming.Add(false);
                }
                else
                {
                    if (count > 0)
                    {
                        string off = body + "\n\n" + (words.OldParts ?? string.Empty).Replace("{parts}", parts);
                        if (cut)
                        {
                            forms.Add(off + " " + words.Log);
                            naming.Add(true);
                        }
                        forms.Add(off);
                        naming.Add(true);
                    }
                    forms.Add(body);
                    naming.Add(false);
                }
                forms.Add(Builds(words.OldShort, running, version));
                naming.Add(false);
            }
            else
            {
                string said = ((kind == BuildKind.Newer ? words.Update : words.Parts) ?? string.Empty).Replace("{parts}", parts);
                if (cut)
                {
                    forms.Add(said + " " + words.Log);
                    naming.Add(true);
                }
                forms.Add(said);
                naming.Add(true);
                forms.Add((words.Short ?? string.Empty).Replace("{count}", count.ToString()));
                naming.Add(false);
            }
            int form = forms.Count - 1;
            for (int i = 0; i < forms.Count; i++)
            {
                if (BoxFitRules.Fits(forms[i], zoom, menuToo))
                {
                    form = i;
                    break;
                }
            }
            return new BuildNotice { Body = forms[form], Named = naming[form] ? named : 0, Form = form };
        }

        private static string Builds(string text, int running, string version)
        {
            return (text ?? string.Empty).Replace("{version}", version ?? string.Empty).Replace("{made}", GameBuild.MadeFor.ToString())
                .Replace("{build}", running.ToString());
        }
    }
}
