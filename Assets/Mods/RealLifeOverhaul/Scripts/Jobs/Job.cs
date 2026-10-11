using BAModAPI;
using Extensions;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Why a job stopped.</summary>
    public enum JobEnd
    {
        Done,          // the shift was worked to its end
        Quit,          // the player stopped it (phone, or by walking away from it)
        Failed,        // the job's own failure: the passenger got out, the round ran out of time
        Interrupted,   // something else took the player away: hospital, custody, a time skip, going indoors
        Fault          // the code met something unexpected
    }

    /// <summary>
    /// What a piece of work paid, line by line. Penalties only ever reduce what the job pays:
    /// the total is never below zero, so a bad shift cannot cost the player money they had.
    /// </summary>
    public struct PaySlip
    {
        public int Units;      // fares, parcels or flyers this covers
        public int Base;
        public int Distance;
        public int Tips;
        public int Bonus;
        public int Penalty;

        public int Total
        {
            get
            {
                int total = Base + Distance + Tips + Bonus - Penalty;
                return total > 0 ? total : 0;
            }
        }

        public void Add(in PaySlip other)
        {
            Units += other.Units;
            Base += other.Base;
            Distance += other.Distance;
            Tips += other.Tips;
            Bonus += other.Bonus;
            Penalty += other.Penalty;
        }
    }

    /// <summary>
    /// What the director hands a job each tick: who and where the player is, the car being
    /// driven if any, and the services every job shares. One object for the whole session,
    /// refreshed in place; a job keeps no copy of anything in it.
    /// </summary>
    public sealed class JobContext
    {
        public JobDirector Director;
        public JobCast Cast;
        public IJobAnimations Animations;
        public IModLogger Logger;

        public PlayerController Player;
        public Vector3 PlayerPosition;
        public CarController Car;        // the car the player is driving right now, or null
        public Rigidbody Body;           // its body
        public float Speed;              // m/s
        public Vector3 Velocity;         // of the car being driven; zero on foot
        public bool Driving => Car != null;

        public JobProps Props;           // what jobs put on the street and in hands
        public JobBody Self;             // the player's own body at work: held still, a clip, a tool
        public JobStand Stand;           // a place of work the player takes by walking on to it
        public JobUniform Uniform;       // work clothes over the player's own, taken off by the director however the job ends
        public JobRental Rental;         // the hired car, when the player has none
        public bool Indoors;             // inside the one building the job allows

        public float Rate;               // level and the pay option, as one factor on what the catalogue says
        public int Level;
        public bool Test;                // started by a test button: needs and daily limits are waived

        /// <summary>The rota shift this job is being worked as, for an employer; null for a gig taken freelance.</summary>
        public CareerShift Shift;

        /// <summary>A set booked at a venue (the agency's bookings); null otherwise.</summary>
        public BookedGig Gig;

        /// <summary>A whole shift's work whatever is left of the day's freelance amount: a test, a rota shift, a booked set.</summary>
        public bool FullShift => Test || Shift != null || Gig != null;

        /// <summary>True if the shift is worked at a rank with this perk.</summary>
        public bool Perk(CareerPerk perk) => Shift != null && Shift.Rank.Has(perk);

        /// <summary>What the rank (or a booking) does to tips: 1 freelance.</summary>
        public float TipFactor => Shift != null ? Shift.Rank.TipFactor : Gig != null ? CareerCatalogue.GigTipFactor : 1f;

        /// <summary>
        /// The shift's figures on the HUD, in a copy of the game's own job panel; null if the game's panel could not be
        /// copied (said once in the log). Made the first time a job asks, shown by the director while that job is on.
        /// </summary>
        public ShiftCard Card => Director.Card();

        /// <summary>Pays a slip through the game's own transaction, counts the work, spends the energy and shows the slip.</summary>
        public void Pay(Job job, in PaySlip slip, string titleKey)
        {
            Director.Pay(job, slip, titleKey);
        }

        /// <summary>
        /// Asks for the job's Moment to be called at a time (Time.time), to the frame: for the instant a hand closes on
        /// something or lets it go (a clip's "grip" or "release"). One at a time: asking again replaces the last.
        /// </summary>
        public void At(float time)
        {
            Director.MomentAt(time);
        }

        /// <summary>Turns the player on the spot to face a place, over a quarter of a second (the game's own turn).</summary>
        public void TurnPlayer(Vector3 towards)
        {
            Vector3 flat = towards - Player.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.04f && Director != null && Director.isActiveAndEnabled)
            {
                Director.StartCoroutine(Player.Character.RotateTowards(towards, 0.25f));
            }
        }

        /// <summary>A line from someone in the job (a passenger, a customer), as one of the game's own toasts.</summary>
        public void Say(string key, float seconds = 7f)
        {
            JobText.Toast(UI.Notification.NotificationType.Info, key, seconds);
        }
    }

    /// <summary>A rota shift for an employer: who for, at what rank, and how it was clocked on to. One object, reused.</summary>
    public sealed class CareerShift
    {
        public Employer Employer;
        public CareerRank Rank;
        public float Hourly;          // the basic wage per hour, raises included, after the pay option and the owner's factor
        public bool Late;
        public float StartMinutes;    // game time at clock-on
        public Vector3 Depot;         // where the employer is: desk and lead shifts are worked there
        public bool HasDepot;
        public bool Counts;           // false for a shift started by a test button: nothing of it goes on the record
    }

    /// <summary>A set the agency booked: where, and what the venue pays on top of the hat.</summary>
    public sealed class BookedGig
    {
        public Vector3 Venue;
        public string VenueName;
        public int Fee;
    }

    /// <summary>
    /// A job the player can take: one small class per job. It is a list of steps the director
    /// ticks five times a second; between ticks it does nothing. It says what to show (the
    /// objective, a marker, a key prompt) by setting the fields below, and the director puts
    /// them on screen. Whatever it puts on the street it takes back in <see cref="End"/>, which
    /// the director calls exactly once on every way a job can stop.
    ///
    /// To add a job: a row in <see cref="JobCatalogue"/>, a class like this in Jobs/Kinds, one
    /// line in <see cref="JobDirector.Create"/>, its strings at the end of en.json.
    /// </summary>
    public abstract class Job
    {
        protected JobContext Ctx;

        public abstract JobDef Def { get; }

        // ------------------------------------------------ what the director shows; set only when it changes
        public string Objective { get; protected set; }
        public string Status { get; protected set; }           // top right of the panel, as words...
        public int ClockSeconds { get; protected set; } = -1;   // ...or a countdown; -1 = none
        public bool HasMarker { get; protected set; }
        public Vector3 MarkerAt { get; protected set; }
        public string MarkerLabel { get; protected set; }
        public string Prompt { get; protected set; }           // null = no key wanted right now
        public Transform PromptOver { get; protected set; }    // whose head the prompt is about (a small marker is drawn over it); null = nobody
        public float PromptLift { get; protected set; }        // metres above that transform the marker sits

        /// <summary>
        /// True while the interaction key is this job's whether or not a prompt is showing: the
        /// mod's own "enter a door, get in a car" key stands aside, and <see cref="Interact"/> is
        /// called on every press (it says so itself when there is nothing in reach).
        /// </summary>
        public virtual bool OwnsInteractKey => false;

        /// <summary>
        /// True for a job that shows everything in the game's own interface (the game's panel over a place, a copy of
        /// its job panel, its activity panel, its notifications): the director then draws nothing of the mod's own
        /// job HUD for it, and its pay slip is one of the game's notifications.
        /// </summary>
        public virtual bool NativeUi => false;

        /// <summary>
        /// True while the job itself has handed the shift to the game's clock (<see cref="ShiftSkip"/>): the game's
        /// time machine running is then the shift being worked, not something that has taken the player away.
        /// </summary>
        public virtual bool PassingTime => false;

        /// <summary>
        /// Up to three things to pick from, shown as a row with their keys (an order's three
        /// possible items, three songs); null when there is nothing to pick. Set only when it changes.
        /// </summary>
        public string[] Choices { get; protected set; }

        /// <summary>Which of the choices is lit (the one last picked), or -1.</summary>
        public int ChoiceLit { get; protected set; } = -1;

        /// <summary>A choice key went down while <see cref="Choices"/> was showing: 0, 1 or 2.</summary>
        public virtual void Choose(int choice, float now)
        {
        }

        /// <summary>
        /// True for the one building this job goes on inside (a client's shop). Entering it does
        /// not end the shift; entering any other does. Street-only jobs say no to everything.
        /// </summary>
        public virtual bool WorksInside(Buildings.Building building)
        {
            return false;
        }

        // ------------------------------------------------ how it is going
        public bool Over { get; private set; }
        public JobEnd Outcome { get; private set; }
        public PaySlip Shift;          // everything paid this shift, for the summary

        /// <summary>Why it could not start, as a key the phone can say; set by <see cref="Begin"/> when it returns false.</summary>
        public string Refusal { get; protected set; }

        /// <summary>Sets the job up where the player is. False (with <see cref="Refusal"/>) if it cannot be done here.</summary>
        public bool Begin(JobContext ctx)
        {
            Ctx = ctx;
            return OnBegin(Time.time);
        }

        protected abstract bool OnBegin(float now);

        /// <summary>Five times a second while the job is on.</summary>
        public abstract void Tick(float now);

        /// <summary>The interaction key, pressed while <see cref="Prompt"/> is shown (or at any time, for a job that <see cref="OwnsInteractKey"/>).</summary>
        public virtual void Interact(float now)
        {
        }

        /// <summary>The time asked for with <see cref="JobContext.At"/> has come (called in the director's frame, not on its tick).</summary>
        public virtual void Moment(float now)
        {
        }

        /// <summary>The car the player is driving hit something, at this closing speed in m/s.</summary>
        public virtual void OnCrash(float speed)
        {
        }

        /// <summary>The player has just been signalled to pull over (PoliceAttention.PulledOver became true).</summary>
        public virtual void OnPoliceStop()
        {
        }

        /// <summary>
        /// Every way out. Takes back whatever is on the street or in the player's hands and
        /// pays for work that was done but not yet paid. Must not throw and must be safe to
        /// call in any state.
        /// </summary>
        public abstract void End(JobEnd why);

        /// <summary>True if stopping now, by choice, counts against the player (work was left undone).</summary>
        public virtual bool QuitIsFailure => false;

        protected void Finish(JobEnd outcome)
        {
            Over = true;
            Outcome = outcome;
        }

        protected void Show(string objective)
        {
            Objective = objective;
        }

        protected void Mark(Vector3 at, string label)
        {
            HasMarker = true;
            MarkerAt = at;
            MarkerLabel = label;
        }

        protected void Unmark()
        {
            HasMarker = false;
        }

        /// <summary>The catalogue's pay for one unit over a distance, at the player's level and the pay option.</summary>
        protected int UnitPay()
        {
            return Mathf.RoundToInt(Def.UnitPay * Ctx.Rate);
        }

        protected int DistancePay(float metres)
        {
            return Mathf.RoundToInt(Def.PayPerMetre * metres * Ctx.Rate);
        }
    }

    /// <summary>The few things every job does with text: look a line up, fill one value in, pick one of several.</summary>
    public static class JobText
    {
        public static string T(string key)
        {
            return JobKeys.Fill(key.GetLocalization());
        }

        /// <summary>A line with one {name} in it. Builds a string: for when something changes, never per frame.</summary>
        public static string T(string key, string name, string value)
        {
            return JobKeys.Fill(key.GetLocalization()).Replace("{" + name + "}", value);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return JobKeys.Fill(key.GetLocalization()).Replace("{" + name + "}", value).Replace("{" + name2 + "}", value2);
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        /// <summary>One of several lines for the same moment, never the one said last time.</summary>
        public static string Pick(string[] keys, ref int last)
        {
            int index = Random.Range(0, keys.Length);
            if (index == last && keys.Length > 1)
            {
                index = (index + 1) % keys.Length;
            }
            last = index;
            return keys[index];
        }

        public static void Toast(UI.Notification.NotificationType type, string key, float seconds = 7f)
        {
            UI.Notification.Notifications.Show(type, key, null, seconds);
        }

        public static void Toast(UI.Notification.NotificationType type, string key, string name, string value, float seconds = 8f)
        {
            // A new dictionary each time: the game keeps hold of it for as long as the toast is listed.
            var data = new System.Collections.Generic.Dictionary<string, string>(1) { { name, value } };
            UI.Notification.Notifications.Show(type, key, data, seconds);
        }
    }
}
