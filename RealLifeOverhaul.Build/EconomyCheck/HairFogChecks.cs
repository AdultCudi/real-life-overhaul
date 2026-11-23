using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// Hair seen in the haze of a street lamp (a player's report: the hair looked gone, the head bald, where the lamp's glow was
// behind or round the character). The game draws a glow object with each street lamp, and its material writes depth; the mod's
// hair finish and its own hair looks are blended transparent layers, and HDRP draws the transparent layers of one pass in the
// order of their render queue, each tested against the depth written so far. So a layer drawn after a depth writing glow that
// lies behind its near face is lost: the layers are drawn before such a glow and after one that does not write depth. What these
// checks hold: that arithmetic (Character/HairRules.QueueFor and UnderlayFor, compiled here as they ship), that the places that
// make hair layers use it once when a look is put on, that the haze has its option and is read when the option changes, that
// the hair check line reports what was applied, and the texts. That the picture is right is for the game to show.
internal static partial class Program
{
    private static void HairFog()
    {
        HairFogQueue();
        HairFogSources();
        HairFogOption();
    }

    private static void HairFogQueue()
    {
        const int W = HairRules.GlowWrites, L = HairRules.GlowLeaves, U = HairRules.GlowUnknown;
        Check(HairRules.PassOf(2998) == "default transparent" && HairRules.PassOf(2750) == "before refraction" && HairRules.PassOf(3400) == "low resolution transparent"
              && HairRules.PassOf(3700) == "after post-process" && HairRules.PassOf(2450) == "opaque",
            "hair fog: the pass a queue draws in is named as HDRP's ranges have it");
        for (int layer = 3000; layer <= 3003; layer++)
        {
            foreach (int depth in new[] { W, L, U })
            {
                Check(HairRules.QueueFor(layer, 0, depth) == layer, "hair fog: with no glow known a hair layer at " + layer + " keeps its queue");
                Check(HairRules.QueueFor(layer, 2750, depth) == layer, "hair fog: a glow drawn before refraction is drawn before the hair anyway; layer " + layer + " unchanged");
                Check(HairRules.QueueFor(layer, 3400, depth) == layer && HairRules.QueueFor(layer, 3700, depth) == layer,
                    "hair fog: a glow in the low resolution or after post-process pass cannot be sorted against; layer " + layer + " unchanged");
            }
        }
        // The game's glow as the 1.0.21 log shows it: queue 3000, writes depth. The layers go just before it, order and gaps kept.
        Check(HairRules.QueueFor(3000, 3000, W) == 2996 && HairRules.QueueFor(3001, 3000, W) == 2997 && HairRules.QueueFor(3002, 3000, W) == 2998 && HairRules.QueueFor(3003, 3000, W) == 2999,
            "hair fog: a glow at 3000 that writes depth puts the layers at 2996 to 2999, before it");
        Check(HairRules.UnderlayFor(3000, W) == 2995, "hair fog: the scalp underlay goes under all of them (2995)");
        Check(HairRules.UnderlayFor(0, U) == 2998 && HairRules.UnderlayFor(3400, W) == 2998 && HairRules.UnderlayFor(3000, L) == 2998 && HairRules.UnderlayFor(3050, W) == 2998,
            "hair fog: the underlay stays at 2998 where the layers are not moved before a glow");
        foreach (int glow in new[] { 2904, 2999, 3000, 3001, 3002, 3003, 3004, 3050, 3090 })
        {
            int last = -1;
            for (int layer = 3000; layer <= 3003; layer++)
            {
                int q = HairRules.QueueFor(layer, glow, W);
                Check(q < glow && q >= 2900 && q <= layer, "hair fog: with a depth writing glow at " + glow + " the layer at " + layer + " is drawn before it, inside the pass (" + q + ")");
                Check(q > last, "hair fog: with a depth writing glow at " + glow + " the layers keep their order");
                last = q;
                Check(HairRules.QueueFor(layer, glow, U) == q, "hair fog: an unknown flag is treated as writing depth (glow " + glow + ", layer " + layer + ")");
            }
            Check(HairRules.UnderlayFor(glow, W) <= HairRules.QueueFor(3000, glow, W) && HairRules.UnderlayFor(glow, W) >= 2900, "hair fog: the underlay is not above any layer for a glow at " + glow);
        }
        foreach (int glow in new[] { 2999, 3000, 3050, 3090 })
        {
            int last = -1;
            for (int layer = 3000; layer <= 3003; layer++)
            {
                int q = HairRules.QueueFor(layer, glow, L);
                Check(q > glow && q >= layer && q <= 3099, "hair fog: with a glow at " + glow + " that does not write depth the layer at " + layer + " is drawn after it, inside the pass (" + q + ")");
                Check(q > last, "hair fog: with a glow at " + glow + " that does not write depth the layers keep their order");
                last = q;
            }
        }
        Check(HairRules.QueueFor(3000, 3003, L) == 3004 && HairRules.QueueFor(3003, 3003, L) == 3007, "hair fog: layers go right after a glow at 3003 that does not write depth, the gaps kept");
        Check(HairRules.QueueFor(3003, 3099, L) == 3099, "hair fog: a glow at the end of the pass leaves the layers at its last queue, never out of the pass");
        Check(HairRules.QueueFor(3000, 3050, W) == 3000 && HairRules.QueueFor(3003, 3050, U) == 3003, "hair fog: layers already before a glow are not moved");
        Check(HairRules.QueueFor(2998, 3003, W) == 2998 && HairRules.QueueFor(2450, 3000, W) == 2450 && HairRules.QueueFor(2450, 3003, L) == 2450,
            "hair fog: the scalp underlay's own queue and an alpha tested hat (2450) are not moved by the layer rule");
        Check(HairRules.QueueFor(HairRules.QueueFor(3001, 3010, W), 3010, W) == HairRules.QueueFor(3001, 3010, W), "hair fog: sorting a layer twice changes nothing more");
    }

    private static void HairFogSources()
    {
        string finish = ModSource(@"Character\HairFinish.cs"), look = ModSource(@"Character\HairLook.cs"), looks = ModSource(@"Character\ModLooks.cs");
        string haze = ModSource(@"Character\LampHaze.cs");
        Check(finish.Contains("finished.renderQueue = QueueFor(layer.renderQueue, settings.GlowQueue, settings.GlowDepth);"), "hair fog: a finished hair layer is queued against the lamp glow when it is made");
        Check(looks.Contains("material.renderQueue = HairFinish.QueueFor((int)RenderQueue.Transparent + order, LampHaze.GlowQueue(), LampHaze.GlowDepth());"),
            "hair fog: the mod's own hair looks (scalp, body, wisps, beards) are queued against the lamp glow when their material is made");
        Check(looks.Contains("HairFinish.Underlay(material, colour, LampHaze.GlowQueue(), LampHaze.GlowDepth());") && finish.Contains("HairRules.UnderlayFor(glow, glowDepth)"),
            "hair fog: the scalp underlay is queued under the layers it lies beneath");
        Check(look.Contains("settings.GlowQueue = LampHaze.GlowQueue();") && look.Contains("settings.GlowDepth = LampHaze.GlowDepth();"), "hair fog: the finish reads the glow's queue and depth flag once for each look it puts on");
        string made = look.Substring(look.IndexOf("private static bool Finish(", StringComparison.Ordinal));
        Check(made.IndexOf("settings.GlowDepth = LampHaze.GlowDepth();", StringComparison.Ordinal) < made.IndexOf("HairFinish.Make(", StringComparison.Ordinal),
            "hair fog: the glow is read before the layer is made");
        Check(look.Contains("\", fog on transparent \"") && look.Contains("\"after the lamp glow\"") && look.Contains("\"before the lamp glow\"") && look.Contains("(it writes depth)")
              && look.Contains("\"street lamp haze: \"") && look.Contains("HairFinish.PassOf(material.renderQueue)"),
            "hair fog: the hair check line reports the pass, the fog keyword, whether the layer is before or after the glow and whether the glow writes depth");
        Check(haze.Contains("public static int GlowDepth()") && haze.Contains("Flag(material, ZWriteId)") && haze.Contains("Flag(material, TransparentZWriteId)"),
            "hair fog: the glow's depth flag is read from its material's _ZWrite or _TransparentZWrite");
        Check(haze.Contains("GetComponentsInChildren<Renderer>(true)") && haze.Contains("affectsVolumetric") && !haze.Contains("SetActive("),
            "hair fog: the haze is hidden by its renderers and the lights' volumetric flag, never by switching the game's glow object off, which the game does itself");
        Check(!haze.Contains("void Update") && !haze.Contains("LateUpdate") && !haze.Contains("FixedUpdate") && !haze.Contains("OnGUI"), "hair fog: the haze code has nothing that runs every frame");
        Check(haze.Contains("RendererWas[i]") && haze.Contains("LightWas[i]") && haze.Contains("FogWas[i]"), "hair fog: what the haze option changed is remembered and put back exactly");
        Check(finish.Contains("SetShaderPassEnabled(DepthPrepass, prepass)"), "hair fog: the finish keeps its depth prepass, which writes the solid core of each strand");
    }

    private static void HairFogOption()
    {
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), quality = ModSource(@"Character\CharacterQuality.cs");
        string list = ModSource(@"Meta\FeatureList.cs"), tree = ModSource(@"Meta\OptionsTree.cs");
        Check(settings.Contains("public const string LampHazeId = \"lamp_haze\";") && settings.Contains("public static bool LampHaze = true;"), "hair fog: the haze option exists and is on by default, as the game has it");
        Check(settings.Contains("LampHaze = Bool(modId, LampHazeId, LampHaze);"), "hair fog: the haze option is read from the saved settings");
        Check(mod.Contains(".AddToggle(Settings.LampHazeId, \"reallife_lamp_haze\", Settings.LampHaze,") && mod.Contains("v => Settings.LampHaze = v)"), "hair fog: the haze option is on the options page and saves what the player sets");
        Check(list.Contains("Settings.LampHazeId") && tree.Contains("\"character_hair\", \"lamp_haze\""), "hair fog: the character feature owns the haze option and the page lists it beside the hair option");
        Check(quality.Contains("Settings.LampHaze != _hazeOn") && quality.Contains("LampHaze.Apply(_hazeOn);") && quality.Contains("LampHaze.Tick();") && quality.Contains("LampHaze.Forget();"),
            "hair fog: a change of the option is applied at once, lamps that arrive later are looked for while it is off, and the scene's end puts everything back");
        string en = System.IO.File.ReadAllText(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        Dictionary<string, string> words = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(en.TrimStart('﻿'));
        string label = words.TryGetValue("reallife_lamp_haze", out string l) ? l : "", tip = words.TryGetValue("reallife_opt_tip_lamp_haze", out string t) ? t : "";
        string hair = words.TryGetValue("reallife_opt_tip_character_hair", out string h) ? h : "";
        Check(label == "Street light haze" && tip.Length > 0 && tip.Contains("hair"), "hair fog: the haze option has its label and a tip that names the hair fault");
        Check(hair.Contains("street light") && hair.Contains("Switch it off"), "hair fog: the hair option's tip says to switch it off if hair looks wrong near a street light");
        foreach (string text in new[] { label, tip, hair })
        {
            Check(text.IndexOf('—') < 0 && text.IndexOf('–') < 0 && !text.Contains(" - ") && !text.Contains("!"), "hair fog: a text has no dash and no exclamation mark: " + text);
        }
    }
}
