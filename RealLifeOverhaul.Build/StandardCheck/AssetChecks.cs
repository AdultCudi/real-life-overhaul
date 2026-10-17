using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-013: an engine asset the mod makes by code (a texture, a mesh, a material, a
// render target, a sprite, a sound, a scriptable object, a copy of a mesh or a material) is destroyed by its owner when it
// is dropped: destroying the object that shows it does not free it. A site passes when the name it is kept under is
// destroyed (Destroy, DestroyImmediate, ReleaseTemporary, a Release of its owner's, CodeAssets.DropSprite or DropProfile) in the files of its type,
// a texture passes with the sprite made of it, and the rest are the assets the mod keeps on purpose, listed in Kept with
// the reason and a piece of code that shows it: take that code away and the site fails again. An entry nothing matches
// any more fails too, so the list never outlives what it excuses. A volume profile given settings with Add is let go
// with CodeAssets.DropProfile, which destroys the settings with it.
internal static partial class Program
{
    private static readonly Regex Makes = new Regex(
        @"\bnew\s+(?:UnityEngine\.)?(?:Texture2D|Texture3D|Cubemap|Mesh|Material|RenderTexture)\s*[({]|\bSprite\.Create\s*\(|\bAudioClip\.Create\s*\(|\bRenderTexture\.GetTemporary\s*\(|\bScriptableObject\.CreateInstance\s*<|\bInstantiate\s*\(\s*\w*(?:[Mm]esh|[Mm]aterial)\w*\s*\)",
        RegexOptions.Compiled);
    private static readonly Regex KeptUnder = new Regex(@"([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*(?:\?\?=|(?<![=!<>+\-*/])=)\s*(?:\([\w.]+\)\s*)?$", RegexOptions.Compiled);
    private static readonly Regex Qualifier = new Regex(@"(?:UnityEngine\.)?(?:Object\.)?$", RegexOptions.Compiled);
    private static readonly Regex ProfileSetting = new Regex(@"\b\w*[Pp]rofile\.Add\s*<", RegexOptions.Compiled);

    // The assets kept on purpose: the file under Scripts, the name the site keeps it under ("return" for one handed
    // back), the code that shows how it is kept or freed ("Other/File.cs|code" for another file), and why.
    private static readonly (string File, string Name, string Evidence, string Why)[] Kept =
    {
        ("Careers/AcademyType.cs", "t", "ModdingAPI.RegisterModBusinessType(t)", "the academy's business type, registered with the game for the whole run"),
        ("Tech/Firms/TechTypes.cs", "t", "ModdingAPI.RegisterModBusinessType(t)", "the tech sector's business types, registered with the game for the whole run"),
        ("Economy/Dealer/DealerType.cs", "_type", "ModdingAPI.RegisterModBusinessType(_type)", "the dealership's business type, registered with the game for the whole run"),
        ("Core/ModState.cs", "modifier", "registry[type] = modifier", "a happiness modifier type in the game's registry: once a type, for the whole run"),
        ("Family/FamilyDirector.cs", "modifier", "registry[type] = modifier", "a happiness modifier type in the game's registry: once a type, for the whole run"),
        ("Travel/TravelEffects.cs", "modifier", "registry[type] = modifier", "a happiness modifier type in the game's registry: once a type, for the whole run"),
        ("Careers/CampusInterior.cs", "_signLook", "private static Material _signLook", "one material for every room sign, made again only for another font"),
        ("Character/GridLooks.cs", "made", "foreach (Sprite sprite in _swatches.Values)", "a garment's swatch, kept by the screen and destroyed with its texture when the screen is let go"),
        ("Character/HairFinish.cs", "finished", "Character/HairLook.cs|UnityEngine.Object.Destroy(layer.Made)", "a finished hair layer, handed to HairLook, which destroys it as it comes off"),
        ("Character/HairFinish.cs", "copy", "Character/HairLook.cs|foreach (Texture2D copy in Denser.Values)", "a denser strand texture, kept by HairLook for each strand texture until the scene ends"),
        ("Character/ModAssets.cs", "made", "PhotoSprites[garment] = made", "a garment photo: a sprite on the bundle's own sheet, one for each garment of the catalogue"),
        ("Character/ModLooks.cs", "material", "UnityEngine.Object.Destroy(piece.Materials[i])", "a worn piece's layer, destroyed with the piece"),
        ("Character/ModLooks.cs", "made", "foreach (Mesh mesh in SkinMeshes.Values)", "the skin under a garment, one for each garment mesh, kept until the scene ends"),
        ("Character/SkinFinish.cs", "texture", "Object.Destroy(made)", "a skin map's copy, kept in the maps and released by the sweep after each change of look"),
        ("Civic/CivicProps.cs", "mesh", "UnityEngine.Object.Destroy(kind.Mesh)", "a stand-in's merged mesh, kept for its kind and destroyed with the pool"),
        ("Core/GameUi/AppIconPainter.cs", "sprite", "CodeAssets.DropSprite(kept.Result)", "an app's icon, kept by its name for the session: one painted again destroys the one it replaces"),
        ("Core/GameUi/PhoneGridPager.cs", "sprite", "_drawnGlyph = sprite", "the More apps glyph, drawn once a session"),
        ("Core/MapPlaces.cs", "sprite", "Glyphs[index] = Draw((MapIcon)index)", "a map glyph, drawn once a session for each kind of place"),
        ("Core/PropLibrary.cs", "made", "UnityEngine.Object.Destroy(tinted)", "the props' materials, one for each kind and each tint, destroyed in Dispose"),
        ("Core/Shapes.cs", "mesh", "Object.Destroy(_post)", "the round post's and the drum's meshes, made once and destroyed in Dispose"),
        ("Districts/DistrictPanel.cs", "_drawnDisc", "private static Sprite _drawnDisc", "the swatches' disc, drawn once a session"),
        ("Events/FireEffects.cs", "clip", "Object.Destroy(_clip)", "a firework's bang, made once a city and destroyed with the fireworks"),
        ("Family/FamilyPhone.cs", "sprite", "Icons[id] = sprite", "a family contact's disc, kept for the session: the game keeps contact icons by the sprite's name"),
        ("Garage/BikeBody.cs", "mesh", "Object.Destroy(meshes[i])", "a bike's meshes, made once for each kind and destroyed in Dispose"),
        ("Jobs/JobPhone.cs", "icon", "_icon = MakeIcon()", "the job contact's icon, drawn once a session"),
        ("Law/Precincts.cs", "sprite", "private static Sprite _icon", "the precinct badge, drawn once a session"),
        ("Navigation/NavBase.cs", "picture", "UnityEngine.Object.Destroy(_kept)", "the minimap's picture of the city, destroyed when another takes its place"),
        ("Navigation/NavGraph.cs", "picture", "UnityEngine.Object.Destroy(_keptPicture)", "the minimap's picture of the roads, destroyed when another takes its place"),
        ("People/ServiceKit.cs", "_bake", "private static Mesh _bake", "one mesh the uniforms are measured with, made once a session"),
        ("Phone/Contacts/ContactPictures.cs", "Sprite", "Pictures.Add(id, p)", "a contact's picture, kept by its id for the run: the game's contact list holds the sprite"),
        ("Phone/PhoneSkin.cs", "sprite", "_frameSprite = MakeSprite(", "the phone's frame, screen and badge shapes, drawn once a session"),
        ("Photos/PhotosGame.cs", "profile", "CodeAssets.DropProfile(volume.sharedProfile)", "the camera's grade, destroyed with its settings when the grade is dropped"),
        ("Social/Flare/FlareCityCamera.cs", "target", "RenderTexture.ReleaseTemporary(_pending)", "a city photo's render target, released when its pixels are back, or by Clear if the photo is stopped"),
        ("Social/Flare/FlareIcons.cs", "s", "Made[glyph] = s", "a Flare glyph, drawn once a session for each"),
        ("Social/Flare/FlareKit.cs", "return", "_round = Make(false)", "Flare's two rounded shapes, drawn once a session"),
        ("Social/SocialPhone.cs", "_badge", "private static Sprite _badge", "the social contacts' badge, drawn once a session"),
        ("Tech/Sites/SiteProps.cs", "made", "Tech/Sites/ServerRoomView.cs|UnityEngine.Object.Destroy(_setA)", "the server room's status lights, destroyed when the room's view ends"),
        ("Traffic/TrafficEffects.cs", "clip", "Object.Destroy(_wailClip)", "the sirens' two sounds, made once a session and destroyed in Dispose"),
    };

    // Findings in folders whose clean-up another change owns: said in a note on every run until that change fixes them,
    // and an entry fails once its finding is gone, so it is taken out with the fix.
    private static readonly (string File, string Fix)[] Elsewhere =
    {
        ("Economy/Dealer/Floor/ShowroomPhotos.cs", "the showroom photo's exposure setting: CodeAssets.DropProfile(profile) in place of its Destroy(profile)"),
    };

    private static void AssetChecks()
    {
        var byRel = new Dictionary<string, SourceFile>(StringComparer.Ordinal);
        var groups = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            byRel[f.Rel] = f;
            string key = GroupKey(f.Rel);
            groups[key] = groups.TryGetValue(key, out string had) ? had + "\n" + f.CodeText : f.CodeText;
        }
        var used = new bool[Kept.Length];
        var pending = new bool[Elsewhere.Length];
        foreach (SourceFile f in Scripts)
        {
            Examine("RLO-PERF-013", f.Rel);
            string group = null;
            // Each setting a volume profile is given with Add is a ScriptableObject of its own, which destroying the profile leaves.
            if (ProfileSetting.IsMatch(f.CodeText) && groups[GroupKey(f.Rel)].IndexOf("CodeAssets.DropProfile(", StringComparison.Ordinal) < 0)
            {
                int line = f.LineAt(ProfileSetting.Match(f.CodeText).Index) + 1;
                int other = Array.FindIndex(Elsewhere, e => f.Rel.EndsWith("/" + e.File, StringComparison.Ordinal));
                if (other >= 0)
                {
                    pending[other] = true;
                    Notes.Add("RLO-PERF-013 " + f.Rel + " line " + line + ": left to its own folder's clean-up: " + Elsewhere[other].Fix);
                }
                else
                {
                    Hit("RLO-PERF-013", f.Rel, line, "a volume profile's settings are made here and never let go with CodeAssets.DropProfile");
                }
            }
            for (int i = 0; i < f.Code.Length; i++)
            {
                foreach (Match m in Makes.Matches(f.Code[i]))
                {
                    group ??= groups[GroupKey(f.Rel)];
                    string name = KeptName(f.Code[i].Substring(0, m.Index));
                    if (Freed(group, name) || (m.Value.StartsWith("new", StringComparison.Ordinal) && m.Value.Contains("Texture2D") && name != "?"
                                                && Regex.IsMatch(group, @"\bSprite\.Create\s*\(\s*" + Regex.Escape(name) + @"\b")))
                    {
                        continue;   // destroyed by name, or a texture whose sprite is the one judged
                    }
                    int kept = Excused(f.Rel, name, byRel);
                    if (kept >= 0)
                    {
                        used[kept] = true;
                        continue;
                    }
                    Hit("RLO-PERF-013", f.Rel, i + 1, (name == "?" ? "made and kept nowhere it is destroyed: " : "'" + name + "' is never destroyed: ") + f.Code[i].Trim());
                }
            }
        }
        for (int k = 0; k < Elsewhere.Length; k++)
        {
            Check(pending[k], "RLO-PERF-013 left elsewhere: " + Elsewhere[k].File + " is put right: take its entry out");
        }
        for (int k = 0; k < Kept.Length; k++)
        {
            Check(used[k], "RLO-PERF-013 kept list: " + Kept[k].File + " no longer makes '" + Kept[k].Name + "' as it says, or no longer keeps it so (" + Kept[k].Evidence
                           + "): take the entry out or put it right");
        }
    }

    // A file's type: its folder and its name up to the first dot, so that the parts of a partial class are read together.
    private static string GroupKey(string rel)
    {
        int slash = rel.LastIndexOf('/');
        string name = rel.Substring(slash + 1);
        int dot = name.IndexOf('.');
        return rel.Substring(0, slash + 1) + (dot > 0 ? name.Substring(0, dot) : name);
    }

    // The name a made asset is kept under, from the code before it on its line: "return" for one handed back, "?" for one
    // passed straight on.
    private static string KeptName(string before)
    {
        string text = Qualifier.Replace(before.TrimEnd(), string.Empty).TrimEnd();
        if (text.EndsWith("return", StringComparison.Ordinal))
        {
            return "return";
        }
        Match m = KeptUnder.Match(text);
        return m.Success ? m.Groups[1].Value : "?";
    }

    private static bool Freed(string group, string name)
    {
        if (name == "?" || name == "return")
        {
            return false;
        }
        string last = name.Substring(name.LastIndexOf('.') + 1);
        return Regex.IsMatch(group, @"\b(?:Destroy|DestroyImmediate|ReleaseTemporary|Release|DropSprite|DropProfile)\s*\([^;)]*\b" + Regex.Escape(last) + @"\b");
    }

    // The entry of Kept that excuses this site, its code still there; -1 if none.
    private static int Excused(string rel, string name, Dictionary<string, SourceFile> byRel)
    {
        string last = name.Substring(name.LastIndexOf('.') + 1);
        for (int k = 0; k < Kept.Length; k++)
        {
            if (!rel.EndsWith("/" + Kept[k].File, StringComparison.Ordinal) || Kept[k].Name != last)
            {
                continue;
            }
            string evidence = Kept[k].Evidence;
            string where = rel;
            int bar = evidence.IndexOf('|');
            if (bar > 0)
            {
                where = ScriptsDir + "/" + evidence.Substring(0, bar);
                evidence = evidence.Substring(bar + 1);
            }
            if (byRel.TryGetValue(where, out SourceFile file) && file.CodeText.IndexOf(evidence, StringComparison.Ordinal) >= 0)
            {
                return k;
            }
        }
        return -1;
    }
}
