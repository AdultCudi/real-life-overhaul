using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>Children's part for the school run, parents' evenings, school plays and a party at a venue.</summary>
    internal static partial class Children
    {
        private static readonly string[] Runs = { "reallife_family_school_run_done_1", "reallife_family_school_run_done_2", "reallife_family_school_run_done_3" };
        private static readonly string[] Plays = { "reallife_family_school_play_done_1", "reallife_family_school_play_done_2", "reallife_family_school_play_done_3" };
        private static readonly string[] Evenings = { "reallife_family_parents_evening_great", "reallife_family_parents_evening_good", "reallife_family_parents_evening_fair" };
        private static int _lastRun = -1, _lastPlay = -1;

        private static ChildRecord Of(Errand errand)
        {
            FamilySave s = FamilyState.S;
            return errand.Child >= 0 && errand.Child < s.Children.Count ? s.Children[errand.Child] : null;
        }

        public static void SchoolRun(Errand errand)
        {
            ChildRecord c = Of(errand);
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            if (c == null)
            {
                return;
            }
            c.Closeness = Mathf.Min(100f, c.Closeness + 3f);
            ModState.AddModifier(ModState.MoodUp);
            FamilyText.Good(FamilyText.Pick(Runs, ref _lastRun), Args.Us.Kid(c.Name));
        }

        public static void SchoolRunMissed(Errand errand)
        {
            // Nothing is owed: the bus took them.
        }

        public static void ParentsEvening(Errand errand)
        {
            ChildRecord c = Of(errand);
            FamilyDirector director = FamilyDirector.Instance;
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            if (c == null)
            {
                return;
            }
            director.Run(director.Passage("reallife_family_passage_parents_evening", 1, null, () =>
            {
                c.Closeness = Mathf.Min(100f, c.Closeness + 3f);
                float standing = c.Closeness + (c.School == 2 ? 12f : 0f);
                FamilyText.Good(Evenings[standing >= 78f ? 0 : standing >= 55f ? 1 : 2], Args.Us.Kid(c.Name));
            }));
        }

        public static void SchoolPlay(Errand errand)
        {
            ChildRecord c = Of(errand);
            FamilyDirector director = FamilyDirector.Instance;
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            if (c == null)
            {
                return;
            }
            if (FamilyBridges.Leisure != null && FamilyBridges.Leisure.FamilyOuting("school_play", c.Name, went => director.Guarded(() => PlayOver(c, went))))
            {
                return;
            }
            director.Run(director.Passage("reallife_family_passage_school_play", 2, null, () => PlayOver(c, true)));
        }

        private static void PlayOver(ChildRecord c, bool went)
        {
            if (!went)
            {
                c.Closeness = Mathf.Max(0f, c.Closeness - 3f);
                return;
            }
            c.Closeness = Mathf.Min(100f, c.Closeness + 6f);
            FamilyModifiers.Add(FamilyModifiers.Joy);
            FamilyState.Remember("reallife_family_memory_school_play", c.Name);
            FamilyText.Good(FamilyText.Pick(Plays, ref _lastPlay), Args.Us.Kid(c.Name));
        }

        public static void SchoolEventMissed(Errand errand)
        {
            ChildRecord c = Of(errand);
            if (c == null)
            {
                return;
            }
            c.Closeness = Mathf.Max(0f, c.Closeness - 3f);
            FamilyText.Message(FamilyPhone.FamilyContact, errand.Kind == Errands.SchoolPlay ? "reallife_family_school_play_missed" : "reallife_family_parents_evening_missed", Args.Us.Kid(c.Name));
        }

        public static void VenueParty(Errand errand)
        {
            ChildRecord c = Of(errand);
            FamilyDirector director = FamilyDirector.Instance;
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            if (c == null)
            {
                return;
            }
            director.Run(director.Passage("reallife_family_passage_party", 3, () => EnergyHelper.SetCurrentHunger(Mathf.Max(SaveGameManager.Current.Hunger, 80f)), () => PartyOver(c, true)));
        }

        private static void PartyOver(ChildRecord c, bool went)
        {
            if (!went)
            {
                return;
            }
            c.Closeness = Mathf.Min(100f, c.Closeness + 8f);
            FamilyModifiers.Add(FamilyModifiers.Joy);
            FamilyState.Remember("reallife_family_memory_party", c.Name);
            FamilyText.Good("reallife_family_birthday_venue_done", Args.Us.Kid(c.Name));
        }

        public static void PartyMissed(Errand errand)
        {
            ChildRecord c = Of(errand);
            if (c == null)
            {
                return;
            }
            c.Closeness = Mathf.Max(0f, c.Closeness - 4f);
            FamilyText.Toast(NotificationType.Info, "reallife_family_birthday_venue_missed", Args.Us.Kid(c.Name));
        }
    }
}
