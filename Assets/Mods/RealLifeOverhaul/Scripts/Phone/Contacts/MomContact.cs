using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one place that says whether Mom may get in touch by herself today (<see cref="MomRules"/>), and notes that she has. The
    /// social life asks before it picks her to text, ring, visit or bring somebody to meet the player, the life events before they
    /// deal one of hers, the family before its own texts of hers. A contact the player started is never asked of it.
    /// No cost a frame: a field read and a compare, on an event.
    /// </summary>
    internal static class MomContact
    {
        /// <summary>The option "Mom gets in touch by herself": off, she never does, and is still a contact the player can call.</summary>
        public static bool On => Settings.FamilyMomContact;

        /// <summary>Whether she may start a contact today.</summary>
        public static bool May => MomRules.May(Settings.FamilyMomContact, TimeHelper.CurrentDay, ModState.LastMomContactDay);

        /// <summary>Whether she may start a contact on <paramref name="day"/>, the social life's own count of it.</summary>
        public static bool MayOn(int day) => MomRules.May(Settings.FamilyMomContact, day, ModState.LastMomContactDay);

        /// <summary>She has started a contact today: no other feature lets her start another.</summary>
        public static void Note(int day)
        {
            ModState.LastMomContactDay = day;
        }
    }
}
