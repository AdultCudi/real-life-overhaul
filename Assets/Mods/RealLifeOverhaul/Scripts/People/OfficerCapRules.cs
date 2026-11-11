namespace RealLifeOverhaul
{
    /// <summary>What an officer wears on the head.</summary>
    internal enum OfficerHead
    {
        Bare,          // nothing: no hat in the game's slot and no modelled cap
        ModelledCap,   // the mod's own cap, hung on the head bone by the service kit
        GameHat        // the game's own hat, in its head accessory slot
    }

    /// <summary>
    /// What an officer's head wears by the "Police officers wear caps" option (police_caps), as plain rules: with the
    /// option off nothing at all, with it on the modelled cap where the props have it and the game's own hat where they
    /// do not, and the game's hat again where the kit refused the modelled cap (too much hair under it). Game-free, so
    /// EconomyCheck runs it as it ships. Every call is a comparison, made as an officer is dressed or the pool looks
    /// at its shelf, never a frame's work. <see cref="ActorPool"/> dresses by it.
    /// </summary>
    internal static class OfficerCapRules
    {
        /// <summary>The head an officer is first dressed for: bare with the option off, props or none.</summary>
        public static OfficerHead First(bool capsOn, bool modelledToBeHad)
        {
            return !capsOn ? OfficerHead.Bare : modelledToBeHad ? OfficerHead.ModelledCap : OfficerHead.GameHat;
        }

        /// <summary>
        /// The head once the kit refused the modelled cap: the game's hat instead. A bare head has none to refuse.
        /// </summary>
        public static OfficerHead AfterRefusal(OfficerHead first)
        {
            return first == OfficerHead.ModelledCap ? OfficerHead.GameHat : first;
        }

        /// <summary>True when an officer dressed for one value of the option is to be dressed for the other.</summary>
        public static bool Redress(bool dressedWithCaps, bool capsOn)
        {
            return dressedWithCaps != capsOn;
        }

        /// <summary>
        /// True when what an officer was found wearing is what the option asked for. <paramref name="modelledFitted"/>:
        /// the kit's cap is on the head. <paramref name="hatOn"/>: the game's hat slot holds a hat. Off, neither;
        /// on, one of the two the props allow, never both and never neither.
        /// </summary>
        public static bool Matches(bool capsOn, bool modelledToBeHad, bool modelledFitted, bool hatOn)
        {
            if (!capsOn)
            {
                return !modelledFitted && !hatOn;
            }
            return modelledToBeHad ? modelledFitted != hatOn : !modelledFitted && hatOn;
        }
    }
}
