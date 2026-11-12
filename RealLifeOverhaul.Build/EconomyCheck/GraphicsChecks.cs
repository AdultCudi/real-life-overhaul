using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Gfx;

// Realistic graphics (Scripts/Graphics): the table of every setting a look may write (in the order of its enum, each with
// its bounds), the Free choice held to sliders of work the game's pipeline already does in every frame and against a list
// of the switches that would start more, nothing beyond the chosen cost, every setting of every look finite and inside
// its bounds for every game minute of the day in every weather and season, nothing jumping from one minute or one
// twentieth of the weather to the next, a grey card followed through the game's picture and through each look's (the
// game's brightness in fair weather, in the sun and in the shade, a little darker in rain, a night that stays readable),
// the looks' curves, what each look does to the light, the measurement's arithmetic and verdicts, the rules' own cost,
// the sources (nothing run per frame, every slider written through the table, everything given back, every game event
// behind the guard), the Guide's figures and the texts.
internal static partial class Program
{
    private const int GfxKnobCount = (int)GraphicsKnob.Count;
    private const int GfxDay = 24 * GraphicsRules.StepsPerHour;   // looks in a day
    private const int GfxTwentieths = GraphicsRules.WeatherSteps;

    private static readonly CultureInfo GfxInv = CultureInfo.InvariantCulture;
    private static readonly Regex GfxNames = new Regex(@"\{[a-z_]+\}");
    private static readonly int[] GfxLooks = { GraphicsLook.Natural, GraphicsLook.Filmic, GraphicsLook.Vivid };
    private static readonly float[] GfxContrasts = { 0f, 10f };   // the Picture slider as the game has it, and as the mod sets it

    // Skies as twentieths of rain, of falling snow and of lying snow; the first is fair weather.
    private static readonly int[][] GfxSkies =
    {
        new[] { 0, 0, 0 }, new[] { 10, 0, 0 }, new[] { 20, 0, 0 }, new[] { 0, 20, 0 }, new[] { 0, 0, 20 }, new[] { 0, 20, 20 }, new[] { 20, 20, 20 }, new[] { 20, 0, 20 }, new[] { 7, 13, 9 }
    };

    // The pipeline's settings whose work the game already does in every frame: its colour table is built each frame from
    // the tone curve, the white balance, the channel mixer and the split toning whatever they are set to; its exposure,
    // its bloom and its fog are on in the game's own profiles; the sky's light is a multiplier in every lit pixel; the
    // sun and the moon are lit. A slider of one of these starts nothing.
    private static readonly string[] GfxAlreadyDone =
    {
        "Tonemapping", "Exposure", "WhiteBalance", "ChannelMixer", "SplitToning", "Bloom", "Fog", "IndirectLightingController", "SunLight", "MoonLight"
    };

    // The switches and sliders that start a pass, a buffer, a texture or more samples, as setting.slider ("*": any slider
    // of that setting). No Free row may be one of these; every row the table calls costly must be.
    private static readonly string[] GfxStarts =
    {
        "ContactShadows.*", "MicroShadowing.*", "HBAO.*", "AmbientOcclusion.*", "ScreenSpaceAmbientOcclusion.*", "ScreenSpaceReflection.*", "ScreenSpaceRefraction.*",
        "GlobalIllumination.*", "VolumetricClouds.*", "CloudLayer.*", "DepthOfField.*", "MotionBlur.*", "FilmGrain.*", "ChromaticAberration.*", "Vignette.*", "LensDistortion.*",
        "PaniniProjection.*", "PathTracing.*", "RecursiveRendering.*", "LightCluster.*", "RayTracingSettings.*", "SubSurfaceScattering.*", "HDShadowSettings.*",
        "VisualEnvironment.*", "GradientSky.*", "HDRISky.*", "PhysicallyBasedSky.*", "LiftGammaGain.*", "ColorAdjustments.*", "ColorCurves.*", "ShadowsMidtonesHighlights.*",
        "Tonemapping.lutTexture", "Tonemapping.lutContribution", "Tonemapping.neutralHDRRangeReductionMode", "Tonemapping.fallbackMode",
        "Exposure.mode", "Exposure.meteringMode", "Exposure.adaptationMode", "Exposure.fixedExposure", "Exposure.limitMin", "Exposure.limitMax", "Exposure.curveMap",
        "Bloom.intensity", "Bloom.quality", "Bloom.resolution", "Bloom.highQualityPrefiltering", "Bloom.highQualityFiltering", "Bloom.anamorphic", "Bloom.dirtTexture",
        "Bloom.dirtIntensity", "Bloom.tint",
        "Fog.enabled", "Fog.enableVolumetricFog", "Fog.quality", "Fog.fogControlMode", "Fog.volumetricFogBudget", "Fog.resolutionDepthRatio", "Fog.screenResolutionPercentage",
        "Fog.volumeSliceCount", "Fog.depthExtent", "Fog.denoisingMode", "Fog.directionalLightsOnly", "Fog.colorMode", "Fog.maxFogDistance", "Fog.mipFogMaxMip",
        "SunLight.shadows", "SunLight.useContactShadow", "SunLight.shadowResolution", "SunLight.intensity", "SunLight.colorTemperature", "SunLight.enabled",
        "MoonLight.shadows", "MoonLight.useContactShadow", "MoonLight.shadowResolution", "MoonLight.intensity", "MoonLight.colorTemperature", "MoonLight.enabled",
    };

    // What may run in a frame, or hang work on one: none of it may be in the picture's files.
    private static readonly string[] GfxPerFrame =
    {
        "void Update(", "void LateUpdate(", "void FixedUpdate(", "void OnUpdate(", "void OnLateUpdate(", "void OnFixedUpdate(", "void OnGUI(", "void OnPreRender(", "void OnPreCull(",
        "void OnPostRender(", "void OnRenderObject(", "void OnWillRenderObject(", "void OnRenderImage(", "RenderPipelineManager.", "onBeforeRender", "Camera.onPre", "Camera.onPost",
        "InvokeRepeating", "yield return null", "yield return new", "WaitForEndOfFrame", "WaitForFixedUpdate", "HotSpots.Time(", "CustomPass", "ScenePrewarm.", "SceneFinds.",
        "FindObjectOfType", "FindObjectsOfType", "FindObjectsByType", "GameObject.Find", "Resources.FindObjectsOfTypeAll",
    };

    private static void Graphics()
    {
        GraphicsKnobTable();
        GraphicsFreeRows();
        GraphicsCostTiers();
        GraphicsEveryMoment();
        GraphicsNoJumps();
        GraphicsGreyCard();
        GraphicsCurves();
        GraphicsLight();
        GraphicsMoments();
        GraphicsVerdicts();
        GraphicsRulesCost();
        GraphicsNothingPerFrame();
        GraphicsWrites();
        GraphicsGivesBack();
        GraphicsRegistered();
        GraphicsTexts();
    }

    private static void GfxWork(int look, int cost, int place, int step, int[] sky, int season, float[] values, bool[] set)
    {
        var at = new GraphicsMoment { Look = look, Cost = cost, Place = place, HourStep = step, Rain = sky[0], Snow = sky[1], Cover = sky[2], Season = season };
        GraphicsRules.Evaluate(in at, values, set);
    }

    private static float GfxHour(int step) => step / (float)GraphicsRules.StepsPerHour;

    private static int GfxStep(float hour) => (int)Math.Round(hour * GraphicsRules.StepsPerHour);

    private static string GfxClock(int step)
    {
        int minutes = step * 60 / GraphicsRules.StepsPerHour;
        return (minutes / 60).ToString("00", GfxInv) + ":" + (minutes % 60).ToString("00", GfxInv);
    }

    private static string GfxWhere(int k, int look, int step, int[] sky, int season)
    {
        return GraphicsRules.Knobs[k].Knob + " in " + GraphicsRules.LookName(look) + " at " + GfxClock(step) + ", season " + season + ", rain " + sky[0] + " snow " + sky[1] + " lying " + sky[2];
    }

    private static string GfxPercent(float value) => value.ToString("0.0", GfxInv);

    private static bool GfxNear(float a, float b, float within) => Math.Abs(a - b) <= within;

    private static bool GfxStartsWork(string setting, string slider)
    {
        foreach (string entry in GfxStarts)
        {
            int dot = entry.IndexOf('.');
            if (entry.Substring(0, dot) == setting && (entry.Substring(dot + 1) == "*" || entry.Substring(dot + 1) == slider))
            {
                return true;
            }
        }
        return false;
    }

    // A value on its bound is one the table held back, unless the bound is the setting's own end: a switch, the largest
    // part of a light's filter (1, so that no light is made brighter), the finest quality.
    private static bool GfxHeldBack(int k, float value)
    {
        KnobInfo info = GraphicsRules.Knobs[k];
        if (info.Least == info.Most)
        {
            return value != info.Least;
        }
        bool ownEnd = (k >= (int)GraphicsKnob.SunRed && k <= (int)GraphicsKnob.MoonBlue) || k == (int)GraphicsKnob.ContactQuality;
        return value < info.Least || value > info.Most || (!ownEnd && (value <= info.Least || value >= info.Most));
    }

    // ---------------------------------------------------------------- the table

    private static void GraphicsKnobTable()
    {
        KnobInfo[] knobs = GraphicsRules.Knobs;
        bool order = knobs.Length == GfxKnobCount, sound = true, split = true, threes = true;
        string first = null;
        int costly = -1;
        for (int k = 0; k < knobs.Length; k++)
        {
            KnobInfo info = knobs[k];
            order &= (int)info.Knob == k;
            bool ok = !string.IsNullOrEmpty(info.Setting) && !string.IsNullOrEmpty(info.Slider) && info.Cost >= GraphicsCost.Free && info.Cost < GraphicsCost.Count
                      && float.IsFinite(info.Least) && float.IsFinite(info.Most) && float.IsFinite(info.Step) && info.Least <= info.Most && info.Step >= 0f
                      && (info.Step == 0f || info.Step < (info.Most - info.Least) / 10f);
            if (!ok)
            {
                sound = false;
                first = first ?? info.Knob.ToString();
            }
            if (info.Cost > GraphicsCost.Free && costly < 0)
            {
                costly = k;
            }
            split &= costly < 0 || info.Cost > GraphicsCost.Free;
        }
        foreach (GraphicsKnob red in new[] { GraphicsKnob.ShadowsRed, GraphicsKnob.HighlightsRed, GraphicsKnob.FogTintRed, GraphicsKnob.SunRed, GraphicsKnob.MoonRed })
        {
            for (int part = 1; order && part < 3; part++)
            {
                KnobInfo a = knobs[(int)red], b = knobs[(int)red + part];
                threes &= a.Setting == b.Setting && a.Slider == b.Slider && a.Cost == b.Cost && a.Least == b.Least && a.Most == b.Most && a.Step == b.Step;
            }
        }
        Check(order, "graphics: the table of settings has one row for each knob, in the enum's order (" + knobs.Length + " rows, " + GfxKnobCount + " knobs)");
        Check(sound, "graphics: every row names its setting and its slider, has a cost of the three, bounds in order and a step under a tenth of its range" + (first != null ? " (" + first + ")" : string.Empty));
        Check(split && costly == (int)GraphicsKnob.ContactShadows,
            "graphics: the Free rows come first and the costly rows begin at the contact shadows, which is where the game side makes its extra settings");
        Check(threes, "graphics: the three parts of a colour (a tint, a light's filter) are three rows of one slider with one set of bounds");
    }

    private static void GraphicsFreeRows()
    {
        var notDone = new List<string>();
        var starts = new List<string>();
        var unknown = new List<string>();
        int free = 0;
        foreach (KnobInfo info in GraphicsRules.Knobs)
        {
            string name = info.Setting + "." + info.Slider;
            if (info.Cost == GraphicsCost.Free)
            {
                free++;
                if (Array.IndexOf(GfxAlreadyDone, info.Setting) < 0)
                {
                    notDone.Add(name);
                }
                if (GfxStartsWork(info.Setting, info.Slider))
                {
                    starts.Add(name);
                }
            }
            else if (!GfxStartsWork(info.Setting, info.Slider))
            {
                unknown.Add(name);
            }
        }
        Check(free >= 30 && notDone.Count == 0, "graphics: each of the Free choice's " + free + " rows is a slider of work the game's pipeline already does in every frame: its colour table, "
                                                + "exposure, bloom, fog, sky light, sun and moon (" + (notDone.Count == 0 ? "all" : string.Join(", ", notDone)) + ")");
        Check(starts.Count == 0, "graphics: no Free row is a switch or a slider that starts a pass, a buffer, a texture or more samples (the " + GfxStarts.Length + " listed in GraphicsChecks.cs"
                                 + (starts.Count == 0 ? string.Empty : "; " + string.Join(", ", starts)) + ")");
        Check(unknown.Count == 0 && GfxStartsWork("ContactShadows", "enable") && !GfxStartsWork("Bloom", "threshold") && !GfxStartsWork("Fog", "meanFreePath"),
            "graphics: every row the table calls costly is on that list, so the list knows what the mod itself adds (" + (unknown.Count == 0 ? "all" : string.Join(", ", unknown)) + ")");
    }

    // ---------------------------------------------------------------- the three costs

    private static void GraphicsCostTiers()
    {
        KnobInfo[] knobs = GraphicsRules.Knobs;
        var free = new float[GfxKnobCount];
        var freeSet = new bool[GfxKnobCount];
        var mid = new float[GfxKnobCount];
        var midSet = new bool[GfxKnobCount];
        var high = new float[GfxKnobCount];
        var highSet = new bool[GfxKnobCount];
        bool nothing = true, within = true, same = true, adds = true, more = true, rooms = true;
        for (int cost = GraphicsCost.Free; cost < GraphicsCost.Count; cost++)
        {
            for (int place = GraphicsPlace.City; place <= GraphicsPlace.Indoors; place++)
            {
                for (int step = 0; step < GfxDay; step += GraphicsRules.StepsPerHour)
                {
                    GfxWork(GraphicsLook.Off, cost, place, step, GfxSkies[6], CalendarRules.Winter, free, freeSet);
                    for (int k = 0; k < GfxKnobCount; k++)
                    {
                        nothing &= !freeSet[k] && free[k] == 0f;
                    }
                }
            }
        }
        int[] finer =
        {
            (int)GraphicsKnob.ContactLength, (int)GraphicsKnob.ContactOpacity, (int)GraphicsKnob.ContactReach, (int)GraphicsKnob.ContactFade, (int)GraphicsKnob.ContactQuality,
            (int)GraphicsKnob.MicroOpacity
        };
        int radius = (int)GraphicsKnob.OcclusionRadius, strength = (int)GraphicsKnob.OcclusionStrength, moon = (int)GraphicsKnob.MoonContact;
        foreach (int look in GfxLooks)
        {
            for (int place = GraphicsPlace.City; place <= GraphicsPlace.Indoors; place++)
            {
                foreach (int[] sky in new[] { GfxSkies[0], GfxSkies[6] })
                {
                    for (int step = 0; step < GfxDay; step += 7)
                    {
                        GfxWork(look, GraphicsCost.Free, place, step, sky, CalendarRules.Summer, free, freeSet);
                        GfxWork(look, GraphicsCost.Balanced, place, step, sky, CalendarRules.Summer, mid, midSet);
                        GfxWork(look, GraphicsCost.High, place, step, sky, CalendarRules.Summer, high, highSet);
                        for (int k = 0; k < GfxKnobCount; k++)
                        {
                            within &= !(freeSet[k] && knobs[k].Cost > GraphicsCost.Free) && !(midSet[k] && knobs[k].Cost > GraphicsCost.Balanced);
                            if (knobs[k].Cost == GraphicsCost.Free)
                            {
                                same &= freeSet[k] == midSet[k] && freeSet[k] == highSet[k] && free[k] == mid[k] && free[k] == high[k];
                            }
                        }
                        adds &= midSet[(int)GraphicsKnob.ContactShadows] && midSet[(int)GraphicsKnob.SunContact] && midSet[(int)GraphicsKnob.MicroShadows] && !midSet[moon] && highSet[moon];
                        foreach (int k in finer)
                        {
                            more &= midSet[k] && highSet[k] && high[k] >= mid[k];
                        }
                        bool city = place == GraphicsPlace.City;
                        rooms &= midSet[radius] == city && midSet[strength] == city && highSet[radius] == city && highSet[strength] == city;
                        more &= !city || (high[radius] > mid[radius] && mid[radius] > 1f && high[strength] > mid[strength] && mid[strength] > 1f);
                    }
                }
            }
        }
        Check(nothing, "graphics: with the look Off nothing is set at all, whatever the cost, the place, the hour and the weather: the picture is the game's own");
        Check(within, "graphics: nothing beyond the chosen cost is ever set: Free sets only Free rows, Balanced nothing of High's (every look and place, round the clock, fair and foul)");
        Check(same, "graphics: the cost changes nothing of the look itself: every Free row is the same number under Free, Balanced and High");
        Check(adds, "graphics: Balanced adds contact shadows from the sun and finer shading in creases, and only High the moon's contact shadows");
        Check(more, "graphics: High is never less than Balanced in any of them, and both make the game's corner shading wider and stronger than the game's own");
        Check(rooms, "graphics: the wider corner shading is for the city only: a room keeps the game's, which is up to twice the street's already");
    }

    // ---------------------------------------------------------------- every moment

    private static bool GfxTintKept(float[] values, GraphicsKnob red)
    {
        int at = (int)red;
        return GfxNear(GraphicsPicture.Luminance(values[at], values[at + 1], values[at + 2]), 0.5f, 0.0005f);
    }

    private static void GraphicsEveryMoment()
    {
        var values = new float[GfxKnobCount];
        var set = new bool[GfxKnobCount];
        var first = new bool[GfxKnobCount];
        var room = new float[GfxKnobCount];
        var roomSet = new bool[GfxKnobCount];
        bool finite = true, inside = true, steady = true, whole = true, grey = true, bright = true, filter = true, rooms = true, alone = true;
        string held = null;
        int looked = 0;
        int sun = (int)GraphicsKnob.SunRed;
        foreach (int look in GfxLooks)
        {
            bool have = false;
            for (int season = -1; season <= CalendarRules.Autumn; season++)
            {
                foreach (int[] sky in GfxSkies)
                {
                    for (int step = 0; step < GfxDay; step++)
                    {
                        GfxWork(look, GraphicsCost.High, GraphicsPlace.City, step, sky, season, values, set);
                        looked++;
                        for (int k = 0; k < GfxKnobCount; k++)
                        {
                            if (!have)
                            {
                                first[k] = set[k];
                            }
                            steady &= first[k] == set[k];
                            if (!set[k])
                            {
                                continue;
                            }
                            finite &= float.IsFinite(values[k]);
                            if (GfxHeldBack(k, values[k]))
                            {
                                inside = false;
                                held = held ?? GfxWhere(k, look, step, sky, season) + " is " + values[k].ToString("0.####", GfxInv);
                            }
                        }
                        have = true;
                        for (int row = 0; row < 3; row++)
                        {
                            int mix = (int)GraphicsKnob.MixRedRed + row * 3;
                            grey &= GfxNear(values[mix] + values[mix + 1] + values[mix + 2], 100f, 0.01f) && values[mix + row] > 60f;
                        }
                        bright &= GfxTintKept(values, GraphicsKnob.ShadowsRed) && GfxTintKept(values, GraphicsKnob.HighlightsRed);
                        float most = Math.Max(values[sun], Math.Max(values[sun + 1], values[sun + 2])), least = Math.Min(values[sun], Math.Min(values[sun + 1], values[sun + 2]));
                        filter &= GfxNear(most, 1f, 0.00001f) && least > 0.1f;
                    }
                }
            }
            for (int k = 0; k < GfxKnobCount; k++)
            {
                whole &= first[k];
            }
            for (int step = 0; step < GfxDay; step++)
            {
                GfxWork(look, GraphicsCost.High, GraphicsPlace.Indoors, step, GfxSkies[0], -1, values, set);
                GfxWork(look, GraphicsCost.High, GraphicsPlace.Indoors, step, GfxSkies[6], CalendarRules.Winter, room, roomSet);
                looked += 2;
                for (int k = 0; k < GfxKnobCount; k++)
                {
                    bool cityOnly = (k >= (int)GraphicsKnob.FogReach && k <= (int)GraphicsKnob.Reflections) || k == (int)GraphicsKnob.OcclusionRadius || k == (int)GraphicsKnob.OcclusionStrength;
                    rooms &= set[k] == !cityOnly;
                    alone &= set[k] == roomSet[k] && values[k] == room[k];
                    if (set[k] && (!float.IsFinite(values[k]) || GfxHeldBack(k, values[k])))
                    {
                        inside = false;
                        held = held ?? GfxWhere(k, look, step, GfxSkies[0], -1) + " indoors is " + values[k].ToString("0.####", GfxInv);
                    }
                }
            }
        }
        Check(looked > 200000 && finite, "graphics: every setting of every look is a number at every game minute of the day, in every season and under " + GfxSkies.Length + " skies (" + looked + " looks)");
        Check(inside, "graphics: none of them is ever held back by its bounds: the table allows what the looks ask" + (held != null ? " (" + held + ")" : string.Empty));
        Check(steady && whole, "graphics: in the city every row is set at every moment (with High), so nothing switches on or off as the clock or the weather moves");
        Check(rooms, "graphics: a room has a look of its own with no haze, no sky light and no corner shading of the mod's: the fog's rows and the sky's are never set indoors");
        Check(alone, "graphics: a room's look does not follow the weather or the season, only the clock");
        Check(grey, "graphics: each row of the colour mixer adds up to 100, so stronger or paler colour never moves a grey");
        Check(bright, "graphics: the tints of the dark and of the bright parts are as bright as the neutral grey they replace, so toning colours and never darkens");
        Check(filter, "graphics: the sun's filter has 1 as its largest part and leaves over a tenth of every colour: the sun is coloured, never made brighter than the game has it");
    }

    // ---------------------------------------------------------------- nothing jumps

    private static void GraphicsNoJumps()
    {
        KnobInfo[] knobs = GraphicsRules.Knobs;
        var now = new float[GfxKnobCount];
        var next = new float[GfxKnobCount];
        var set = new bool[GfxKnobCount];
        var setNext = new bool[GfxKnobCount];
        float worst = 0f, worstSky = 0f;
        string where = null, whereSky = null, moved = null, sunMoved = null;
        int sun = (int)GraphicsKnob.SunRed, sunFrom = GfxStep(GraphicsModel.SunFrom), sunTo = GfxStep(GraphicsModel.SunTo);
        foreach (int look in GfxLooks)
        {
            for (int season = -1; season <= CalendarRules.Autumn; season++)
            {
                foreach (int[] sky in GfxSkies)
                {
                    for (int place = GraphicsPlace.City; place <= GraphicsPlace.Indoors; place++)
                    {
                        if (place == GraphicsPlace.Indoors && (sky != GfxSkies[0] || season >= 0))
                        {
                            continue;   // a room knows no weather and no season
                        }
                        GfxWork(look, GraphicsCost.High, place, 0, sky, season, now, set);
                        for (int step = 0; step < GfxDay; step++)
                        {
                            GfxWork(look, GraphicsCost.High, place, (step + 1) % GfxDay, sky, season, next, setNext);
                            for (int k = 0; k < GfxKnobCount; k++)
                            {
                                float size = Math.Abs(next[k] - now[k]);
                                bool sunOut = k >= sun && k < sun + 3 && !(step >= sunFrom && step + 1 <= sunTo);
                                if (!set[k])
                                {
                                    continue;
                                }
                                if (sunOut)
                                {
                                    // While the game has its sun out the filter stands still, but for midnight, when it turns from the evening's to the morning's.
                                    if (size > 0f && step != GfxDay - 1)
                                    {
                                        sunMoved = sunMoved ?? GfxWhere(k, look, step, sky, season);
                                    }
                                }
                                else if (knobs[k].Step == 0f)
                                {
                                    if (size > 0f)
                                    {
                                        moved = moved ?? GfxWhere(k, look, step, sky, season);
                                    }
                                }
                                else if (size / knobs[k].Step > worst)
                                {
                                    worst = size / knobs[k].Step;
                                    where = GfxWhere(k, look, step, sky, season) + ": " + size.ToString("0.#####", GfxInv) + " of " + knobs[k].Step.ToString("0.#####", GfxInv);
                                }
                                now[k] = next[k];
                            }
                        }
                    }
                }
                // One twentieth more rain, falling snow or lying snow, under each sky it can come to, at every half hour.
                for (int step = 0; step < GfxDay; step += GraphicsRules.StepsPerHour / 2)
                {
                    for (int which = 0; which < 3; which++)
                    {
                        foreach (int[] under in new[] { new[] { 0, 0 }, new[] { 20, 0 }, new[] { 0, 20 }, new[] { 20, 20 } })
                        {
                            for (int level = 0; level < GfxTwentieths; level++)
                            {
                                int[] sky = which == 0 ? new[] { level, under[0], under[1] } : which == 1 ? new[] { under[0], level, under[1] } : new[] { under[0], under[1], level };
                                GfxWork(look, GraphicsCost.High, GraphicsPlace.City, step, sky, season, now, set);
                                sky[which]++;
                                GfxWork(look, GraphicsCost.High, GraphicsPlace.City, step, sky, season, next, setNext);
                                for (int k = 0; k < GfxKnobCount; k++)
                                {
                                    float size = Math.Abs(next[k] - now[k]);
                                    if (knobs[k].Step == 0f)
                                    {
                                        moved = size > 0f ? moved ?? GfxWhere(k, look, step, sky, season) : moved;
                                    }
                                    else if (size / knobs[k].Step > worstSky)
                                    {
                                        worstSky = size / knobs[k].Step;
                                        whereSky = GfxWhere(k, look, step, sky, season) + ": " + size.ToString("0.#####", GfxInv) + " of " + knobs[k].Step.ToString("0.#####", GfxInv);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        Check(worst <= 1f && worst > 0.5f, "graphics: from one game minute to the next no setting moves by more than its step, in any look, season or weather, in the city or a room (the nearest: "
                                           + where + ")");
        Check(worstSky <= 1f && worstSky > 0.5f, "graphics: one twentieth more rain, falling snow or lying snow moves no setting by more than its step (the nearest: " + whereSky + ")");
        Check(moved == null, "graphics: a setting whose step is 0 never moves with the clock or the weather: the curve, the haze's height, the moon's filter, the costly rows"
                             + (moved != null ? " (" + moved + ")" : string.Empty));
        Check(sunMoved == null, "graphics: while the game has its sun out (before " + GraphicsModel.SunFrom.ToString("0", GfxInv) + " and from " + GraphicsModel.SunTo.ToString("0", GfxInv)
                                + ") its filter stands still, so nothing is written for a light that does not shine" + (sunMoved != null ? " (" + sunMoved + ")" : string.Empty));
    }

    // ---------------------------------------------------------------- the grey card

    private static void GraphicsGreyCard()
    {
        var values = new float[GfxKnobCount];
        var set = new bool[GfxKnobCount];
        float fair = 0f, foul = 0f, floor = float.MaxValue, indoors = 0f, darker = float.MinValue;
        string fairAt = null, foulAt = null;
        foreach (int look in GfxLooks)
        {
            for (int s = 0; s < GfxSkies.Length; s++)
            {
                int[] sky = GfxSkies[s];
                float gone = GraphicsModel.Grey(sky[0] / (float)GfxTwentieths, sky[1] / (float)GfxTwentieths, sky[2] / (float)GfxTwentieths);
                for (int season = -1; season <= CalendarRules.Autumn; season++)
                {
                    for (int step = 0; step < GfxDay; step += s == 0 ? 1 : 5)
                    {
                        float hour = GfxHour(step);
                        GfxWork(look, GraphicsCost.Free, GraphicsPlace.City, step, sky, season, values, set);
                        foreach (float contrast in GfxContrasts)
                        {
                            CardPair game = GraphicsModel.Game(hour, gone, contrast), mine = GraphicsModel.Look(hour, gone, contrast, values, set);
                            float lit = (mine.Lit / game.Lit - 1f) * 100f, shade = (mine.Shade / game.Shade - 1f) * 100f;
                            float off = Math.Max(Math.Abs(lit), Math.Abs(shade));
                            if (s == 0 && off > fair)
                            {
                                fair = off;
                                fairAt = GraphicsRules.LookName(look) + " at " + GfxClock(step) + ", season " + season + ", contrast " + contrast.ToString("0", GfxInv) + ": lit "
                                         + lit.ToString("+0.0;-0.0", GfxInv) + "%, shade " + shade.ToString("+0.0;-0.0", GfxInv) + "%";
                            }
                            if (s != 0 && off > foul)
                            {
                                foul = off;
                                foulAt = GraphicsRules.LookName(look) + " at " + GfxClock(step) + ", season " + season + ", rain " + sky[0] + " snow " + sky[1] + " lying " + sky[2] + ": lit "
                                         + lit.ToString("+0.0;-0.0", GfxInv) + "%, shade " + shade.ToString("+0.0;-0.0", GfxInv) + "%";
                            }
                            if (GraphicsRules.Night(hour) >= 0.999f)
                            {
                                floor = Math.Min(floor, mine.Shade / game.Shade);
                            }
                            if (s == 2 && contrast == 0f && season < 0 && step == GfxStep(13f))
                            {
                                darker = Math.Max(darker, lit);   // a rainy early afternoon against the game's
                            }
                        }
                    }
                }
            }
            GfxWork(look, GraphicsCost.Free, GraphicsPlace.Indoors, GfxStep(12f), GfxSkies[0], -1, values, set);
            foreach (float contrast in GfxContrasts)
            {
                indoors = Math.Max(indoors, Math.Abs(GraphicsModel.Indoors(contrast, values, set) / GraphicsModel.Indoors(contrast, null, null) - 1f) * 100f);
            }
        }
        var off0 = new float[GfxKnobCount];
        var none = new bool[GfxKnobCount];
        GfxWork(GraphicsLook.Off, GraphicsCost.Free, GraphicsPlace.City, GfxStep(15f), GfxSkies[0], -1, off0, none);
        CardPair own = GraphicsModel.Game(15f, 0f, 0f), same = GraphicsModel.Look(15f, 0f, 0f, off0, none);
        Check(own.Lit == same.Lit && own.Shade == same.Shade && own.Lit > own.Shade && own.Shade > 0.2f && own.Lit < 0.9f && GfxNear(own.Middle, (own.Lit + own.Shade) * 0.5f, 0.0001f),
            "graphics: the model's grey card in the game's own picture: sunlit over shade by day, neither black nor white, and with the look Off a look's card is the game's");
        Check(fair <= GraphicsRules.GreyTolerance && fair > 1f, "graphics: in fair weather a grey card in the sun and in the shade stays within " + GraphicsRules.GreyTolerance.ToString("0", GfxInv)
                                                                + "% of the brightness the game gives it, every minute of the day, each season, at the game's and the mod's own contrast (the farthest: "
                                                                + fairAt + ")");
        Check(foul <= GraphicsRules.RainTolerance && GraphicsRules.RainTolerance >= GraphicsRules.GreyTolerance,
            "graphics: in rain and snow it stays within " + GraphicsRules.RainTolerance.ToString("0", GfxInv) + "% (the farthest: " + foulAt + ")");
        Check(darker < -1f, "graphics: a rainy afternoon is a little darker than the game's, on purpose (" + darker.ToString("+0.0;-0.0", GfxInv) + "% at the least)");
        Check(floor >= GraphicsRules.ShadeFloor && GraphicsRules.ShadeFloor > 0.85f && GraphicsRules.ShadeFloor <= 1f,
            "graphics: through the night a grey card in the shade never falls under " + GraphicsRules.ShadeFloor.ToString("0.00", GfxInv) + " of the game's own: the night stays readable (least "
            + floor.ToString("0.000", GfxInv) + ")");
        Check(indoors <= GraphicsRules.GreyTolerance, "graphics: a grey card in a room stays within the same " + GraphicsRules.GreyTolerance.ToString("0", GfxInv) + "% (" + GfxPercent(indoors) + "%)");
    }

    // ---------------------------------------------------------------- the curves

    private static void GraphicsCurves()
    {
        var values = new float[GfxKnobCount];
        var set = new bool[GfxKnobCount];
        var contrast = new float[GraphicsLook.Count];
        bool shape = true, whites = true, blacks = true, greys = true;
        string first = null;
        foreach (int look in GfxLooks)
        {
            GfxWork(look, GraphicsCost.Free, GraphicsPlace.City, GfxStep(12f), GfxSkies[0], -1, values, set);
            ToneCurve curve = ToneCurve.Make(values[(int)GraphicsKnob.ToeStrength], values[(int)GraphicsKnob.ToeLength], values[(int)GraphicsKnob.ShoulderStrength],
                values[(int)GraphicsKnob.ShoulderLength], values[(int)GraphicsKnob.ShoulderAngle], 1f);
            float before = 0f, fall = 0f, rise = 0f;
            for (int i = 1; i <= 4000; i++)
            {
                float y = curve.At(curve.White * i / 4000f);
                fall = Math.Max(fall, before - y);
                rise = Math.Max(rise, y - before);
                before = y;
            }
            bool ok = curve.At(0f) == 0f && GfxNear(curve.At(curve.White), 1f, 0.0001f) && GfxNear(curve.At(curve.White * 3f), 1f, 0.0001f) && fall <= 0f && rise < 0.01f
                      && curve.White > 8f && curve.ToeEnd > 0f && curve.ToeEnd < curve.MiddleEnd && curve.MiddleEnd < 1f
                      && GfxNear(curve.Toe.At(curve.ToeEnd), curve.Middle.At(curve.ToeEnd), 0.0001f) && GfxNear(curve.Middle.At(curve.MiddleEnd), curve.Shoulder.At(curve.MiddleEnd), 0.0001f);
            if (!ok)
            {
                shape = false;
                first = first ?? GraphicsRules.LookName(look) + ": fall " + fall + ", rise " + rise + ", white " + curve.White;
            }
            // What the game shows at 245 of 255 a look shows as white; a deep black is deeper and still there; a grey card is the grey it was.
            whites &= GraphicsModel.Through(values, 8f) >= 0.999f && GraphicsModel.ThroughGame(8f) < 0.965f && GraphicsModel.Through(values, 3f) > GraphicsModel.ThroughGame(3f);
            blacks &= GraphicsModel.Through(values, 0.005f) < GraphicsModel.ThroughGame(0.005f) && GraphicsModel.Through(values, 0.005f) > 0.004f;
            greys &= GfxNear(GraphicsModel.Through(values, GraphicsPicture.MidGrey) / GraphicsModel.ThroughGame(GraphicsPicture.MidGrey), 1f, 0.05f);
            contrast[look] = GraphicsModel.Through(values, 1f) / GraphicsModel.Through(values, 0.02f);
        }
        Check(shape, "graphics: each look's curve starts at black, rises without a step or a fall, joins its three pieces with no gap, and reaches white for good" + (first != null ? " (" + first + ")" : string.Empty));
        Check(GfxNear(GraphicsPicture.GameCurve(1000f), 0.9486f, 0.001f) && GraphicsPicture.GameCurve(0f) == 0f && GfxNear(GraphicsPicture.Screen(1f), 1f, 0.00001f) && GraphicsPicture.Screen(0f) == 0f
              && GfxNear(GraphicsPicture.Screen(0.2140f), 0.5f, 0.001f),
            "graphics: the game's own curve tops out at 0.949, never quite white, and the screen's encoding is the standard one (a linear 0.214 is half way)");
        Check(whites, "graphics: whiter whites: what the game shows at 245 of 255 every look shows as white, and a white wall in the sun is whiter than the game's");
        Check(blacks, "graphics: deeper blacks that are still there: a deep shadow is darker than the game's and not crushed to nothing");
        Check(greys, "graphics: each look's curve leaves a grey card within 5% of the game's before any exposure is added");
        Check(contrast[GraphicsLook.Filmic] > contrast[GraphicsLook.Natural] && contrast[GraphicsLook.Filmic] > contrast[GraphicsLook.Vivid],
            "graphics: Filmic has the most contrast between a deep shadow and a bright wall (" + contrast[GraphicsLook.Filmic].ToString("0.0", GfxInv) + " to 1, Natural "
            + contrast[GraphicsLook.Natural].ToString("0.0", GfxInv) + ", Vivid " + contrast[GraphicsLook.Vivid].ToString("0.0", GfxInv) + ")");
        // The white balance's and the contrast's arithmetic, which the grey card stands on.
        Check(GfxNear(GraphicsPicture.BalanceGain(0f), 1f, 0.002f) && GfxNear(GraphicsPicture.BalanceGain(12f), 1f, 0.06f) && GfxNear(GraphicsPicture.BalanceGain(-12f), 1f, 0.06f),
            "graphics: the white balance leaves a grey's brightness alone at 0 and moves it by a few percent at most at a look's warmest and coolest");
        Check(GfxNear(GraphicsPicture.Contrast(GraphicsPicture.MidGrey, 10f, false), GraphicsPicture.MidGrey, 0.0001f) && GraphicsPicture.Contrast(0.5f, 10f, false) > 0.5f
              && GraphicsPicture.Contrast(0.05f, 10f, false) < 0.05f && GraphicsPicture.Contrast(0.5f, 10f, true) > 0.5f && GraphicsPicture.Contrast(0.05f, 10f, true) < 0.05f
              && GraphicsPicture.Contrast(0.3f, 0f, true) == 0.3f && GfxNear(GraphicsPicture.Contrast(0.225f, 10f, true), 0.225f, 0.002f),
            "graphics: the Picture slider's contrast turns about the grey card under the game's curve and a little higher (0.225) under a look's, as the pipeline does it");
    }

    // ---------------------------------------------------------------- the light

    private static float GfxOf(int look, float hour, int[] sky, int season, GraphicsKnob knob, int place = GraphicsPlace.City)
    {
        var values = new float[GfxKnobCount];
        var set = new bool[GfxKnobCount];
        GfxWork(look, GraphicsCost.Free, place, GfxStep(hour), sky, season, values, set);
        return values[(int)knob];
    }

    private static void GraphicsLight()
    {
        int[] fairSky = GfxSkies[0], rainSky = GfxSkies[2], lying = GfxSkies[4], snowing = GfxSkies[3], snowOverRain = { 20, 20, 0 };
        bool warmLow = true, blueNight = true, golden = true, rain = true, snow = true, seasons = true, haze = true, glow = true, turned = true;
        foreach (int look in GfxLooks)
        {
            // A warm sun when it is low, nearer the game's own white at noon.
            float noonBlue = GfxOf(look, 12f, fairSky, -1, GraphicsKnob.SunBlue);
            foreach (float low in new[] { 7f, 19f })
            {
                float red = GfxOf(look, low, fairSky, -1, GraphicsKnob.SunRed), green = GfxOf(look, low, fairSky, -1, GraphicsKnob.SunGreen), blue = GfxOf(look, low, fairSky, -1, GraphicsKnob.SunBlue);
                warmLow &= red == 1f && green < 0.85f && blue < green && blue < noonBlue - 0.25f;
            }
            warmLow &= noonBlue > 0.75f && GraphicsPicture.Luminance(1f, GfxOf(look, 12f, fairSky, -1, GraphicsKnob.SunGreen), noonBlue) > 0.9f;
            // A blue night: a blue moon, blue in the dark parts, a cooler white.
            blueNight &= GfxOf(look, 1f, fairSky, -1, GraphicsKnob.MoonBlue) == 1f && GfxOf(look, 1f, fairSky, -1, GraphicsKnob.MoonGreen) < 1f
                         && GfxOf(look, 1f, fairSky, -1, GraphicsKnob.MoonRed) < GfxOf(look, 1f, fairSky, -1, GraphicsKnob.MoonGreen)
                         && GfxOf(look, 1f, fairSky, -1, GraphicsKnob.ShadowsBlue) > GfxOf(look, 12f, fairSky, -1, GraphicsKnob.ShadowsBlue) + 0.02f
                         && GfxOf(look, 1f, fairSky, -1, GraphicsKnob.ShadowsBlue) > GfxOf(look, 1f, fairSky, -1, GraphicsKnob.ShadowsRed)
                         && GfxOf(look, 1f, fairSky, -1, GraphicsKnob.Temperature) < GfxOf(look, 12f, fairSky, -1, GraphicsKnob.Temperature) - 2f;
            // Golden hours after sunrise and before sunset, a blue hour either side of the night.
            golden &= GfxOf(look, 7.2f, fairSky, -1, GraphicsKnob.Temperature) > GfxOf(look, 12f, fairSky, -1, GraphicsKnob.Temperature) + 5f
                      && GfxOf(look, 19.4f, fairSky, -1, GraphicsKnob.Temperature) > GfxOf(look, 12f, fairSky, -1, GraphicsKnob.Temperature) + 5f
                      && GfxOf(look, 5.75f, fairSky, -1, GraphicsKnob.Temperature) < GfxOf(look, 12f, fairSky, -1, GraphicsKnob.Temperature) - 3f
                      && GfxOf(look, 21.15f, fairSky, -1, GraphicsKnob.Temperature) < GfxOf(look, 12f, fairSky, -1, GraphicsKnob.Temperature) - 3f;
            // Rain: a little darker, cooler and paler, the far streets nearer in the mist, glossier, and lamps that glow more at night.
            rain &= GfxOf(look, 13f, rainSky, -1, GraphicsKnob.Compensation) < GfxOf(look, 13f, fairSky, -1, GraphicsKnob.Compensation) - 0.1f
                    && GfxOf(look, 13f, rainSky, -1, GraphicsKnob.Temperature) < GfxOf(look, 13f, fairSky, -1, GraphicsKnob.Temperature) - 3f
                    && GfxOf(look, 13f, rainSky, -1, GraphicsKnob.MixRedRed) < GfxOf(look, 13f, fairSky, -1, GraphicsKnob.MixRedRed) - 5f
                    && GfxOf(look, 13f, rainSky, -1, GraphicsKnob.FogReach) < GfxOf(look, 13f, fairSky, -1, GraphicsKnob.FogReach) * 0.6f
                    && GfxOf(look, 13f, rainSky, -1, GraphicsKnob.Reflections) > GfxOf(look, 13f, fairSky, -1, GraphicsKnob.Reflections) + 0.1f
                    && GfxOf(look, 1f, rainSky, -1, GraphicsKnob.BloomThreshold) < GfxOf(look, 1f, fairSky, -1, GraphicsKnob.BloomThreshold)
                    && GfxOf(look, 13f, rainSky, -1, GraphicsKnob.Ambient) == 1f && GfxOf(look, 13f, fairSky, -1, GraphicsKnob.Ambient) < 1f;
            // Snow: brighter where it lies, with blue shade, and the white ground does not glow.
            snow &= GfxOf(look, 13f, lying, -1, GraphicsKnob.Compensation) > GfxOf(look, 13f, fairSky, -1, GraphicsKnob.Compensation) + 0.05f
                    && GfxOf(look, 13f, lying, -1, GraphicsKnob.ShadowsBlue) > GfxOf(look, 13f, fairSky, -1, GraphicsKnob.ShadowsBlue) + 0.01f
                    && GfxOf(look, 13f, lying, -1, GraphicsKnob.BloomThreshold) > GfxOf(look, 13f, fairSky, -1, GraphicsKnob.BloomThreshold) + 0.1f
                    && GfxOf(look, 13f, snowing, -1, GraphicsKnob.FogReach) < GfxOf(look, 13f, fairSky, -1, GraphicsKnob.FogReach) * 0.6f;
            // While it snows the game's rain is the snow: none of rain's own darkness, gloss or mist is laid on top of the snow's.
            turned &= GfxOf(look, 13f, snowOverRain, -1, GraphicsKnob.Compensation) == GfxOf(look, 13f, snowing, -1, GraphicsKnob.Compensation)
                      && GfxOf(look, 13f, snowOverRain, -1, GraphicsKnob.Reflections) == GfxOf(look, 13f, snowing, -1, GraphicsKnob.Reflections)
                      && GfxOf(look, 13f, snowOverRain, -1, GraphicsKnob.FogReach) == GfxOf(look, 13f, snowing, -1, GraphicsKnob.FogReach)
                      && GfxOf(look, 13f, snowOverRain, -1, GraphicsKnob.MixRedRed) == GfxOf(look, 13f, snowing, -1, GraphicsKnob.MixRedRed);
            // The seasons, while they are on: a cooler, paler winter, a warmer, fuller summer; with them off, neither.
            seasons &= GfxOf(look, 13f, fairSky, CalendarRules.Winter, GraphicsKnob.Temperature) < GfxOf(look, 13f, fairSky, -1, GraphicsKnob.Temperature)
                       && GfxOf(look, 13f, fairSky, CalendarRules.Summer, GraphicsKnob.Temperature) > GfxOf(look, 13f, fairSky, -1, GraphicsKnob.Temperature)
                       && GfxOf(look, 13f, fairSky, CalendarRules.Autumn, GraphicsKnob.Temperature) > GfxOf(look, 13f, fairSky, -1, GraphicsKnob.Temperature)
                       && GfxOf(look, 13f, fairSky, CalendarRules.Winter, GraphicsKnob.MixRedRed) < GfxOf(look, 13f, fairSky, -1, GraphicsKnob.MixRedRed)
                       && GfxOf(look, 13f, fairSky, CalendarRules.Summer, GraphicsKnob.MixRedRed) > GfxOf(look, 13f, fairSky, -1, GraphicsKnob.MixRedRed);
            // The haze: far above the game's ground mist (25 m at most), far thinner than it (42 to 60 m), the sky's colour by day.
            for (int step = 0; step < GfxDay; step += GraphicsRules.StepsPerHour / 4)
            {
                foreach (int[] sky in GfxSkies)
                {
                    haze &= GfxOf(look, GfxHour(step), sky, CalendarRules.Winter, GraphicsKnob.FogReach) >= 100f;
                }
            }
            haze &= GfxOf(look, 13f, fairSky, -1, GraphicsKnob.FogHeight) >= 100f && GfxOf(look, 13f, fairSky, -1, GraphicsKnob.FogBase) == 0f
                    && GfxOf(look, 13f, fairSky, -1, GraphicsKnob.FogTintRed) == 1f && GfxOf(look, 13f, fairSky, -1, GraphicsKnob.FogTintGreen) == 1f
                    && GfxOf(look, 13f, fairSky, -1, GraphicsKnob.FogTintBlue) == 1f && GfxOf(look, 1f, fairSky, -1, GraphicsKnob.FogTintBlue) > GfxOf(look, 1f, fairSky, -1, GraphicsKnob.FogTintRed);
            // The glow: the game's own at night (from a brightness of 1), kept to lamps, headlights and signs by day.
            glow &= GfxOf(look, 1f, fairSky, -1, GraphicsKnob.BloomThreshold) == 1f && GfxOf(look, 13f, fairSky, -1, GraphicsKnob.BloomThreshold) >= 1.5f
                    && GfxOf(look, 13f, fairSky, -1, GraphicsKnob.BloomScatter, GraphicsPlace.Indoors) > 0.5f;
        }
        Check(warmLow, "graphics: the sun is warm when it is low (an hour after it comes up, two before it goes) and nearly the game's own at noon, in every look");
        Check(blueNight, "graphics: the night is blue: a blue moon, blue in the dark parts, a cooler white than by day");
        Check(golden, "graphics: golden light after sunrise and before sunset, and a blue hour either side of the night");
        Check(rain, "graphics: rain is a little darker, cooler and paler, with mist over the far streets, glossier paint and glass, lamps that glow more at night, and the sky's light the game's own");
        Check(snow, "graphics: lying snow is a little brighter with blue shade and does not glow, and falling snow hides the far streets");
        Check(turned, "graphics: while it snows over the game's rain, none of rain's own darkness, gloss or mist is laid on top of the snow's");
        Check(seasons, "graphics: with the seasons on, winter is cooler and paler and summer warmer and fuller than with them off");
        Check(haze, "graphics: the haze reaches at least 100 m up from the ground and a ray goes at least 100 m in it, in any weather, and by day it has the sky's own colour");
        Check(glow, "graphics: at night things glow as in the game's own picture, by day only what is half as bright again as white");
        // What the Guide says of the three looks, against each other.
        float naturalColour = GfxOf(GraphicsLook.Natural, 13f, fairSky, -1, GraphicsKnob.MixRedRed), filmicColour = GfxOf(GraphicsLook.Filmic, 13f, fairSky, -1, GraphicsKnob.MixRedRed);
        float vividColour = GfxOf(GraphicsLook.Vivid, 13f, fairSky, -1, GraphicsKnob.MixRedRed);
        Check(filmicColour < 100f && filmicColour < naturalColour && naturalColour < vividColour && vividColour > 110f
              && GfxOf(GraphicsLook.Filmic, 13f, fairSky, -1, GraphicsKnob.ShadowsBlue) > GfxOf(GraphicsLook.Natural, 13f, fairSky, -1, GraphicsKnob.ShadowsBlue)
              && GfxOf(GraphicsLook.Filmic, 13f, fairSky, -1, GraphicsKnob.HighlightsRed) > GfxOf(GraphicsLook.Natural, 13f, fairSky, -1, GraphicsKnob.HighlightsRed),
            "graphics: Filmic is paler than the game with cooler shadows and warmer highlights than Natural, and Vivid is the most colourful (red in red " + filmicColour.ToString("0", GfxInv)
            + ", " + naturalColour.ToString("0", GfxInv) + ", " + vividColour.ToString("0", GfxInv) + ")");
        var values = new float[GfxKnobCount];
        var set = new bool[GfxKnobCount];
        GfxWork(GraphicsLook.Vivid, GraphicsCost.Free, GraphicsPlace.City, GfxStep(13f), fairSky, -1, values, set);
        float vivid = GraphicsModel.Look(13f, 0f, 0f, values, set).Middle;
        GfxWork(GraphicsLook.Natural, GraphicsCost.Free, GraphicsPlace.City, GfxStep(13f), fairSky, -1, values, set);
        float natural = GraphicsModel.Look(13f, 0f, 0f, values, set).Middle;
        Check(vivid > natural, "graphics: Vivid is brighter than Natural (a grey card at one in the afternoon " + (vivid * 255f).ToString("0", GfxInv) + " against " + (natural * 255f).ToString("0", GfxInv)
                               + " of 255)");
    }

    // ---------------------------------------------------------------- the moment

    private static void GraphicsMoments()
    {
        GraphicsMoment a = GraphicsRules.Moment(GraphicsLook.Filmic, GraphicsCost.High, GraphicsPlace.Indoors, 7.5f, 0.5f, 0.26f, 1.4f, CalendarRules.Summer);
        Check(a.Look == GraphicsLook.Filmic && a.Cost == GraphicsCost.High && a.Place == GraphicsPlace.Indoors && a.HourStep == GfxStep(7.5f) && a.Rain == 10 && a.Snow == 5 && a.Cover == GfxTwentieths
              && a.Season == CalendarRules.Summer && GfxNear(a.Hour, 7.5f, 0.0001f),
            "graphics: a moment keeps the look, the cost, the place, the clock's minute, the weather in twentieths (never over the whole) and the season");
        GraphicsMoment junk = GraphicsRules.Moment(9, 7, 5, -0.5f, -3f, float.NaN, 0.024f, 4);
        Check(junk.Look == GraphicsLook.Off && junk.Cost == GraphicsCost.Free && junk.Place == GraphicsPlace.City && junk.HourStep == GfxDay - GraphicsRules.StepsPerHour / 2 && junk.Rain == 0
              && junk.Snow >= 0 && junk.Snow <= GfxTwentieths && junk.Cover == 0 && junk.Season == -1 && GraphicsRules.Moment(-1, -1, -1, 24f, 0f, 0f, 0f, -3).HourStep == 0
              && GraphicsRules.Moment(1, 0, 0, 23.999f, 0f, 0f, 0f, -1).HourStep == 0,
            "graphics: a look, a cost, a place or a season there is not is the game's own, free, in the city, with no season; an hour before midnight or past it wraps round the day");
        GraphicsMoment b = a;
        bool same = a.Same(in b);
        b.HourStep++;
        bool differs = !a.Same(in b);
        b = a;
        b.Rain++;
        differs &= !a.Same(in b);
        b = a;
        b.Snow++;
        differs &= !a.Same(in b);
        b = a;
        b.Cover--;
        differs &= !a.Same(in b);
        b = a;
        b.Season = -1;
        differs &= !a.Same(in b);
        b = a;
        b.Look = GraphicsLook.Vivid;
        differs &= !a.Same(in b);
        b = a;
        b.Cost = GraphicsCost.Free;
        differs &= !a.Same(in b);
        b = a;
        b.Place = GraphicsPlace.City;
        differs &= !a.Same(in b);
        GraphicsMoment near = GraphicsRules.Moment(GraphicsLook.Filmic, GraphicsCost.High, GraphicsPlace.Indoors, 7.5f + 0.004f, 0.51f, 0.27f, 1f, CalendarRules.Summer);
        Check(same && differs && a.Same(in near), "graphics: two moments are the same picture when nothing of them differs, and a quarter of a minute or a hundredth of the weather on is the same moment: nothing is written");
        Check(GraphicsRules.LookOf(GraphicsLook.Off) == GraphicsLook.Off && GraphicsRules.LookOf(GraphicsLook.Vivid) == GraphicsLook.Vivid && GraphicsRules.LookOf(GraphicsLook.Count) == GraphicsLook.Off
              && GraphicsRules.LookOf(-1) == GraphicsLook.Off && GraphicsRules.CostOf(GraphicsCost.High) == GraphicsCost.High && GraphicsRules.CostOf(GraphicsCost.Count) == GraphicsCost.Free
              && GraphicsRules.CostOf(-1) == GraphicsCost.Free,
            "graphics: a saved look or cost that is not one of the list reads as Off and as Free");
        Check(GraphicsRules.DefaultCost == GraphicsCost.Free && GraphicsRules.LookOf(GraphicsRules.DefaultLook) == GraphicsRules.DefaultLook,
            "graphics: until the player chooses otherwise the cost is Free: nothing that adds work for the graphics card is ever on by itself");
        Check(GraphicsRules.StepsPerHour == 60 && GfxTwentieths == 20, "graphics: the picture follows the clock minute by minute, as the Guide says, and the weather in twentieths");
        Check(GraphicsRules.LookName(GraphicsLook.Off) == "Off" && GraphicsRules.LookName(GraphicsLook.Natural) == "Natural" && GraphicsRules.LookName(GraphicsLook.Filmic) == "Filmic"
              && GraphicsRules.LookName(GraphicsLook.Vivid) == "Vivid" && GraphicsRules.CostName(GraphicsCost.Free) == "Free" && GraphicsRules.CostName(GraphicsCost.Balanced) == "Balanced"
              && GraphicsRules.CostName(GraphicsCost.High) == "High",
            "graphics: the looks and the costs are named in Player.log as on the options page");
        bool phases = true;
        for (int step = 0; step < GfxDay; step++)
        {
            float h = GfxHour(step);
            float day = GraphicsRules.Day(h), night = GraphicsRules.Night(h), golden = GraphicsRules.Golden(h), blue = GraphicsRules.Blue(h), up = GraphicsRules.SunHeight(h);
            phases &= day >= 0f && day <= 1f && night >= 0f && night <= 1f && golden >= 0f && golden <= 1f && blue >= 0f && blue <= 1f && up >= 0f && up <= 1f && !(day > 0.5f && night > 0.5f);
        }
        Check(phases && GraphicsRules.Day(13f) == 1f && GraphicsRules.Night(13f) == 0f && GraphicsRules.Night(1f) == 1f && GraphicsRules.Day(1f) == 0f && GraphicsRules.Golden(13f) == 0f
              && GraphicsRules.Blue(13f) == 0f && GraphicsRules.Golden(1f) == 0f && GraphicsRules.Blue(1f) == 0f && GraphicsRules.SunHeight(13f) == 1f
              && GraphicsRules.SunHeight(GraphicsModel.SunFrom) == 0f && GraphicsRules.SunHeight(GraphicsModel.SunTo) == 0f,
            "graphics: the day's phases are shares from 0 to 1, day and night never both, and the sun's height is 0 at the hours the game lights it and puts it out");
    }

    // ---------------------------------------------------------------- the measurement

    private static void GraphicsVerdicts()
    {
        float[] windows = { 10f, 10.4f, 10f, 10.6f, 10.2f, 10.5f, 10.1f };   // off, on, off ... ms: the pairs are 0.4, 0.5 and 0.35
        Check(GfxNear(GraphicsRules.Added(windows, 3), 1.25f / 3f, 0.0005f) && GfxNear(GraphicsRules.Spread(windows, 3), 0.15f, 0.0005f),
            "graphics: what a setting adds to a frame is the mean over the pairs of each window with it less the two without it either side, and the pairs' spread is the measurement's noise");
        var drift = new float[GraphicsRules.CostPairs * 2 + 1];
        for (int i = 0; i < drift.Length; i++)
        {
            drift[i] = 8f + 0.25f * i + ((i & 1) == 1 ? 0.3f : 0f);   // a computer warming up by a quarter of a millisecond a window
        }
        Check(GfxNear(GraphicsRules.Added(drift, GraphicsRules.CostPairs), 0.3f, 0.0005f) && GfxNear(GraphicsRules.Spread(drift, GraphicsRules.CostPairs), 0f, 0.0005f),
            "graphics: a steady drift over the measurement cancels: 0.3 ms added reads as 0.3 ms while every window is a quarter of a millisecond slower than the last");
        Check(GraphicsRules.CostVerdict(0.05f, 0.2f, 0.1f) == GraphicsRules.Pass && GraphicsRules.CostVerdict(0.1f, 0f, 0.1f) == GraphicsRules.Pass
              && GraphicsRules.CostVerdict(-0.2f, 0.1f, 0.1f) == GraphicsRules.Pass && GraphicsRules.CostVerdict(0.3f, 0.25f, 0.1f) == GraphicsRules.Unsure
              && GraphicsRules.CostVerdict(0.3f, 0.1f, 0.1f) == GraphicsRules.Fail && GraphicsRules.CostVerdict(0.3f, -1f, 0.1f) == GraphicsRules.Fail,
            "graphics: within the budget passes, over it by less than the pairs disagree is not shown either way, over it by more fails");
        Check(GraphicsRules.Budget(GraphicsCost.Free) == GraphicsRules.FreeBudgetMs && GraphicsRules.Budget(GraphicsCost.Balanced) == GraphicsRules.BalancedBudgetMs
              && GraphicsRules.Budget(GraphicsCost.High) == GraphicsRules.HighBudgetMs && GraphicsRules.Budget(9) == GraphicsRules.FreeBudgetMs
              && GraphicsRules.FreeBudgetMs <= 0.1f && GraphicsRules.FreeBudgetMs < GraphicsRules.BalancedBudgetMs && GraphicsRules.BalancedBudgetMs < GraphicsRules.HighBudgetMs
              && GraphicsRules.HighBudgetMs <= 1.5f,
            "graphics: the budgets: Free a tenth of a millisecond at most (the noise of a measurement), Balanced more, High the most, and a cost there is not is held to Free's");
        float length = GraphicsCost.Count * (GraphicsRules.CostPairs * 2 + 1) * (GraphicsRules.CostWindowSeconds + GraphicsRules.CostSettleSeconds);
        Check(GraphicsRules.CostPairs >= 3 && GraphicsRules.CostWindowSeconds >= 1.5f && GraphicsRules.CostSettleSeconds >= 0.5f && length >= 40f && length <= 80f
              && GraphicsRules.HourShownSeconds >= 2f && GraphicsRules.HourShownSeconds <= 6f,
            "graphics: the measurement takes about a minute, as its button says (" + length.ToString("0", GfxInv) + " s: three pairs at least for each cost, the pipeline given time to settle)");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float GfxRun(int from, int count, float[] values, bool[] set)
    {
        float sink = 0f;
        for (int i = from; i < from + count; i++)
        {
            GraphicsMoment at = GraphicsRules.Moment(1 + i % 3, i % 3, i % 2, (i % GfxDay) / (float)GraphicsRules.StepsPerHour, (i % 21) / 20f, (i % 7) / 6f, (i % 5) / 4f, i % 5 - 1);
            GraphicsRules.Evaluate(in at, values, set);
            sink += values[(int)GraphicsKnob.Compensation] + values[(int)GraphicsKnob.SunBlue];
        }
        return sink;
    }

    private static void GraphicsRulesCost()
    {
        var values = new float[GfxKnobCount];
        var set = new bool[GfxKnobCount];
        float sink = GfxRun(0, 20000, values, set);
        var watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        sink += GfxRun(20000, 50000, values, set);
        long made = GC.GetAllocatedBytesForCurrentThread() - before;
        watch.Stop();
        double each = watch.Elapsed.TotalMilliseconds / 50000.0;
        Check(made == 0 && each < 0.005, "graphics: a look worked out (the director's work once a second of game time, at the most) allocates nothing and takes " + (each * 1000.0).ToString("0.00", GfxInv)
                                         + " microseconds (under 5; " + made + " bytes made; " + (float.IsFinite(sink) ? "ok" : "?") + ")");
        // The checker's runtime keeps a small array that does not escape off the heap, and the game's does not: the code is read too.
        string rules = GfxRead("Graphics", "GraphicsRules.cs"), picture = GfxRead("Graphics", "GraphicsPicture.cs"), model = GfxRead("Graphics", "GraphicsModel.cs");
        int from = rules.IndexOf("public static float Budget(int cost)", StringComparison.Ordinal);   // from here on is what the director calls: the budget, the verdict, the moment, the look
        string worked = from > 0 ? rules.Substring(from) : "new";
        foreach (string helper in new[] { "public static void Kelvin(", "public static void Filter(", "public static void MixerRow(", "public static float ToningGreen(" })
        {
            string body = Block(picture, helper);
            worked += body.Length > 20 ? body : "new";
        }
        worked += Block(model, "private static float At(float[] table, float hour)").Length > 20 ? Block(model, "private static float At(float[] table, float hour)") : "new";
        worked = Regex.Replace(worked, @"//[^\n]*", string.Empty).Replace("new GraphicsMoment", string.Empty);
        Check(!Regex.IsMatch(worked, @"\bnew\b") && !worked.Contains("\"") && !worked.Contains("foreach") && !worked.Contains("=> new"),
            "graphics: the code that works a look out holds no new object and no text: the moment, a struct, is all it makes");
    }

    // ---------------------------------------------------------------- the sources

    private static string GfxScripts() => System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));

    private static string GfxRead(params string[] parts)
    {
        string path = GfxScripts();
        foreach (string part in parts)
        {
            path = System.IO.Path.Combine(path, part);
        }
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : string.Empty;
    }

    // The picture's own files: its folder and its wiring.
    private static string GfxAllSources(out int files)
    {
        var text = new System.Text.StringBuilder(160000);
        string folder = System.IO.Path.Combine(GfxScripts(), "Graphics");
        files = 0;
        if (System.IO.Directory.Exists(folder))
        {
            foreach (string file in System.IO.Directory.GetFiles(folder, "*.cs"))
            {
                text.Append(System.IO.File.ReadAllText(file)).Append('\n');
                files++;
            }
        }
        text.Append(GfxRead("Wiring", "Wiring.GraphicsLinks.cs"));
        return text.ToString();
    }

    private static void GraphicsNothingPerFrame()
    {
        string all = GfxAllSources(out int files);
        string director = GfxRead("Graphics", "GraphicsDirector.cs"), tests = GfxRead("Graphics", "GraphicsDirector.Tests.cs"), game = GfxRead("Graphics", "GraphicsGame.cs");
        var found = new List<string>();
        foreach (string word in GfxPerFrame)
        {
            if (all.Contains(word))
            {
                found.Add(word);
            }
        }
        Check(files >= 8 && all.Length > 60000 && found.Count == 0, "graphics: nothing in the picture's " + files + " files runs in a frame or hangs work on one: no frame method, no camera or pipeline "
                                                                     + "callback, no wait of a frame, no search of the scene (" + (found.Count == 0 ? "none of " + GfxPerFrame.Length : string.Join(", ", found)) + ")");
        Check(director.Contains("private const float Every = 1f;") && Regex.IsMatch(director, @"var wait = new WaitForSeconds\(Every\);\s*while \(true\)\s*\{\s*yield return wait;")
              && Occurs(all, "new WaitForSeconds(") == 1 && Occurs(all, "StartCoroutine(") == 3 && Occurs(director, "StartCoroutine(Guard.Routine(Tick()))") == 1,
            "graphics: the director's only timer is one wait of a second of game time, made once before its loop, behind the guard: in a pause it stands still");
        Check(Regex.IsMatch(director, @"if \(_shown && !look && now\.Same\(_moment\) && !_game\.ShadeMoved\)\s*\{\s*return;") && Occurs(all, "GraphicsRules.Evaluate(") == 2
              && Occurs(director, "GraphicsRules.Evaluate(in now, _values, _set);") == 1 && director.Contains("private readonly float[] _values = new float[(int)GraphicsKnob.Count];")
              && director.Contains("private readonly bool[] _set = new bool[(int)GraphicsKnob.Count];"),
            "graphics: a second in which the moment is the same ends there: a look is worked out only when the minute, the weather, the place or an option moved, into two arrays made once");
        Check(Regex.IsMatch(director, @"if \(look\)\s*\{\s*_game\.LookForOthers\(\);\s*\}") && Occurs(all, "LookForOthers()") == 2 && Occurs(all, "GetVolumes(") == 1,
            "graphics: other mods' volumes are looked for (the pipeline copies its list) only on a door, the map, the designer or an option, never on the timer");
        // The writers: nothing made, nothing found, no words, and a slider written only when it has moved.
        bool lean = true;
        string fat = null;
        foreach (string writer in new[]
                 {
                     "public void Apply(float[] values, bool[] set)", "private void Slide(int k, float value, bool on)", "private void Colour(int red, float[] values, bool on)",
                     "private void Toggle(int k, bool on)", "private void Curve(bool on)", "private void Level(float level, bool on)", "private void Shade(float[] values, bool[] set)",
                     "private void Lights(float[] values, bool[] set)", "private void Filter(Light light, Color was, int red, float[] values, bool[] set)",
                     "private void Contact(HDAdditionalLightData data, ref bool held, bool on, bool wasOverride, bool wasValue)"
                 })
        {
            string body = Regex.Replace(Block(game, writer), @"//[^\n]*", string.Empty);
            bool ok = body.Length > 40 && !Regex.IsMatch(body.Replace("new Color(", string.Empty), @"\bnew\b") && !body.Contains("\"") && !body.Contains("GetComponent") && !body.Contains("Find")
                      && !body.Contains("=>") && !body.Contains("foreach") && !body.Contains(".ToString(");
            if (!ok)
            {
                lean = false;
                fat = fat ?? writer;
            }
        }
        Check(lean, "graphics: writing a look makes nothing, finds nothing and builds no words: the ten writers hold no new object, no text, no look-up" + (fat != null ? " (" + fat + ")" : string.Empty));
        Check(Block(game, "private void Slide(int k, float value, bool on)").Contains("if (on && (!_on[k] || Mathf.Abs(_written[k] - value) > Close))")
              && game.Contains("private const float Close = 0.0005f;") && Block(game, "private void Toggle(int k, bool on)").Contains("if (on != _on[k])"),
            "graphics: a slider is written again only when it has moved, and a switch only when it changes");
        Check(Occurs(all, "AddComponent<PerfSampler>()") == 1 && Block(tests, "private IEnumerable MeasureSteps(").Contains("gameObject.AddComponent<PerfSampler>()")
              && Occurs(tests, "new WaitForSecondsRealtime(") == 4 && Occurs(all, "new WaitForSecondsRealtime(") == 4,
            "graphics: the measuring test's sampler is the performance folder's own, made when the button is first pressed and off outside a window; the tests' four waits are each made once");
    }

    private static void GraphicsWrites()
    {
        string game = GfxRead("Graphics", "GraphicsGame.cs"), contract = GfxRead("Core", "GameContract.cs"), enhancer = GfxRead("World", "VisualEnhancer.cs");
        KnobInfo[] knobs = GraphicsRules.Knobs;
        var wrong = new List<string>();
        int bound = 0;
        for (int k = 0; k < knobs.Length; k++)
        {
            KnobInfo info = knobs[k];
            string knob = info.Knob.ToString();
            bool light = info.Setting == "SunLight" || info.Setting == "MoonLight";
            bool second = k > 0 && knobs[k - 1].Setting == info.Setting && knobs[k - 1].Slider == info.Slider;   // a colour's green and blue ride on its red
            bool ok;
            if (light)
            {
                ok = info.Slider == "color" ? game.Contains("light.color = ") : info.Slider == "useContactShadow" && game.Contains("data.useContactShadow.useOverride = on || wasOverride;");
            }
            else if (info.Setting == "HBAO")
            {
                ok = Regex.IsMatch(contract, knob + @" = Field\(GraphicsShade, Shade, ""[\w.]+"", """ + info.Slider + @"""") && game.Contains("GameContract." + knob + ".Field");
            }
            else if (info.Knob == GraphicsKnob.ToneCurve)
            {
                ok = game.Contains("_curve = _tone." + info.Slider + ";") && Regex.IsMatch(game, @"private\s+" + info.Setting + @"\s+_tone;");
            }
            else if (info.Knob == GraphicsKnob.ContactQuality)
            {
                ok = game.Contains("_level = _contact." + info.Slider + ";") && Regex.IsMatch(game, @"private\s+" + info.Setting + @"\s+_contact;");
            }
            else if (second)
            {
                ok = !game.Contains("GraphicsKnob." + knob + ",");
            }
            else
            {
                Match line = Regex.Match(game, @"Bind(Colour|Switch)?\(GraphicsKnob\." + knob + @", (_\w+)\." + info.Slider + @"\);");
                bool colour = k + 2 < knobs.Length && knobs[k + 1].Setting == info.Setting && knobs[k + 1].Slider == info.Slider;
                string kind = colour ? "Colour" : info.Slider == "enable" ? "Switch" : string.Empty;
                ok = line.Success && line.Groups[1].Value == kind && Regex.IsMatch(game, @"private\s+" + info.Setting + @"\s+" + line.Groups[2].Value + ";");
                bound += line.Success ? 1 : 0;
            }
            if (!ok)
            {
                wrong.Add(knob);
            }
        }
        int lines = Regex.Matches(game, @"\bBind(?:Colour|Switch)?\(GraphicsKnob\.").Count;
        Check(wrong.Count == 0 && bound >= 30 && lines == bound, "graphics: the game side binds each row of the table to the slider the table names, of the setting it names, and binds nothing else ("
                                                                   + bound + " rows bound, " + lines + " lines" + (wrong.Count == 0 ? string.Empty : "; not: " + string.Join(", ", wrong)) + ")");
        Check(Regex.Matches(game, @"\.overrideState\s*=[^=]").Count == 7 && Regex.Matches(game, @"(?<![\w.])(?:slider|colour|_switches\[k\]|_curve|_level|_radius|_strength)\.value\s*=[^=]").Count == 7
              && Regex.Matches(game, @"\.value\s*=[^=]").Count == 7,
            "graphics: a slider of the pipeline is written and switched by the seven writers of the table's rows and nowhere else in the game side");
        var free = new SortedSet<string>(StringComparer.Ordinal);
        var costly = new SortedSet<string>(StringComparer.Ordinal);
        foreach (KnobInfo info in knobs)
        {
            if (info.Setting != "SunLight" && info.Setting != "MoonLight" && info.Setting != "HBAO")
            {
                (info.Cost == GraphicsCost.Free ? free : costly).Add(info.Setting);
            }
        }
        var made = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(Block(game, "private void Build(Transform root)"), @"_profile\.Add<(\w+)>\(\)"))
        {
            made.Add(m.Groups[1].Value);
        }
        var extra = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(Block(game, "private void BuildExtras()"), @"_profile\.Add<(\w+)>\(\)"))
        {
            extra.Add(m.Groups[1].Value);
        }
        Check(made.SetEquals(free) && extra.SetEquals(costly) && Regex.Matches(game, @"_profile\.Add[<(]").Count == free.Count + costly.Count + 1
              && Regex.IsMatch(Block(game, "public void Apply(float[] values, bool[] set)"),
                  @"if \(set\[\(int\)GraphicsKnob\.ContactShadows\] \|\| set\[\(int\)GraphicsKnob\.MicroShadows\]\)\s*\{\s*BuildExtras\(\);") && Occurs(game, "BuildExtras()") == 2,
            "graphics: the volume carries the Free rows' settings (" + string.Join(", ", made) + ") and is given the costly ones (" + string.Join(", ", extra)
            + ", the corner shading) only when a costly choice first asks: the Free picture never holds them");
        Match ours = Regex.Match(game, @"public const float Priority = (\d+)f;"), theirs = Regex.Match(enhancer, @"_volume\.priority = (\d+)f;");
        Check(ours.Success && theirs.Success && int.Parse(ours.Groups[1].Value, GfxInv) > 100 && int.Parse(ours.Groups[1].Value, GfxInv) < int.Parse(theirs.Groups[1].Value, GfxInv)
              && game.Contains("_volume.isGlobal = true;") && game.Contains("_volume.priority = Priority;") && game.Contains("_volume.enabled = false;"),
            "graphics: the look is one global volume of the mod's own, off until a look is written, over every volume of the game's (its menu's is 100) and under the mod's Picture sliders ("
            + (ours.Success ? ours.Groups[1].Value : "?") + " under " + (theirs.Success ? theirs.Groups[1].Value : "?") + ")");
        Check(game.Contains("public const int VolumeLayer = 31;") && game.Contains("private const int LayerBit = unchecked((int)0x80000000);") && game.Contains("_host.layer = VolumeLayer;")
              && game.Contains("_cameraData.volumeLayerMask = _cameraMask | LayerBit;") && game.Contains("data.volumeLayerMask = mask & ~LayerBit;"),
            "graphics: the volume is on a layer of its own that the player's camera is given and the staff preview's camera is kept off: no other camera of the game sees the look");
        Check(game.Contains("_volume.sharedProfile = _profile;") && game.Contains("_volume.profile = _profile;")
              && game.Contains("VolumeProfile profile = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;") && Regex.Matches(game, @"\.profile\b").Count == 2,
            "graphics: the volume's profile is its own already (another mod asking for it gets no copy), and another volume is read without making it a copy of its profile");
        var touched = new List<string>();
        foreach (string word in new[]
                 {
                     "HDShadowSettings", "LiftGammaGain", "ColorAdjustments", "ShadowsMidtonesHighlights", "ColorCurves", "frameSettings", "FrameSettings", "QualitySettings.SetQualityLevel",
                     "shadowResolution", "antialiasing", "renderScale", "DynamicResolution", ".sharedProfile.", "lodBias", "shadowDistance", "farClipPlane", "cullingMask ="
                 })
        {
            if (game.Contains(word))
            {
                touched.Add(word);
            }
        }
        string everything = GfxAllSources(out _);
        Check(touched.Count == 0 && !Regex.IsMatch(everything, @"\b(?:vSyncCount|targetFrameRate|timeScale)\s*=[^=]"),
            "graphics: the game's own profiles, its shadow and frame settings, its gamma and the Picture sliders' setting are never touched, and v-sync and the frame cap are only read ("
            + (touched.Count == 0 ? "none" : string.Join(", ", touched)) + ")");
        Check(game.Contains("Type kind = GameContract.GraphicsShade.Off ? null : GameContract.OcclusionKind.Type;") && contract.Contains("ContractBook.Part(\"graphics_shade\", \"graphics\",")
              && game.Contains("bool games = _gameOcclusion != null && _gameOcclusion.active && _gameStrength.overrideState && _gameStrength.value > 0f;")
              && Block(game, "private void Shade(float[] values, bool[] set)").Contains("bool on = set[radius] && set[strength] && games;"),
            "graphics: the game's corner shading is reached through the game contract, and made wider only while the game's own is on: its strength is what starts the pass");
    }

    private static void GraphicsGivesBack()
    {
        string game = GfxRead("Graphics", "GraphicsGame.cs"), director = GfxRead("Graphics", "GraphicsDirector.cs"), tests = GfxRead("Graphics", "GraphicsDirector.Tests.cs");
        string guide = GfxRead("Graphics", "GraphicsGuide.cs"), links = GfxRead("Wiring", "Wiring.GraphicsLinks.cs");
        string lights = Block(game, "private void GiveBackLights()"), camera = Block(game, "private void GiveBackCamera()"), back = Block(game, "public void PutBack(bool all)");
        Check(lights.Contains("_sun.color = _sunWas;") && lights.Contains("_moon.color = _moonWas;") && lights.Contains("_sunData.useContactShadow.useOverride = _sunContactOverride;")
              && lights.Contains("_sunData.useContactShadow.@override = _sunContactValue;") && lights.Contains("_moonData.useContactShadow.useOverride = _moonContactOverride;")
              && lights.Contains("_moonData.useContactShadow.@override = _moonContactValue;") && Block(game, "private void HoldLights()").Contains("_sunWas = _sun.color;")
              && Block(game, "private void HoldLights()").Contains("_moonWas = _moon.color;"),
            "graphics: the sun's and the moon's colour and contact shadows are kept as found before the first write and given back exactly");
        Check(camera.Contains("_cameraData.volumeLayerMask = _cameraMask;") && camera.Contains("_previewData.volumeLayerMask = _previewMask;")
              && back.Contains("_volume.enabled = false;") && back.Contains("GiveBackLights();") && Regex.IsMatch(back, @"if \(all\)\s*\{\s*GiveBackCamera\(\);"),
            "graphics: the game's own picture back is the volume off and the two lights as found, and with the option off the cameras' layers as found too");
        string refresh = Block(director, "private void Refresh(bool look)");
        Check(refresh.Contains("if (off || GraphicsGame.MapOpen || _designer || GraphicsGame.Blueprints)") && refresh.Contains("_game.PutBack(off);")
              && game.Contains("public static bool MapOpen => CityMap.IsOpen;") && game.Contains("public static bool Blueprints => InteriorDesignerHelper.BlueprintCreatorMode;")
              && Block(director, "private void DesignerOpened()").Contains("_designer = true;") && Block(director, "private void DesignerShut()").Contains("_designer = false;"),
            "graphics: with the look Off, on the city map (the mod's build mode is the map), in the interior designer and in the blueprint maker the picture is the game's own");
        string gone = Block(game, "public void Dispose()");
        Check(gone.Contains("DropEvents();") && gone.Contains("PutBack(true);") && gone.Contains("CodeAssets.DropProfile(_profile);") && gone.Contains("UnityEngine.Object.Destroy(_host);")
              && Block(director, "private void OnDestroy()").Contains("StopTests();") && Block(director, "private void OnDestroy()").Contains("_game?.Dispose();"),
            "graphics: as the mod unloads the tests stop, the events are let go, everything is given back, and the volume's profile is destroyed with its settings");
        Check(Block(director, "private void Fault(Exception e)").Contains("_game?.PutBack(true);") && Block(director, "private void Fault(Exception e)").Contains("_broken = true;")
              && Block(director, "public void PutAway()").Contains("StopTests();") && Block(director, "public void PutAway()").Contains("_game?.PutBack(true);")
              && guide.Contains(".Cleanup(() => GraphicsDirector.Instance?.PutAway())"),
            "graphics: a fault switches the look off for the session with the game's picture back, and the options' clean-up gives everything back too");
        var events = new List<string>();
        foreach (string name in new[]
                 {
                     "GlobalEvents.onCityMapToggle", "GlobalEvents.onEnterBuilding", "GlobalEvents.onExitBuilding", "InteriorDesignerUI.onOpenInteriorDesigner",
                     "InteriorDesignerUI.onCloseInteriorDesigner", "UndergroundParkingManager.onEnter", "UndergroundParkingManager.onExit"
                 })
        {
            if (!game.Contains("EventHold.Keep(ref " + name + ", ") || !game.Contains(name + " -= "))
            {
                events.Add(name);
            }
        }
        Check(events.Count == 0 && Occurs(game, "EventHold.Keep(") == 7 && Occurs(Block(game, "public void Hook("), "Guard.Wrap") == 6
              && game.Contains("GlobalEvents.RegisterOnGameLoadedLateCallback(Guard.Wrap(loaded));") && !Regex.IsMatch(game, @"(?:GlobalEvents|InteriorDesignerUI|UndergroundParkingManager)\.\w+ \+= "),
            "graphics: each of the game's seven events it listens to is kept through the loads and let go again, every handler behind the guard, and the first look comes of the game's own "
            + "late call once a save is loaded (" + (events.Count == 0 ? "all" : string.Join(", ", events)) + ")");
        Check(director.Contains("MapParts.Add(\"the picture's look\", MapLook);") && Block(director, "private int MapLook(StringBuilder line)").Contains("return back ? 0 : 1;"),
            "graphics: the map check of the session record can ask whether the look is on again once the map has shut");
        string measure = Block(tests, "private IEnumerator Measure()"), hours = Block(tests, "private IEnumerator Hours()");
        Check(measure.Contains("finally") && measure.Contains("_forcedLook = _forcedCost = -1;") && Block(tests, "private void EndMeasure(string why)").Contains("_forcedLook = _forcedCost = -1;")
              && hours.Contains("finally") && hours.Contains("GraphicsGame.HoursEnd();") && Block(tests, "private void EndHours()").Contains("GraphicsGame.HoursEnd();")
              && Block(game, "public static void HoursEnd()").Contains("clock.usingSliderDebugTool = false;") && Block(tests, "private void StopTests()").Contains("EndMeasure(")
              && Block(tests, "private void StopTests()").Contains("EndHours();"),
            "graphics: however a test ends (its own end, a second press, a fault, the city going) the look it held is let go and the game's light is back on the save's own hour");
        Check(Block(tests, "private void StartHours()").Contains("GraphicsGame.InSession") && Block(game, "public static void HoursShow(float hour)").Contains("clock.UpdateHourlyValues(hour);")
              && !game.Contains("SaveGameManager.Current.Hour =") && !game.Contains("SaveGameManager.Current.Minute =") && !tests.Contains("SaveGameManager"),
            "graphics: the hours' test never moves the save's clock, and is refused in a shared session, where the game's clock tool would set the session's traffic");
        Check(tests.Contains("\"Graphics check (cost, \"") && tests.Contains("GraphicsRules.CostVerdict(") && tests.Contains("GraphicsRules.Added(_card, GraphicsRules.CostPairs)")
              && tests.Contains("GraphicsRules.Budget(cost)") && tests.Contains("\"PASS\"") && tests.Contains("\"FAIL\"") && Occurs(tests, "new float[GraphicsRules.CostPairs * 2 + 1]") == 2
              && tests.Contains("\"Graphics check (now): look \"") && director.Contains("\"Graphics check (\""),
            "graphics: the measuring test writes one line for each cost with PASS or FAIL against the rules' budget, by the rules' own arithmetic, and the other two tests a line each");
        Check(Occurs(links, "Fault(\"GraphicsLinks.") == 3 && Occurs(links, "catch (Exception e)") == 3 && links.Contains("GraphicsBridges.Season = Season;")
              && links.Contains("GraphicsBridges.SnowFalling = SnowFalling;") && links.Contains("GraphicsBridges.SnowCover = SnowCover;") && links.Contains("GraphicsBridges.Reset();"),
            "graphics: the three answers it asks of the seasons each have a try with a wiring fault line, and the bridges are emptied when the mod unloads");
    }

    private static void GraphicsRegistered()
    {
        string settings = GfxRead("Core", "Settings.cs"), guide = GfxRead("Graphics", "GraphicsGuide.cs"), mod = GfxRead("Core", "RealLifeOverhaulMod.cs");
        Match look = Regex.Match(settings, @"public static int GraphicsLook = (\d+);"), cost = Regex.Match(settings, @"public static int GraphicsCost = (\d+);");
        Match lookLoad = Regex.Match(settings, @"GraphicsLook = System\.Math\.Max\(0, System\.Math\.Min\((\d+), Int\(modId, GraphicsLookId, GraphicsLook\)\)\);");
        Match costLoad = Regex.Match(settings, @"GraphicsCost = System\.Math\.Max\(0, System\.Math\.Min\((\d+), Int\(modId, GraphicsCostId, GraphicsCost\)\)\);");
        Check(look.Success && cost.Success && int.Parse(look.Groups[1].Value, GfxInv) == GraphicsRules.DefaultLook && int.Parse(cost.Groups[1].Value, GfxInv) == GraphicsRules.DefaultCost
              && settings.Contains("public const string GraphicsLookId = \"graphics_look\";") && settings.Contains("public const string GraphicsCostId = \"graphics_cost\";"),
            "graphics: the two options start as the rules say (look " + GraphicsRules.LookName(GraphicsRules.DefaultLook) + ", cost " + GraphicsRules.CostName(GraphicsRules.DefaultCost) + ")");
        Check(lookLoad.Success && costLoad.Success && int.Parse(lookLoad.Groups[1].Value, GfxInv) == GraphicsLook.Count - 1 && int.Parse(costLoad.Groups[1].Value, GfxInv) == GraphicsCost.Count - 1,
            "graphics: a saved option is held to the looks and the costs there are when the settings load");
        Check(guide.Contains("new[] { \"reallife_gfx_look_off\", \"reallife_gfx_look_natural\", \"reallife_gfx_look_filmic\", \"reallife_gfx_look_vivid\" }") && GraphicsLook.Off == 0
              && GraphicsLook.Natural == 1 && GraphicsLook.Filmic == 2 && GraphicsLook.Vivid == 3 && GraphicsLook.Count == 4
              && guide.Contains("new[] { \"reallife_gfx_cost_free\", \"reallife_gfx_cost_balanced\", \"reallife_gfx_cost_high\" }") && GraphicsCost.Free == 0 && GraphicsCost.Balanced == 1
              && GraphicsCost.High == 2 && GraphicsCost.Count == 3,
            "graphics: the options page lists the looks and the costs in the order of their numbers, Off and Free first");
        Check(Occurs(guide, "GraphicsDirector.OptionChanged();") == 2 && guide.Contains(".Owns(Settings.GraphicsLookId, Settings.GraphicsCostId)")
              && guide.Contains(".Test(\"reallife_gfx_test_cost\", \"reallife_gfx_test_hours\", \"reallife_gfx_test_say\")") && guide.Contains("Features.Add(\"graphics\", \"reallife_meta_title_graphics\")"),
            "graphics: either option moved is on the picture at once, and the feature owns its two options and its three tests");
        Check(GfxRead("Meta", "FeatureList.cs").Contains("Gfx.GraphicsGuide.Register();") && mod.Contains(".Guarded(o => Gfx.GraphicsOptions.AddGraphics(o))")
              && mod.Contains("Guard.Step(\"realistic graphics\", () => Gfx.GraphicsDirector.Attach(_root, context.Logger));")
              && GfxRead("Core", "FeatureOwners.cs").Contains("{ \"RealLifeOverhaul.Gfx\", \"graphics\" }")
              && GfxRead("Meta", "FeatureCities.cs").Contains("new Line(\"graphics\", CityScope.AnyCity,")
              && GfxRead("Meta", "OptionsTree.cs").Contains("S(\"graphics\").For(\"graphics\").Opts(\"graphics_look\", \"graphics_cost\")")
              && GfxRead("Wiring", "Wiring.cs").Contains("GraphicsLinks.Connect") && GfxRead("Wiring", "Wiring.cs").Contains("GraphicsLinks.Disconnect"),
            "graphics: the feature is registered as the others are: the feature list, the options page and its tree, the start behind the guard, the owner of its faults, any city, the wiring");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        var facts = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(guide, "\\.Fact\\(\"([a-z_]+)\""))
        {
            facts.Add(m.Groups[1].Value);
        }
        var missing = new List<string>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        if (en != null && en.TryGetValue("reallife_guide_graphics", out string page))
        {
            foreach (Match m in Regex.Matches(page, "\\{([a-z_]+)\\}"))
            {
                used.Add(m.Groups[1].Value);
                if (!facts.Contains(m.Groups[1].Value))
                {
                    missing.Add(m.Groups[1].Value);
                }
            }
        }
        else
        {
            missing.Add("(no Guide page)");
            page = string.Empty;
        }
        Check(missing.Count == 0 && facts.Count >= 7 && used.SetEquals(facts), "graphics: every figure the Guide page names comes from the rules, and every figure given to it is on the page (" + facts.Count
                                                                               + " facts" + (missing.Count > 0 ? "; not: " + string.Join(", ", missing) : string.Empty) + ")");
        string tip = en != null && en.TryGetValue("reallife_opt_tip_graphics_cost", out string t) ? t : string.Empty;
        string balanced = GraphicsRules.BalancedBudgetMs.ToString("0.0#", GfxInv) + " ms", high = GraphicsRules.HighBudgetMs.ToString("0.0#", GfxInv) + " ms";
        Check(tip.Contains("budget " + balanced) && tip.Contains("budget " + high) && page.Contains("{gfx_free_ms} ms") && page.Contains("{gfx_balanced_ms} ms") && page.Contains("{gfx_high_ms} ms"),
            "graphics: the cost option's tip states the rules' own budgets (" + balanced + ", " + high + "), and the Guide page reads all three from the rules");
        string armed = en != null && en.TryGetValue("reallife_gfx_test_cost_armed", out string a) ? a : string.Empty;
        string button = en != null && en.TryGetValue("reallife_gfx_test_cost", out string b) ? b : string.Empty;
        Check(GfxRead("Graphics", "GraphicsDirector.Tests.cs").Contains("private const float ArmSeconds = 2f;") && armed.Contains("two seconds") && button.Contains("about a minute")
              && page.Contains("minute by minute") && page.Contains("in the sun or in the shade"),
            "graphics: the texts say what the code does: the measurement starts two seconds after the menus shut and takes about a minute, the look follows the clock minute by minute");
    }

    // ---------------------------------------------------------------- the texts

    private static void GraphicsTexts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        Check(en != null, "graphics: the English texts are where the checks look for them (" + folder + ")");
        if (en == null)
        {
            return;
        }
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(GfxAllSources(out _), "\"(reallife_[a-z0-9_]+)\""))
        {
            keys.Add(m.Groups[1].Value);
        }
        foreach (string key in new[] { "reallife_opt_sec_graphics", "reallife_opt_tip_graphics_look", "reallife_opt_tip_graphics_cost", "reallife_contract_part_graphics_shade" })
        {
            keys.Add(key);
        }
        var lacking = new List<string>();
        bool plain = true, brief = true;
        foreach (string key in keys)
        {
            if (!en.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text))
            {
                lacking.Add(key);
                continue;
            }
            plain &= !text.Contains("—") && !text.Contains("–") && !text.Contains(" - ") && !text.Contains("!") && !text.Contains(";");
            brief &= key == "reallife_guide_graphics" || text.Length <= 240;
        }
        Check(keys.Count >= 28 && lacking.Count == 0, "graphics: each of its " + keys.Count + " texts is in English (" + (lacking.Count == 0 ? "all" : string.Join(", ", lacking)) + ")");
        Check(plain && brief, "graphics: its English texts have no dashes, exclamation marks or semicolons, and a notification is 240 characters at most");
        // The other languages are a later step: one that has any of the texts must have them all, each with the English's names in curly brackets.
        var part = new List<string>();
        int whole = 0;
        foreach (string file in System.IO.Directory.GetFiles(folder, "*.json"))
        {
            string code = System.IO.Path.GetFileNameWithoutExtension(file);
            Dictionary<string, string> words = code == "en" ? null : LoadWords(file);
            if (words == null)
            {
                continue;
            }
            int has = 0, wrong = 0;
            foreach (string key in keys)
            {
                if (words.TryGetValue(key, out string text) && !string.IsNullOrWhiteSpace(text))
                {
                    has++;
                    wrong += en.TryGetValue(key, out string english) && GfxNamesOf(text) == GfxNamesOf(english) ? 0 : 1;
                }
            }
            whole += has == keys.Count ? 1 : 0;
            if ((has > 0 && has < keys.Count) || wrong > 0)
            {
                part.Add(code + " (" + has + " of " + keys.Count + (wrong > 0 ? ", " + wrong + " with other names in curly brackets" : string.Empty) + ")");
            }
        }
        Check(part.Count == 0, "graphics: a language that has any of its texts has them all, each with the English's names in curly brackets (" + whole + " languages have them"
                               + (part.Count == 0 ? string.Empty : "; " + string.Join(", ", part)) + ")");
        Check(WhatsNewHas(en, "Realistic graphics, under Performance on the options page"), "graphics: What's new has its sentence in English (any of its rows)");
        string look = en.TryGetValue("reallife_guide_graphics", out string page) ? page : string.Empty;
        bool named = true;
        foreach (string key in new[]
                 {
                     "reallife_gfx_look_off", "reallife_gfx_look_natural", "reallife_gfx_look_filmic", "reallife_gfx_look_vivid", "reallife_gfx_cost_free", "reallife_gfx_cost_balanced",
                     "reallife_gfx_cost_high"
                 })
        {
            named &= en.TryGetValue(key, out string choice) && look.Contains("<b>" + choice + "</b>");
        }
        Check(named && look.Contains("volumetric fog") && look.Contains("Player.log") && look.Contains("'Graphics check (cost'"),
            "graphics: the Guide page names every look and every cost as the options page shows it, says what this game's renderer cannot do, and where the measurement's lines are");
    }

    private static string GfxNamesOf(string text)
    {
        var found = new List<string>();
        foreach (Match m in GfxNames.Matches(text))
        {
            found.Add(m.Value);
        }
        found.Sort(StringComparer.Ordinal);
        return string.Join(",", found);
    }
}
