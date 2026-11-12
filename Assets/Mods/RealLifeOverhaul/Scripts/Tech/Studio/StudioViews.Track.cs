namespace RealLifeOverhaul.Tech
{
    public static partial class StudioViews
    {
        /// <summary>The five points of a game's making the page shows, in order: the pitch, the first review, the playtest, the second review and the launch (the model's own, no more).</summary>
        public const int MilestoneCount = 5;
        public const int MsPitch = 0, MsReview1 = 1, MsPlaytest = 2, MsReview2 = 3, MsLaunch = 4;

        /// <summary>Where a point stands: met, waiting for the player at the desk or on its way now, the one after it, or later on.</summary>
        public const int StateDone = 0, StateNow = 1, StateNext = 2, StateLater = 3;

        /// <summary>
        /// Where each of the five points stands for a game, written into <paramref name="states"/>: the pitch is met by the game existing, a review by its milestone having passed (it
        /// waits while the milestone is open), the playtest once the game is in polish (it waits when the build has enough to show and bugs lie unfound), the launch by the sale.
        /// Returns how many are met.
        /// </summary>
        public static int Milestones(GameDev g, int[] states)
        {
            int met = 0;
            bool building = g.Phase <= (int)GamePhase.Polish;
            bool playtestWaits = g.Phase == (int)GamePhase.Production && StudioRules.Progress(g) >= StudioCatalogue.TrailerWork && g.Found == 0 && g.Hidden > 0;
            states[MsPitch] = StateDone;
            states[MsReview1] = g.Milestone == (int)GamePhase.Pre ? StateNow : g.Phase >= (int)GamePhase.Production ? StateDone : StateNext;
            states[MsPlaytest] = g.Phase >= (int)GamePhase.Polish ? StateDone : playtestWaits ? StateNow : g.Phase == (int)GamePhase.Production ? StateNext : StateLater;
            states[MsReview2] = g.Milestone == (int)GamePhase.Production ? StateNow : g.Phase >= (int)GamePhase.Polish ? StateDone : g.Phase == (int)GamePhase.Production ? StateNext : StateLater;
            states[MsLaunch] = g.Launched >= 0 ? StateDone : g.Phase == (int)GamePhase.Ready ? StateNow : building && g.Phase == (int)GamePhase.Polish ? StateNext : StateLater;
            for (int i = 0; i < MilestoneCount; i++)
            {
                met += states[i] == StateDone ? 1 : 0;
            }
            return met;
        }
    }
}
