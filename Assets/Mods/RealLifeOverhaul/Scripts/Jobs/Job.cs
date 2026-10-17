using System.Globalization;
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

    // (PaySlip, what a piece of work paid line by line, is in Jobs/PaySlip.cs: game-free, so the offline check compiles it.)

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

        public float Rate;               // level, the pay option and the offer's fee, as one factor on what the catalogue says
        public GigOffer Offer;           // what makes this shift differ from another of the gig: its size, fee, crowd and events (the table's figures for a shift that is not a freelance gig)
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
    /// ticks five times a second; between ticks it does nothing. It shows everything in the game's
    /// own interface (<see cref="ShiftFigures"/> on a copy of the game's job panel, <see cref="JobPanel"/>
    /// over the place of work, <see cref="ShiftClock"/> for the rest of a shift left to the game's
    /// clock) and says where the game's guider points by setting a marker. Whatever it puts on the
    /// street or on screen it takes back in <see cref="End"/>, which the director calls exactly once
    /// on every way a job can stop.
    ///
    /// To add a job: a row in <see cref="JobCatalogue"/>, a class like this in Jobs/Kinds, one
    /// line in <see cref="JobDirector.Create"/>, its strings at the end of en.json.
    /// </summary>
    public abstract class Job
    {
        protected JobContext Ctx;

        public abstract JobDef Def { get; }

        // ------------------------------------------------ what the job says; set only when it changes
        /// <summary>What to do now, in words: the job writes it on line 1 of its shift card (<see cref="ShiftFigures"/>).</summary>
        public string Objective { get; protected set; }

        /// <summary>The place the game's own guider (arrow and city map) points at; the director keeps it where the job puts it.</summary>
        public bool HasMarker { get; protected set; }
        public Vector3 MarkerAt { get; protected set; }
        public string MarkerLabel { get; protected set; }

        /// <summary>
        /// True while the interaction key is the job's panel's first button (<see cref="JobPanel"/>, the game's own box over a
        /// place): the mod's own "enter a door, get in a car" key stands aside. The game runs the button on its key or
        /// on a click; the mod reads no key.
        /// </summary>
        public virtual bool OwnsInteractKey => false;

        /// <summary>
        /// True while the job itself has handed the shift to the game's clock (<see cref="ShiftSkip"/>): the game's
        /// time machine running is then the shift being worked, not something that has taken the player away.
        /// </summary>
        public virtual bool PassingTime => false;

        /// <summary>One of the job's panel buttons that is a choice of several (a drink, a song, a van, what to say): 0, 1 or 2. Called by the button, never by a key.</summary>
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

        /// <summary>
        /// What <see cref="End"/> would pay if the shift stopped now through nobody's fault, for work done by hand and not
        /// yet paid: the slip the job's own settlement would hand over, and nothing else. It changes nothing (it is asked
        /// when the game is saved, and the shift goes on). A job that pays each unit as it is done has nothing owed.
        /// </summary>
        protected virtual bool Owed(out PaySlip slip)
        {
            slip = default;
            return false;
        }

        /// <summary>The clock the rest of this job's shift can be left to, or null: what it has done in a run that is still going is owed too.</summary>
        protected virtual ShiftClock Clocked => null;

        /// <summary>What the clock has done in a run that is still going, for a job that runs the clock itself (the drinks cart). Changes nothing.</summary>
        protected virtual bool ClockOwed(out PaySlip slip)
        {
            slip = default;
            ShiftClock clock = Clocked;
            return clock != null && clock.Owed(out slip);
        }

        /// <summary>
        /// Everything this shift has earned and not been paid, as one slip: the work done by hand with the tips and
        /// bonuses its events earned (one slip, bounded at nothing, as the director pays it), and what the clock has done in
        /// a run that is still going. Changes nothing: for the line a save carries (<see cref="JobSave"/>).
        /// </summary>
        public PaySlip Unpaid()
        {
            if (!Owed(out PaySlip slip))
            {
                slip = default;
            }
            slip.Tips += _eventTips;
            slip.Bonus += _eventBonus;
            slip = slip.Bounded();
            if (ClockOwed(out PaySlip clock))
            {
                slip.Add(clock);
            }
            return slip;
        }

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

        /// <summary>The catalogue's pay for one unit over a distance, at the player's level, the pay option and the offer's fee.</summary>
        protected int UnitPay()
        {
            return Mathf.RoundToInt(Def.UnitPay * Ctx.Rate);
        }

        // ------------------------------------------------ what the offer drew (GigOffers): the job rolls none of it itself

        /// <summary>How big this shift is: the offer's number of units, and on a freelance day never more than the day has left.</summary>
        protected int ShiftSize()
        {
            int units = Ctx.Offer.Units > 0 ? Ctx.Offer.Units : Def.ShiftUnits;
            return Ctx.FullShift ? units : Mathf.Min(units, JobState.LeftToday(Def));
        }

        private int _eventTips;
        private int _eventBonus;
        private bool _tipperDone;
        private float _speedBy = -1f;
        private bool _speedSettled;

        /// <summary>Money the shift's events have earned that no slip has carried yet.</summary>
        public bool HasEventMoney => _eventTips > 0 || _eventBonus > 0;

        /// <summary>Puts the events' money on the slip that is being paid, once.</summary>
        public void TakeEventMoney(ref PaySlip slip)
        {
            slip.Tips += _eventTips;
            slip.Bonus += _eventBonus;
            _eventTips = 0;
            _eventBonus = 0;
        }

        /// <summary>
        /// A unit has been done by hand: <paramref name="done"/> of them so far. The one big tipper of the shift, if the offer drew
        /// one, turns up at the unit it says: a tip of so many units' pay (<paramref name="unitMoney"/> is what one unit is worth,
        /// the fare for a ride), said in one of the game's notifications and paid with the next slip.
        /// </summary>
        protected void UnitDone(int done, int unitMoney)
        {
            GigOffer offer = Ctx.Offer;
            if (_tipperDone || offer.TipperAt <= 0 || done < offer.TipperAt)
            {
                return;
            }
            _tipperDone = true;
            int tip = Mathf.RoundToInt(offer.TipperUnits * unitMoney);
            if (tip <= 0)
            {
                return;
            }
            _eventTips += tip;
            JobText.Toast(UI.Notification.NotificationType.Success, "reallife_gig_tipper", "amount", JobText.Money(tip), 8f);
            Ctx.Logger?.Info("Jobs: '" + Def.Id + "' event: a big tipper at unit " + done + " (the offer drew unit " + offer.TipperAt + ") tips " + tip + " (" + offer.TipperUnits.ToString("F2", CultureInfo.InvariantCulture) + " units of " + unitMoney + ").");
        }

        /// <summary>
        /// The client's speed bonus, if the offer drew one: said at the start with its money and its time, paid by
        /// <see cref="EndSpeed"/> if the work is finished in time. <paramref name="allowed"/> is the real seconds the job gives the whole shift.
        /// </summary>
        protected void BeginSpeed(float now, float allowed)
        {
            GigOffer offer = Ctx.Offer;
            if (!offer.HasSpeed)
            {
                return;
            }
            _speedBy = now + allowed * offer.SpeedShare;
            int bonus = Mathf.RoundToInt(offer.SpeedUnits * UnitPay());
            JobText.Toast(UI.Notification.NotificationType.Info, "reallife_gig_speed_offer", "amount", JobText.Money(bonus), "time", ShiftFigures.Clock(Mathf.RoundToInt(allowed * offer.SpeedShare)), 10f);
            Ctx.Logger?.Info("Jobs: '" + Def.Id + "' event: the client adds " + bonus + " (" + offer.SpeedUnits.ToString("F2", CultureInfo.InvariantCulture) + " units) if the shift is done within "
                             + Mathf.RoundToInt(allowed * offer.SpeedShare) + " s of " + Mathf.RoundToInt(allowed) + " s.");
        }

        /// <summary>The whole shift has been done by hand: the speed bonus is paid if there was one and the time was kept.</summary>
        protected void EndSpeed(float now)
        {
            if (_speedBy < 0f || _speedSettled)
            {
                return;
            }
            _speedSettled = true;
            int bonus = Mathf.RoundToInt(Ctx.Offer.SpeedUnits * UnitPay());
            bool kept = now <= _speedBy;
            if (kept && bonus > 0)
            {
                _eventBonus += bonus;
                JobText.Toast(UI.Notification.NotificationType.Success, "reallife_gig_speed_paid", "amount", JobText.Money(bonus), 8f);
            }
            else
            {
                JobText.Toast(UI.Notification.NotificationType.Info, "reallife_gig_speed_missed", 6f);
            }
            Ctx.Logger?.Info("Jobs: '" + Def.Id + "' event: speed bonus " + (kept ? "kept, " + bonus + " added" : "missed by " + (now - _speedBy).ToString("F0") + " s") + ".");
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

        private static readonly string[] Numbers = new string[100];

        /// <summary>A small whole number as a text, made once each: for a panel that is written on every tick.</summary>
        public static string Number(int number)
        {
            if (number < 0 || number >= Numbers.Length)
            {
                return number.ToString();
            }
            return Numbers[number] ?? (Numbers[number] = number.ToString());
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

        public static void Toast(UI.Notification.NotificationType type, string key, string name, string value, string name2, string value2, float seconds)
        {
            var data = new System.Collections.Generic.Dictionary<string, string>(2) { { name, value }, { name2, value2 } };
            UI.Notification.Notifications.Show(type, key, data, seconds);
        }

        public static void Toast(UI.Notification.NotificationType type, string key, string name, string value, float seconds = 8f)
        {
            // A new dictionary each time: the game keeps hold of it for as long as the toast is listed.
            var data = new System.Collections.Generic.Dictionary<string, string>(1) { { name, value } };
            UI.Notification.Notifications.Show(type, key, data, seconds);
        }
    }
}
