using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>A child that came of a night (Social/Love): a real child of the player's family whose other parent is not the partner.</summary>
    internal static partial class Children
    {
        /// <summary>
        /// The child joins the family's children, its other parent named and marked as not the partner. A child who lives with the other
        /// parent (<paramref name="away"/>) is on the Family page and has birthdays and milestones, but the player pays support (Social/Love)
        /// and none of the home's routines, care costs, school runs or walks apply to them; a child at home shares its costs with the other parent.
        /// Returns the name, or null when the family cannot take another child.
        /// </summary>
        internal static string ArriveFromNight(bool girl, string otherName, int otherIndex, bool away)
        {
            FamilySave s = FamilyState.S;
            if (!Settings.FamilyChildren || s.Children.Count >= FamilyBalance.MaxChildren)
            {
                return null;
            }
            var child = new ChildRecord
            {
                Girl = girl, Name = PickName(girl), Age = 0f, ArrivedDay = TimeHelper.CurrentDay, Closeness = away ? 45f : 60f, Birthdays = 0, LastCheckup = -1f,
                OtherParent = otherName, OtherIndex = otherIndex, Away = away
            };
            s.Children.Add(child);
            FamilyState.Remember(away ? "reallife_family_memory_born_away" : "reallife_family_memory_born", child.Name);
            FamilyDirector.Logger?.Info("Family: a child of a night arrived (" + (away ? "lives with the other parent" : "at home, the other parent shares the costs")
                    + "); " + s.Children.Count + " in all.");
            return child.Name;
        }

        /// <summary>How many more children the family can take, less one for a pregnancy or an adoption under way.</summary>
        internal static int Room()
        {
            if (!Settings.FamilyChildren)
            {
                return 0;
            }
            return Mathf.Max(0, FamilyBalance.MaxChildren - FamilyState.S.Children.Count - (FamilyState.Process != ProcessKind.None ? 1 : 0));
        }
    }
}
