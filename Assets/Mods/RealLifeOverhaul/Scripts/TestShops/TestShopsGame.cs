using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using BigAmbitions.Tags;
using Buildings;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.TestShops
{
    /// <summary>
    /// Options > Tests, rivals: takes over the nearest business of each kind, or only the nearest one, through the game's own
    /// BizManPresentation.OvertakeBusiness, for free, so the new interface can be tried on shops that run without building
    /// any by hand. The search walks the save's registrations once, on the press, and nothing runs per frame. In a Going
    /// Public session it does nothing but say so: a business taken over there would change another player's save.
    /// </summary>
    internal static class TestShopsGame
    {
        private const string Test = "Take over businesses";
        private const string OfficeType = "ba:buildingtype_office";

        private static readonly List<ShopCandidate> Found = new List<ShopCandidate>(512);
        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(512);
        private static readonly ShopPick Pick = new ShopPick();

        /// <summary>The button "take over one business of each kind" (<paramref name="oneOfEach"/>) or "the nearest business".</summary>
        public static void Run(bool oneOfEach)
        {
            try
            {
                Press(oneOfEach);
            }
            catch (Exception e)
            {
                if (!Guard.StandsDown(e, "the test that takes businesses over"))
                {
                    GameRegistrations.Logger?.Error(e);
                }
            }
        }

        private static void Press(bool oneOfEach)
        {
            if (TestShopsRules.Refused(GoingPublic.InSession))
            {
                TestRefusals.TellKey(GameRegistrations.Logger, Test, "reallife_testshops_single");
                return;
            }
            if (!ModState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(GameRegistrations.Logger, Test, TestRefusal.NotLoaded);
                return;
            }
            bool diploma = EducationHelper.HasCompletedDiploma(DiplomaName.OfficeBusinesses);
            Read();
            TestShopsRules.Pick(Found, oneOfEach, diploma, Pick);
            if (Pick.Chosen.Count == 0)
            {
                bool offices = Pick.OfficesLeftOut > 0;
                Notifications.Show(NotificationType.Warning, offices ? "reallife_testshops_office" : "reallife_testshops_none",
                    offices ? new Dictionary<string, string>(1) { { "name", DiplomaName.OfficeBusinesses.GetLocalization() } } : null, 8f);
                GameRegistrations.Logger?.Info(Test + ": nothing taken over (" + Found.Count + " businesses read, " + Pick.OfficesLeftOut
                                               + " offices and " + Pick.RivalsLeftOut + " of a special rival left out).");
                Release();
                return;
            }
            var kinds = new List<string>(Pick.Chosen.Count);
            foreach (int index in Pick.Chosen)
            {
                BuildingRegistration reg = Regs[index];
                kinds.Add(reg.businessTypeName.GetLocalization());
                Take(reg);
            }
            string more = "reallife_testshops_more".GetLocalization();
            string done = "reallife_testshops_done".GetLocalization();
            int room = TestShopsRules.NoticeMax - done.Replace("{count}", string.Empty).Replace("{kinds}", string.Empty).Length - Pick.Chosen.Count.ToString().Length;
            Notifications.Show(NotificationType.Success, "reallife_testshops_done", new Dictionary<string, string>(2)
            {
                { "count", Pick.Chosen.Count.ToString() },
                { "kinds", TestShopsRules.Kinds(kinds, room, more) }
            }, 10f);
            GameRegistrations.Logger?.Info(Test + ": took over " + Pick.Chosen.Count + " (" + string.Join(", ", kinds) + "); " + Pick.OfficesLeftOut + " offices and "
                                           + Pick.RivalsLeftOut + " of a special rival left out.");
            Release();
        }

        // Kept in a method of its own: the Mono JIT fails a method as a whole when it names a member the player's build lacks.
        private static void Take(BuildingRegistration reg)
        {
            BizManPresentation.OvertakeBusiness(reg);
        }

        private static void Read()
        {
            Found.Clear();
            Regs.Clear();
            Vector3 player = PlayerHelper.PlayerController.transform.position;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            bool rivals = Economy.ServicesGame.RivalsOn;
            foreach (BuildingRegistration reg in SaveGameManager.Current.BuildingRegistrations)
            {
                Building building = reg != null && reg.Address != null ? GameRegistrations.BuildingOf(reg) : null;
                CityBuildingController controller = building != null && city != null ? city.FindCityBuildingController(reg.Address) : null;
                if (controller == null)
                {
                    continue;
                }
                Found.Add(new ShopCandidate
                {
                    Kind = reg.businessTypeName,
                    Distance = CitySpots.Flat(controller.transform.position - player),
                    Rented = reg.RentedByPlayer,
                    Owned = reg.BuildingOwnedByPlayer,
                    CanBeTakenOver = BuildingTypeHelper.GetData(building).HasTag(TagRef.Buildingtypetag.canbetakenover),
                    SpecialService = building.SpecialService != null,
                    Empty = reg.businessTypeName == "ba:businesstype_empty",
                    Office = building.BuildingType == OfficeType,
                    SpecialRivalActive = rivals && SpecialRivalActive(reg),
                    Unavailable = GameDlc.Gated(building) || Inside(reg)
                });
                Regs.Add(reg);
            }
        }

        private static bool SpecialRivalActive(BuildingRegistration reg)
        {
            SpecialRival special = RivalsHelper.GetSpecialRival(reg.buildingOwnerRivalId);
            return special != null && special.rivalData != null && RivalsHelper.GetSpecialRivalState(special.rivalData.id)?.isActive == true;
        }

        // The game refuses a takeover of the building the player stands in.
        private static bool Inside(BuildingRegistration reg)
        {
            return BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance.building.Address == reg.Address;
        }

        private static void Release()
        {
            Found.Clear();
            Regs.Clear();
            Pick.Clear();
        }
    }
}
