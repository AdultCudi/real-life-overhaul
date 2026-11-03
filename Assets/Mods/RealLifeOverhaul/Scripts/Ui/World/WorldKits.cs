using RealLifeOverhaul.GameUi;
using UI.Overlays;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The world's pieces on the RLO interface, each behind its own switch under "RLO interface: status bar and world":
    /// the world prompt (every box the game stands over a door, a lift or a desk, the mod's WorldPanel and DoorPanel
    /// among them), the building's panel (BuildingResume), the picker (every TravelList and DeskList of the mod's), the
    /// job offer (the game's JobOffer) and the game's smaller windows (the private driver, the items list, a sign's look,
    /// the preview terminal, the preview's bar, the colour picker). Installed once a second by the director while the interface is on; each switch
    /// gives its piece back to the game's own at once. Cost: a few comparisons a second.
    /// </summary>
    public static class WorldKits
    {
        public const string PromptId = UiWorld.PromptId, BuildingId = UiWorld.BuildingId, PickerId = UiWorld.PickerId, OfferId = UiWorld.OfferId;

        private static readonly UiGameHost Offer = new UiGameHost(new JobOfferKit());

        // The game's smaller windows over the world, each on its own switch: the private driver, the items list, a sign's
        // look, the preview terminal, the preview's bar and the colour picker's frame.
        private static readonly UiGameHost Driver = new UiGameHost(new PrivateDriverKit());
        private static readonly UiGameHost Items = new UiGameHost(new ItemsListKit());
        private static readonly UiGameHost Sign = new UiGameHost(new SignAppearanceKit());
        private static readonly UiGameHost Terminal = new UiGameHost(new PreviewTerminalKit());
        private static readonly UiGameHost Preview = new UiGameHost(new BuildingPreviewKit());
        private static readonly UiGameHost Colour = new UiGameHost(new ColourPickerKit());
        private static readonly UiGameHost[] Tools = { Driver, Items, Sign, Terminal, Preview, Colour };

        /// <summary>What each piece is doing, for the interface's line at load.</summary>
        public static string State => WorldHost.State + ", the picker (" + WorldPicker.State + "), the job offer (" + Offer.State + "), the private driver ("
                                      + Driver.State + "), the items list (" + Items.State + "), a sign's look (" + Sign.State + "), the preview terminal ("
                                      + Terminal.State + "), the preview's bar (" + Preview.State + "), the colour picker (" + Colour.State + ")";

        /// <summary>True while the job offer stands on the kit, for the tests.</summary>
        public static bool OfferShowing => Offer.Showing;

        /// <summary>The host of a smaller window by its id, for the tests; null for none.</summary>
        internal static UiGameHost Tool(string id)
        {
            for (int i = 0; i < Tools.Length; i++)
            {
                if (Tools[i].Id == id)
                {
                    return Tools[i];
                }
            }
            return null;
        }

        /// <summary>Each piece on the kit or the game's own, as its switch says.</summary>
        public static void Install()
        {
            if (UiScreenSwitches.On(PromptId) || UiScreenSwitches.On(BuildingId))
            {
                WorldHost.Ensure();
            }
            else
            {
                WorldHost.Remove("their switches are off");
            }
            if (UiScreenSwitches.On(PickerId))
            {
                TravelList.Face = WorldPicker.Instance;
            }
            else
            {
                WorldPicker.Give("its switch is off");
            }
            if (UiScreenSwitches.On(OfferId))
            {
                Offer.Install();
            }
            else
            {
                Offer.Remove("its switch is off");
            }
            for (int i = 0; i < Tools.Length; i++)
            {
                if (UiScreenSwitches.On(Tools[i].Id))
                {
                    Tools[i].Install();
                }
                else
                {
                    Tools[i].Remove("its switch is off");
                }
            }
        }

        /// <summary>Every piece the game's own again, exactly as found.</summary>
        public static void Remove(string why)
        {
            WorldHost.Remove(why);
            WorldPicker.Give(why);
            Offer.Remove(why);
            for (int i = 0; i < Tools.Length; i++)
            {
                Tools[i].Remove(why);
            }
        }

        /// <summary>A new city: a piece that never looked as expected in the last one is looked at again.</summary>
        public static void Rearm()
        {
            Offer.Rearm();
            for (int i = 0; i < Tools.Length; i++)
            {
                Tools[i].Rearm();
            }
            WorldPictures.Forget();
        }

        /// <summary>
        /// The glyph for what a box stands over, by its own kind or by the locale key of its first button (the mod's panels
        /// name their actions by what they do: a bus's by the bus, a class's by the class); a door when nothing says more.
        /// </summary>
        public static UiIcon IconFor(object overlay, string firstKey)
        {
            if (overlay is ElevatorOverlay)
            {
                return UiIcon.Up;
            }
            if (overlay is GasStationOverlay)
            {
                return UiIcon.Car;
            }
            string k = firstKey == null ? string.Empty : firstKey.ToLowerInvariant();
            if (Has(k, "bus", "train", "subway", "station", "ride", "taxi", "ferry"))
            {
                return UiIcon.Train;
            }
            if (Has(k, "lift", "elevator", "floor"))
            {
                return UiIcon.Up;
            }
            if (Has(k, "doctor", "hospital", "clinic", "nurse", "pharmac", "medic"))
            {
                return UiIcon.Heartbeat;
            }
            if (Has(k, "court", "hearing", "lawyer", "judge", "trial"))
            {
                return UiIcon.Scales;
            }
            if (Has(k, "police", "custody", "cell", "jail", "law_", "officer"))
            {
                return UiIcon.ShieldCheck;
            }
            if (Has(k, "class", "school", "study", "lesson", "exam", "theory", "course", "learn", "licence", "license"))
            {
                return UiIcon.Cap;
            }
            if (Has(k, "garage", "repair", "mechanic", "tuning", "wash", "fuel"))
            {
                return UiIcon.Wrench;
            }
            if (Has(k, "coffee", "cafe"))
            {
                return UiIcon.Coffee;
            }
            if (Has(k, "eat", "meal", "food", "cook", "takeaway", "takeout", "order", "fridge", "kitchen"))
            {
                return UiIcon.ForkKnife;
            }
            if (Has(k, "hotel", "room", "reception", "key"))
            {
                return UiIcon.Key;
            }
            if (Has(k, "home", "bed", "sleep", "rest", "household"))
            {
                return UiIcon.Home;
            }
            if (Has(k, "shop", "buy", "store", "vendor", "cart", "sell"))
            {
                return UiIcon.ShoppingCart;
            }
            if (Has(k, "talk", "chat", "ask", "meet", "interview", "pitch"))
            {
                return UiIcon.Message;
            }
            if (Has(k, "work", "shift", "job", "desk", "office", "dispatch", "tender", "design"))
            {
                return UiIcon.Briefcase;
            }
            return UiIcon.Building;
        }

        private static bool Has(string key, params string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (key.IndexOf(words[i], System.StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
