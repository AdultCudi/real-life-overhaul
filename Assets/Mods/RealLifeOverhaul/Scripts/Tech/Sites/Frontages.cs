using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The street side of the player's own data centres: a sign, a booth, a barrier and fence where they fit. Local, nothing saved;
    /// one frontage a second at most.
    /// </summary>
    internal sealed class Frontages
    {
        private const float DressWithin = 160f;
        private static readonly float[] SignAside = { -4.4f, 4.4f, -6.2f, 6.2f };

        private sealed class Site
        {
            public string Place;
            public CityBuildingController Building;
            public GameObject Dressing;
            public int Stamp;
            public bool Done;
            public Vector3 Front;
            public bool HasFront;
        }

        private readonly IModLogger _logger;
        private readonly Dictionary<string, Site> _sites = new Dictionary<string, Site>(StringComparer.Ordinal);
        private readonly List<string> _gone = new List<string>(4);
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

        public Frontages(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>Changes when the data centres or their names do, for the map.</summary>
        public int Stamp { get; private set; }

        /// <summary>Once a second outdoors: dresses the frontages near the player and takes away those no longer the player's.</summary>
        public void Tick(Vector3 player, InfraFile infra)
        {
            _seen.Clear();
            CityManager city = InstanceBehavior<CityManager>.Instance;
            bool dressed = false;
            for (int i = 0; infra != null && city != null && i < infra.Centres.Count; i++)
            {
                DataCentre d = infra.Centres[i];
                _seen.Add(d.Place);
                if (!_sites.TryGetValue(d.Place, out Site site))
                {
                    BuildingRegistration reg = TalentGame.Find(d.Place);
                    CityBuildingController building = reg != null && GoingPublic.Mine(reg) ? city.FindCityBuildingController(reg.Address) : null;
                    if (building == null)
                    {
                        continue;
                    }
                    site = new Site { Place = d.Place, Building = building };
                    site.HasFront = JobSpots.Front(building, out site.Front);
                    _sites.Add(d.Place, site);
                    Stamp++;
                }
                int stamp = StampOf(infra.Cloud, d);
                if (dressed || (site.Done && site.Stamp == stamp) || !site.HasFront || CitySpots.Flat(site.Front - player) > DressWithin * DressWithin)
                {
                    continue;
                }
                Undress(site);
                Dress(site, infra.Cloud, d);
                site.Stamp = stamp;
                site.Done = true;
                dressed = true;   // one a second
            }
            _gone.Clear();
            foreach (KeyValuePair<string, Site> pair in _sites)
            {
                if (!_seen.Contains(pair.Key))
                {
                    _gone.Add(pair.Key);
                }
            }
            for (int i = 0; i < _gone.Count; i++)
            {
                Undress(_sites[_gone[i]]);
                _sites.Remove(_gone[i]);
                Stamp++;
            }
        }

        /// <summary>The front of a data centre's building, for the map: false until it is known.</summary>
        public bool Front(string place, out Vector3 front, out CityBuildingController building)
        {
            if (place != null && _sites.TryGetValue(place, out Site site) && site.HasFront)
            {
                front = site.Front;
                building = site.Building;
                return true;
            }
            front = default;
            building = null;
            return false;
        }

        private static int StampOf(Cloud cloud, DataCentre d)
        {
            unchecked
            {
                return TechRandom.Of(cloud.Open ? cloud.Name : d.Name) * 31 + (cloud.Open ? 1 : 0) * 7 + (cloud.Hyper ? 1 : 0) * 3 + TechRandom.Of(d.District);
            }
        }

        private void Dress(Site site, Cloud cloud, DataCentre d)
        {
            if (!Shapes.Available || !CitySpots.Along(site.Front, out Vector3 along))
            {
                site.Dressing = null;
                return;
            }
            var root = new GameObject("RealLifeOverhaul Data Centre Frontage");
            site.Dressing = root;
            string name = cloud.Open && !string.IsNullOrEmpty(cloud.Name) ? cloud.Name : d.Name;
            string under = cloud.Open && cloud.Hyper ? TechText.T("reallife_sites_sign_region", "region", HiringGame.District(d.District)) : TechText.T("reallife_sites_sign_centre");
            int signSide = 0;
            Vector3 signAt = default;
            for (int i = 0; i < SignAside.Length && signSide == 0; i++)
            {
                if (Against(site.Front, along * SignAside[i], 0.30f, 0.45f, 1.25f, 3f, out Vector3 at, out Vector3 outward))
                {
                    Transform sign = SiteProps.Make(SiteProps.Sign, root.transform, out _);
                    sign.SetPositionAndRotation(at, Quaternion.LookRotation(outward, Vector3.up));
                    SiteProps.Letter(sign, name, SiteProps.SignGlass + new Vector3(0f, 0.09f, 0.004f), new Vector2(1.86f, 0.32f), new Color(0.95f, 0.97f, 1.0f), 3f);
                    SiteProps.Letter(sign, under, SiteProps.SignGlass + new Vector3(0f, -0.19f, 0.004f), new Vector2(1.86f, 0.14f), new Color(0.55f, 0.80f, 1.0f), 1.2f);
                    signSide = SignAside[i] < 0f ? -1 : 1;
                    signAt = at;
                }
            }
            // the booth on the other side of the door, deeper from the wall
            for (int k = 0; k < 3; k++)
            {
                float aside = -(signSide == 0 ? 1 : signSide) * (4.2f + k * 1.6f);
                if (Against(site.Front, along * aside, 1.12f, 0.70f, 1.0f, 3.2f, out Vector3 at, out Vector3 outward)
                    && (signSide == 0 || CitySpots.Flat(at - signAt) > 3.0f * 3.0f) && CitySpots.ClearQuietly(at, 1.0f))
                {
                    Transform booth = SiteProps.Make(SiteProps.Booth, root.transform, out _);
                    booth.SetPositionAndRotation(at, Quaternion.LookRotation(outward, Vector3.up));
                    break;
                }
            }
            Gate(site, root.transform);
        }

        // A piece with its back a distance off the wall and along from the door; false where the wall or pavement is not there.
        private static bool Against(Vector3 door, Vector3 offset, float fromWall, float standOff, float room, float awayFromDoor, out Vector3 at, out Vector3 outward)
        {
            at = default;
            outward = default;
            if (!CitySpots.Shopfront(door + offset, fromWall + standOff, room, out Vector3 stand, out outward))
            {
                return false;
            }
            at = stand - outward * standOff;
            if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - stand.y) > 0.25f || JobSpots.Flat(at, door) < awayFromDoor)
            {
                return false;
            }
            at.y = ground;
            return true;
        }

        // a barrier beside the vehicle door with its arm raised, and fence along the wall beyond it
        private static void Gate(Site site, Transform root)
        {
            DriveInEntrance[] gates = site.Building.driveInEntrances;
            DriveInEntrance gate = gates != null && gates.Length > 0 ? gates[0] : null;
            if (gate == null || !CitySpots.Along(gate.transform.position, out Vector3 along))
            {
                return;
            }
            Vector3 middle = site.Building.transform.position;
            Vector3 outward = Vector3.Cross(Vector3.up, along);
            if (Vector3.Dot(outward, gate.transform.position - middle) < 0f)
            {
                outward = -outward;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 post = gate.transform.position + along * side * 2.4f + outward * 0.9f;
                if (!CitySpots.OnPavement(post, out Vector3 spot) || CitySpots.Flat(spot - post) > 0.3f * 0.3f || !CitySpots.ClearQuietly(spot, 0.4f))
                {
                    continue;
                }
                Transform barrier = SiteProps.Make(SiteProps.Barrier, root, out _);
                barrier.SetPositionAndRotation(spot, Quaternion.LookRotation(outward, Vector3.up));
                for (int k = 0; k < 2; k++)
                {
                    Vector3 panel = gate.transform.position + along * side * (4.0f + k * 2.5f) + outward * 0.25f;
                    if (!CitySpots.OnPavement(panel, out Vector3 at) || CitySpots.Flat(at - panel) > 0.3f * 0.3f || !CitySpots.ClearQuietly(at, 0.5f))
                    {
                        break;
                    }
                    Transform fence = SiteProps.Make(SiteProps.Fence, root, out _);
                    fence.SetPositionAndRotation(at, Quaternion.LookRotation(outward, Vector3.up));
                }
                return;
            }
        }

        private static void Undress(Site site)
        {
            if (site.Dressing != null)
            {
                UnityEngine.Object.Destroy(site.Dressing);
            }
            site.Dressing = null;
            site.Done = false;
        }

        /// <summary>Takes every frontage away, when the option is off or the city goes.</summary>
        public void Clear()
        {
            foreach (Site site in _sites.Values)
            {
                Undress(site);
            }
            if (_sites.Count > 0)
            {
                Stamp++;
            }
            _sites.Clear();
        }
    }
}
