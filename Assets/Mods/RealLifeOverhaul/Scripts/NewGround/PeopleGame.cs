using Culling;
using UnityEngine;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// Pedestrians on new ground, the game's own way. People are put on the street at the game's StationaryAiSpawner
    /// zones, 50 to 90 m from the player (PedestrianSpawnerPositionProvider), and stand in a zone while it is in view
    /// (its OnLod0); a zone joins the game's list by itself in its Start. So a zone on new ground is a copy of one of the
    /// game's own (its pools are the game's), given a square of its own on the new ground; taken away, it lets its
    /// people go and leaves the game's list and its culling group. A crossing is a copy of the game's crosswalk trigger
    /// (tagged Crosswalk, with CrosswalkToTrafficLightLink), linked to the red lamp of a junction's road: pedestrians wait
    /// at it until that lamp is lit (PedestrianMovementHandler.OnTriggerEnter). Called on a build or a test, never per frame.
    /// </summary>
    public static class PeopleGame
    {
        public const string ZoneName = "RealLifeOverhaul new ground zone";
        public const string CrossingName = "RealLifeOverhaul new ground crossing";
        public const float ZoneHeight = 2f;   // m: the height of a zone's square, as the game's own zones stand on the pavement

        private static readonly Collider[] Standing = new Collider[32];
        private static int _humanMask;

        /// <summary>
        /// A zone people stand in and come to on new ground, copied from the nearest of the game's zones people come to
        /// from outside; null with why when the game has none.
        /// </summary>
        public static StationaryAiSpawner Zone(Vector3 at, float yaw, Vector3 size, out string why)
        {
            why = null;
            StationaryAiSpawner template = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < StationaryAiSpawner.AllPedestrianSpawners.Count; i++)
            {
                StationaryAiSpawner zone = StationaryAiSpawner.AllPedestrianSpawners[i];
                if (zone == null || zone.disabled || !zone.canPedestriansComeFromOutside || zone.pedestrianPool == null || zone.name == ZoneName)
                {
                    continue;
                }
                float distance = (zone.transform.position - at).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    template = zone;
                }
            }
            if (template == null)
            {
                why = "the game has no pedestrian zone people come to from outside (" + StationaryAiSpawner.AllPedestrianSpawners.Count + " zones)";
                return null;
            }
            GameObject copy = Object.Instantiate(template.gameObject, at, Quaternion.Euler(0f, yaw, 0f));
            copy.name = ZoneName;
            var made = copy.GetComponent<StationaryAiSpawner>();
            made.canSpawnStreetPerformers = false;   // the performer's own square stays where the game put it
            made.canSpawnHomeless = false;
            BoundsPositionGiver giver = made.boundsPositionGiver;
            if (giver == null || !giver.transform.IsChildOf(copy.transform))
            {
                giver = copy.AddComponent<BoundsPositionGiver>();   // the template's square is elsewhere: one of the copy's own
                made.boundsPositionGiver = giver;
            }
            else if (giver.transform != copy.transform)
            {
                giver.transform.localPosition = Vector3.zero;
                giver.transform.localRotation = Quaternion.identity;
            }
            var square = new Vector3(size.x, ZoneHeight, size.z);
            giver.size = square;
            giver.bounds = new Bounds(Vector3.zero, square);
            return made;
        }

        /// <summary>Takes a zone of the mod's away: its people go back to the game's pools, the game's lists forget it. Safe on null.</summary>
        public static void Unzone(ref StationaryAiSpawner zone)
        {
            if (zone == null)
            {
                return;
            }
            zone.ClearPedestrians();
            StationaryAiSpawner.AllPedestrianSpawners.Remove(zone);
            CullingManager culling = InstanceBehavior<CullingManager>.IsInitialized ? InstanceBehavior<CullingManager>.Instance : null;
            culling?.generalCullingGroupController?.UnregisterCullable(zone);
            SceneryTrees.Destroy(zone.gameObject);
            zone = null;
        }

        /// <summary>People standing on a square of ground just now (the game's Human layer).</summary>
        public static int StandingOn(Vector3 at, float yaw, Vector3 size)
        {
            if (_humanMask == 0)
            {
                _humanMask = LayerMask.GetMask("Human");
            }
            int count = Physics.OverlapBoxNonAlloc(at + Vector3.up, new Vector3(size.x * 0.5f, 1.2f, size.z * 0.5f), Standing, Quaternion.Euler(0f, yaw, 0f),
                _humanMask, QueryTriggerInteraction.Collide);
            return count;
        }

        /// <summary>
        /// A crossing on new ground: a copy of one of the game's crosswalk triggers, its box set to the crossing's and
        /// linked to the red lamp people wait for; whatever the copy shows is switched off (the street's own paint marks it).
        /// </summary>
        public static GameObject Crossing(CrosswalkToTrafficLightLink template, Vector3 at, float yaw, Vector3 size, GameObject redLamp)
        {
            if (template == null || redLamp == null)
            {
                return null;
            }
            GameObject copy = Object.Instantiate(template.gameObject, at, Quaternion.Euler(0f, yaw, 0f));
            copy.name = CrossingName;
            copy.GetComponent<CrosswalkToTrafficLightLink>().redTrafficLight = redLamp;
            foreach (Renderer shown in copy.GetComponentsInChildren<Renderer>(true))
            {
                shown.enabled = false;
            }
            var box = copy.GetComponent<BoxCollider>();
            if (box == null)
            {
                foreach (Collider other in copy.GetComponents<Collider>())
                {
                    other.enabled = false;
                }
                box = copy.AddComponent<BoxCollider>();
            }
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = size;
            return copy;
        }

        /// <summary>One of the game's crosswalk triggers to copy, found once a city behind the loading screen or a pause; null where the city has none.</summary>
        public static CrosswalkToTrafficLightLink CrossingTemplate()
        {
            if (_crossing != null)
            {
                return _crossing;
            }
            CrosswalkToTrafficLightLink[] found = SceneFinds.All<CrosswalkToTrafficLightLink>("new ground: one of the game's crosswalk triggers to copy (a search of the whole scene, behind a pause)");
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].redTrafficLight != null && found[i].name != CrossingName)
                {
                    _crossing = found[i];
                    break;
                }
            }
            return _crossing;
        }

        private static CrosswalkToTrafficLightLink _crossing;

        /// <summary>The city is going: what was found in it is let go.</summary>
        public static void Forget()
        {
            _crossing = null;
            _humanMask = 0;
        }
    }
}
