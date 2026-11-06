using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Flare;

// When Flare draws a face of the game's portrait camera (Social/Flare/FlareFaceMoment.cs), on the model of the game's own
// fades in DoorFadeCheck.cs. The owner's session of 25 Oct 2026: 61 faces "behind a black screen", each on the first frame
// of a door's fade out (UiFader.isFading), with the street showing; 0.2 s later the game's own frame at the door
// (165 to 597 ms under "coroutines and timers", the heap up 11 to 108 MB), which a walk in with no face had too (228 ms,
// 42 MB). A face is 7 to 18 ms of one frame; it now waits for the overlay at full strength.
internal static partial class Program
{
    private const float FaceReadBack = 2f * FadeFrame;   // seconds: the camera's read-back comes a frame or two after the render
    private const float FaceRest = 0.25f;                // seconds: PortraitTurn.HiddenPaceSeconds

    private static void FaceFadeChecks()
    {
        foreach (bool tweenFirst in new[] { false, true })
        {
            string order = tweenFirst ? " (the game's tween stepped before the mod's Update)" : " (the mod's Update first)";
            foreach (int load in new[] { 1, 3, 12, 40 })
            {
                FaceFade(FadeModel(g => GameEnter(g, load), tweenFirst, 100f), "a walk into a building, its load " + load + " frame(s)" + order, 1);
            }
            FaceFade(FadeModel(GameExit, tweenFirst, 200f), "a walk out of a building" + order, 1);
            FaceFade(FadeModel(GameTaxi, tweenFirst, 300f), "a taxi's ride" + order, 4);
        }
        FaceMoments();
        FaceSources();
    }

    // Faces waiting all along: each one drawn with nothing showing through, at least 'least' of them; the rule before seen.
    private static void FaceFade(List<FadeFrameState> frames, string what, int least)
    {
        int drawn = 0, seen = 0, seenBefore = 0;
        float next = 0f, nextBefore = 0f;
        foreach (FadeFrameState f in frames)
        {
            bool covered = f.Dark && f.Read >= 0.99f;   // SceneFinds.Covered, read while the game fades
            if (f.At >= next && FlareFaceMoment.Of(false, false, false, f.Dark, covered, true, true) == FaceMoment.Black)
            {
                drawn++;
                seen += f.Shown < 0.95f || f.Read < 0.99f ? 1 : 0;   // (a step of 9.6 ms of a fade of 0.2 s: the picture at 5% at the most)
                next = f.At + FaceReadBack + FaceRest;
            }
            if (f.At >= nextBefore && f.Dark)
            {
                seenBefore += f.Shown < 0.95f ? 1 : 0;   // up to 0.52.1: any frame of the fade
                nextBefore = f.At + FaceReadBack + FaceRest;
            }
        }
        Check(drawn >= least && seen == 0, "face pace: " + what + ": every face with the screen black (" + drawn + " drawn, " + seen + " seen)");
        Check(seenBefore >= 1, "face pace: " + what + ": the rule before drew a face with the picture showing through the fade (" + seenBefore + ")");
    }

    // The moments themselves: the loading screen held for the faces, a load not held, the fades, play with the app open.
    private static void FaceMoments()
    {
        Check(FlareFaceMoment.Of(true, true, true, true, false, true, true) == FaceMoment.Load
              && FlareFaceMoment.Of(true, false, true, true, true, true, true) == FaceMoment.None,
            "face pace: behind the held loading screen only while its faces are under way");
        Check(FlareFaceMoment.Of(false, true, true, true, true, true, true) == FaceMoment.None, "face pace: a load not held for the faces draws none");
        Check(FlareFaceMoment.Of(false, false, false, true, false, true, true) == FaceMoment.None
              && FlareFaceMoment.Of(false, false, false, true, false, false, false) == FaceMoment.None,
            "face pace: in a fade, the picture showing through, none, not even for the app open");
        Check(FlareFaceMoment.Of(false, false, false, true, true, false, false) == FaceMoment.Black, "face pace: the black screen at full strength, the faces drawn ahead");
        Check(FlareFaceMoment.Of(false, false, false, false, false, true, true) == FaceMoment.App
              && FlareFaceMoment.Of(false, false, false, false, false, true, false) == FaceMoment.None
              && FlareFaceMoment.Of(false, false, false, false, false, false, true) == FaceMoment.None,
            "face pace: in play only with Flare open and a card in view waiting");
        Check(FlareFaceMoment.Of(false, false, false, false, true, false, false) == FaceMoment.None, "face pace: an overlay read as full with no fade under way draws none");
    }

    // Over the sources: the painter asks the rule, with the overlay's strength, and reads the fade nowhere else.
    private static void FaceSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string painter = Source(scripts, @"Social\Flare\FlareFacePainter.cs");
        string now = Body(painter, "private static FaceMoment Now()");
        Check(now.Contains("FlareFaceMoment.Of(") && now.Contains("fading && SceneFinds.Covered"), "face pace: the painter's moment is the rule's, with the black screen's strength");
        int fades = 0;
        foreach (string line in painter.Split('\n'))
        {
            fades += line.Contains("UiFader.isFading") ? 1 : 0;
        }
        Check(fades == 1, "face pace: the painter reads the game's fade in one place, its moment (" + fades + ")");
        string update = Body(painter, "private void OnUpdate()");
        Check(update.Contains("FaceMoment moment = Now();") && update.Contains("moment == FaceMoment.None"), "face pace: no face is drawn but at its moment");
    }
}
