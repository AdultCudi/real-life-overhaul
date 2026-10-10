using System;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Driving licences with the law and the courts:
        ///
        ///   Licences.Law            an arrest made by Licences is served through the courts' custody when they are on
        ///                           (counted as time served) and the old roadside time skip when they are not;
        ///   Justice.Bridges.Licences a sentence that suspends the licence suspends the real one, and "suspended" is one
        ///                           answer whether twelve points or a court did it;
        ///   Licences.Driving.TwoWheelerOf, OwnsMotorbike, GarageBridges.HasMotorcycleLicence   the Garage's motorbikes are the motorcycle class
        ///   SuspendedAtStop, DrunkDrivingBooked   what a stop found that is the courts' business too: credited, never owed twice.
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
                Licences.Driving.TwoWheelerOf = GarageBridges.TwoWheelerKind;       // a motorbike is the motorcycle class; a bicycle needs no licence
                Licences.Driving.OwnsMotorbike = GarageBridges.OwnsMotorbike;       // a save that already owns one is given the class once
                GarageBridges.HasMotorcycleLicence = () => Licences.Driving.Has(LicenceClass.Motorcycle);   // the dealer says so at the sale; riding is policed like driving
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
            /// Stopped at the wheel with the licence suspended, and fined for it at the window: the courts charge the same
            /// thing (Justice.PoliceContact), so what was just paid is credited to that case and not owed twice.
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
            /// Drink at the wheel was booked at a stop (fined there, points added). The second time inside the weeks points
            /// count for, it is a case as well: dangerous driving, with the roadside fine credited. Not when they were caught
            /// after running: that arrest is already before the court on the same charge.
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

            /// <summary>Licences asks the law.</summary>
            private sealed class LawForLicences : ILicenceLawBridge
            {
                private readonly DefaultLaw _own = new DefaultLaw();   // the old roadside time skip, for when the courts are off

                public void Record(string offenceId, bool arrested)
                {
                    // Law's record has no line for a licence offence (it is the police's own, of tickets and cells): nothing to add here.
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

                // A suspension the court handed down before the two were connected (or in a save from before) is brought across once.
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
                        return false;   // licences are off: the courts keep their own date, as before
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
