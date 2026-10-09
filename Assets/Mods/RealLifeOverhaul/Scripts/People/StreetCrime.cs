using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The name the rest of the mod knows street incidents by. The scenes themselves (the foot
    /// chase, the stop and the disturbance that used to live here, and every crime since) are
    /// in Scripts/Crime and are run by CrimeDirector; this forwards to it, so that traffic
    /// accidents, patrols and pedestrian incidents keep asking the same question they always
    /// did: is an incident on the street right now? Nothing here runs per frame.
    /// </summary>
    public class StreetCrime : MonoBehaviour
    {
        public static IModLogger Logger;
        public static StreetCrime Instance { get; private set; }

        /// <summary>True while a crime scene is running. Only one incident of any kind runs at a time.</summary>
        public static bool IsActive => CrimeDirector.IsActive;

        private void Awake()
        {
            Instance = this;
            enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Options-menu test button: stages a street crime right away.</summary>
        public void StageNow()
        {
            if (CrimeDirector.Instance != null)
            {
                CrimeDirector.Instance.StageNow(0);
            }
        }

        /// <summary>Ends whatever scene is running, at once and without a message.</summary>
        public static void EndActive()
        {
            CrimeDirector.EndActive();
        }
    }
}
