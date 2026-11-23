using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// Hair seen in the haze of a street lamp (a player's report: the hair looked gone, the head bald, where the lamp's glow was
// behind or round the character). The game draws a glow object with each street lamp; the mod's hair finish and its own hair
// looks are blended transparent layers, and HDRP draws the transparent layers of one pass in the order of their render queue.
// What these checks hold: the arithmetic that puts a hair layer after the glow (Character/HairRules.QueueAfter, compiled here
// as it ships), that the three places that make hair layers use it once when a look is put on, that the haze has its option and
// is read when the option changes, that the hair check line reports what was applied, and the texts. That the picture is
// right is for the game to show (the test list says what to look at).
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
        Check(HairRules.PassOf(2998) == "default transparent" && HairRules.PassOf(2750) == "before refraction" && HairRules.PassOf(3400) == "low resolution transparent"
              && HairRules.PassOf(3700) == "after post-process" && HairRules.PassOf(2450) == "opaque",
            "hair fog: the pass a queue draws in is named as HDRP's ranges have it");
        for (int layer = 3000; layer <= 3003; layer++)
        {
            Check(HairRules.QueueAfter(layer, 0) == layer, "hair fog: with no glow known a hair layer at " + layer + " keeps its queue");
            Check(HairRules.QueueAfter(layer, 2750) == layer, "hair fog: a glow drawn before refraction is drawn before the hair anyway; layer " + layer + " unchanged");
            Check(HairRules.QueueAfter(layer, 3400) == layer && HairRules.QueueAfter(layer, 3700) == layer,
                "hair fog: a glow in the low resolution or after post-process pass cannot be sorted against; layer " + layer + " unchanged");
        }
        foreach (int glow in new[] { 2900, 2999, 3000, 3001, 3002, 3050, 3090 })
        {
            int last = -1;
            for (int layer = 3000; layer <= 3003; layer++)
            {
                int q = HairRules.QueueAfter(layer, glow);
                Check(q > glow && q >= layer && q <= 3099, "hair fog: with the glow at " + glow + " the layer at " + layer + " is drawn after it, inside the pass (" + q + ")");
                Check(q > last, "hair fog: with the glow at " + glow + " the layers keep their order, scalp then body then strands");
                last = q;
            }
        }
        Check(HairRules.QueueAfter(3000, 3003) == 3004 && HairRules.QueueAfter(3003, 3003) == 3007, "hair fog: the layers are drawn right after a glow at 3003, the gaps between them kept");
        Check(HairRules.QueueAfter(3003, 3099) == 3099, "hair fog: a glow at the end of the pass leaves the layers at its last queue, never out of the pass");
        Check(HairRules.QueueAfter(2998, 3003) == 2998 && HairRules.QueueAfter(2450, 3003) == 2450,
            "hair fog: the scalp underlay (2998) and an alpha tested hat (2450) are not moved");
        // A layer is always sorted from its template's queue (HairFinish.Make, ModLooks.FinishHair), never from a sorted one,
        // so what has to hold is that the result is after the glow and inside the pass.
        Check(HairRules.QueueAfter(3001, 3010) == 3012 && HairRules.QueueAfter(3001, 3010) > 3010, "hair fog: a layer sorted from its template is after the glow");
    }

    private static void HairFogSources()
    {
        string finish = ModSource(@"Character\HairFinish.cs"), look = ModSource(@"Character\HairLook.cs"), looks = ModSource(@"Character\ModLooks.cs");
        string haze = ModSource(@"Character\LampHaze.cs");
        Check(finish.Contains("finished.renderQueue = QueueAfter(layer.renderQueue, settings.AfterQueue);"), "hair fog: a finished hair layer is queued after the lamp glow when it is made");
        Check(looks.Contains("material.renderQueue = HairFinish.QueueAfter((int)RenderQueue.Transparent + order, LampHaze.GlowQueue());"),
            "hair fog: the mod's own hair looks are queued after the lamp glow when their material is made");
        Check(look.Contains("settings.AfterQueue = LampHaze.GlowQueue();"), "hair fog: the finish reads the glow's queue once for each look it puts on");
        string made = look.Substring(look.IndexOf("private static bool Finish(", StringComparison.Ordinal));
        Check(made.IndexOf("settings.AfterQueue = LampHaze.GlowQueue();", StringComparison.Ordinal) < made.IndexOf("HairFinish.Make(", StringComparison.Ordinal),
            "hair fog: the glow's queue is read before the layer is made");
        Check(look.Contains("\", fog on transparent \"") && look.Contains("\", after the lamp glow \"") && look.Contains("\"street lamp haze: \"") && look.Contains("HairFinish.PassOf(material.renderQueue)"),
            "hair fog: the hair check line reports the pass, the fog keyword, whether the layer is after the glow and what the haze option has done");
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
