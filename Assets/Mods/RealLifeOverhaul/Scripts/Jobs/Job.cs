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

    /// <summary>What the director hands a job each tick: the player, the car being driven and the shared services. Refreshed in place.</summary>
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

        public JobProps Props;           // street props and what is in hand
        public JobBody Self;             // the player's own body at work
        public JobStand Stand;           // a place of work the player takes by walking on to it
        public JobUniform Uniform;       // taken off by the director however the job ends
        public JobRental Rental;         // the hired car, when the player has none
        public bool Indoors;             // inside the one building the job allows

        public float Rate;               // level, pay option and the offer's fee as one factor on the catalogue's pay
        public GigOffer Offer;           // what makes this shift differ: size, fee, crowd, events (the table's figures outside freelance)
        public int Level;
        public bool Test;                // started by a test button: needs and daily limits waived
        public bool HeavyLoad;           // asked for as a heavy load: the courier round in a heavy truck (HeavyRules)

        /// <summary>The rota shift this job is being worked as, for an employer; null for a gig taken freelance.</summary>
        public CareerShift Shift;

        /// <summary>A set booked at a venue (the agency's bookings); null otherwise.</summary>
        public BookedGig Gig;

        /// <summary>A whole shift's work whatever is left of the day's freelance amount: a test, a rota shift, a booked set.</summary>
        public bool FullShift => Test || Shift != null || Gig != null;

        public bool Perk(CareerPerk perk) => Shift != null && Shift.Rank.Has(perk);

        /// <summary>What the rank (or a booking) does to tips: 1 freelance.</summary>
        public float TipFactor => Shift != null ? Shift.Rank.TipFactor : Gig != null ? CareerCatalogue.GigTipFactor : 1f;

        /// <summary>The shift's figures on a copy of the game's job panel; null if it could not be copied. Made on first use.</summary>
        public ShiftCard Card => Director.Card();

        /// <summary>Pays a slip through the game's own transaction, counts the work, spends the energy and shows the slip.</summary>
        public void Pay(Job job, in PaySlip slip, string titleKey)
        {
            Director.Pay(job, slip, titleKey);
        }

        /// <summary>Calls the job's Moment at this Time.time, to the frame (a hand closing on something). Asking again replaces the last.</summary>
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
        public float Hourly;          // per hour: raises, pay option and owner's factor included
        public float Piece = 1f;      // factor on what the work pays: the rank's, with the raises at a rank paid only by what it takes (CareerState.Piece)
        public bool Late;
        public float StartMinutes;    // game minutes
        public Vector3 Depot;         // where the employer is: desk and lead shifts are worked there
        public bool HasDepot;
        public bool Counts;           // false for a test button's shift: nothing goes on the record
    }

    /// <summary>A set the agency booked: where, and what the venue pays on top of the hat.</summary>
    public sealed class BookedGig
    {
        public Vector3 Venue;
        public string VenueName;
        public int Fee;
    }

    /// <summary>
    /// A job the player can take, one class per kind, ticked by the director. What it puts out it takes back in <see cref="End"/>,
    /// called exactly once however it stops. A new kind is a row in <see cref="JobCatalogue"/> and a case in <see cref="JobDirector.Create"/>.
    /// </summary>
    public abstract class Job
    {
        protected JobContext Ctx;

        public abstract JobDef Def { get; }

        /// <summary>What to do now, in words: line 1 of the shift card.</summary>
        public string Objective { get; protected set; }

        /// <summary>Where the game's guider points (its arrow and the city map).</summary>
        public bool HasMarker { get; protected set; }
        public Vector3 MarkerAt { get; protected set; }
        public string MarkerLabel { get; protected set; }

        /// <summary>True while the interaction key is the job panel's first button: the mod's own door and car key stands aside.</summary>
        public virtual bool OwnsInteractKey => false;

        /// <summary>True while the job has left the shift to the game's clock (<see cref="ShiftSkip"/>): the time machine is not an interruption.</summary>
        public virtual bool PassingTime => false;

        /// <summary>A panel button that is one of several choices: 0, 1 or 2.</summary>
        public virtual void Choose(int choice, float now)
        {
        }

        /// <summary>True for the one building this job works inside (a client's shop); entering any other ends the shift.</summary>
        public virtual bool WorksInside(Buildings.Building building)
        {
            return false;
        }

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

        /// <summary>The time asked for with <see cref="JobContext.At"/> has come; called in the director's frame.</summary>
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

        /// <summary>Every way out: takes back what is on the street or in hand and pays for work done. Must not throw, in any state.</summary>
        public abstract void End(JobEnd why);

        /// <summary>True if stopping now, by choice, counts against the player (work was left undone).</summary>
        public virtual bool QuitIsFailure => false;

        /// <summary>What <see cref="End"/> would pay for unpaid work if the shift stopped now through nobody's fault. Changes nothing.</summary>
        protected virtual bool Owed(out PaySlip slip)
        {
            slip = default;
            return false;
        }

        /// <summary>The clock the rest of the shift can be left to, or null.</summary>
        protected virtual ShiftClock Clocked => null;

        /// <summary>What the clock has done in a run still going (the drinks cart). Changes nothing.</summary>
        protected virtual bool ClockOwed(out PaySlip slip)
        {
            slip = default;
            ShiftClock clock = Clocked;
            return clock != null && clock.Owed(out slip);
        }

        /// <summary>Everything earned and not yet paid, as one slip, for the line a save carries (<see cref="JobSave"/>).</summary>
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

        /// <summary>A closed or refused client calls the work off without a strike; the clock settles before the job's usual end settles the hand work.</summary>
        protected bool ClientClosed(in DoorSpot client, bool refused = false)
        {
            if (Over)
            {
                return true;
            }
            if (!refused && JobSites.Available(client))
            {
                return false;
            }
            Clocked?.Stop();
            string name = JobSites.NameOf(client);
            Ctx.Logger?.Info("Jobs: '" + Def.Id + "' client " + name + " (" + client.Name + ") has closed or refused entry; the shift ends as Interrupted, with earned work paid and no strike.");
            JobText.Toast(UI.Notification.NotificationType.Info, "reallife_job_client_closed", "place", name, 8f);
            Finish(JobEnd.Interrupted);
            return true;
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

        // The offer (GigOffers) drew these figures: a job rolls none of them itself.

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

        /// <summary>A unit is done, <paramref name="done"/> so far: the offer's big tipper, if any, tips here so many units of <paramref name="unitMoney"/>.</summary>
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
            Ctx.Logger?.Info("Jobs: '" + Def.Id + "' event: a big tipper at unit " + done + " (the offer drew unit " + offer.TipperAt + ") tips " + tip
                             + " (" + offer.TipperUnits.ToString("F2", CultureInfo.InvariantCulture) + " units of " + unitMoney + ").");
        }

        /// <summary>Announces the offer's speed bonus, if it drew one; <paramref name="allowed"/> is the real seconds for the whole shift.</summary>
        protected void BeginSpeed(float now, float allowed)
        {
            GigOffer offer = Ctx.Offer;
            if (!offer.HasSpeed)
            {
                return;
            }
            _speedBy = now + allowed * offer.SpeedShare;
            int bonus = Mathf.RoundToInt(offer.SpeedUnits * UnitPay());
            JobText.Toast(UI.Notification.NotificationType.Info, "reallife_gig_speed_offer", "amount", JobText.Money(bonus), "time",
                ShiftFigures.Clock(Mathf.RoundToInt(allowed * offer.SpeedShare)), 10f);
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
