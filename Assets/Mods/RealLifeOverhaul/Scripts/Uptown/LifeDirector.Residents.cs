using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// A block of the player's whose last flat is sold given to its residents (docs/features/construction.md, "The handover"): until
    /// then it is the player's, its deed written as it stands, so no system of the city's or the game's takes it (a city's building
    /// with no owner is given one of the city's landlords, LifeDirector.Complete; the game sells and lets only what nobody holds).
    /// Now the district's record is the city's, the player's deed goes as the game's own sale leaves a building, its flats are
    /// their buyers' homes, and the whole-building check gives it its landlord as every city building has one. A block an earlier
    /// version gave its residents at the keys with flats unsold is given back. Nothing here runs a frame; a call is a look at the
    /// standing buildings, when a block's last flat sells or once a day while an earlier version's block waits to be given back.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private const int GiveBackDays = 30;   // days after a block's handover its record may have been made (the district asked again each day): a later one is another building's

        /// <summary>
        /// The block finished on a lot given to its residents (DistrictBuildings.ToResidents). Null when done or the city's already;
        /// else why not: no record, a Going Public session (the player's things there are the host's record), the district's line
        /// unreadable, or a block set up as the player's that does not stand at this load (its deed cannot be reached; asked again).
        /// </summary>
        public static string ToResidents(string lotId)
        {
            FinishedBuilding f = _state != null ? _state.On(lotId) : null;
            if (f == null)
            {
                return "no finished building of the district's is recorded on lot " + (lotId ?? "(none)");
            }
            if (f.Owner == FinishOwner.City)
            {
                return null;
            }
            if (GoingPublic.InSession)
            {
                return "a Going Public session: who owns a building is the host's record";
            }
            if (_stateUnread)
            {
                return "the district's own line in this save cannot be read, and is kept as it is";
            }
            MadeBuilding made = StandingMade(lotId);
            if (f.Settled)
            {
                if (made == null)
                {
                    return "it does not stand at this load, so the player's deed cannot be reached";
                }
                string why = LifeGame.ToResidents(made, SaveGameManager.Current);
                if (why != null)
                {
                    return why;
                }
            }
            f.Owner = FinishOwner.City;   // a block not yet stood is set up as the city's when it stands (LifeGame.Settle)
            f.Price = 0.0;
            WriteState();
            _stamp++;
            if (made != null && f.Settled)
            {
                CompleteOne(made, f, "its flats sold");   // its landlord, as every city building has one, and the whole-building line
                Mapped(made, "given to its residents");
            }
            Logger?.Info("Uptown: the block on lot " + lotId + (made != null ? " at " + made.Address : string.Empty) + " has sold its last flat: its residents', the city's in the district's line"
                         + (f.Settled ? ", the player's deed gone" : ", set up as the city's when it stands") + ".");
            return null;
        }

        /// <summary>
        /// A block handed over on <paramref name="handed"/> that an earlier version gave its residents though flats were unsold, the
        /// player's again (DistrictBuildings.GiveBack): its record the player's, their deed written at <paramref name="price"/> as a
        /// handover writes it and the landlord named for it gone (LifeGame.Settle). Null when done or the player's already; else why
        /// not: no record, one not of that handover (finished more than <see cref="GiveBackDays"/> days after it, so a building the
        /// city has put up there since is never taken), a session, the district's line unreadable, or not standing at this load.
        /// </summary>
        public static string GiveBack(string lotId, int handed, double price)
        {
            FinishedBuilding f = _state != null ? _state.On(lotId) : null;
            if (f == null)
            {
                return "no finished building of the district's is recorded on lot " + (lotId ?? "(none)");
            }
            if (f.Owner == FinishOwner.Player)
            {
                return null;
            }
            if (f.Day < handed || f.Day > handed + GiveBackDays)
            {
                return "the building recorded there was finished on day " + f.Day + ", not at the block's handover on day " + handed;
            }
            if (GoingPublic.InSession)
            {
                return "a Going Public session: who owns a building is the host's record";
            }
            if (_stateUnread)
            {
                return "the district's own line in this save cannot be read, and is kept as it is";
            }
            MadeBuilding made = StandingMade(lotId);
            if (f.Settled)
            {
                if (made == null)
                {
                    return "it does not stand at this load, so the player's deed cannot be written";
                }
                string why = LifeGame.Settle(made, FinishOwner.Player, price, SaveGameManager.Current);
                if (why != null)
                {
                    return why;
                }
            }
            f.Owner = FinishOwner.Player;   // a block not yet stood gets its deed when it stands (LifeGame.Settle)
            f.Price = price;
            WriteState();
            _stamp++;
            if (made != null && f.Settled)
            {
                CompleteOne(made, f, "given back");
                Mapped(made, "given back to the player");
            }
            Logger?.Info("Uptown: the block on lot " + lotId + (made != null ? " at " + made.Address : string.Empty) + ", its residents' from its handover on day " + handed
                         + " with flats unsold, is the player's again" + (f.Settled ? ", their deed written" : ", their deed written when it stands") + ".");
            return null;
        }
    }
}
