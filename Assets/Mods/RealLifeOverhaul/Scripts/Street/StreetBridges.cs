using System;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Street
{
    /// <summary>A stranger who said yes to swapping numbers, as the social life is to meet them.</summary>
    public sealed class StreetMeet
    {
        public string First, Last;
        public bool Female;
        public int Age;
        public int Job, District;
        public SocialTrait Traits;
        public bool Warm;          // the talk went well (the social life's warm first meeting)
        public string Look;        // the figure's look (People/PersonLook's text); null if it could not be read
    }

    /// <summary>What street talk asks of the rest of the mod, set by Wiring/StreetLinks; asked on a press or a click, never per frame.</summary>
    public static class StreetBridges
    {
        /// <summary>Fills what the city knows of the player and the day. Default: a newcomer on a quiet day.</summary>
        public static Action<StreetFacts> Facts;

        /// <summary>True while an officer is near enough to a point (metres) to hear what is said there. Default: none.</summary>
        public static Func<UnityEngine.Vector3, float, bool> PoliceNear;

        /// <summary>The police take notice of a threat: they watch the player more closely today. Default: nothing.</summary>
        public static Action Noticed;

        /// <summary>True when the social life has somebody of its cast left to meet. Default: no.</summary>
        public static Func<bool> CanMeet;

        /// <summary>Somebody met in the street becomes somebody the player knows: their place in the social life, or -1. Default: never.</summary>
        public static Func<StreetMeet, int> Meet;

        /// <summary>Fills what the social life knows for an invitation (FlareBridges.InviteLook). Default: nothing can be booked.</summary>
        public static Action<FlareInviteAsk> InviteLook;

        /// <summary>Books a yes's plan as a call does, meeting somebody new in the street first: their place, or -1. Default: never.</summary>
        public static Func<FlareInviteAsk, StreetMeet, int> InviteBook;

        /// <summary>Word of mouth for a business changed, so its customers are written again. Default: nothing.</summary>
        public static Action<string> MouthChanged;

        public static void Reset()
        {
            Facts = null;
            PoliceNear = null;
            Noticed = null;
            CanMeet = null;
            Meet = null;
            InviteLook = null;
            InviteBook = null;
            MouthChanged = null;
        }
    }
}
