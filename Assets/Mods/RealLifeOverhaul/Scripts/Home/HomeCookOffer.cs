using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Helpers;
using Player.HUD.ItemInfoOverlays;

namespace RealLifeOverhaul
{
    /// <summary>
    /// "Click to cook" on the cookers at home, offered the way the game offers every action on an object.
    ///
    /// How the game does it (Player.HUD.ItemInfoOverlays): pointing at an object asks CtaManager for its "call to
    /// action". CtaManager goes through its list of ICtaBehavior and takes the first whose ShouldShow is true; that
    /// one gives a text key and what a click does. The object is outlined, the key is shown in the game's blue under
    /// the object's name (CtaOverlay), and a left click runs the action (EntityController.OnIoLeftClick). An object
    /// reacts to the pointer at all only if its simpleOverlayType has a flag set (EntityController.ShouldReactToIoEnter);
    /// the game itself switches ClickToAction on in code for a vehicle that gets an action (PrivateDriverVehicle).
    ///
    /// So this class adds one behaviour to the front of CtaManager's list and sets ClickToAction on the home's
    /// cookers; nothing is drawn by the mod. Both are taken back when the home director goes. If the game's list
    /// cannot be reached, Installed stays false and the home director keeps its key prompt.
    ///
    /// The fridge is left as the game has it: its click opens the game's own panel of what is in it ("Click to use").
    /// </summary>
    public static class HomeCookOffer
    {
        public const string TextKey = "reallife_home_cta_cook";

        public static IModLogger Logger;

        private sealed class CookCta : ICtaBehavior
        {
            public override bool ShouldShow(EntityController entityController)
            {
                return Offers(entityController);
            }

            public override (string, Action) GetCta(EntityController entityController)
            {
                return (TextKey, Click);
            }
        }

        private static readonly Action Click = Clicked;
        private static readonly Dictionary<ItemController, SimpleOverlayType> Marked = new Dictionary<ItemController, SimpleOverlayType>(4);
        private static readonly List<ItemController> Gone = new List<ItemController>(4);
        private static CookCta _behaviour;
        private static List<ICtaBehavior> _list;
        private static bool _failed;

        public static bool Installed => _behaviour != null;

        /// <summary>
        /// Makes sure the game offers "Click to cook" on these cookers. True if it does (the key prompt is then not
        /// needed where there is a cooker); false if the game's list could not be reached.
        /// </summary>
        public static bool Mark(List<ItemController> cookers)
        {
            if (!Installed && !Install())
            {
                return false;
            }
            for (int i = 0; i < cookers.Count; i++)
            {
                ItemController cooker = cookers[i];
                if (cooker != null && !Marked.ContainsKey(cooker))
                {
                    Marked[cooker] = cooker.simpleOverlayType;
                    cooker.simpleOverlayType |= SimpleOverlayType.ClickToAction;
                }
            }
            if (Marked.Count > cookers.Count)
            {
                Forget(cookers);
            }
            return cookers.Count > 0;
        }

        // Cookers of a home that has been left are gone with it; one that was sold is no longer a cooker of this home.
        private static void Forget(List<ItemController> cookers)
        {
            Gone.Clear();
            foreach (KeyValuePair<ItemController, SimpleOverlayType> pair in Marked)
            {
                if (pair.Key == null || !cookers.Contains(pair.Key))
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
                    Logger?.Warn("Home: the game's list of click actions was not found, so cooking keeps its key prompt.");
                    return false;
                }
                _behaviour = new CookCta();
                _list.Insert(0, _behaviour);   // asked first: it answers only for a cooker at home, everything else goes on as before
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home: 'Click to cook' added to the game's click actions (" + _list.Count + " in the list).");
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

        /// <summary>Takes the behaviour out of the game's list and gives every cooker back what it had.</summary>
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
            _behaviour = null;
            _list = null;
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
            try
            {
                HomeDirector.Instance?.CookClicked();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
