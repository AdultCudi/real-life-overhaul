using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Streets.Pedestrians;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Everything "traffic sees people in the road" asks of the game, in one place.
    ///
    /// WHAT THE CARS SENSE (Read, once for a city, the first time a car and a walking pedestrian are both
    /// out). A traffic car stops for whatever non-trigger collider enters the sensor box ahead of it, if
    /// that collider is on one of the layers the traffic system was set up with
    /// (VehicleComponent.OnTriggerEnter; the masks are LayerSetupData's obstaclesLayers and playerLayers,
    /// handed to every car by TrafficManager.Initialize), and for a trigger only if it is tagged
    /// "AiVehicleHalt". Whether the game's pedestrians are on such a layer, whether their capsule is
    /// switched on while they walk, and whether the physics settings let the sensor's layer meet theirs
    /// cannot be read from the code, so they are read here from a real car and a real pedestrian, said
    /// in one log line, and the mode follows:
    ///   Native     the cars already sense a walking pedestrian's capsule. Stand-ins are used only for
    ///              people who have none switched on (standing figures, bodies, the mod's own people);
    ///   StandIns   they do not. Everybody in a car's way gets a stand-in;
    ///   BrakesOnly no stand-in can be made (no layer the cars read meets their sensor, or the game no
    ///              longer has the tag): the mod's own braking (SolidBrakes) stops the cars by itself.
    ///
    /// THE STAND-INS. Sixteen trigger capsules of a person's size, tagged "AiVehicleHalt", on a layer the
    /// cars read, each with a kinematic body. Made once, the first time one is needed; never made or
    /// destroyed after that. One in use stands on its person and is moved to them eight times a second;
    /// one not in use is switched off. Being triggers they push nothing and stop nobody: not the
    /// player's car, not a person, not a sweep or ray that ignores triggers. A car that senses one stops
    /// for it and sets off again by the traffic system's own bookkeeping (its obstacle list), and a
    /// stand-in that is taken away is first reported to the traffic system the way the game reports a
    /// pedestrian put back in its pool (Manager.TriggerColliderRemovedEvent), so no car is left waiting
    /// for something that is gone.
    ///
    /// THE FADE. The game fades a person out while a vehicle is within two metres of them
    /// (HumanHider: ten times a second it asks physics whether anything on its layersToHideFromWhenNear is
    /// near, and sets "_NearVehicle" on their renderers). For somebody a car is braking for or has hit
    /// that fade is held off: their hider's mask is set to nothing, so its own next look finds nothing near
    /// and it shows them again itself, hand-held things included; the mask is put back when no car has
    /// had them in its way for a second and a half and they are not lying in the road. At most 24 people
    /// at once; nothing is added to anybody.
    ///
    /// COST. Nothing per frame. Read: once. A stand-in placed: one position set. One taken away: one call
    /// that walks the traffic's cars (a list lookup each). A car's measurements (its sensor's reach, its
    /// width, its nose, its horn): once for each pooled car in a session, then array reads.
    /// </summary>
    internal sealed class SolidGame
    {
        public enum Mode { Unknown, Native, StandIns, BrakesOnly }

        private const string HaltTag = "AiVehicleHalt";   // the one tag a trigger must carry for a traffic car to stop for it (VehicleComponent.OnTriggerEnter)
        private const string SetupName = "LayerSetupData"; // the traffic system's layer set-up, as TrafficManager.Initialize loads it
        private const int Hiders = 24;                     // people whose fade is held off at once
        private const float HiderFor = 1.5f;               // seconds after a car last had them in its way
        private const float ParkedAt = -500f;              // metres under the world: where a stand-in is before its first use

        public static IModLogger Logger;

        /// <summary>How the cars are made to see people; Unknown until a car and a pedestrian have been looked at.</summary>
        public Mode How { get; private set; }

        /// <summary>The sensor box's length in metres, as read.</summary>
        public float SensorLength { get; private set; }

        /// <summary>What was read, in one sentence, for the log and for whoever asks.</summary>
        public string Said { get; private set; } = "nothing read yet";

        // ---------------------------------------------------------------- what the cars sense

        private int _proxyLayer = -1;
        private bool _proxiesFailed;

        /// <summary>
        /// Reads what a traffic car senses and how a walking pedestrian stands to that. False (and nothing decided)
        /// while there is no car with a sensor or no pedestrian walking to look at.
        /// </summary>
        public bool Read(List<VehicleComponent> cars, List<Pedestrian> walkers)
        {
            VehicleComponent car = null;
            for (int i = 0; cars != null && i < cars.Count && car == null; i++)
            {
                if (cars[i] != null && cars[i].frontTrigger != null)
                {
                    car = cars[i];
                }
            }
            Pedestrian walker = null;
            for (int i = 0; walkers != null && i < walkers.Count && walker == null; i++)
            {
                Pedestrian one = walkers[i];
                if (one != null && one.isActiveAndEnabled && one.tpc != null && one.tpc.capsuleCollider != null && one.tpc.navmeshAgent != null
                    && one.tpc.navmeshAgent.isActiveAndEnabled && !one.tpc.navmeshAgent.isStopped)
                {
                    walker = one;
                }
            }
            if (car == null || walker == null)
            {
                return false;
            }

            LayerSetup setup = Resources.Load<LayerSetup>(SetupName);
            BoxCollider box = car.frontTrigger.GetComponentInChildren<BoxCollider>(true);
            CapsuleCollider capsule = walker.tpc.capsuleCollider;
            int sensed = setup != null ? setup.obstaclesLayers.value | setup.playerLayers.value : 0;
            int buildings = setup != null ? setup.buildingsLayers.value : 0;
            int sensorLayer = box != null ? box.gameObject.layer : car.frontTrigger.gameObject.layer;
            int carLayer = car.gameObject.layer;
            int layer = capsule.gameObject.layer;
            bool on = capsule.enabled && !capsule.isTrigger;
            bool meets = !Physics.GetIgnoreLayerCollision(sensorLayer, layer);
            bool read = ((sensed >> layer) & 1) != 0 && ((buildings >> layer) & 1) == 0 && layer != carLayer;
            SensorLength = box != null ? Mathf.Abs(box.size.z * box.transform.lossyScale.z) : 0f;

            // The layer the stand-ins go on: the people's own if the cars read it (it is what they stand for), else the
            // first the cars read that their sensor meets. Never a building's layer (a car backs away from those) and
            // never the cars' own (the game takes anything there for a vehicle).
            _proxyLayer = read && meets ? layer : -1;
            for (int candidate = 0; candidate < 32 && _proxyLayer < 0; candidate++)
            {
                if (((sensed >> candidate) & 1) != 0 && ((buildings >> candidate) & 1) == 0 && candidate != carLayer
                    && !Physics.GetIgnoreLayerCollision(sensorLayer, candidate))
                {
                    _proxyLayer = candidate;
                }
            }
            for (int i = 0; _made && i < _proxy.Length; i++)
            {
                if (_proxy[i] != null && _proxyLayer >= 0)
                {
                    _proxy[i].layer = _proxyLayer;   // (another city's traffic was read: the stand-ins follow what it senses)
                }
            }
            bool native = read && meets && on && box != null;
            How = native ? Mode.Native : _proxyLayer >= 0 && box != null && !_proxiesFailed ? Mode.StandIns : Mode.BrakesOnly;

            Said = "cars sense layers [" + Names(sensed) + "]; pedestrians are on '" + LayerMask.LayerToName(layer) + "' with their capsule " + (on ? "on" : "off")
                   + "; sensor box " + SensorLength.ToString("F1") + " m long on '" + LayerMask.LayerToName(sensorLayer) + "', which "
                   + (meets ? "meets" : "does not meet") + " the pedestrians' layer in the physics settings; mode: "
                   + (How == Mode.Native ? "the cars sense walking people themselves; stand-ins only for people without a capsule"
                       : How == Mode.StandIns ? "stand-ins on '" + LayerMask.LayerToName(_proxyLayer) + "' for everybody in a car's way"
                       : "no stand-in can be made (" + (box == null ? "the car has no sensor box" : _proxiesFailed ? "the game no longer has the tag '" + HaltTag + "'" : "no layer the cars read meets their sensor")
                         + "): the mod's own braking alone");
            Logger?.Info("Traffic check (senses): " + Said + ".");
            return true;
        }

        private static string Names(int mask)
        {
            string names = string.Empty;
            for (int layer = 0; layer < 32; layer++)
            {
                if (((mask >> layer) & 1) != 0)
                {
                    string name = LayerMask.LayerToName(layer);
                    names += (names.Length > 0 ? ", " : string.Empty) + (string.IsNullOrEmpty(name) ? layer.ToString() : name);
                }
            }
            return names.Length > 0 ? names : "none";
        }

        /// <summary>True if this pedestrian is sensed by the cars as they are: the cars read walking people, and this one's capsule is on.</summary>
        public bool SensedAsIs(Pedestrian walker)
        {
            if (How != Mode.Native || walker == null || walker.tpc == null)
            {
                return false;
            }
            CapsuleCollider capsule = walker.tpc.capsuleCollider;
            return capsule != null && capsule.enabled && capsule.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// A car that is owed an accident stops seeing this pedestrian's own capsule (Native only): its sensor is told
        /// the collider is gone, the way the game tells it (VehicleComponent.ColliderRemoved). Every other car still sees them.
        /// </summary>
        public void Unsee(VehicleComponent car, Pedestrian walker)
        {
            if (How == Mode.Native && car != null && walker != null && walker.tpc != null && walker.tpc.capsuleCollider != null)
            {
                car.ColliderRemoved(walker.tpc.capsuleCollider);
            }
        }

        // ---------------------------------------------------------------- the stand-ins

        private readonly GameObject[] _proxy = new GameObject[SolidRules.Proxies];
        private readonly Transform[] _proxyAt = new Transform[SolidRules.Proxies];
        private readonly Collider[] _proxyCollider = new Collider[SolidRules.Proxies];
        private readonly bool[] _proxyOut = new bool[SolidRules.Proxies];
        private bool _made;

        /// <summary>True if stand-ins can be used at all (the mode allows it and they could be made).</summary>
        public bool StandInsUsable => (How == Mode.Native || How == Mode.StandIns) && _proxyLayer >= 0 && !_proxiesFailed;

        // The sixteen, made once. False (and the mode falls back on the mod's own braking) if the game refuses the tag.
        private bool Make(Transform parent)
        {
            if (_made)
            {
                return true;
            }
            try
            {
                for (int i = 0; i < _proxy.Length; i++)
                {
                    var stand = new GameObject("RealLifeOverhaul Person Ahead " + (i + 1));
                    _proxy[i] = stand;
                    stand.transform.SetParent(parent, false);
                    stand.transform.position = new Vector3(0f, ParkedAt, 0f);
                    stand.layer = _proxyLayer;
                    stand.tag = HaltTag;   // throws if the game has no such tag any more
                    Rigidbody body = stand.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    CapsuleCollider capsule = stand.AddComponent<CapsuleCollider>();
                    capsule.isTrigger = true;
                    capsule.direction = 1;
                    capsule.radius = SolidRules.ProxyRadius;
                    capsule.height = SolidRules.ProxyHeight;
                    capsule.center = new Vector3(0f, SolidRules.ProxyHeight * 0.5f, 0f);
                    _proxyAt[i] = stand.transform;
                    _proxyCollider[i] = capsule;
                    stand.SetActive(false);
                }
                _made = true;
                Logger?.Info($"Traffic sees people: {_proxy.Length} stand-ins made on layer '{LayerMask.LayerToName(_proxyLayer)}' (triggers tagged '{HaltTag}'): made once, switched off until needed.");
                return true;
            }
            catch (Exception e)
            {
                Logger?.Warn("Traffic sees people: the stand-ins could not be made (" + e.Message + "); cars are stopped for people by the mod's own braking alone.");
                DestroyStandIns();
                _proxiesFailed = true;
                How = Mode.BrakesOnly;
                return false;
            }
        }

        /// <summary>Puts a stand-in on somebody (their feet), switching it on if it was off. False if stand-ins cannot be used.</summary>
        public bool Place(int slot, Vector3 feet, Transform parent)
        {
            if (!StandInsUsable || !Make(parent))
            {
                return false;
            }
            _proxyAt[slot].position = feet;
            if (!_proxyOut[slot])
            {
                _proxyOut[slot] = true;
                _proxy[slot].SetActive(true);
            }
            return true;
        }

        /// <summary>
        /// Takes a stand-in off the road: the traffic system is told the collider has gone (the cars drop it from
        /// their lists and set off if nothing else holds them), and it is switched off.
        /// </summary>
        public void Park(int slot)
        {
            if (!_made || !_proxyOut[slot])
            {
                return;
            }
            _proxyOut[slot] = false;
            if (_proxy[slot] == null)
            {
                return;   // destroyed with the mod's root object
            }
            if (TrafficManager.HasInstance && TrafficManager.IsInitialized)
            {
                Manager.TriggerColliderRemovedEvent(_proxyCollider[slot]);
            }
            _proxy[slot].SetActive(false);
        }

        /// <summary>The mod is closing: the stand-ins go with it.</summary>
        public void DestroyStandIns()
        {
            for (int i = 0; i < _proxy.Length; i++)
            {
                if (_proxy[i] != null)
                {
                    if (_proxyOut[i] && TrafficManager.HasInstance && TrafficManager.IsInitialized)
                    {
                        Manager.TriggerColliderRemovedEvent(_proxyCollider[i]);
                    }
                    Object.Destroy(_proxy[i]);
                }
                _proxy[i] = null;
                _proxyAt[i] = null;
                _proxyCollider[i] = null;
                _proxyOut[i] = false;
            }
            _made = false;
        }

        // ---------------------------------------------------------------- each pooled car, measured once

        private object _pool;            // the traffic system the measurements belong to
        private Transform[] _carAt = new Transform[0];
        private float[] _axle = new float[0];        // metres from the car's origin to its front sensor's origin
        private float[] _reach = new float[0];       // ...and to the far end of its sensor box
        private float[] _nose = new float[0];        // ...and to its nose; NaN until measured
        private float[] _halfWidth = new float[0];
        private AiCarHorn[] _horn = new AiCarHorn[0];
        private bool[] _hornAsked = new bool[0];
        private bool[] _known = new bool[0];

        /// <summary>
        /// Makes room for the traffic system's cars, forgetting what was measured if it is another traffic system
        /// than last time (another city). True if it was.
        /// </summary>
        public bool Pool(object traffic, int cars)
        {
            if (ReferenceEquals(traffic, _pool) && cars <= _known.Length)
            {
                return false;
            }
            _pool = traffic;
            _carAt = new Transform[cars];
            _axle = new float[cars];
            _reach = new float[cars];
            _nose = new float[cars];
            _halfWidth = new float[cars];
            _horn = new AiCarHorn[cars];
            _hornAsked = new bool[cars];
            _known = new bool[cars];
            How = Mode.Unknown;
            _proxiesFailed = false;
            return true;
        }

        private const int MeasuredAtOnce = 3;        // cars measured for the first time in one look: the rest wait for the next (an eighth of a second)
        private int _budget;

        /// <summary>A new look begins: a few more cars may be measured in it.</summary>
        public void BeginLook()
        {
            _budget = MeasuredAtOnce;
        }

        /// <summary>
        /// The car's transform, with its measurements taken the first time it is seen. Null if it has not been
        /// measured yet and enough have been in this look: it is looked at from the next one.
        /// </summary>
        public Transform At(int index, VehicleComponent car)
        {
            if (!_known[index])
            {
                if (_budget <= 0)
                {
                    return null;
                }
                _budget--;
                Transform body = car.transform;
                Transform trigger = car.frontTrigger;
                _carAt[index] = body;
                _halfWidth[index] = Mathf.Max(0.8f, car.colliderWidth * 0.5f);
                _axle[index] = trigger != null ? body.InverseTransformPoint(trigger.position).z : Mathf.Max(1.5f, car.length * 0.5f);
                BoxCollider box = trigger != null ? trigger.GetComponentInChildren<BoxCollider>(true) : null;
                // (Measured in the sensor's own space: it is turned with the wheels, and its length is not to depend on that.)
                _reach[index] = box != null
                    ? _axle[index] + trigger.InverseTransformPoint(box.transform.TransformPoint(box.center + new Vector3(0f, 0f, box.size.z * 0.5f))).z
                    : _axle[index];
                // Its nose, from its colliders (CarStrikes measures each pooled car once and remembers).
                CarStrikes strikes = CarStrikes.Instance;
                float nose = _axle[index] + 0.9f;
                if (strikes != null)
                {
                    strikes.Box(car, out Vector3 centre, out Vector3 half);
                    nose = centre.z + half.z;
                }
                _nose[index] = nose;
                _known[index] = true;
            }
            return _carAt[index];
        }

        public float HalfWidth(int index) => _halfWidth[index];
        public float Axle(int index) => _axle[index];

        /// <summary>Metres from the car's origin to its nose.</summary>
        public float Nose(int index) => _nose[index];

        /// <summary>The gap this car aims to leave: where the game's sensor has the person's stand-in, or 2.5 m when no stand-in can be used.</summary>
        public float Gap(int index)
        {
            return StandInsUsable ? SolidRules.GapFor(_reach[index] - _nose[index]) : SolidRules.Gap;
        }

        /// <summary>Sounds the car's own horn (the game's AiCarHorn, its two short blasts). False if it has none.</summary>
        public bool Hoot(int index, VehicleComponent car)
        {
            if (!_hornAsked[index])
            {
                _hornAsked[index] = true;
                _horn[index] = car.GetComponentInChildren<AiCarHorn>();
            }
            AiCarHorn horn = _horn[index];
            if (horn == null)
            {
                return false;
            }
            TrafficTools.Hoot(horn);
            return true;
        }

        // ---------------------------------------------------------------- the fade

        private readonly BaseHuman[] _hidden = new BaseHuman[Hiders];
        private readonly HumanHider[] _hider = new HumanHider[Hiders];
        private readonly LayerMask[] _hiderMask = new LayerMask[Hiders];
        private readonly float[] _hiderUntil = new float[Hiders];
        private int _hiders;
        private bool _hiderSaid;

        /// <summary>
        /// Somebody a car is braking for, or has hit: the game's fade beside vehicles is held off for them, from now
        /// until a moment after this was last called for them. Costs a search of 24 places; the first call for a
        /// person also finds their hider (once).
        /// </summary>
        public void Unfade(BaseHuman human, float now)
        {
            if (human == null)
            {
                return;
            }
            int free = -1;
            for (int i = 0; i < _hidden.Length; i++)
            {
                if ((object)_hidden[i] == human)
                {
                    _hiderUntil[i] = now + HiderFor;
                    return;
                }
                if (free < 0 && (object)_hidden[i] == null)
                {
                    free = i;
                }
            }
            if (free < 0)
            {
                return;
            }
            HumanHider hider = human.GetComponentInChildren<HumanHider>(true);
            if (hider == null)
            {
                hider = human.GetComponentInParent<HumanHider>();
            }
            _hidden[free] = human;
            _hider[free] = hider;
            _hiderUntil[free] = now + HiderFor;
            _hiders++;
            if (hider == null)
            {
                return;   // remembered all the same, so that it is not looked for again at every look
            }
            _hiderMask[free] = hider.layersToHideFromWhenNear;
            hider.layersToHideFromWhenNear = 0;
            if (!_hiderSaid)
            {
                _hiderSaid = true;
                Logger?.Info($"Traffic check (fade): the game fades a person within {hider.detectionDistance:F1} m of anything on layers [{Names(_hiderMask[free].value)}]; "
                             + "held off for whoever a car is braking for or has hit, and put back a moment after: PASS");
            }
        }

        /// <summary>Eight times a second while anybody's fade is held off: whoever no car has had in its way for a moment, and who is not lying in the road, fades as the game has it again.</summary>
        public void TickFade(float now)
        {
            if (_hiders == 0)
            {
                return;
            }
            for (int i = 0; i < _hidden.Length; i++)
            {
                BaseHuman human = _hidden[i];
                if ((object)human == null)
                {
                    continue;
                }
                if (human != null && (now < _hiderUntil[i] || RagdollSystem.Holds(human)))
                {
                    continue;
                }
                Restore(i);
            }
        }

        /// <summary>Everybody's fade is the game's again, now.</summary>
        public void RestoreFades()
        {
            for (int i = 0; i < _hidden.Length; i++)
            {
                if ((object)_hidden[i] != null)
                {
                    Restore(i);
                }
            }
            _hiders = 0;
        }

        private void Restore(int i)
        {
            HumanHider hider = _hider[i];
            if (hider != null && hider.layersToHideFromWhenNear.value == 0)
            {
                hider.layersToHideFromWhenNear = _hiderMask[i];   // (only if nobody has set it since)
            }
            _hidden[i] = null;
            _hider[i] = null;
            _hiders = Mathf.Max(0, _hiders - 1);
        }

        /// <summary>People whose fade is held off now.</summary>
        public int Unfaded => _hiders;
    }
}
