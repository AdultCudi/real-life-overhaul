using System;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Driving licences with the law and the courts: an arrest by Licences is served in the courts' custody when they
        /// are on, a court's suspension is the licence's own, the Garage's motorbikes are the motorcycle class, and what a
        /// stop fined is credited to the courts' case, never owed twice.
        /// </summary>
        public static class LicenceLinks
        {
            private static readonly LawForLicences Law = new LawForLicences();
            private static readonly LicencesForCourt Court = new LicencesForCourt();

            internal static void Connect()
            {
                Court.Forget();
                Licences.Law = Law;
                Justice.Bridges.Licences = Court;
                Licences.Driving.TwoWheelerOf = GarageBridges.TwoWheelerKind;       // a bicycle needs no licence
                Licences.Driving.OwnsMotorbike = GarageBridges.OwnsMotorbike;       // a save that already owns one is given the class once
                GarageBridges.HasMotorcycleLicence = () => Licences.Driving.Has(LicenceClass.Motorcycle);
            }

            internal static void Disconnect()
            {
                Licences.Law = null;
                Justice.Bridges.Licences = null;
                Licences.Driving.TwoWheelerOf = null;
                Licences.Driving.OwnsMotorbike = null;
                GarageBridges.HasMotorcycleLicence = null;
                Court.Forget();
            }

            /// <summary>
            /// Fined at a stop for driving while suspended: the courts charge the same (Justice.PoliceContact), so the fine
            /// is credited to that case.
            /// </summary>
            public static void SuspendedAtStop(int finedAtScene)
            {
                try
                {
                    if (Justice.Active)
                    {
                        Justice.Summon(new JusticeReport(JusticeLaw.Unlicensed) { Witnesses = 1, InTheAct = true, Complied = true, PaidAtScene = finedAtScene });
                    }
                }
                catch (Exception e)
                {
                    Fault("LicenceLinks.SuspendedAtStop", e);
                }
            }

            /// <summary>
            /// Drink driving booked at a stop: the second time while the points count it is also a case of dangerous
            /// driving, the fine credited. Not after running away: that arrest is already before the court.
            /// </summary>
            internal static void DrunkDrivingBooked(bool fled)
            {
                try
                {
                    if (fled || !Justice.Active)
                    {
                        return;
                    }
                    int today = TimeHelper.CurrentDay;
                    int times = 0;
                    for (int i = 0; i < LicenceState.Entries.Count; i++)
                    {
                        if (LicenceState.Entries[i].Code == PointCode.DrunkDriving && today - LicenceState.Entries[i].Day < LicenceLaw.PointsExpireDays)
                        {
                            times++;
                        }
                    }
                    if (times >= 2)
                    {
                        Justice.Summon(new JusticeReport(JusticeLaw.Reckless) { Witnesses = 1, InTheAct = true, Complied = true, PaidAtScene = CrimeLaw.DrunkDrivingFine });
                    }
                }
                catch (Exception e)
                {
                    Fault("LicenceLinks.DrunkDrivingBooked", e);
                }
            }

            private sealed class LawForLicences : ILicenceLawBridge
            {
                private readonly DefaultLaw _own = new DefaultLaw();   // the old roadside time skip, for when the courts are off

                public void Record(string offenceId, bool arrested)
                {
                    // Law's record has no line for a licence offence: it holds only tickets and cells.
                }

                public bool Custody(int hours)
                {
                    try
                    {
                        return Justice.Active ? Justice.Hold(hours) : _own.Custody(hours);
                    }
                    catch (Exception e)
                    {
                        Fault("LicenceLinks.Custody", e);
                        return false;
                    }
                }
            }

            /// <summary>The courts ask Licences. Its date is the first day driving is allowed again; the courts' is the last day it is not.</summary>
            private sealed class LicencesForCourt : ILicenceBridge
            {
                private int _court = -1;   // the date in LicenceState that stands for the court's suspension; 0 none, -1 not looked at yet

                public void Forget()
                {
                    _court = -1;
                }

                private static bool Kept => LicenceState.Loaded && Settings.DrivingLicence;

                // A court's suspension from before the two were connected is brought across once.
                private void Sync()
                {
                    if (_court >= 0)
                    {
                        return;
                    }
                    _court = JusticeState.Loaded && JusticeState.Suspended(TimeHelper.CurrentDay) ? JusticeState.SuspendedUntil + 1 : 0;
                    if (_court > LicenceState.SuspendedUntil)
                    {
                        LicenceState.SuspendedUntil = _court;
                    }
                }

                public bool SuspendDriving(int untilDay)
                {
                    if (!Kept)
                    {
                        return false;   // licences are off: the courts keep their own date
                    }
                    Sync();
                    if (untilDay > 0)
                    {
                        _court = untilDay + 1;
                        if (LicenceState.SuspendedUntil < _court)
                        {
                            LicenceState.SuspendedUntil = _court;
                        }
                    }
                    else
                    {
                        // Lifted (an appeal won). A longer suspension for points is not the court's to lift.
                        if (_court > 0 && LicenceState.SuspendedUntil == _court)
                        {
                            LicenceState.SuspendedUntil = 0;
                        }
                        _court = 0;
                    }
                    return true;
                }

                public bool? DrivingSuspended
                {
                    get
                    {
                        if (!Kept)
                        {
                            return null;
                        }
                        Sync();
                        return Licences.Driving.Suspended;
                    }
                }
            }
        }
    }
}
