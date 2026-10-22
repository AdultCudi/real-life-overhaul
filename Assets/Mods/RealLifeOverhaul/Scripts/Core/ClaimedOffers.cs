using System;
using System.Collections.Generic;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using Streets;
using UI;
using UI.Smartphone.Apps.MarketInsider;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps the mod's buildings (<see cref="SpecialBuildings"/>) off the market. Every way the game lets the player
    /// rent, buy or take over a building ends in BizMan's presentation of it, so the refusal is made there
    /// (<see cref="ClaimedPresentation"/>), in the Market Insider's real estate list (<see cref="ClaimedRealEstate"/>),
    /// on the city map (<see cref="ClaimedMap"/>) and on the building's signs (<see cref="FrontSigns"/>). What the player
    /// already rents or owns is never refused. Nothing is saved or sent: every machine has the same claims.
    /// COST: nothing while neither page is open; a reference comparison a frame while one is.
    /// </summary>
    internal static class ClaimedOffers
    {
        private static ClaimedPresentation _presentation;
        private static ClaimedRealEstate _estate;
        private static bool _presentationMissing;
        private static bool _estateMissing;

        /// <summary>Called once a second by SpecialNames: puts the two watchers on the game's pages once they are there.</summary>
        internal static void Keep()
        {
            if ((_presentationMissing || _presentation != null) && (_estateMissing || _estate != null))
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.fullMenu == null)
            {
                return;
            }
            if (_presentation == null && !_presentationMissing)
            {
                BizManBusiness business = uis.fullMenu.bizMan != null ? uis.fullMenu.bizMan.business : null;
                BizManPresentation page = business != null ? business.GetComponentInChildren<BizManPresentation>(true) : null;
                if (page == null)
                {
                    return;   // the phone's apps are not in yet
                }
                _presentation = page.gameObject.AddComponent<ClaimedPresentation>();
                if (!_presentation.Bind(page, business))
                {
                    UnityEngine.Object.Destroy(_presentation);
                    _presentation = null;
                    _presentationMissing = true;
                    SpecialBuildings.Logger?.Warn("Special buildings: BizMan's presentation of a building is not as expected; "
                                                  + "the mod's buildings are kept off the market by moving off any the player takes.");
                }
            }
            if (_estate == null && !_estateMissing)
            {
                Transform app = uis.fullMenu.appsContainer != null ? uis.fullMenu.appsContainer.Find("MarketInsider") : null;
                var insider = app != null ? app.GetComponent<global::UI.Smartphone.Apps.MarketInsider.MarketInsider>() : null;
                RealEstateScrollerController table = insider != null ? insider.realEstateScrollerController : null;
                if (table == null)
                {
                    _estateMissing = app != null;   // the app without its list: not looked for again
                    if (_estateMissing)
                    {
                        SpecialBuildings.Logger?.Warn("Special buildings: the Market Insider has no list of real estate; the mod's buildings stay in it.");
                    }
                    return;
                }
                _estate = table.gameObject.AddComponent<ClaimedRealEstate>();
                _estate.Table = table;
            }
        }

        /// <summary>The city is unloading: the pages are looked for again in the next.</summary>
        internal static void Forget()
        {
            _presentation = null;
            _estate = null;
            _presentationMissing = false;
            _estateMissing = false;
        }
    }

    /// <summary>
    /// On BizMan's presentation of a building: a building the mod has taken shows no offer to rent, buy or take over, and
    /// says whose it is. The game's own boxes are kept hidden while it shows (the game shows them again from its own
    /// buttons). COST: runs only while the page is open; two comparisons a frame, eight active flags while refused.
    /// </summary>
    internal sealed class ClaimedPresentation : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "ClaimedPresentation", typeof(ClaimedPresentation));

        private const string LineName = "RealLifeOverhaul TakenBy";

        private BizManPresentation _page;
        private BizManBusiness _business;
        private GameObject _rent, _forSale, _buy, _notForSale, _offer, _owner, _hamptons, _line;
        private BuildingRegistration _shown;   // the building last looked at
        private int _stamp = -1;
        private string _taken;                 // whose it is, while refused
        private string _words;
        private bool _ownerHidden;
        private bool _failed;

        /// <summary>Finds the game's boxes by their place on the page. False if one is not there.</summary>
        internal bool Bind(BizManPresentation page, BizManBusiness business)
        {
            _page = page;
            _business = business;
            Transform root = page.transform;
            _rent = Find(root, "Layout 30-70/Right/RentView/AvailableForRent");
            _forSale = Find(root, "Layout 30-70/Left/ForSaleBox");
            _buy = Find(root, "Layout 30-70/Left/BuyBuildingOfferBox");
            _notForSale = Find(root, "Layout 30-70/Left/NotForSaleBox");
            _hamptons = Find(root, "Layout 30-70/Right/HamptonsPurchaseBox");
            _offer = _notForSale != null ? Find(_notForSale.transform, "ShowBuyBuildingOfferButton") : null;
            _owner = _notForSale != null ? Find(_notForSale.transform, "RivalBuildingOwner") : null;
            GameObject label = _notForSale != null ? Find(_notForSale.transform, "NotForSaleLabel") : null;
            if (_rent == null || _forSale == null || _buy == null || _notForSale == null || _hamptons == null || _offer == null || _owner == null || label == null
                || page.overtakeOfferPanel == null || page.sendOvertakeOfferPanel == null)
            {
                return false;
            }
            // The game's own "not for sale" line, copied asleep, under it.
            Transform dormant = GameUi.UiClone.Dormant(transform, "RealLifeOverhaul Dormant");
            _line = GameUi.UiClone.Asleep(label, dormant, LineName);
            if (_line == null)
            {
                return false;
            }
            _line.SetActive(false);
            _line.transform.SetParent(_notForSale.transform, false);
            _line.transform.SetSiblingIndex(label.transform.GetSiblingIndex() + 1);
            return true;
        }

        private void OnEnable()
        {
            _shown = null;   // the game has written the page afresh
        }

        private void OnDisable()
        {
            if (_line != null)
            {
                _line.SetActive(false);
            }
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Core: ClaimedPresentation.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (_failed)
            {
                return;
            }
            try
            {
                BuildingRegistration registration = _business.buildingRegistration;
                int stamp = SpecialBuildings.Stamp;
                if (!ReferenceEquals(registration, _shown) || stamp != _stamp)
                {
                    bool same = ReferenceEquals(registration, _shown);
                    _shown = registration;
                    _stamp = stamp;
                    Look(registration, same);
                }
                if (_taken != null)
                {
                    Hold();
                }
            }
            catch (Exception e)
            {
                _failed = true;
                SpecialBuildings.Logger?.Error(e);
                SpecialBuildings.Logger?.Warn("Special buildings: BizMan's presentation could not be kept after the error above; "
                                              + "the mod's buildings are kept off the market by moving off any the player takes.");
                if (_line != null)
                {
                    _line.SetActive(false);
                }
            }
        }

        private void Look(BuildingRegistration registration, bool same)
        {
            string taken = SpecialBuildings.TakenBy(registration);
            bool was = _taken != null;
            _taken = taken;
            if (taken == null)
            {
                _line.SetActive(false);
                if (_ownerHidden)
                {
                    _owner.SetActive(true);
                    _ownerHidden = false;
                }
                if (was && same)
                {
                    // The same building is free again: the game writes its page afresh.
                    gameObject.SetActive(false);
                    gameObject.SetActive(true);
                }
                return;
            }
            // The owner as BizManPresentation.LoadLeftSide says it; the game's own services have none.
            bool service = _business.building != null && _business.building.SpecialService != null;
            if (!service)
            {
                var owner = _owner.GetComponent<TextLocalizationComponent>();
                if (owner != null)
                {
                    owner.SetData(BusinessHelper.GetBuildingOwnerDescription(registration));
                }
            }
            if (_owner.activeSelf == service)
            {
                _owner.SetActive(!service);
            }
            _ownerHidden = service;
            _words = "reallife_claimed_taken".GetLocalization().Replace("{name}", taken);
            GameUi.UiClone.Words(_line.GetComponent<TMPro.TMP_Text>(), _words);
            if (!was || !same)
            {
                SpecialBuildings.Logger?.Info("Special buildings: " + registration.Address.ToFormattedString() + " is off the market here (" + taken + ").");
            }
        }

        // Every frame while a refused building shows: what the game put up since is taken down again.
        private void Hold()
        {
            Off(_rent);
            Off(_forSale);
            Off(_buy);
            Off(_offer);
            Off(_hamptons);
            Off(_page.overtakeOfferPanel);
            Off(_page.sendOvertakeOfferPanel);
            if (!_notForSale.activeSelf)
            {
                _notForSale.SetActive(true);
            }
            if (!_line.activeSelf)
            {
                _line.SetActive(true);
                GameUi.UiClone.Words(_line.GetComponent<TMPro.TMP_Text>(), _words);   // again once awake: its component writes on waking
            }
        }

        private static void Off(GameObject box)
        {
            if (box.activeSelf)
            {
                box.SetActive(false);
            }
        }

        private static GameObject Find(Transform root, string path)
        {
            Transform found = root.Find(path);
            if (found == null)
            {
                SpecialBuildings.Logger?.Info("Special buildings: '" + path + "' is not on BizMan's presentation.");
            }
            return found != null ? found.gameObject : null;
        }
    }

    /// <summary>
    /// On the Market Insider's real estate list: the mod's buildings are taken out of each list the game makes (a new one
    /// for every filter and order). COST: runs only while the list is open; one reference comparison a frame, and a
    /// dictionary look per row when the game has made a new list.
    /// </summary>
    internal sealed class ClaimedRealEstate : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "ClaimedRealEstate", typeof(ClaimedRealEstate));

        internal RealEstateScrollerController Table;

        private List<RealEstateCellView.RealEstateModel> _seen;
        private int _stamp = -1;
        private bool _failed;

        private void OnEnable()
        {
            _seen = null;
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Core: ClaimedRealEstate.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (_failed || Table == null)
            {
                return;
            }
            List<RealEstateCellView.RealEstateModel> rows = Table.data;
            int stamp = SpecialBuildings.Stamp;
            if (ReferenceEquals(rows, _seen) && stamp == _stamp)
            {
                return;
            }
            _seen = rows;
            _stamp = stamp;
            try
            {
                int taken = 0;
                for (int i = rows != null ? rows.Count - 1 : -1; i >= 0; i--)
                {
                    RealEstateCellView.RealEstateModel row = rows[i];
                    if (row != null && SpecialBuildings.TakenBy(row.Address) != null)
                    {
                        rows.RemoveAt(i);
                        taken++;
                    }
                }
                if (taken > 0 && Table.scroller != null)
                {
                    Table.scroller.ReloadData();
                }
            }
            catch (Exception e)
            {
                _failed = true;
                SpecialBuildings.Logger?.Error(e);
                SpecialBuildings.Logger?.Warn("Special buildings: the Market Insider's list of real estate could not be kept after the error above; the mod's buildings stay in it.");
            }
        }
    }
}
