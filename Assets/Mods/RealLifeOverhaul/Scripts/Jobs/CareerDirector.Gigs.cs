using System;
using System.Globalization;
using System.Text;
using BAModAPI;
using Entities;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Careers: the agency's booked sets.</summary>
    public sealed partial class CareerDirector
    {
        private const float GigReach = 14f;   // metres
        private static readonly Color GuideColour = new Color(0.20f, 0.62f, 0.95f, 1f);

        private BookedGig _gig;
        private int _gigDay;
        private bool _gigGuided;
        private float _gigRetry;

        public bool GigPending => _gig != null;

        public string GigVenue => _gig != null ? _gig.VenueName : null;

        /// <summary>Why the agency has no set to book right now, or null if it has.</summary>
        public string GigRefusal()
        {
            JobDef busker = JobCatalogue.Find(JobCatalogue.Busker);
            if (!On)
            {
                return "reallife_career_no_off";
            }
            if (JobState.Level(busker) < CareerCatalogue.GigNeedsLevel)
            {
                return "reallife_career_gig_no_level";
            }
            if (CareerState.GigDay == TimeHelper.CurrentDay || _gig != null)
            {
                return "reallife_career_gig_no_today";
            }
            if (JobState.Sacked(busker, TimeHelper.CurrentDay))
            {
                return "reallife_job_no_sacked";
            }
            return null;
        }

        /// <summary>Books a set at a venue near the player: an open business with a clear front. False if none was found.</summary>
        public bool BookGig(bool test)
        {
            if (!JobSpots.RandomDoor(PlayerPosition(), test ? 20f : 90f, test ? 160f : 480f, null, 0f, out DoorSpot spot, JobSites.AnyClient, 96))
            {
                return false;
            }
            _gig = new BookedGig
            {
                Venue = spot.Front,
                VenueName = JobSites.NameOf(spot),
                Fee = Mathf.RoundToInt(CareerCatalogue.GigFee * Mathf.Clamp(Settings.JobPay, 25, 300) / 100f)
            };
            _gigDay = TimeHelper.CurrentDay;
            if (!test)
            {
                CareerState.GigDay = _gigDay;
            }
            Guide();
            _logger?.Info("Careers: a set is booked at " + _gig.VenueName + " for " + _gig.Fee + ".");
            return true;
        }

        public int GigFee => _gig != null ? _gig.Fee : Mathf.RoundToInt(CareerCatalogue.GigFee * Mathf.Clamp(Settings.JobPay, 25, 300) / 100f);

        // The player has come to the venue on foot: the set begins, where they stand.
        private void Gig(Vector3 player, bool free)
        {
            if (!free || Time.unscaledTime < _gigRetry || FlatSqr(_gig.Venue - player) > GigReach * GigReach)
            {
                return;
            }
            BookedGig gig = _gig;
            _gig = null;
            Unguide();
            if (!_director.StartGig(gig, out string reason))
            {
                JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_here", 8f);
                _gig = gig;       // no room for a pitch just here: tried again in a while, from wherever they stand then
                _gigRetry = Time.unscaledTime + 12f;
                Guide();
            }
        }

        private void Guide()
        {
            try
            {
                if (_gig != null && SaveGameManager.Current != null && SaveGameManager.Current.currentPlayerMission == null)
                {
                    GuidersManager.SetGuiderTarget(_gig.Venue, _gig.VenueName, JobPhone.Icon, GuideColour, DirectionGuiderType.JobDestination);
                    _gigGuided = true;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void Unguide()
        {
            if (!_gigGuided)
            {
                return;
            }
            _gigGuided = false;
            try
            {
                if (SaveGameManager.Current == null || SaveGameManager.Current.currentPlayerMission == null)
                {
                    GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }
    }
}
