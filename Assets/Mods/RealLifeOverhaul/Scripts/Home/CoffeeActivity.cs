using BigAmbitions.Characters;
using Helpers;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A cup from a coffee machine in the player's home: a little energy (inside the game's own daily limit for food and drink), a little calm, three a day.</summary>
    public sealed class CoffeeActivity : HomeActivity
    {
        private readonly ItemController _machine;

        public CoffeeActivity(HomeDirector host, ItemController machine) : base(host)
        {
            _machine = machine;
            Total = 10;
        }

        public override Doing Kind => Doing.HomeRoutine;
        protected override string Title => "reallife_home_coffee_title";
        protected override string StartKey => "reallife_home_coffee_start";
        protected override EntityController Furniture => _machine;

        protected override LabelInfo[] Labels()
        {
            return new[] { Line("reallife_home_coffee_info", new { cups = HomeState.CoffeesToday, most = HomeBalance.CoffeesADay }) };
        }

        protected override void Begin(PlayerController player)
        {
            Gesture(player, AnimationType.UsingProducer);
        }

        protected override void End(PlayerController player, bool completed, int minutes)
        {
            if (!completed)
            {
                return;
            }
            HomeState.CoffeesToday++;
            Gesture(player, AnimationType.ConsumeDrink);
            if (HomeState.CoffeesToday > HomeBalance.CoffeesADay)
            {
                HomeState.Stress = HomeState.Clamp(HomeState.Stress + 4f, 0f, 100f);
                Toast(NotificationType.Warning, "reallife_home_coffee_too_many");
                return;
            }
            GameInstance save = SaveGameManager.Current;
            float room = EnergyHelper.MaxDailyEnergyGeneratedFromConsumables - save.EnergyGeneratedFromConsumables;
            float energy = Mathf.Min(HomeBalance.CoffeeEnergy, room);
            if (energy > 0f)
            {
                save.EnergyGeneratedFromConsumables += energy;
                EnergyHelper.GenerateEnergy(energy);
            }
            HomeState.Stress = HomeState.Clamp(HomeState.Stress - 3f, 0f, 100f);
            Toast(NotificationType.Success, energy > 0f ? "reallife_home_coffee_done" : "reallife_home_coffee_no_kick");
        }
    }
}
