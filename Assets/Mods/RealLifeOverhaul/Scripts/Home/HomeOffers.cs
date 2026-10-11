using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Helpers;
using Player.HUD.ItemInfoOverlays;

namespace RealLifeOverhaul
{
    /// <summary>
    /// "Click to cook" on the cookers at home and "Click to make a coffee" on the coffee machines, offered the way the
    /// game offers every action on an object.
    ///
    /// How the game does it (Player.HUD.ItemInfoOverlays): pointing at an object asks CtaManager for its "call to
    /// action". CtaManager goes through its list of ICtaBehavior and takes the first whose ShouldShow is true; that
    /// one gives a text key and what a click does. The object is outlined, the key is shown in the game's blue under
    /// the object's name (CtaOverlay), and a left click runs the action (EntityController.OnIoLeftClick). An object
    /// reacts to the pointer at all only if its simpleOverlayType has a flag set (EntityController.ShouldReactToIoEnter);
    /// the game itself switches ClickToAction on in code for a vehicle that gets an action (PrivateDriverVehicle).
    ///
    /// So this class adds one behaviour to the front of CtaManager's list and sets ClickToAction on the home's
    /// cookers and coffee machines; nothing is drawn by the mod. Both are taken back when the home director goes.
    ///
    /// The fridge is left as the game has it: its click opens the game's own panel of what is in it ("Click to use"),
    /// and cooking is a button in that panel (<see cref="HomeFridgeButton"/>).
    /// </summary>
    public static class HomeOffers
    {
        public const string CookKey = "reallife_home_cta_cook";
        public const string CoffeeKey = "reallife_home_cta_coffee";

        public static IModLogger Logger;

        private sealed class HomeCta : ICtaBehavior
        {
            public override bool ShouldShow(EntityController entityController)
            {
                return Offers(entityController);
            }

            public override (string, Action) GetCta(EntityController entityController)
            {
                var item = entityController as ItemController;
                bool coffee;
                if (item == null || !Kinds.TryGetValue(item, out coffee))
                {
                    return ("", null);
                }
                _pointed = item;
                return (coffee ? CoffeeKey : CookKey, Click);
            }
        }

        private static readonly Action Click = Clicked;
        private static readonly Dictionary<ItemController, SimpleOverlayType> Marked = new Dictionary<ItemController, SimpleOverlayType>(8);
        private static readonly Dictionary<ItemController, bool> Kinds = new Dictionary<ItemController, bool>(8);   // true: a coffee machine
        private static readonly List<ItemController> Gone = new List<ItemController>(4);
        private static HomeCta _behaviour;
        private static List<ICtaBehavior> _list;
        private static ItemController _pointed;   // what the game last asked about: the one a click is on
        private static bool _failed;

        public static bool Installed => _behaviour != null;

        /// <summary>
        /// Makes sure the game offers a click on these cookers and coffee machines. False if the game's list could
        /// not be reached (said once in Player.log): cooking is then a button in the fridge's panel only, and there
        /// is no coffee.
        /// </summary>
        public static bool Mark(List<ItemController> cookers, List<ItemController> coffeeMachines)
        {
            if (!Installed && !Install())
            {
                return false;
            }
            Add(cookers, false);
            Add(coffeeMachines, true);
            if (Marked.Count > cookers.Count + coffeeMachines.Count)
            {
                Forget(cookers, coffeeMachines);
            }
            return true;
        }

        private static void Add(List<ItemController> items, bool coffee)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ItemController item = items[i];
                if (item != null && !Marked.ContainsKey(item))
                {
                    Marked[item] = item.simpleOverlayType;
                    Kinds[item] = coffee;
                    item.simpleOverlayType |= SimpleOverlayType.ClickToAction;
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Home check (offer): '" + (coffee ? "Click to make a coffee" : "Click to cook") + "' is on " + item.itemName + " at "
                                     + item.transform.position.ToString("F1") + ".");
                    }
                }
            }
        }

        // Furniture of a home that has been left is gone with it; a piece that was sold is no longer of this home.
        private static void Forget(List<ItemController> cookers, List<ItemController> coffeeMachines)
        {
            Gone.Clear();
            foreach (KeyValuePair<ItemController, SimpleOverlayType> pair in Marked)
            {
                if (pair.Key == null || (!cookers.Contains(pair.Key) && !coffeeMachines.Contains(pair.Key)))
                {
                    Gone.Add(pair.Key);
                }
            }
            for (int i = 0; i < Gone.Count; i++)
            {
                SimpleOverlayType before;
                if (Gone[i] != null && Marked.TryGetValue(Gone[i], out before))
                {
                    Gone[i].simpleOverlayType = before;
                }
                Marked.Remove(Gone[i]);
                Kinds.Remove(Gone[i]);
            }
            Gone.Clear();
        }

        private static bool Install()
        {
            if (_failed)
            {
                return false;
            }
            try
            {
                FieldInfo field = typeof(CtaManager).GetField("CtaBehaviors", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                _list = field != null ? field.GetValue(null) as List<ICtaBehavior> : null;
                if (_list == null)
                {
                    _failed = true;
                    Logger?.Warn("Home: the game's list of click actions was not found, so cookers and coffee machines cannot be clicked. Cooking is still a button in the fridge's own panel.");
                    return false;
                }
                _behaviour = new HomeCta();
                _list.Insert(0, _behaviour);   // asked first: it answers only for a cooker or a coffee machine at home, everything else goes on as before
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (offer): the home's click actions are in the game's list (" + _list.Count + " entries): PASS.");
                }
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                _behaviour = null;
                Logger?.Error(e);
                return false;
            }
        }

        /// <summary>Takes the behaviour out of the game's list and gives every piece of furniture back what it had.</summary>
        public static void Remove()
        {
            try
            {
                if (_list != null && _behaviour != null)
                {
                    _list.Remove(_behaviour);
                }
                foreach (KeyValuePair<ItemController, SimpleOverlayType> pair in Marked)
                {
                    if (pair.Key != null)
                    {
                        pair.Key.simpleOverlayType = pair.Value;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Marked.Clear();
            Kinds.Clear();
            _behaviour = null;
            _list = null;
            _pointed = null;
            _failed = false;
        }

        // Asked by the game for whatever the pointer is on: one type test and one look-up unless it is one of ours.
        private static bool Offers(EntityController entity)
        {
            var item = entity as ItemController;
            return item != null && Marked.ContainsKey(item) && Settings.HomeLife && Settings.HomeRoutines
                   && HomeDirector.Instance != null && !PlayerHelper.IsUsingVehicle && HomeTidy.AtHome();
        }

        private static void Clicked()
        {
            ItemController item = _pointed;
            try
            {
                bool coffee;
                if (item == null || !Kinds.TryGetValue(item, out coffee))
                {
                    return;
                }
                HomeDirector.Instance?.OfferClicked(coffee ? HomeWish.Coffee : HomeWish.Cook, item);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
