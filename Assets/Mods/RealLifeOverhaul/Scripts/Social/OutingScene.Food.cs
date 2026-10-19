using System.Globalization;
using BigAmbitions.Items;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A completed outing's serving fills the player as the game's food does, and tells Home through the bridge.</summary>
    public sealed partial class OutingScene
    {
        private void Feed()
        {
            if (_def.MealItem == null)
            {
                return;
            }
            SocialLinks.Wellbeing?.Ate();   // Home's record of the meal, however the company went
            GameInstance save = SaveGameManager.Current;
            Item food = ItemsGetter.GetByName(_def.MealItem, true);
            if (save == null || food == null || food.saturation <= 0 || Mathf.RoundToInt(save.Hunger) >= 100)
            {
                return;
            }
            float before = save.Hunger;
            float wanted = Mathf.Min(before + food.saturation, 100f);
            // Set directly: ChangeHunger's food-eaten event would record the meal a second time.
            EnergyHelper.SetCurrentHunger(wanted);
            if (Meta.Dev.Verbose)
            {
                SocialDirector.Logger?.Info("Social check (meal): " + _who.Name + " at " + _venue.Name
                    + ", hunger " + before.ToString("F1", CultureInfo.InvariantCulture)
                    + " to " + save.Hunger.ToString("F1", CultureInfo.InvariantCulture)
                    + ", wanted " + wanted.ToString("F1", CultureInfo.InvariantCulture)
                    + " from " + food.itemName + " (saturation " + food.saturation + "): "
                    + (save.Hunger == wanted ? "PASS." : "FAIL."));
            }
        }
    }
}
