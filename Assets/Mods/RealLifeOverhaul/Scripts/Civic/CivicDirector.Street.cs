using System;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Civic life in the street: the campaign and the sites near the player, and the key prompts for a ballot, a leaflet, a site notice, a door and a poster.</summary>
    public sealed partial class CivicDirector
    {
        private int _knockBuilding;
        private string _knockDistrict, _knockAddress;
        private Vector3 _posterAt, _posterNormal;
        private string _knockFirst, _posterFirst;
        private const string ButtonKnock = "reallife_civic_office_panel_knock";
        private const string ButtonPoster = "reallife_civic_office_panel_poster";
        private static readonly Collider[] Nearby = new Collider[24];
        private static int _buildingMask;

        private PromptKind _prompt;
        private SiteState _promptSite;
        private string _voteFirst, _leafletFirst, _noticeFirst, _canvassFirst, _canvassTitle;
        private const string ButtonVote = "reallife_civic_panel_vote";
        private const string ButtonLeaflet = "reallife_civic_panel_leaflet";
        private const string ButtonNotice = "reallife_civic_panel_notice";
        private const string ButtonOffer = "reallife_civic_panel_offer";

        private void World(float now, int day, int hour)
        {
            if (_worldFailed)
            {
                return;
            }
            try
            {
                var uis = InstanceBehavior<UIs>.Instance;
                PlayerController player = PlayerHelper.PlayerController;
                bool outdoors = player != null && !BuildingManager.IsInsideBuilding && (uis == null || !uis.timeMachine.isRunning);
                if (!outdoors)
                {
                    if (_worldOut)
                    {
                        ClearWorld();
                    }
                    return;
                }
                _worldOut = true;
                Vector3 at = player.transform.position;
                bool onFoot = !PlayerHelper.IsUsingVehicle;
                if (Elections)
                {
                    _campaign.Tick(now, at, day, hour, onFoot, _sites.CrewOut > 0);
                }
                else if (_campaign.Dressing > 0 || _campaign.PeopleOut > 0)
                {
                    _campaign.Clear();
                }
                if (Settings.CivicConstruction)
                {
                    _sites.Tick(now, at, day, hour, !_campaign.NeedsCast);
                }
                else if (_sites.DressedCount > 0)
                {
                    _sites.Clear();
                }
                _cast.ClearFallen();
                Prompt(at, onFoot, day, hour);
            }
            catch (Exception e)
            {
                _worldFailed = true;
                Logger?.Error(e);
                Logger?.Warn("Civic: the street part (campaign dressing, sites, their people) is off for this session after the error above; elections and sites go on as data, on the phone.");
                try
                {
                    ClearWorld();
                }
                catch (Exception inner)
                {
                    Logger?.Error(inner);
                }
            }
        }

        // Every closed lane is opened first, then props and people go. Each step is guarded alone, so one fault (a thing
        // already destroyed with the scene) does not stop the rest.
        private void ClearWorld()
        {
            _worldOut = false;
            _prompt = PromptKind.None;
            _promptSite = null;
            Step(_clearSites ?? (_clearSites = () => _sites?.Clear()));
            Step(_clearCampaign ?? (_clearCampaign = () => _campaign?.Clear()));
            Step(_clearCast ?? (_clearCast = () => _cast?.ReleaseAll()));
            Step(_clearShift ?? (_clearShift = () => Volunteer?.Stop()));
            Step(_clearHud ?? (_clearHud = () => _hud?.HideAll()));
        }

        private Action _clearSites, _clearCampaign, _clearCast, _clearShift, _clearHud;

        /// <summary>The player's own rally is set up near them (paid for from their fund). False if there is no room for one now.</summary>
        public bool OwnRally()
        {
            if (_worldFailed || BuildingManager.IsInsideBuilding || !_campaign.OwnRally())
            {
                return false;
            }
            _worldOut = true;
            return true;
        }

        private static void Step(Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Prompt(Vector3 player, bool onFoot, int day, int hour)
        {
            PromptKind kind = PromptKind.None;
            SiteState site = null;
            if (onFoot && !Volunteer.Active && !JobDirector.TakesInteractKey && !ThirdPersonCamera.IsUiOpen())
            {
                if (Elections && Government.PollsOpen(day, hour) && Government.State.Voted < 0 && _campaign.PollingInReach(player))
                {
                    kind = PromptKind.Vote;
                }
                else if (Elections && _campaign.CanvasserInReach(player) >= 0)
                {
                    kind = PromptKind.Leaflet;
                }
                else if (Settings.CivicConstruction && (site = _sites.NoticeInReach(player)) != null)
                {
                    kind = PromptKind.Notice;
                }
                else if (Elections && CivicOffice.Enabled && !SharedCity.Follows('G') && !_scenes.Active)
                {
                    kind = OfficePrompt(player, day, hour);
                }
            }
            _prompt = kind;
            _promptSite = site;
            if (Volunteer.Active)
            {
                return;   // the shift has the two lines
            }
            if (kind == PromptKind.None)
            {
                _hud.ClearOffer();
                return;
            }
            if (kind == PromptKind.Vote)
            {
                _voteFirst = _voteFirst ?? CivicText.T("reallife_civic_door_vote");
                _hud.Offer(_voteFirst, ButtonVote, player);
            }
            else if (kind == PromptKind.Leaflet)
            {
                _leafletFirst = _leafletFirst ?? CivicText.T("reallife_civic_door_leaflet");
                _hud.Offer(_leafletFirst, ButtonLeaflet, player);
            }
            else if (kind == PromptKind.Knock)
            {
                _knockFirst = _knockFirst ?? CivicText.T("reallife_civic_office_door_prompt");
                _hud.Offer(_knockFirst, ButtonKnock, player);
            }
            else if (kind == PromptKind.Poster)
            {
                _posterFirst = _posterFirst ?? CivicText.T("reallife_civic_office_poster_prompt");
                _hud.Offer(_posterFirst, ButtonPoster, player);
            }
            else
            {
                _noticeFirst = _noticeFirst ?? CivicText.T("reallife_civic_door_notice");
                _hud.Offer(_noticeFirst, ButtonNotice, player);
            }
        }

        private void PromptPressed()
        {
            if (_failed || ThirdPersonCamera.IsUiOpen())
            {
                return;
            }
            try
            {
                if (Volunteer.Active)
                {
                    _volunteerKey = true;
                }
                else if (_prompt != PromptKind.None)
                {
                    Interact();
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Interact()
        {
            PromptKind kind = _prompt;
            _prompt = PromptKind.None;
            _hud.ClearOffer();
            switch (kind)
            {
                case PromptKind.Vote:
                    CityHallPhone.OpenBallot();
                    break;
                case PromptKind.Leaflet:
                    _campaign.TakeLeaflet();
                    break;
                case PromptKind.Notice:
                    if (_promptSite != null)
                    {
                        CityHallPhone.OpenNotice(_promptSite);
                    }
                    break;
                case PromptKind.Knock:
                    _scenes.Door(_knockBuilding, _knockDistrict, _knockAddress);
                    break;
                case PromptKind.Poster:
                    PutUpPoster();
                    break;
            }
        }

        // The player's own run in the street: a poster for the wall ahead, a door to knock on.
        private PromptKind OfficePrompt(Vector3 player, int day, int hour)
        {
            OfficeState s = Office.State;
            if (!s.Running)
            {
                return PromptKind.None;
            }
            PlayerController controller = PlayerHelper.PlayerController;
            // never beside a door: there the key is the door's
            if (s.PostersHeld > 0 && Office.Campaigning && controller != null && NearestDoor(player, 2.5f) == null
                && _campaign.WallAhead(player, controller.transform.forward, out _posterAt, out _posterNormal))
            {
                return PromptKind.Poster;
            }
            if (hour < OfficeRules.DoorsFrom || hour >= OfficeRules.DoorsTo)
            {
                return PromptKind.None;
            }
            // A residential street door that is not the player's own.
            CityBuildingController building = NearestDoor(player, 3f);
            if (building == null || building.building == null || building.building.BuildingType != "ba:buildingtype_residential"
                || !Office.MayKnock(day, hour, building.building.Neighbourhood, building.GetInstanceID()) || CivicOffice.IsMine(building)
                || SpecialBuildings.Filed(building))   // nobody lives where a feature of the mod's is
            {
                return PromptKind.None;
            }
            _knockBuilding = building.GetInstanceID();
            _knockDistrict = building.building.Neighbourhood;
            _knockAddress = building.building.Address != null ? building.building.Address.ToFormattedStringSafe() : null;
            return PromptKind.Knock;
        }

        // The nearest street door within reach, found through the colliders round the player.
        private static CityBuildingController NearestDoor(Vector3 player, float reach)
        {
            if (_buildingMask == 0)
            {
                _buildingMask = LayerMask.GetMask("Buildings", "BuildingsOutlined");
                if (_buildingMask == 0)
                {
                    _buildingMask = Physics.AllLayers;
                }
            }
            CityBuildingController best = null;
            float bestSqr = reach * reach;
            int count = Physics.OverlapSphereNonAlloc(player, 12f, Nearby, _buildingMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                CityBuildingController controller = Nearby[i] != null ? Nearby[i].GetComponentInParent<CityBuildingController>() : null;
                if (!CivicCity.HasDoor(controller) || GameMembers.Fenced(controller.building))
                {
                    continue;
                }
                float sqr = (controller.entranceDoors[0].doorTransform.position - player).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = controller;
                }
            }
            return best;
        }

        private void PutUpPoster()
        {
            Vector3 player = PlayerHelper.GetPosition();
            if (Office.State.PostersHeld <= 0 || !_campaign.PutUp(_posterAt, _posterNormal, player))
            {
                return;
            }
            Office.PosterUp(NeighborhoodHelper.CurrentNeighborhood);
            _worldOut = true;
            CivicText.Toast(NotificationType.Info, "reallife_civic_office_poster_up", "count", Office.State.PostersHeld.ToString(), 6f);
        }
    }
}
