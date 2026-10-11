using System;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    // ------------------------------------------------------------------------------------------
    // HOW OTHER FEATURES TALK TO THE JUSTICE SYSTEM
    //
    // Inbound (they call us). All of it is safe at any time, with the feature off, before a save
    // is loaded or with no director: nothing happens then and null / false comes back.
    //
    //   Somebody has been arrested for something serious (police, custody):
    //       Justice.Arrest(new JusticeReport(JusticeLaw.Evading) { Witnesses = 1, InTheAct = true, Fled = true, PaidAtScene = fine, HeldHours = hours });
    //   Charged without being taken in (a summons through the post):
    //       Justice.Summon(new JusticeReport(JusticeLaw.HitRunInjury) { Cameras = true });
    //   The police have the player in front of them (a traffic stop, a station desk): a bench warrant is served.
    //       Justice.PoliceContact(driving: true);
    //   Someone else's car or body was damaged by the player's driving (a civil suit may follow):
    //       Justice.CrashVictim(damages, injured, killed);
    //
    // Outbound (we call them), through the holders in Justice.Bridges. Each has a default that works
    // without the other feature; a feature replaces it once, when it starts:
    //       Justice.Bridges.Law = new MyLawBridge();          // Scripts\Law: record, custody at a precinct, where one is let out
    //       Justice.Bridges.Licences = new MyLicenceBridge(); // Scripts\Licences: suspending the driving licence
    //       Justice.Bridges.Social = new MySocialBridge();    // Scripts\Social: a close friend as character witness and prison visitor
    //       Justice.Bridges.Transport = new MyTransportBridge(); // whoever looks after the car left behind
    // and puts the default back (Justice.Bridges.Reset()) when it is destroyed.
    // ------------------------------------------------------------------------------------------

    /// <summary>What a feature tells the justice system about an arrest or a charge. Minor offences (tickets) are never reported.</summary>
    public sealed class JusticeReport
    {
        public string[] Charges;        // ids from JusticeLaw (JusticeLaw.Evading, ...); unknown ids are ignored
        public int Witnesses;           // people who saw it, the officer included
        public bool Cameras;            // it was filmed
        public bool InTheAct;           // caught doing it
        public bool Fled;               // ran from the police
        public bool Complied;           // stopped when told to, gave no trouble
        public int PaidAtScene;         // already fined on the spot for the same thing: credited against any court fine
        public int HeldHours;           // hours of custody that follow this arrest: counted if the arrest turns out wrongful
        public float[] Evidence;        // optional: the strength of each charge (0 to 1), in the order of Charges, instead of the mod's own sum

        public JusticeReport(params string[] charges)
        {
            Charges = charges;
        }
    }

    /// <summary>Scripts\Law: the criminal record, custody at a precinct, standing with the police.</summary>
    public interface ILawBridge
    {
        /// <summary>Convictions on the player's record that count against them, or -1 to let the justice system use its own list.</summary>
        int PriorConvictions { get; }

        /// <summary>A court convicted the player of this charge (JusticeLaw id) on this day.</summary>
        void Convicted(string chargeId, int day, int caseNumber);

        /// <summary>A conviction in this case was overturned, or the case was dismissed: take it off the record.</summary>
        void Cleared(int caseNumber);

        /// <summary>The record was sealed after a long clean period.</summary>
        void RecordSealed();

        /// <summary>
        /// Hold the player for some hours (never more than JusticeLaw.CustodyCapHours) and call <paramref name="released"/>
        /// when they are out, however it ended. <paramref name="reasonKey"/> is the locale key of the sentence that says why
        /// (<see cref="JusticeLaw.HoldRemand"/>, <see cref="JusticeLaw.HoldSentence"/>). Return false to leave it to the
        /// justice system's own plain time skip.
        /// </summary>
        bool Custody(int hours, string reasonKey, Action released);

        /// <summary>True while the player sits in a cell of theirs, or a save says they do and the stay has yet to be taken up again: nobody in a cell is counted absent from court.</summary>
        bool Holding { get; }

        /// <summary>Where somebody let out of custody or prison is put down: the pavement outside a police station, and that station's name (what the player is told). False to use the courthouse.</summary>
        bool ReleaseSpot(out Vector3 spot, out string name);
    }

    /// <summary>Scripts\Licences: the driving licence.</summary>
    public interface ILicenceBridge
    {
        /// <summary>Suspend the driving licence up to and including this in-game day. Return false to let the justice system keep the date itself.</summary>
        bool SuspendDriving(int untilDay);

        /// <summary>True, false, or null to let the justice system answer from its own date.</summary>
        bool? DrivingSuspended { get; }
    }

    /// <summary>Scripts\Social: the people in the player's life.</summary>
    public interface ISocialBridge
    {
        /// <summary>Whether the player has a friend close enough to speak for them in court and visit them in prison, and that friend's name as shown.</summary>
        bool CloseFriend(out string name);

        /// <summary>That friend did so: a relationship can grow from it. <paramref name="visit"/> is false for a phone call.</summary>
        void Stood(string name, bool visit);
    }

    /// <summary>Whoever looks after the player's car while they are away.</summary>
    public interface ITransportBridge
    {
        /// <summary>
        /// The player is out after <paramref name="daysAway"/> days: bring the car they were taken from to them, or say where
        /// it is impounded. Return false to let the justice system have it towed to them (the mod's VehicleTow).
        /// </summary>
        bool ReturnCar(VehicleInstance car, Vector3 near, int daysAway);
    }

    internal sealed class DefaultLawBridge : ILawBridge
    {
        public int PriorConvictions => -1;
        public void Convicted(string chargeId, int day, int caseNumber) { }
        public void Cleared(int caseNumber) { }
        public void RecordSealed() { }
        public bool Custody(int hours, string reasonKey, Action released) => false;
        public bool Holding => false;

        public bool ReleaseSpot(out Vector3 spot, out string name)
        {
            spot = default;
            name = null;
            return false;
        }
    }

    internal sealed class DefaultLicenceBridge : ILicenceBridge
    {
        public bool SuspendDriving(int untilDay) => false;
        public bool? DrivingSuspended => null;
    }

    // Without Scripts\Social the mod's own friend contact stands in: a friend rung within the last week is a close one.
    internal sealed class DefaultSocialBridge : ISocialBridge
    {
        private const int CloseDays = 7;

        public bool CloseFriend(out string name)
        {
            name = null;
            if (PhoneContacts.Friend == null || ModState.LastFriendCallDay <= 0 || TimeHelper.CurrentDay - ModState.LastFriendCallDay > CloseDays)
            {
                return false;
            }
            name = JusticeText.T("reallife_justice_friend_name");
            return true;
        }

        public void Stood(string name, bool visit)
        {
            ModState.AddModifier(ModState.Social);
        }
    }

    internal sealed class DefaultTransportBridge : ITransportBridge
    {
        public bool ReturnCar(VehicleInstance car, Vector3 near, int daysAway) => false;
    }

    /// <summary>The one door into the justice system for the rest of the mod. See the top of this file.</summary>
    public static class Justice
    {
        /// <summary>The features the justice system leans on. Never null: assigning null puts the default back.</summary>
        public static class Bridges
        {
            private static ILawBridge _law = new DefaultLawBridge();
            private static ILicenceBridge _licences = new DefaultLicenceBridge();
            private static ISocialBridge _social = new DefaultSocialBridge();
            private static ITransportBridge _transport = new DefaultTransportBridge();

            public static ILawBridge Law
            {
                get => _law;
                set => _law = value ?? new DefaultLawBridge();
            }

            public static ILicenceBridge Licences
            {
                get => _licences;
                set => _licences = value ?? new DefaultLicenceBridge();
            }

            public static ISocialBridge Social
            {
                get => _social;
                set => _social = value ?? new DefaultSocialBridge();
            }

            public static ITransportBridge Transport
            {
                get => _transport;
                set => _transport = value ?? new DefaultTransportBridge();
            }

            public static void Reset()
            {
                Law = null;
                Licences = null;
                Social = null;
                Transport = null;
            }
        }

        /// <summary>True when the justice system is switched on and has a loaded save to work with.</summary>
        public static bool Active => JusticeSettings.Enabled && JusticeState.Loaded && JusticeDirector.Instance != null && JusticeDirector.Instance.Working;

        /// <summary>An arrest for something serious: a case is opened, and the bail decision is put to the player once they are free to hear it. Null if nothing was opened.</summary>
        public static JusticeCase Arrest(JusticeReport report)
        {
            return Active ? JusticeDirector.Instance.OpenCase(report, arrested: true, test: false) : null;
        }

        /// <summary>Charged without being taken in: a case with a court date and no bail. Null if nothing was opened.</summary>
        public static JusticeCase Summon(JusticeReport report)
        {
            return Active ? JusticeDirector.Instance.OpenCase(report, arrested: false, test: false) : null;
        }

        /// <summary>
        /// The police have the player in front of them. With a bench warrant out, they are taken in as soon as the stop
        /// is over; driving on a suspended licence is charged. Costs nothing when neither applies.
        /// </summary>
        public static void PoliceContact(bool driving)
        {
            if (Active)
            {
                JusticeDirector.Instance.OnPoliceContact(driving);
            }
        }

        /// <summary>The player's driving cost somebody else: now and then that somebody sues. Bounded, one suit at a time.</summary>
        public static void CrashVictim(int damages, int injured, int killed)
        {
            if (Active)
            {
                JusticeDirector.Instance.OnCrashVictim(damages, injured, killed);
            }
        }

        /// <summary>True while the interaction key belongs to the justice system: at the courthouse sign, at a lawyer's desk, over a piece of litter.</summary>
        public static bool TakesInteractKey => JusticeDirector.Instance != null && JusticeDirector.Instance.WantsKey;

        /// <summary>
        /// The police hold the player for some hours on an arrest another feature made (Licences): served as custody as soon as
        /// nothing else has them, and counted as time served in the case opened today. False if it could not be taken on.
        /// </summary>
        public static bool Hold(int hours)
        {
            return Active && JusticeDirector.Instance.HoldFor(hours);
        }

        /// <summary>
        /// The police have let the player out of the cell they sat in after the arrest behind this case (Scripts/Wiring, from
        /// Scripts/Law). Bail paid there is this case's bail, and no second bail decision is put to them.
        /// </summary>
        public static void ReleasedByPolice(JusticeCase c, int bailPaid)
        {
            if (Active)
            {
                JusticeDirector.Instance.OnReleasedByPolice(c, bailPaid);
            }
        }

        public static bool OnProbation => JusticeState.Loaded && JusticeState.OnProbation(TimeHelper.CurrentDay);

        /// <summary>A bench warrant is out for the player.</summary>
        public static bool Wanted => JusticeState.Loaded && JusticeState.HasWarrant;

        public static bool InPrison => JusticeState.Loaded && JusticeState.InPrison;

        /// <summary>A court has suspended the player's driving licence (asked of the Licences bridge first).</summary>
        public static bool DrivingSuspended
        {
            get
            {
                if (!JusticeState.Loaded)
                {
                    return false;
                }
                bool? theirs = null;
                try
                {
                    theirs = Bridges.Licences.DrivingSuspended;
                }
                catch (Exception)
                {
                    // a bridge that throws is treated as one that has no answer
                }
                return theirs ?? JusticeState.Suspended(TimeHelper.CurrentDay);
            }
        }

        /// <summary>Convictions that count against the player: the Law bridge's figure if it keeps one, else the justice system's own.</summary>
        public static int Priors
        {
            get
            {
                int theirs = -1;
                try
                {
                    theirs = Bridges.Law.PriorConvictions;
                }
                catch (Exception)
                {
                    // as above
                }
                return theirs >= 0 ? theirs : JusticeState.Priors;
            }
        }
    }
}

namespace RealLifeOverhaul
{
    /// <summary>
    /// The reports the mod's own police code makes, so that each place it arrests or charges the
    /// player needs one line. A method returns null when what happened is a matter for a ticket,
    /// not a court; Justice.Arrest and Justice.Summon do nothing with null.
    /// </summary>
    public static class JusticeReports
    {
        /// <summary>Caught after running from a traffic stop (Crime/PoliceAttention): evading, and whatever was done to people on the way.</summary>
        public static JusticeReport Fled(int hurt, int killed, int finedAtScene, int custodyHours)
        {
            var report = killed > 0 ? new JusticeReport(JusticeLaw.Evading, JusticeLaw.HitRunDeath)
                : hurt > 0 ? new JusticeReport(JusticeLaw.Evading, JusticeLaw.HitRunInjury)
                : new JusticeReport(JusticeLaw.Evading, JusticeLaw.Reckless);
            report.Witnesses = 1;        // the officer who chased them
            report.InTheAct = true;
            report.Fled = true;
            report.PaidAtScene = finedAtScene;
            report.HeldHours = custodyHours;
            return report;
        }

        /// <summary>Caught after driving off from a crash (Traffic/PolicePursuit).</summary>
        public static JusticeReport HitAndRun(int hurt, int killed, int finedAtScene, int custodyHours)
        {
            var report = new JusticeReport(killed > 0 ? JusticeLaw.HitRunDeath : hurt > 0 ? JusticeLaw.HitRunInjury : JusticeLaw.HitRun, JusticeLaw.Evading)
            {
                Witnesses = hurt + killed > 0 ? 2 : 1, InTheAct = true, Fled = true, PaidAtScene = finedAtScene, HeldHours = custodyHours
            };
            return report;
        }

        /// <summary>
        /// Somebody on foot was hit and it was settled where it happened. Stayed and nobody died: a fine, no case (null).
        /// Stayed and somebody died: causing death by driving. Left: leaving the scene.
        /// </summary>
        public static JusticeReport Struck(int hurt, int killed, bool stopped, int finedAtScene)
        {
            if (stopped && killed <= 0)
            {
                return null;
            }
            if (!stopped && hurt <= 0 && killed <= 0)
            {
                return null;
            }
            return new JusticeReport(stopped ? JusticeLaw.Manslaughter : killed > 0 ? JusticeLaw.HitRunDeath : JusticeLaw.HitRunInjury)
            {
                Witnesses = 1, InTheAct = stopped, Fled = !stopped, Complied = stopped, PaidAtScene = finedAtScene
            };
        }

        /// <summary>Got away after hurting somebody: the plate was seen, so a summons follows, on thinner evidence than an arrest at the scene.</summary>
        public static JusticeReport Escaped(bool killed)
        {
            return new JusticeReport(killed ? JusticeLaw.HitRunDeath : JusticeLaw.HitRunInjury)
            {
                Witnesses = UnityEngine.Random.value < 0.5f ? 1 : 0, Cameras = UnityEngine.Random.value < 0.35f, Fled = true
            };
        }
    }
}
