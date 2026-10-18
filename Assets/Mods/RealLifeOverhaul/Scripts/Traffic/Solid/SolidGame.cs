using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Streets.Pedestrians;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Everything "traffic sees people in the road" asks of the game. Read: what a car's sensor stops for, tried against a
    /// real walking pedestrian, sets the Mode: Native (cars sense walkers), StandIns, or BrakesOnly (SolidBrakes alone). The
    /// stand-ins are sixteen trigger capsules tagged "AiVehicleHalt", made once and reported gone as the game reports a
    /// pedestrian put away. The fade: the game's HumanHider is held off for somebody a car brakes for. Cost: nothing per
    /// frame; each pooled car is measured once a session.
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

        // What the cars sense.

        private int _proxyLayer = -1;
        private bool _proxiesFailed;

        /// <summary>Reads what a traffic car senses and how a walking pedestrian stands to that; false while there is no such car or walker.</summary>
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
                   + "; sensor box " + SensorLength.ToString("F1", CultureInfo.InvariantCulture) + " m long on '" + LayerMask.LayerToName(sensorLayer) + "', which "
                   + (meets ? "meets" : "does not meet") + " the pedestrians' layer in the physics settings; mode: "
                   + (How == Mode.Native ? "the cars sense walking people themselves; stand-ins only for people without a capsule"
                       : How == Mode.StandIns ? "stand-ins on '" + LayerMask.LayerToName(_proxyLayer) + "' for everybody in a car's way"
                       : "no stand-in can be made (" + (box == null ? "the car has no sensor box" : _proxiesFailed
                           ? "the game no longer has the tag '" + HaltTag + "'" : "no layer the cars read meets their sensor")
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

        /// <summary>A car owed an accident stops seeing this walker's own capsule (Native only), told as the game tells it (ColliderRemoved).</summary>
        public void Unsee(VehicleComponent car, Pedestrian walker)
        {
            if (How == Mode.Native && car != null && walker != null && walker.tpc != null && walker.tpc.capsuleCollider != null)
            {
                car.ColliderRemoved(walker.tpc.capsuleCollider);
            }
        }

        // The stand-ins.

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
                Logger?.Info($"Traffic sees people: {_proxy.Length} stand-ins made on layer '{LayerMask.LayerToName(_proxyLayer)}' "
                             + $"(triggers tagged '{HaltTag}'): made once, switched off until needed.");
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

        /// <summary>Takes a stand-in off the road: the traffic system is told the collider has gone, and it is switched off.</summary>
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

        // Each pooled car, measured once.

        private object _pool;            // the traffic system the measurements belong to
        private Transform[] _carAt = new Transform[0];
        private float[] _axle = new float[0];        // metres from the car's origin to its front sensor's origin
        private float[] _reach = new float[0];       // ...and to the far end of its sensor box
        private float[] _nose = new float[0];        // ...and to its nose; NaN until measured
        private float[] _halfWidth = new float[0];
        private AiCarHorn[] _horn = new AiCarHorn[0];
        private bool[] _hornAsked = new bool[0];
        private bool[] _known = new bool[0];

        /// <summary>Makes room for the traffic system's cars, forgetting what was measured if it is another city's; true if it was.</summary>
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

        /// <summary>The car's transform, measured the first time it is seen; null if not measured yet and this look's budget is spent.</summary>
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

        // The fade.

        private readonly BaseHuman[] _hidden = new BaseHuman[Hiders];
        private readonly HumanHider[] _hider = new HumanHider[Hiders];
        private readonly LayerMask[] _hiderMask = new LayerMask[Hiders];
        private readonly float[] _hiderUntil = new float[Hiders];
        private int _hiders;
        private bool _hiderSaid;

        /// <summary>Holds the game's fade off for somebody a car is braking for or has hit, until a moment after the last call. A search of 24.</summary>
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
                Logger?.Info(FormattableString.Invariant(
                    $"Traffic check (fade): the game fades a person within {hider.detectionDistance:F1} m of anything on layers [{Names(_hiderMask[free].value)}]; ")
                             + "held off for whoever a car is braking for or has hit, and put back a moment after: PASS");
            }
        }

        /// <summary>While any fade is held off: whoever no car has had in its way for a moment, and is not lying down, fades again.</summary>
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
