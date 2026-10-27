using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>The kinds of large scene the mod stages in the street. One of each at most; and, through <see cref="Scenes"/>, one at all.</summary>
        [Flags]
        public enum Scene
        {
            None = 0,
            Accident = 1,      // Traffic/TrafficAccidents: wrecks, a cordon, ambulance and police
            Crime = 2,         // Crime/CrimeDirector: a staged crime and its arrest
            Event = 4,         // Events/EventDirector: a fire, or a street event on a closed road
            Rally = 8          // Civic/CampaignWorld: a campaign rally
        }

        /// <summary>
        /// The one question every street scene asks before it starts: is another one on? Every scene is staged around the
        /// player, so the directors' flags are the whole answer; a building site stands for days and is asked about by
        /// place. Four field reads, asked on each director's roll timer. A new scene is a value in <see cref="Scene"/>, a
        /// line in <see cref="Active"/>, and AnyBut(its own value) in its start test.
        /// </summary>
        public static class Scenes
        {
            /// <summary>How near a building site counts as beside it, in metres: the length of road an accident or an event can take up.</summary>
            public const float Beside = 90f;

            public static Scene Active =>
                (TrafficAccidents.IsActive ? Scene.Accident : Scene.None)
                | (CrimeDirector.IsActive ? Scene.Crime : Scene.None)
                | (Events.EventDirector.IsActive ? Scene.Event : Scene.None)
                | (CivicDirector.RallyOn ? Scene.Rally : Scene.None);

            public static bool AnyLargeSceneActive => Active != Scene.None;

            /// <summary>True if a scene other than the asker's own kind is on.</summary>
            public static bool AnyBut(Scene own) => (Active & ~own) != Scene.None;

            /// <summary>True if a new scene at this place would stand beside another scene or a building site.</summary>
            public static bool Near(Vector3 position, float metres, Scene own = Scene.None)
            {
                return AnyBut(own) || CivicDirector.SiteNear(position, metres);
            }

            /// <summary>
            /// True while a panel, test paper or driven test of the mod's has the player's attention; a new panel waits for
            /// false, so panels queue and never stack. The game's own dialogs are LifeEvents.IsSafeForEvent.
            /// </summary>
            public static bool PlayerHeld => PlayerInScene || ThirdPersonCamera.IsUiOpen();

            /// <summary>
            /// PlayerHeld without the game's own screens that need the pointer (the phone, the map): only the mod's scenes and panels.
            /// A thing started from the phone asks this one, since the phone is up when it is pressed.
            /// </summary>
            public static bool PlayerInScene =>
                Law.Custody.Active || JusticePanel.IsOpen || LicenceDirector.IsActive || Meta.MetaUi.AnyOpen;
        }
    }
}
