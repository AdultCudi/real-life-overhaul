using GleyTrafficSystem;
using UI.Notification;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The ways into a cell, one for each catcher. True if the law has taken over (the caller keeps its patrol car still
    /// while <see cref="Custody.OnScene"/>); false if the caller does what it did before. The caller charges the fines,
    /// once, and passes what they came to.
    /// </summary>
    public static class Arrests
    {
        /// <summary>Caught after driving on from a traffic stop (Crime/PoliceAttention).</summary>
        public static bool FledStop(bool crashed, int hurt, int killed, int paid, VehicleComponent patrol)
        {
            int charges = LawBook.Bit(Charge.FledStop)
                          | (crashed ? LawBook.Bit(Charge.HitAndRun) : 0)
                          | (hurt > 0 ? LawBook.Bit(Charge.InjuryFled) : 0)
                          | (killed > 0 ? LawBook.Bit(Charge.DeathFled) : 0);
            return Custody.Arrest(charges, paid, patrol);
        }

        /// <summary>Caught after driving off from a crash (Traffic/PolicePursuit). With nobody hurt, only a crash is held for.</summary>
        public static bool HitAndRun(bool crashed, int hurt, int killed, int paid, VehicleComponent patrol)
        {
            int charges = (killed > 0 ? LawBook.Bit(Charge.DeathFled) : 0) | (hurt > 0 ? LawBook.Bit(Charge.InjuryFled) : 0);
            if (charges == 0 || crashed)
            {
                charges |= LawBook.Bit(Charge.HitAndRun);
            }
            return Custody.Arrest(charges, paid, patrol);
        }

        /// <summary>Stopped and waited after hitting somebody: an injury is a fine, a death an arrest on the spot.</summary>
        public static bool StayedAtScene(int killed, int paid, VehicleComponent patrol)
        {
            return killed > 0 && Settings.LawArrests && Custody.Arrest(LawBook.Bit(Charge.DeathStayed), paid, patrol);
        }

        /// <summary>One more 911 call about nothing today (Crime/CrimeDirector): from the fifth, a few hours.</summary>
        public static bool WastedCall(int callsToday, int paid)
        {
            return callsToday >= LawBook.WastedCallsForCustody && Settings.LawArrests && Custody.Arrest(LawBook.Bit(Charge.WastedCalls), paid, null);
        }

        /// <summary>Drove off after a crash and was not caught: the car is known for the day, and with somebody hurt they are wanted.</summary>
        public static void Escaped(int hurt, int killed)
        {
            if (!Settings.LawArrests || !LawState.Loaded)
            {
                return;
            }
            PoliceAttention.KnowCar();
            if (hurt + killed > 0)
            {
                Record.Add(LawBook.Bit(Charge.Wanted), 0, 0, Outcome.Escaped);
                // What they ran from, for the day they hand themselves in at a desk.
                LawState.WantedCharges = LawBook.Bit(killed > 0 ? Charge.DeathFled : Charge.InjuryFled);
                LawState.WantedDay = TimeHelper.CurrentDay;
            }
            if (SaveGameManager.Current != null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_law_wanted", null, 10f);
            }
        }

        /// <summary>A ticket at the roadside (Crime/PoliceAttention): no cell, a line on the record.</summary>
        public static void Cited(int fine)
        {
            if (LawState.Loaded)
            {
                Record.Add(LawBook.Bit(Charge.Citation), 0, fine, Outcome.Cited);
            }
        }
    }
}
