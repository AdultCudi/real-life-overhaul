using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What a feature tells the justice system about an arrest or a charge. Tickets are never reported.</summary>
    public sealed class JusticeReport
    {
        public string[] Charges;        // JusticeLaw ids; unknown ids are ignored
        public int Witnesses;           // the officer included
        public bool Cameras;
        public bool InTheAct;
        public bool Fled;               // ran from the police
        public bool Complied;           // stopped when told to and gave no trouble
        public int PaidAtScene;         // fined on the spot for the same thing: credited against any court fine
        public int Damages;             // owed for what was hit and not yet charged: the case holds it, paid only with a conviction
        public int HeldHours;           // custody after this arrest: compensated if the arrest turns out wrongful
        public float[] Evidence;        // optional: each charge's strength (0 to 1) in place of the mod's own sum

        public JusticeReport(params string[] charges)
        {
            Charges = charges;
        }
    }

    /// <summary>Scripts/Law: the criminal record, custody at a precinct, and where somebody is let out.</summary>
    public interface ILawBridge
    {
        /// <summary>Convictions that count against the player, or -1 to use the justice system's own record.</summary>
        int PriorConvictions { get; }

        void Convicted(string chargeId, int day, int caseNumber);

        /// <summary>A conviction in this case was quashed, or the case dismissed: off the record.</summary>
        void Cleared(int caseNumber);

        void RecordSealed();

        /// <summary>Holds the player some hours and calls <paramref name="released"/> however it ends. False: the justice system's time skip.</summary>
        bool Custody(int hours, string reasonKey, Action released);

        /// <summary>True while the player sits in one of its cells (or a save says so): nobody in a cell is counted absent.</summary>
        bool Holding { get; }

        /// <summary>The pavement outside a police station, and its name, where somebody let out is put down. False: the courthouse.</summary>
        bool ReleaseSpot(out Vector3 spot, out string name);
    }

    /// <summary>Scripts/Licences: the driving licence.</summary>
    public interface ILicenceBridge
    {
        /// <summary>Suspends the licence up to and including this day. False lets the justice system keep the date itself.</summary>
        bool SuspendDriving(int untilDay);

        /// <summary>Null lets the justice system answer from its own date.</summary>
        bool? DrivingSuspended { get; }
    }

    /// <summary>Scripts/Social: the people in the player's life.</summary>
    public interface ISocialBridge
    {
        /// <summary>A friend close enough to speak for them in court and visit them in prison, and the name shown.</summary>
        bool CloseFriend(out string name);

        /// <summary>That friend did so; <paramref name="visit"/> is false for a phone call.</summary>
        void Stood(string name, bool visit);
    }

    /// <summary>Whoever looks after the player's car while they are away.</summary>
    public interface ITransportBridge
    {
        /// <summary>Brings back the car the player was taken from, <paramref name="daysAway"/> days on. False: the justice system tows it.</summary>
        bool ReturnCar(VehicleInstance car, Vector3 near, int daysAway);
    }

    /// <summary>Without Scripts/Law: no record of its own, no cells, released at the courthouse.</summary>
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

    /// <summary>Without Scripts/Licences: the justice system keeps the suspension date itself.</summary>
    internal sealed class DefaultLicenceBridge : ILicenceBridge
    {
        public bool SuspendDriving(int untilDay) => false;
        public bool? DrivingSuspended => null;
    }

    /// <summary>Without Scripts/Social: the mod's own Friend contact, close if rung within the last week.</summary>
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

    /// <summary>Without a transport feature: the car is towed to the player.</summary>
    internal sealed class DefaultTransportBridge : ITransportBridge
    {
        public bool ReturnCar(VehicleInstance car, Vector3 near, int daysAway) => false;
    }

    /// <summary>
    /// The one door into the justice system for the rest of the mod, safe at any time: with the feature off or no save, a
    /// call does nothing. Justice reaches other features through <see cref="Bridges"/>, set by each when it starts.
    /// </summary>
    public static class Justice
    {
        /// <summary>The features the justice system leans on, each with a default that works alone. Assigning null puts the default back.</summary>
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

            /// <summary>Thousandths more a lawyer asks of somebody famous. Null: none.</summary>
            public static Func<int> Fame;

            /// <summary>A hearing of the player's is over (somebody famous makes it a story).</summary>
            public static Action Heard;

            public static void Reset()
            {
                Law = null;
                Licences = null;
                Social = null;
                Transport = null;
                Fame = null;
                Heard = null;
            }
        }

        /// <summary>True when the justice system is switched on, working, and has a loaded save.</summary>
        public static bool Active => JusticeSettings.Enabled && JusticeState.Loaded && JusticeDirector.Instance != null && JusticeDirector.Instance.Working;

        /// <summary>An arrest for something serious: bail is put to the player once they are free to hear it. Null if no case was opened.</summary>
        public static JusticeCase Arrest(JusticeReport report)
        {
            return Active ? JusticeDirector.Instance.OpenCase(report, arrested: true, test: false) : null;
        }

        /// <summary>Charged without being taken in: a court date and no bail. Null if no case was opened.</summary>
        public static JusticeCase Summon(JusticeReport report)
        {
            return Active ? JusticeDirector.Instance.OpenCase(report, arrested: false, test: false) : null;
        }

        /// <summary>The police have the player in front of them: a bench warrant is served, driving while suspended is charged.</summary>
        public static void PoliceContact(bool driving)
        {
            if (Active)
            {
                JusticeDirector.Instance.OnPoliceContact(driving);
            }
        }

        /// <summary>The player's driving cost somebody else, who now and then sues. One suit at a time.</summary>
        public static void CrashVictim(int damages, int injured, int killed)
        {
            if (Active)
            {
                JusticeDirector.Instance.OnCrashVictim(damages, injured, killed);
            }
        }

        /// <summary>True while the interaction key belongs to the justice system (the courthouse sign, a lawyer's desk, litter).</summary>
        public static bool TakesInteractKey => JusticeDirector.Instance != null && JusticeDirector.Instance.WantsKey;

        /// <summary>Hours in a cell on another feature's arrest, served when nothing else has the player and counted in today's case.</summary>
        public static bool Hold(int hours)
        {
            return Active && JusticeDirector.Instance.HoldFor(hours);
        }

        /// <summary>The police let the player out after this case's arrest: bail paid there is the case's bail.</summary>
        public static void ReleasedByPolice(JusticeCase c, int bailPaid)
        {
            if (Active)
            {
                JusticeDirector.Instance.OnReleasedByPolice(c, bailPaid);
            }
        }

        public static bool OnProbation => JusticeState.Loaded && JusticeState.OnProbation(TimeHelper.CurrentDay);

        public static bool Wanted => JusticeState.Loaded && JusticeState.HasWarrant;

        public static bool InPrison => JusticeState.Loaded && JusticeState.InPrison;

        /// <summary>A court has suspended the player's driving licence (the Licences bridge is asked first).</summary>
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
                    // a bridge that throws has no answer
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
                    // a bridge that throws has no answer
                }
                return theirs >= 0 ? theirs : JusticeState.Priors;
            }
        }
    }

    /// <summary>The reports the mod's own police code makes. Null is a matter for a ticket, which Arrest and Summon ignore.</summary>
    public static class JusticeReports
    {
        /// <summary>Caught after running from a traffic stop (Crime/PoliceAttention).</summary>
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
            return new JusticeReport(killed > 0 ? JusticeLaw.HitRunDeath : hurt > 0 ? JusticeLaw.HitRunInjury : JusticeLaw.HitRun, JusticeLaw.Evading)
            {
                Witnesses = hurt + killed > 0 ? 2 : 1, InTheAct = true, Fled = true, PaidAtScene = finedAtScene, HeldHours = custodyHours
            };
        }

        /// <summary>A pedestrian hit: null if the player stayed and nobody died, else manslaughter or leaving the scene.</summary>
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

        /// <summary>
        /// Got away after hurting somebody but the plate was seen: a summons, on thinner evidence than an arrest. The case
        /// takes the damages from the later bill, so that a dismissal clears them with the charge.
        /// </summary>
        public static JusticeReport Escaped(bool killed, int damages)
        {
            return new JusticeReport(killed ? JusticeLaw.HitRunDeath : JusticeLaw.HitRunInjury)
            {
                Witnesses = UnityEngine.Random.value < 0.5f ? 1 : 0, Cameras = UnityEngine.Random.value < 0.35f, Fled = true, Damages = Math.Max(0, damages)
            };
        }
    }
}
