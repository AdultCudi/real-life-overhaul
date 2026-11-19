using RealLifeOverhaul.Careers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The scene as an officer on a police shift (Careers/Police) answers it: what each move does to the people on the street.</summary>
    internal abstract partial class CrimeScene
    {
        /// <summary>
        /// A call for backup brings an officer on foot if none is there. Settled: an arrest has the officer put the suspect in the cuffs and into the car as
        /// every arrest here does (the suspect is one of the street's people, who have no record or court of the player's, so the arrest is the officer's file's
        /// only), and a talk or a ticket sends everyone on their way. Not settled: the suspect runs and the rest leave. Over before it is begun: an arrest or the
        /// leaving already under way is left to play out. Cost: a few role calls, once.
        /// </summary>
        public bool Duty(DutyMove move, DutyResult result, float now, Vector3 player)
        {
            if (!Active || Step >= StepArrest)
            {
                return false;
            }
            if (move == DutyMove.Backup)
            {
                if (!Cop.Present && !Indoors)
                {
                    SendOfficer(Scene, 16f, 26f);
                }
                return true;
            }
            Catchable = null;
            if (result != DutyResult.Success)
            {
                Flee(First, player);
                Go(StepLeaving, now, 20f);
                return true;
            }
            if (move == DutyMove.Arrest && First.Present)
            {
                if (!Cop.Present && !Indoors)
                {
                    SendOfficer(Scene, 14f, 24f);
                }
                if (Cop.Present)
                {
                    Arrest(First, now, ArrestEnding());
                    return true;
                }
            }
            Leave(now, player);
            return true;
        }

        // The name of the ending of this crime that is an arrest, so the cuffs are announced with its lines; "" (nothing announced) where it has none.
        private string ArrestEnding()
        {
            CrimeEnding[] endings = Entry != null ? Entry.Endings : null;
            for (int i = 0; endings != null && i < endings.Length; i++)
            {
                if (endings[i].Arrest)
                {
                    return endings[i].Name;
                }
            }
            return string.Empty;
        }
    }
}
