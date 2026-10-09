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
        /// The one question every street scene asks before it starts: is another one on? Each
        /// director keeps its own flag (a field read); this puts them side by side, so that a
        /// scene added later is one line here and not one line in every other director.
        ///
        ///     if (Wiring.Scenes.AnyBut(Wiring.Scene.Crime)) { not now }          a director's own start test
        ///     if (Wiring.Scenes.Near(place, Wiring.Scenes.Beside)) { not here }   the same, and no building site beside it
        ///
        /// Everything listed in <see cref="Scene"/> is staged around the player, so two of them
        /// are always "beside" each other and the flag is the whole answer; a building site
        /// (Civic) stands where it stands for days, and is asked about by place. Costs four field
        /// reads; a director asks on its own roll timer, never per frame.
        ///
        /// ADDING A SCENE: a value in Scene, a line in <see cref="Active"/>, and the new
        /// director's start test asks AnyBut(its own value).
        /// </summary>
        public static class Scenes
        {
            /// <summary>How near a building site counts as beside it, in metres: the length of road an accident or an event can take up.</summary>
            public const float Beside = 90f;

            /// <summary>Which large scenes are on right now.</summary>
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
            /// True while something of the mod's has the player's attention on screen: a panel with buttons, a test paper,
            /// a driven test. Whoever wants to put up a panel of its own waits for this to be false,
            /// so panels queue and never stack. (The game's own dialogs, time skips and pause are LifeEvents.IsSafeForEvent.)
            /// </summary>
            public static bool PlayerHeld =>
                Law.Custody.Active || JusticePanel.IsOpen || LicenceDirector.IsActive
                || (LicenceHud.Exists && LicenceHud.Instance.SheetShown) || Meta.MetaUi.AnyOpen || ThirdPersonCamera.IsUiOpen();
        }
    }
}
