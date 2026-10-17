using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>What can be played where the player stands, offered on the game's panel over the place.</summary>
    public sealed partial class LeisureDirector
    {
        private string _offerText;
        private string _offerFor;          // what the text was built for: a kind or a landmark
        private int _offerKeys = -1;

        // The water test runs only while the player stands still by the water.
        private void Look(Vector3 position, Vector3 forward, bool indoors, BuildingManager manager, string business)
        {
            if (indoors)
            {
                if (Settings.ClothesShops && business == LeisureVenues.ClothingStore && _rack.Find(position, out Vector3 rack)
                    && CitySpots.Flat(rack - position) < 2.6f)
                {
                    SetOffer(LeisureKind.Rack, rack, "rack");
                }
                else if (Settings.LeisureNightOut && business == LeisureVenues.Nightclub
                         && LeisureVenues.Nearest(LeisureVenues.Bars, position, 2.8f) is ItemController bar)
                {
                    SetOffer(LeisureKind.Drinks, bar.transform.position, "drinks");
                }
                else if (Settings.LeisureNightOut && business == LeisureVenues.Nightclub
                         && LeisureVenues.Nearest(LeisureVenues.DanceFloors, position, 3.5f) is ItemController floor)
                {
                    SetOffer(LeisureKind.Dance, floor.transform.position, "dance");
                }
                else
                {
                    SetOffer(LeisureKind.None, default, null);
                }
                return;
            }
            if (Settings.LeisureChess && _corner.Out && _corner.OpponentThere && CitySpots.Flat(_corner.Stool - position) < 3f)
            {
                SetOffer(LeisureKind.Chess, _corner.Stool, "chess");
                return;
            }
            if (Settings.LeisureFishing && _stoodStill && LeisureVenues.WatersideDistance(position) < 320f
                && LeisureVenues.WaterAhead(position, forward, out Vector3 water, out _))
            {
                SetOffer(LeisureKind.Fishing, water, "fishing");
                return;
            }
            if (Settings.LeisureSights)
            {
                Landmark mark = LeisureVenues.LandmarkAt(position);
                if (mark != null && !LeisureState.HasSeen(mark.Id))
                {
                    SetOffer(LeisureKind.Sightseeing, position, mark.Id);
                    return;
                }
            }
            SetOffer(LeisureKind.None, default, null);
        }

        // The prompt's text is built only when the offer or the key's binding changes.
        private void SetOffer(LeisureKind kind, Vector3 place, string what)
        {
            _offer = kind;
            if (kind == LeisureKind.None)
            {
                if (_offerFor != null)
                {
                    _offerFor = null;
                    if (_activity == null)
                    {
                        _screen.HideOffer();
                    }
                }
                return;
            }
            if (what != _offerFor || _offerKeys != Keys.Version)
            {
                _offerFor = what;
                _offerKeys = Keys.Version;
                switch (kind)
                {
                    case LeisureKind.Chess: _offerText = JobText.T("reallife_leisure_offer_chess", "amount", JobText.Money(ChessGame.Stake)); break;
                    case LeisureKind.Fishing: _offerText = JobText.T("reallife_leisure_offer_fishing", "amount", JobText.Money(Fishing.Bait)); break;
                    case LeisureKind.Dance: _offerText = JobText.T("reallife_leisure_offer_dance"); break;
                    case LeisureKind.Drinks: _offerText = JobText.T("reallife_leisure_offer_drinks"); break;
                    case LeisureKind.Rack: _offerText = JobText.T("reallife_leisure_offer_rack"); break;
                    default:
                        Landmark mark = LeisureVenues.LandmarkAt(_lastPosition);
                        _offerText = JobText.T("reallife_leisure_offer_sights", "place", mark != null ? JobText.T(mark.NameKey) : string.Empty);
                        break;
                }
            }
            if (_activity == null)
            {
                _screen.Offer(_offerText, place, _lastPosition);
            }
        }

        private void Accept()
        {
            LeisureKind kind = _offer;
            string refusal;
            bool begun = kind == LeisureKind.Rack ? ClothesScreen.OpenShop(out refusal) : StartActivity(kind, null, false, out refusal);
            if (!begun && refusal != null)
            {
                Notifications.Show(NotificationType.Warning, refusal, null, 6f);
            }
        }
    }
}
