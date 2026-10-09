using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The scene that plays each catalogue entry: one line per crime. The scenes are created
    /// once and reused; idle, each is a handful of fields. CatalogueCheck reads this file to
    /// make sure every dealt entry has a scene and every scene an entry.
    /// </summary>
    internal static class CrimeScenes
    {
        public static Dictionary<string, ICrimeScene> Build()
        {
            var scenes = new Dictionary<string, ICrimeScene>();
            Add(scenes, new FootChaseScene());
            Add(scenes, new QuestioningScene());
            Add(scenes, new DisturbanceScene());
            Add(scenes, new SnatchScene());
            Add(scenes, new MuggingScene());
            Add(scenes, new AssaultScene());
            Add(scenes, new DrugDealScene());
            Add(scenes, new LoneOffenderScene("graffiti", OffenderStyle.AtWall));
            Add(scenes, new LoneOffenderScene("car_breakin", OffenderStyle.AtParkedCar));
            Add(scenes, new LoneOffenderScene("drunk", OffenderStyle.Drunk));
            Add(scenes, new LoneOffenderScene("street_vendor", OffenderStyle.Pitch));
            Add(scenes, new LoneOffenderScene("panhandler", OffenderStyle.Pitch));
            Add(scenes, new LoneOffenderScene("fare_dodger", OffenderStyle.Stopped));
            Add(scenes, new CarPursuitScene("car_theft", CarCrime.Theft));
            Add(scenes, new CarPursuitScene("traffic_stop", CarCrime.Stop));
            Add(scenes, new CarPursuitScene("reckless_driver", CarCrime.Chase));
            Add(scenes, new ParkingTicketScene());
            Add(scenes, new OwnShopTheftScene());
            Add(scenes, new ShopIncidentScene("shoplifter", holdUp: false));
            Add(scenes, new ShopIncidentScene("holdup", holdUp: true));
            Add(scenes, new PlayerPickpocketScene());
            Add(scenes, new PlayerMuggingScene());
            return scenes;
        }

        private static void Add(Dictionary<string, ICrimeScene> scenes, ICrimeScene scene)
        {
            scenes[scene.Id] = scene;
        }
    }
}
