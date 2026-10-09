using System.Collections.Generic;
using Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// The people of fires and street events: a pool of eight of its own (a second instance of
    /// the living city's pool class, so they are hidden when out of view, knocked down by the
    /// player's car like anybody else, and never taken from the accident pool or from the
    /// living city). Nobody is made until the first scene needs them, and then one at a time.
    ///
    /// What this adds to that pool:
    /// - people who may stand and walk on a closed carriageway (the navigation mesh the player
    ///   walks on) as well as on the pavement; they still only ever move by their agent;
    /// - things worn on a bone (helmet, air pack, a runner's number), fitted to the character
    ///   as it stands rather than by a table of offsets;
    /// - a staff carried in the hand (placard, banner) at the grip of the stop-paddle clip;
    /// - walking in from out of view and walking off out of view.
    /// Everything put on a person here comes off again in <see cref="Release"/>, which is the
    /// only way a person goes back.
    /// </summary>
    internal sealed class EventPeople
    {
        private const float ArriveNear = 26f;
        private const float ArriveFar = 50f;
        private const float DepartNear = 40f;
        private const float DepartFar = 75f;
        /// <summary>The clip a staff is held with: the wished-for one once the bundle has it, the stop paddle's until then.</summary>
        public static string StaffClip => ClipLibrary.Has("placard_hold") ? "placard_hold" : "paddle_hold";

        /// <summary>A clip from the wish-list if the bundle has it by now, else the one that stands in for it.</summary>
        public static string Clip(string wished, string fallback)
        {
            return ClipLibrary.Has(wished) ? wished : fallback;
        }

        private struct Extra
        {
            public CityPerson Who;
            public Transform Prop;
            public HeldTool Held;   // null for something worn (the person's own Bring holds that)
        }

        private readonly CityPeople _pool;
        private readonly EventProps _props;
        private readonly List<Extra> _extras = new List<Extra>(12);
        private readonly List<CityPerson> _leaving = new List<CityPerson>(8);
        private readonly List<CityPerson> _out = new List<CityPerson>(CityPeople.MaxPeople);

        public EventPeople(GameObject host, EventProps props)
        {
            _props = props;
            _pool = host.AddComponent<CityPeople>();
            _pool.Setup(props.Shelf);
        }

        public int Count => _pool.Count;
        public int Free => _pool.Free;
        public EventProps Props => _props;

        /// <summary>Makes one more character if the pool is not full. True while there is more to make.</summary>
        public bool Warm(Vector3 near)
        {
            return _pool.Warm(near + Vector3.down * 40f);
        }

        /// <summary>Everybody made, at once: for a test button, which cannot wait.</summary>
        public void WarmAll(Vector3 near)
        {
            while (Warm(near))
            {
            }
        }

        /// <summary>
        /// Somebody at a spot, facing a point. On the road if asked (the spot must be on the
        /// carriageway's navigation mesh), else on the pavement. Null if nobody is free or the
        /// spot will not take them.
        /// </summary>
        public CityPerson Put(CityLook look, Vector3 spot, Vector3 face, bool road, bool fresh = true)
        {
            Vector3 first = spot;
            if (road && !CitySpots.Pavement(spot, 45f, out first))
            {
                return null;   // the pool can only start somebody on pavement
            }
            CityPerson person = _pool.Take(look, first, face, fresh);
            if (person == null)
            {
                return null;
            }
            if (fresh)
            {
                FireOutfit.Forget(person);   // the pool has just dressed them as somebody else
            }
            if (road && !ToRoad(person, spot))
            {
                _pool.Release(person);
                return null;
            }
            person.Turn(face);
            person.Stand();
            _out.Add(person);
            return person;
        }

        /// <summary>
        /// One more person, put on the pavement out of view a short walk away and already walking
        /// to a place (which may be on a closed road). Null if nobody is free or there is no way in.
        /// </summary>
        public CityPerson WalkIn(CityLook look, Vector3 goal, bool road, bool jog = false)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (!CitySpots.PavementRing(goal, ArriveNear, ArriveFar, true, out Vector3 from) || !CitySpots.ClearQuietly(from, 0.4f))
                {
                    continue;
                }
                CityPerson person = _pool.Take(look, from, goal, false);
                if (person == null)
                {
                    return null;
                }
                if (FireOutfit.Wearing(person))
                {
                    _pool.Release(person);   // still in turnout gear from the last fire: not somebody to walk in as a passer-by
                    person = _pool.Take(look, from, goal, true);
                    if (person == null)
                    {
                        return null;
                    }
                    FireOutfit.Forget(person);
                }
                if ((!road || ToRoad(person, from)) && Reaches(person, goal) && person.WalkTo(goal, jog))
                {
                    _out.Add(person);
                    return person;
                }
                _pool.Release(person);
            }
            return null;
        }

        /// <summary>Somebody walks off to pavement out of view and is taken back there (<see cref="Tick"/>).</summary>
        public void SendOff(CityPerson person, bool jog = false)
        {
            if (person == null || person.Lost || _leaving.Contains(person))
            {
                return;
            }
            if (DropStaff(person))
            {
                person.StopOver(0.2f);   // the arm that held the staff comes down; anything else carried (a cup) stays in the hand
            }
            _leaving.Add(person);
            if (CitySpots.PavementRing(person.Position, DepartNear, DepartFar, true, out Vector3 away) || CitySpots.PavementRing(person.Position, DepartNear, DepartFar, false, out away))
            {
                person.WalkTo(away, jog);
            }
        }

        public bool Leaving(CityPerson person)
        {
            return _leaving.Contains(person);
        }

        /// <summary>How many are still on their way off.</summary>
        public int LeavingCount => _leaving.Count;

        /// <summary>Once a second: takes back whoever has walked out of view (or was lost), and whoever a car knocked down.</summary>
        public void Tick(Vector3 centre, bool force)
        {
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                CityPerson person = _leaving[i];
                if (person.Lost || force || (!CitySpots.InView(person.Position) && (person.Arrived || CitySpots.Flat(person.Position - centre) > DepartNear * DepartNear)))
                {
                    _leaving.RemoveAt(i);
                    Release(person);
                }
                else if (person.Arrived && (CitySpots.PavementRing(person.Position, DepartNear, DepartFar, true, out Vector3 away)))
                {
                    person.WalkTo(away);   // arrived where they can still be seen: on they go
                }
            }
            for (int i = _extras.Count - 1; i >= 0; i--)
            {
                if (_extras[i].Who.Lost)
                {
                    Strip(i);   // what they carried goes back on the shelf; the casualty system has the person
                }
            }
        }

        /// <summary>The only way back into the pool: whatever was put on them here comes off first.</summary>
        public void Release(CityPerson person)
        {
            if (person == null)
            {
                return;
            }
            for (int i = _extras.Count - 1; i >= 0; i--)
            {
                if (_extras[i].Who == person)
                {
                    Strip(i);
                }
            }
            _leaving.Remove(person);
            _out.Remove(person);
            _pool.Release(person);
        }

        public void ReleaseAll()
        {
            for (int i = _extras.Count - 1; i >= 0; i--)
            {
                Strip(i);
            }
            _out.Clear();
            _leaving.Clear();
            _pool.ReleaseAll();
        }

        // ---------------------------------------------------------------- the carriageway

        /// <summary>Moves somebody who has just been taken from the pool on to the navigation mesh the player walks on, at a spot.</summary>
        private static bool ToRoad(CityPerson person, Vector3 spot)
        {
            NavMeshAgent agent = person.Character.navmeshAgent;
            agent.enabled = false;
            agent.agentTypeID = NavMeshHelper.PlayerAgentTypeId;
            person.Character.transform.position = spot;
            agent.enabled = true;
            return agent.Warp(spot) && agent.isOnNavMesh;
        }

        private static readonly NavMeshPath Path = new NavMeshPath();

        private static bool Reaches(CityPerson person, Vector3 goal)
        {
            NavMeshAgent agent = person.Character.navmeshAgent;
            return agent.isOnNavMesh && agent.CalculatePath(goal, Path) && Path.status == NavMeshPathStatus.PathComplete;
        }

        // ---------------------------------------------------------------- worn and carried

        /// <summary>
        /// Something worn on a bone, fitted to the character as it stands now: the prop's pivot is put
        /// a given way above and in front of the bone, upright and facing the way the character faces.
        /// A person can wear two things. False (and nothing changed) if it cannot be had.
        /// </summary>
        public bool Wear(CityPerson person, string id, HumanBodyBones bone, float up, float forward)
        {
            if (person == null || person.Lost)
            {
                return false;
            }
            Animator animator = person.Character.animator;
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.Update(0f);   // somebody just taken from the pool still has the pose they were put away in: measured standing, as the game's idle has them
            }
            Transform at = animator != null ? animator.GetBoneTransform(bone) : null;
            Transform prop = at != null ? _props.Take(id) : null;
            if (prop == null)
            {
                return false;
            }
            Transform body = person.Character.transform;
            Vector3 world = at.position + Vector3.up * up + body.forward * forward;
            Vector3 local = Vector3.Scale(at.InverseTransformPoint(world), at.lossyScale);
            if (!person.Bring(prop, bone, local, Quaternion.Inverse(at.rotation) * body.rotation))
            {
                _props.Return(prop);
                return false;
            }
            _extras.Add(new Extra { Who = person, Prop = prop });
            return true;
        }

        /// <summary>
        /// A staff (placard, banner) in the right hand at the grip the stop-paddle clip was made
        /// with: the staff stands upright beside them whatever that hand bone's own axes are. The
        /// caller plays <see cref="StaffClip"/> (whole, standing) or lays it over the walk (right arm).
        /// </summary>
        public bool CarryStaff(CityPerson person, string id)
        {
            if (person == null || person.Lost || !ClipLibrary.TryGet(StaffClip, out CityClip clip) || !clip.HasTool)
            {
                return false;
            }
            Transform prop = _props.Take(id);
            if (prop == null)
            {
                return false;
            }
            ToolGrip grip = clip.Grip;
            grip.TwoHanded = false;
            var held = new HeldTool();
            if (!held.Attach(person.Character.animator, prop, grip))
            {
                _props.Return(prop);
                return false;
            }
            _extras.Add(new Extra { Who = person, Prop = prop, Held = held });
            return true;
        }

        /// <summary>Takes the staff out of their hand. True if they had one.</summary>
        public bool DropStaff(CityPerson person)
        {
            bool had = false;
            for (int i = _extras.Count - 1; i >= 0; i--)
            {
                if (_extras[i].Who == person && _extras[i].Held != null)
                {
                    Strip(i);
                    had = true;
                }
            }
            return had;
        }

        private void Strip(int index)
        {
            Extra extra = _extras[index];
            _extras.RemoveAt(index);
            if (extra.Held != null)
            {
                extra.Held.Detach(null);
            }
            else if (extra.Prop != null && extra.Who.Valid && !extra.Who.Lost)
            {
                extra.Who.Leave(extra.Prop.name);
            }
            if (extra.Prop != null)
            {
                _pool.Fallen.Remove(extra.Prop);   // if a car knocked them down the pool listed it as dropped: it is back on the shelf now
                extra.Prop.SetParent(null, true);
                _props.Return(extra.Prop);
            }
        }
    }
}
