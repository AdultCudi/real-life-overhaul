using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Economy;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// What the lens sees at the moment of a shot: the business or building nearest the middle of the frame, its district, and
    /// how many people stand in the frame. One pass over the city's building controllers (the game's own list, through GameDlc)
    /// and the active pedestrians, each projected through the lens, run once when the shutter is pressed and never on a frame. The
    /// game gives a pedestrian no name, so the people are counted. The rules are PhotoSeenRules'.
    /// </summary>
    internal static class PhotosSeen
    {
        /// <summary>
        /// Fills the sidecar's business, place of the business, people and the district of the subject building. A building with no
        /// business is still the subject, so its district is kept; the place the HUD named is left as the camera wrote it.
        /// </summary>
        public static void Look(Camera camera, in PhotoSeenRules.View view, ref PhotoSidecar side)
        {
            if (camera == null)
            {
                return;
            }
            side.People = 0;
            side.HasPeople = true;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city == null)
            {
                return;
            }
            Vector3 eye = camera.transform.position;
            Building(city, camera, eye, view, ref side);
            side.People = PhotoSeenRules.People(Pedestrians(city, camera, eye, view));
        }

        private static void Building(CityManager city, Camera camera, Vector3 eye, in PhotoSeenRules.View view, ref PhotoSidecar side)
        {
            CityBuildingController[] all = GameDlc.Open(city);
            CityBuildingController best = null;
            float bestOff = float.NaN, bestDepth = 0f;
            float range = PhotoSeenRules.BuildingRange, range2 = range * range;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                if (b == null || !JobSpots.DoorOf(b, out Vector3 door))
                {
                    continue;
                }
                float dx = door.x - eye.x, dz = door.z - eye.z;
                if (dx * dx + dz * dz > range2)
                {
                    continue;
                }
                Vector3 v = camera.WorldToViewportPoint(door + Vector3.up * 2f);
                if (!PhotoSeenRules.InFrame(v.x, v.y, v.z, range, view))
                {
                    continue;
                }
                float off = PhotoSeenRules.Off(v.x, v.y, view);
                if (PhotoSeenRules.Better(off, v.z, bestOff, bestDepth))
                {
                    best = b;
                    bestOff = off;
                    bestDepth = v.z;
                }
            }
            if (best == null)
            {
                return;
            }
            BuildingRegistration reg = best.buildingRegistration;
            if (reg != null && !string.IsNullOrEmpty(reg.BusinessName) && Books.IsBusiness(reg))
            {
                side.Business = reg.BusinessName;
                side.BusinessPlace = Flare.FlareGame.Place(reg);
            }
            string district = best.building != null ? best.building.Neighbourhood : null;
            if (!string.IsNullOrEmpty(district))
            {
                side.District = LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
            }
        }

        private static int Pedestrians(CityManager city, Camera camera, Vector3 eye, in PhotoSeenRules.View view)
        {
            List<Pedestrian> walkers = city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null)
            {
                return 0;
            }
            float range = PhotoSeenRules.PeopleRange, range2 = range * range;
            int people = 0;
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian p = walkers[i];
                if (p == null || !p.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 at = p.transform.position;
                float dx = at.x - eye.x, dz = at.z - eye.z;
                if (dx * dx + dz * dz > range2)
                {
                    continue;
                }
                Vector3 v = camera.WorldToViewportPoint(at + Vector3.up);
                people += PhotoSeenRules.InFrame(v.x, v.y, v.z, range, view) ? 1 : 0;
            }
            return people;
        }
    }
}
