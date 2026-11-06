using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul;

// Checks for the hair finish's search behind the black screen, compiled here as it ships:
//   Character/HairWearers.cs   whose a drawn renderer of strand hair is: its nearest character if that one wears it, else
//                              the wearer (or nobody) a search of every character found, kept by the renderer's id
//
// On a model of a scene: characters switched on and off, dressed again (their merged mesh made anew), destroyed; renderers
// under their wearer, under a character that does not wear them (a piece the mod hangs on the player's double), under
// nobody (a wig on a stand), or worn by a character they are not under (a merged mesh made elsewhere); scenes changed.
// At every door the search as CharacterQuality.FindOthers makes it must find the very characters the search of every
// character found (the one made at every door up to 0.55.0), and make that search only for a renderer new to it.
// Over the sources: the door's search goes through the engine's list of skinned meshes, the search of every character
// is left only where nothing else can place a renderer, and both ask the one same question of a renderer.
internal static partial class Program
{
    // A character of the model: its object switched on or off, there or destroyed, and the renderers its hair is on.
    private sealed class WornModel
    {
        public int Id;
        public bool On = true;
        public bool Alive = true;
        public readonly List<int> Worn = new List<int>(4);
    }

    // A skinned mesh of the model: drawn in strand hair or not, and the nearest character above it (null: none).
    private sealed class DrawnModel
    {
        public int Id;
        public bool Hair = true;
        public bool Alive = true;
        public WornModel Under;
    }

    private static bool ModelWears(WornModel character, int renderer)
    {
        return character != null && character.Alive && character.Worn.Contains(renderer);
    }

    // The search made at every door up to 0.55.0: every character switched on, kept if a drawn renderer of strand hair is one it wears.
    private static HashSet<int> EveryCharacter(List<WornModel> characters, List<DrawnModel> drawn)
    {
        var found = new HashSet<int>();
        foreach (WornModel c in characters)
        {
            if (!c.Alive || !c.On)
            {
                continue;
            }
            foreach (DrawnModel r in drawn)
            {
                if (r.Alive && r.Hair && Drawn(r) && c.Worn.Contains(r.Id))
                {
                    found.Add(c.Id);
                }
            }
        }
        return found;
    }

    // A renderer is drawn only with every object above it switched on: its nearest character's among them.
    private static bool Drawn(DrawnModel r)
    {
        return r.Under == null || (r.Under.Alive && r.Under.On);
    }

    // The search as CharacterQuality.FindOthers makes it: from each drawn renderer of strand hair to its wearer; the
    // search of every character, once, when one cannot be placed, which then learns the wearer of each it could not.
    private static HashSet<int> FromTheHair(List<WornModel> characters, List<DrawnModel> drawn, HairWearers<WornModel> wearers, ref int whole)
    {
        var found = new HashSet<int>();
        var unplaced = new List<DrawnModel>();
        foreach (DrawnModel r in drawn)
        {
            if (!r.Alive || !r.Hair || !Drawn(r))
            {
                continue;   // what the engine's list of skinned meshes switched on, and DrawsStrandHair, leave out
            }
            WornModel wearer = wearers.Wearer(r.Id, r.Under != null && r.Under.Alive ? r.Under : null, out bool unknown);
            if (unknown)
            {
                unplaced.Add(r);
            }
            else if (wearer != null && wearer.On)
            {
                found.Add(wearer.Id);
            }
        }
        if (unplaced.Count > 0)
        {
            whole++;
            found.UnionWith(EveryCharacter(characters, drawn));
            foreach (DrawnModel r in unplaced)
            {
                WornModel wearer = null;
                foreach (WornModel c in characters)
                {
                    if (wearer == null && ModelWears(c, r.Id))
                    {
                        wearer = c;   // switched off or on: asked of everyone the search returns with switched-off objects
                    }
                }
                wearers.Learn(r.Id, wearer);
            }
        }
        return found;
    }

    private static void HairWearersChecks()
    {
        HairWearersPlaces();
        HairWearersScenes();
        HairWearersSources();
    }

    private static HairWearers<WornModel> NewWearers()
    {
        return new HairWearers<WornModel>(ModelWears);
    }

    // One question at a time: added, destroyed, switched off and on again, dressed again, a scene changed, the cap.
    private static void HairWearersPlaces()
    {
        HairWearers<WornModel> wearers = NewWearers();
        var player = new WornModel { Id = 1 };
        player.Worn.Add(100);
        Check(wearers.Wearer(100, player, out bool unknown) == player && !unknown && wearers.Count == 0,
            "hair wearers: a renderer under the character that wears it is that character's, with nothing learned and no whole search");

        Check(wearers.Wearer(101, player, out unknown) == null && unknown,
            "hair wearers: a piece under a character that does not wear it (the mod's own on the double) needs the whole search once");
        wearers.Learn(101, null);
        Check(wearers.Wearer(101, player, out unknown) == null && !unknown,
            "hair wearers: a renderer the whole search found nobody wears is nobody's at the next door, with no whole search");

        Check(wearers.Wearer(200, null, out unknown) == null && unknown, "hair wearers: a renderer under no character (a wig on a stand) is unknown at first");
        wearers.Learn(200, null);
        Check(wearers.Wearer(200, null, out unknown) == null && !unknown, "hair wearers: ...and nobody's after the whole search");

        var shopper = new WornModel { Id = 2 };
        shopper.Worn.Add(300);
        Check(wearers.Wearer(300, null, out unknown) == null && unknown, "hair wearers: a merged mesh made away from its character is unknown at first");
        wearers.Learn(300, shopper);
        Check(wearers.Wearer(300, null, out unknown) == shopper && !unknown, "hair wearers: ...and its wearer's after the whole search, at every later door");

        shopper.On = false;
        shopper.On = true;
        Check(wearers.Wearer(300, null, out unknown) == shopper && !unknown, "hair wearers: a wearer switched off and on again is still the wearer (a learned answer is kept)");

        shopper.Worn.Clear();
        shopper.Worn.Add(301);
        Check(wearers.Wearer(300, null, out unknown) == null && unknown, "hair wearers: a wearer dressed again (its merged mesh made anew) is no answer for the old renderer");
        Check(wearers.Count == 2, "hair wearers: ...and the old answer is let go (" + wearers.Count + " known)");

        wearers.Learn(301, shopper);
        shopper.Alive = false;
        Check(wearers.Wearer(301, null, out unknown) == null && unknown, "hair wearers: a destroyed wearer is no answer: the whole search is asked again");
        Check(wearers.Wearer(100, null, out unknown) == null && unknown, "hair wearers: a nearest character that is not given (destroyed) places nothing");

        wearers.Clear();
        Check(wearers.Count == 0 && wearers.Wearer(200, null, out unknown) == null && unknown, "hair wearers: a new scene keeps nothing learned");

        for (int i = 0; i < HairWearers<WornModel>.Most; i++)
        {
            wearers.Learn(10000 + i, null);
        }
        wearers.Learn(10000, player);
        Check(wearers.Count == HairWearers<WornModel>.Most, "hair wearers: learning again what is known keeps the count at the cap (" + wearers.Count + ")");
        wearers.Learn(99999, null);
        Check(wearers.Count == 1 && wearers.Wearer(99999, null, out unknown) == null && !unknown,
            "hair wearers: past the cap of " + HairWearers<WornModel>.Most + " everything is let go and only the newest kept (" + wearers.Count + ")");

        bool threw = false;
        try
        {
            new HairWearers<WornModel>(null);
        }
        catch (ArgumentNullException)
        {
            threw = true;
        }
        Check(threw, "hair wearers: made without the question it asks of a character, it says so at once");

        Stopwatch watch = Stopwatch.StartNew();
        var many = NewWearers();
        int placed = 0;
        for (int i = 0; i < 200000; i++)
        {
            placed += many.Wearer(100, player, out unknown) == player ? 1 : 0;
        }
        watch.Stop();
        Check(placed == 200000 && watch.Elapsed.TotalMilliseconds < 400.0,
            "hair wearers: 200,000 renderers placed in " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms (a door has a few hundred skinned meshes; 400 ms allowed)");
    }

    // Many doors on a changing scene: at each one the same characters as the search of every character, and that search
    // made only when a renderer new to the search is drawn.
    private static void HairWearersScenes()
    {
        var random = new Random(20261026);
        int doors = 0, differ = 0, whole = 0, wholeWanted = 0, scenes = 0, foundAny = 0;
        int nextId = 1;
        string first = null;
        for (int scene = 0; scene < 40; scene++)
        {
            scenes++;
            var characters = new List<WornModel>();
            var drawn = new List<DrawnModel>();
            HairWearers<WornModel> wearers = NewWearers();   // the search's keeper goes with its scene (CharacterQuality per city)
            var seen = new HashSet<int>();
            for (int step = 0; step < 60; step++)
            {
                int what = random.Next(10);
                if (what <= 2 || characters.Count == 0)
                {
                    // Someone new: hair under them, maybe a piece of the mod's on them, maybe a merged mesh made away from them.
                    var c = new WornModel { Id = nextId++, On = random.Next(5) > 0 };
                    characters.Add(c);
                    var hair = new DrawnModel { Id = nextId++, Under = c, Hair = random.Next(4) > 0 };
                    c.Worn.Add(hair.Id);
                    drawn.Add(hair);
                    if (random.Next(4) == 0)
                    {
                        drawn.Add(new DrawnModel { Id = nextId++, Under = c });   // not worn: the scalp under the double's own hair
                    }
                    if (random.Next(6) == 0)
                    {
                        var away = new DrawnModel { Id = nextId++, Under = null };
                        c.Worn.Add(away.Id);
                        drawn.Add(away);
                    }
                }
                else if (what == 3)
                {
                    drawn.Add(new DrawnModel { Id = nextId++, Under = random.Next(2) == 0 ? null : characters[random.Next(characters.Count)] });   // a wig on a stand, or a hat
                }
                else if (what == 4)
                {
                    WornModel c = characters[random.Next(characters.Count)];
                    c.On = !c.On;
                }
                else if (what == 5)
                {
                    WornModel c = characters[random.Next(characters.Count)];
                    c.Alive = false;
                    foreach (DrawnModel r in drawn)
                    {
                        if (r.Under == c || c.Worn.Contains(r.Id))
                        {
                            r.Alive = false;   // its pieces and its merged mesh go with it
                        }
                    }
                }
                else if (what == 6)
                {
                    // Dressed again: the merged mesh made anew, the old one destroyed; the new one under it or made away from it.
                    WornModel c = characters[random.Next(characters.Count)];
                    if (c.Alive && c.Worn.Count > 0)
                    {
                        int gone = c.Worn[c.Worn.Count - 1];
                        foreach (DrawnModel r in drawn)
                        {
                            if (r.Id == gone)
                            {
                                r.Alive = false;
                            }
                        }
                        c.Worn.RemoveAt(c.Worn.Count - 1);
                        var made = new DrawnModel { Id = nextId++, Under = random.Next(3) == 0 ? null : c, Hair = random.Next(4) > 0 };
                        c.Worn.Add(made.Id);
                        drawn.Add(made);
                    }
                }
                else if (what == 7)
                {
                    DrawnModel r = drawn[random.Next(drawn.Count)];
                    r.Hair = !r.Hair;   // a hat on, or hair the game's material puts back
                }

                // A door.
                doors++;
                bool fresh = false;
                foreach (DrawnModel r in drawn)
                {
                    if (r.Alive && r.Hair && Drawn(r) && !seen.Contains(r.Id) && !(r.Under != null && r.Under.Worn.Contains(r.Id)))
                    {
                        fresh = true;   // a drawn renderer neither its nearest character wears nor the search has met
                    }
                }
                int before = whole;
                HashSet<int> old = EveryCharacter(characters, drawn);
                HashSet<int> now = FromTheHair(characters, drawn, wearers, ref whole);
                foreach (DrawnModel r in drawn)
                {
                    if (r.Alive && r.Hair && Drawn(r))
                    {
                        seen.Add(r.Id);
                    }
                }
                wholeWanted += fresh ? 1 : 0;
                foundAny += old.Count > 0 ? 1 : 0;
                if (!old.SetEquals(now) || (whole > before && !fresh))
                {
                    differ++;
                    first ??= "scene " + scene + " step " + step + ": the search of every character " + old.Count + ", from the hair " + now.Count
                              + (whole > before && !fresh ? ", and a whole search made with nothing new" : "");
                }
            }
        }
        Check(differ == 0, "hair wearers: on " + doors + " doors in " + scenes + " changing scenes the search from the hair finds the very characters the search of every character found"
                           + (first != null ? " (first difference: " + first + ")" : ""));
        Check(foundAny > doors / 2, "hair wearers: the scenes put characters in strand hair at most doors (" + foundAny + " of " + doors + ")");
        Check(whole <= wholeWanted && whole < doors / 3,
            "hair wearers: the search of every character made at " + whole + " of " + doors + " doors, only where a drawn renderer was new to the search (" + wholeWanted + ")");
        Console.WriteLine("hair wearers: " + doors + " doors in " + scenes + " scenes: the search of every character made at " + whole
                          + " (up to 0.55.0: at every one, twice), the same characters found at each");
    }

    // Over the sources: what the door's search is made of.
    private static void HairWearersSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string quality = Source(scripts, @"Character\CharacterQuality.cs");
        string look = Source(scripts, @"Character\HairLook.cs");
        string find = Body(quality, "private void FindOthers()");
        Check(find.Contains("SceneFinds.All<SkinnedMeshRenderer>(DrawnSearch)") && !find.Contains("SceneFinds.All<AppearanceSetter>")
              && find.Contains("FindOthersEverywhere();") && find.Contains("_unplaced.Count > 0"),
            "hair wearers: the door's search goes through the engine's list of skinned meshes, and the search of every character only for a renderer it cannot place");
        string everywhere = Body(quality, "private void FindOthersEverywhere()");
        Check(everywhere.Contains("SceneFinds.All<AppearanceSetter>(WholeSearch, FindObjectsInactive.Include)") && everywhere.Contains("activeInHierarchy")
              && everywhere.Contains("_wearers.Learn("),
            "hair wearers: the search of every character looks at those switched on, as before, and teaches each unplaced renderer its wearer");
        string wearer = Body(quality, "private AppearanceSetter DrawnWearer(");
        Check(wearer.Contains("HairLook.DrawsStrandHair(renderer)") && wearer.Contains("GetComponentInParent<AppearanceSetter>(true)") && wearer.Contains("activeInHierarchy"),
            "hair wearers: a drawn renderer is asked the finish's own question, placed from its nearest character, and a switched-off wearer is none");
        string wears = Body(look, "public static bool WearsStrandHair(");
        Check(wears.Contains("DrawsStrandHair(renderers[i])") && !wears.Contains("GetSharedMaterials"),
            "hair wearers: a character wears strand hair by the very question the door's search asks of each renderer (HairLook.DrawsStrandHair)");
        string worn = Body(look, "private static List<SkinnedMeshRenderer> WornBy(");
        string tells = Body(look, "public static bool Wears(");
        Check(worn.Contains("GetSelectedMeshRenderers()") && worn.Contains("lastMergedMeshRenderer") && tells.Contains("GetSelectedMeshRenderers()")
              && tells.Contains("character.skipMeshMerging ? null : character.lastMergedMeshRenderer"),
            "hair wearers: whose a renderer is, is asked of the same pieces WornBy gives the finish (the worn pieces and the merged mesh)");
        Check(Body(quality, "private void Stop()").Contains("_wearers.Clear();"), "hair wearers: nothing learned is kept once the finish stops");
    }
}
