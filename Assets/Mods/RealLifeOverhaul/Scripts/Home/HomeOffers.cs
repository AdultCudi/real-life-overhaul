using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BAModAPI;
using Helpers;
using Player.HUD.ItemInfoOverlays;

namespace RealLifeOverhaul
{
    /// <summary>
    /// "Click to cook" and "Click to make a coffee" on the home's cookers and coffee machines, the way the game offers any
    /// object's action: a behaviour at the front of CtaManager's list, and ClickToAction on the furniture's overlay type.
    /// The fridge keeps the game's own click (<see cref="HomeFridgeButton"/>).
    /// </summary>
    public static class HomeOffers
    {
        public const string CookKey = "reallife_home_cta_cook";
        public const string CoffeeKey = "reallife_home_cta_coffee";

        public static IModLogger Logger;

        private sealed class HomeCta : ICtaBehavior
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("home", "HomeOffers.HomeCta", typeof(HomeCta));   // the game's calls of it (Core/Guard)

            public override bool ShouldShow(EntityController entityController)
            {
                if (FrameGuard.Off)
                {
                    return false;
                }
                try
                {
                    return Offers(entityController);
                }
                catch (Exception fault)
                {
                    return Guard.Failed(FrameGuard, fault, false);
                }
            }

            public override (string, Action) GetCta(EntityController entityController)
            {
                if (FrameGuard.Off)
                {
                    return (string.Empty, (Action)null);
                }
                try
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
                catch (Exception fault)
                {
                    return Guard.Failed(FrameGuard, fault, (string.Empty, (Action)null));
                }
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

        private static bool Installed => _behaviour != null;

        /// <summary>Makes sure the game offers a click on these cookers and coffee machines. False if its list could not be reached.</summary>
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
                                     + item.transform.position.ToString("F1", CultureInfo.InvariantCulture) + ".");
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
                FieldInfo field = GameContract.CtaBehaviours.Field;   // (resolved once: Core/GameContract)
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
