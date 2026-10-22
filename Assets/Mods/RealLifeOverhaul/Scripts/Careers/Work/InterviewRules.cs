namespace RealLifeOverhaul.Careers
{
    /// <summary>Where and how a job interview is held (<see cref="InterviewRules.Way"/>).</summary>
    public enum InterviewWay
    {
        Panel = 0,     // on the game's panel where the player stands: a video interview from a home with no computer
        Door = 1,      // in at the employer's entrance door, the black screen while it lasts, out at the same door
        InPlace = 2,   // inside the employer's building already, or no door of it found: the black screen where the player stands
        Computer = 3   // a video interview at the home's computer, walked to and sat at as the game's own computer seats its user
    }

    /// <summary>How an interview's activity ended, whichever way the game or the mod ended it (<see cref="InterviewRules.End"/>).</summary>
    public enum InterviewEnd
    {
        Finished = 0,   // its time passed, at the clock's pace or by the game's skip
        GivenUp = 1,    // Stop, the skip called off part way, or the energy gone
        CalledOff = 2,  // the walk to the door stopped before it began
        LetGo = 3       // the careers let it go: a load, the option switched off, a fault, the city going
    }

    /// <summary>What an interview's end counts (<see cref="InterviewRules.Count"/>).</summary>
    public enum InterviewCount
    {
        Nothing = 0,   // nothing counted and nothing said
        Left = 1,      // the player left it: a notice, and it can still be attended within its hour
        Whole = 2      // it was sat: an offer or a no
    }

    /// <summary>What the screen and the player get back once an interview at a place is over (<see cref="InterviewRules.Out"/>).</summary>
    public enum InterviewOut
    {
        Nothing = 0,   // no black screen was put up: nothing to give back
        Unfade = 1,    // the black screen taken off where the player stands
        AtDoor = 2,    // the player put out at the door they went in at, facing the street, then the black screen taken off
        Cleared = 3    // the city went from under it (a load): the black screen's flag put right, the player left where the load put them
    }

    /// <summary>
    /// The rules of a job interview's place: the way it is held, what each end counts, what is given back and how the self-check
    /// judges the player's place at the door afterwards. Game-free (EconomyCheck runs it).
    /// </summary>
    public static class InterviewRules
    {
        public const float OutReach = 0.6f;    // metres from the step chosen at the door within which the player counts as out at it
        public const float OutFacing = 0.9f;   // the least cosine between the player's heading and the door's outward one: facing the street

        /// <summary>
        /// The way an interview is held: a video interview at the home's computer, else on the panel where the player stands; one
        /// at a place in at its entrance door when the player is outside by it and the door is found, else where the player stands.
        /// </summary>
        public static InterviewWay Way(bool remote, bool inside, bool door, bool computer)
        {
            if (remote)
            {
                return computer ? InterviewWay.Computer : InterviewWay.Panel;
            }
            return !inside && door ? InterviewWay.Door : InterviewWay.InPlace;
        }

        /// <summary>True if the interview's time passes behind the black screen by the game's own skip; in a Going Public session the clock is everyone's and passes at its pace.</summary>
        public static bool SkipBehind(bool clockShared) => !clockShared;

        /// <summary>How the activity ended. settle: its minutes go to the director (else the careers let it go); begun: the player was in; whole: its whole length passed.</summary>
        public static InterviewEnd End(bool settle, bool begun, bool whole)
        {
            if (!settle)
            {
                return InterviewEnd.LetGo;
            }
            if (!begun)
            {
                return InterviewEnd.CalledOff;
            }
            return whole ? InterviewEnd.Finished : InterviewEnd.GivenUp;
        }

        /// <summary>What an end counts: the whole interview an offer or a no, one given up a notice, the rest nothing.</summary>
        public static InterviewCount Count(InterviewEnd end)
        {
            switch (end)
            {
                case InterviewEnd.Finished: return InterviewCount.Whole;
                case InterviewEnd.GivenUp: return InterviewCount.Left;
                default: return InterviewCount.Nothing;
            }
        }

        /// <summary>
        /// What is given back on every end, the same whatever the end: nothing if the screen never went black; with the city gone,
        /// only the black screen's flag; one that went in at the door comes out at it; one held where the player stood stays there.
        /// </summary>
        public static InterviewOut Out(InterviewWay way, bool faded, bool sameCity)
        {
            if (!faded)
            {
                return InterviewOut.Nothing;
            }
            if (!sameCity)
            {
                return InterviewOut.Cleared;
            }
            return way == InterviewWay.Door ? InterviewOut.AtDoor : InterviewOut.Unfade;
        }

        /// <summary>True if the player is told that a computer at home would seat them: a video interview from a home with none, once.</summary>
        public static bool TellNoComputer(bool remote, bool computer, bool told) => remote && !computer && !told;

        /// <summary>The self-check's judgement of the player out at the door: this close to the step chosen, facing the street.</summary>
        public static bool OutAtDoor(float metres, float facing) => metres <= OutReach && facing >= OutFacing;
    }
}
