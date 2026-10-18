using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The name the rest of the mod asks about street incidents by; <see cref="CrimeDirector"/> runs the scenes.
    /// Disabled on Awake: no frame cost.
    /// </summary>
    public class StreetCrime : MonoBehaviour
    {
        public static IModLogger Logger;

        /// <summary>True while a crime scene runs. Only one incident of any kind runs at a time.</summary>
        public static bool IsActive => CrimeDirector.IsActive;

        private void Awake()
        {
            enabled = false;
        }

        /// <summary>Ends whatever scene is running, at once and without a message.</summary>
        public static void EndActive()
        {
            CrimeDirector.EndActive();
        }
    }
}
