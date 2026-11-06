namespace RealLifeOverhaul.Fame
{
    /// <summary>What stops the player joining an invitation's video call. None means they may. Saved nowhere.</summary>
    public enum FameJoinBlock
    {
        None = 0,
        Driving = 1,
        Talking = 2,        // a conversation, or a question box
        Activity = 3,       // the work or study panel
        Scene = 4,          // a scene or panel of the mod's own (custody, a hearing, a licence test, the options page's messages)
        TripWindow = 5,     // the game's list window: bus stops, the cooking list and more
        Subway = 6,
        Boat = 7,
        Other = 8           // loading, dead, in hospital, the time machine, a camera or a screen that needs the pointer
    }

    /// <summary>What the game and the mod say about the player at the moment they press Join. Filled by FameGame.JoinNow.</summary>
    public struct FameJoinState
    {
        public bool Driving, Talking, Activity, Scene, TripWindow, Subway, Boat, Other;
        // The phone's full menu and the game being paused are deliberately not here: the call is joined from the phone, which
        // pauses the game and sits over the world, so both are true on every honest press of Join.
    }

    /// <summary>
    /// The decision to join a video call, game-free so EconomyCheck can test it. The phone being open and the game being
    /// paused never block: Join is pressed on the Standing page, which is in the phone, and the phone pauses the game
    /// (FullMenu.Toggle). Cost: a few comparisons, once per press.
    /// </summary>
    public static class FameJoin
    {
        /// <summary>The first thing that blocks, most specific first, or None.</summary>
        public static FameJoinBlock Decide(FameJoinState s)
        {
            return s.Driving ? FameJoinBlock.Driving
                : s.Talking ? FameJoinBlock.Talking
                : s.Activity ? FameJoinBlock.Activity
                : s.Subway ? FameJoinBlock.Subway
                : s.Boat ? FameJoinBlock.Boat
                : s.Scene ? FameJoinBlock.Scene
                : s.TripWindow ? FameJoinBlock.TripWindow
                : s.Other ? FameJoinBlock.Other
                : FameJoinBlock.None;
        }

        /// <summary>The locale key of the refusal that names the blocker.</summary>
        public static string Key(FameJoinBlock block)
        {
            switch (block)
            {
                case FameJoinBlock.Driving: return "reallife_fame_join_driving";
                case FameJoinBlock.Talking: return "reallife_fame_join_talking";
                case FameJoinBlock.Activity: return "reallife_fame_join_activity";
                case FameJoinBlock.Scene: return "reallife_fame_join_scene";
                case FameJoinBlock.TripWindow: return "reallife_fame_join_window";
                case FameJoinBlock.Subway: return "reallife_fame_join_subway";
                case FameJoinBlock.Boat: return "reallife_fame_join_boat";
                default: return "reallife_fame_join_busy";
            }
        }
    }
}
