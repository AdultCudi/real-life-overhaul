using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>What the director runs, one at a time: a fire or a street event.</summary>
    internal interface IEventScene
    {
        /// <summary>The locale key of what the marker says.</summary>
        string Label { get; }
        Vector3 Focus { get; }
        /// <summary>Metres from the focus beyond which the player has left it behind and it is cleared.</summary>
        float Reach { get; }
        /// <summary>Four times a second; false when it has nothing more to do, and the director then calls End.</summary>
        bool Tick(float now, Vector3 player);
        /// <summary>Everything given back and the road reopened. Safe in any state, any number of times.</summary>
        void End(bool trafficRunning);
        /// <summary>The locale key of what the interaction key would do where the player stands, or null.</summary>
        string Prompt(Vector3 player);
        void Interact(float now, Vector3 player);
        /// <summary>The player rang 911 about it: true if the call did something.</summary>
        bool Report(float now);
    }
}
