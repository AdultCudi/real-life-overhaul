namespace RealLifeOverhaul.Flare
{
    /// <summary>When Flare may draw a face (FlareFaceMoment.Of).</summary>
    public enum FaceMoment
    {
        None = 0,     // not now
        Load = 1,     // behind the loading screen held for the faces: every face waiting, no rest
        Black = 2,    // behind the game's black screen at full strength: every face waiting, a quarter of a second apart
        App = 3       // Flare open: the faces cards in view ask for, two seconds apart
    }

    /// <summary>
    /// When a face of the game's portrait camera (7 to 18 ms of one frame: the look merged and the camera's render) may be
    /// drawn, so that no frame the player sees pays for one. A door's fades take 0.2 s each way and the picture shows
    /// through them: up to 0.52.1 a face was drawn on the first frame of the fade out (UiFader.isFading), seen, 0.2 s before
    /// the game's own frame at the door. Now only on a frame the overlay covers whole. Game-free (the flags are handed in),
    /// checked by NavCheck on the model of the game's fades. Cost: a few flag tests, asked only while faces wait.
    /// </summary>
    public static class FlareFaceMoment
    {
        /// <param name="holding">Core/LoadHold holds the loading screen.</param>
        /// <param name="loadOn">The faces behind that loading screen are under way and not stopped.</param>
        /// <param name="loading">The game's loading screen is up (not held for the faces).</param>
        /// <param name="fading">UI.UiFader.isFading: the game's black screen, its fades included.</param>
        /// <param name="covered">The black overlay at full strength (Core/SceneFinds.Covered).</param>
        /// <param name="appShowing">Flare is open.</param>
        /// <param name="inView">Cards in view wait for a face.</param>
        public static FaceMoment Of(bool holding, bool loadOn, bool loading, bool fading, bool covered, bool appShowing, bool inView)
        {
            if (holding)
            {
                return loadOn ? FaceMoment.Load : FaceMoment.None;
            }
            if (loading)
            {
                return FaceMoment.None;   // a load not held for the faces: the city's portrait camera may not be there
            }
            if (fading)
            {
                return covered ? FaceMoment.Black : FaceMoment.None;   // in a fade the picture shows: nothing, not even the app's
            }
            return appShowing && inView ? FaceMoment.App : FaceMoment.None;
        }
    }
}
