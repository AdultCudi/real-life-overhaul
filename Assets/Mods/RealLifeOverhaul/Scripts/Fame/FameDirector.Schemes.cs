namespace RealLifeOverhaul.Fame
{
    // What an inquiry's findings and bought favour ask of Standing (Scripts/Schemes, through the wiring): the player
    // exposed, the push before the council and a councillor who carries it, and a place on the list of a city reception.
    public sealed partial class FameDirector
    {
        /// <summary>An inquiry found the player out: integrity falls, and a felony is a scandal as a bribe come out is.</summary>
        public void Exposed(bool scandal)
        {
            if (!Running || Save == null)
            {
                return;
            }
            FameCity.Exposed(_file, Save.Day, scandal);
            Logger?.Info("Standing: found out by an inquiry" + (scandal ? ": a scandal until day " + _file.ScandalUntil : string.Empty) + "; integrity "
                         + _file.Rep[(int)FamePart.Integrity] + ".");
        }

        /// <summary>The push before the council, by its number; 0 for none.</summary>
        public int PushPending()
        {
            FameLobby pending = Running ? FameCity.Pending(_file) : null;
            return pending != null ? pending.Id : 0;
        }

        /// <summary>What the lobbyist's fee for a push is today.</summary>
        public long PushFee() => Running ? FameCity.LobbyFee(_file.Weekly) : 0L;

        /// <summary>A councillor was bought: the push before the council is carried on its day. False with none pending.</summary>
        public bool PushCarry()
        {
            FameLobby pending = Running ? FameCity.Pending(_file) : null;
            if (pending == null)
            {
                return false;
            }
            pending.Chance = 100;
            Logger?.Info("Standing: the push before the council (lever " + pending.Lever + " to " + pending.Position + ") is carried: a councillor was bought.");
            return true;
        }

        /// <summary>A place on the list of the next city reception, bought: the invitation comes as any other. False while one waits already or the press is off.</summary>
        public bool BoughtInvite()
        {
            if (!Running || Save == null || !Settings.StandingPress || FamePress.Open(_file) != null)
            {
                return false;
            }
            Invited(FamePress.Offer(_file, FameInviteKind.CityEvent, Save.Day, FameCity.ReceptionHour));
            return true;
        }
    }
}
