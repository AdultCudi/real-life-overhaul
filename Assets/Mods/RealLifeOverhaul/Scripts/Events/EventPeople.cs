using System.Collections.Generic;
using Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// The people of fires and street events: a pool of eight of their own, a second <see cref="CityPeople"/>, made one at
    /// a time once a scene needs them. It adds people who stand and walk on a closed carriageway, things worn on a bone
    /// (helmet, air pack, a runner's number) fitted as the character stands, a staff carried in the hand, and walking in
    /// and off out of view. Everything put on a person comes off again in <see cref="Release"/>, the only way back.
    /// </summary>
    internal sealed class EventPeople
    {
        private const float ArriveNear = 26f;   // metres from their goal that people walk in from
        private const float ArriveFar = 50f;
        private const float DepartNear = 40f;   // metres away that people walk off to
        private const float DepartFar = 75f;

        /// <summary>The clip a staff is held with: placard_hold once the bundle has it, the stop paddle's until then.</summary>
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
            public HeldTool Held;   // null for something worn, which the person's own Bring holds
        }

        private readonly CityPeople _pool;
        private readonly EventProps _props;
        private readonly List<Extra> _extras = new List<Extra>(12);
        private readonly List<CityPerson> _leaving = new List<CityPerson>(8);

        public EventPeople(GameObject host, EventProps props)
        {
            _props = props;
            _pool = host.AddComponent<CityPeople>();
            _pool.Setup(props.Shelf);
        }

        public int Count => _pool.Count;
        public int Free => _pool.Free;
        public EventProps Props => _props;

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

        /// <summary>Somebody at a spot, facing a point, on the road or the pavement; null if nobody is free or fits there.</summary>
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
            return person;
        }

        /// <summary>One more person put out of view a short walk away, walking to a place that may be on a closed road; null if not possible.</summary>
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
                    _pool.Release(person);   // still in turnout gear from the last fire
                    person = _pool.Take(look, from, goal, true);
                    if (person == null)
                    {
                        return null;
                    }
                    FireOutfit.Forget(person);
                }
                if ((!road || ToRoad(person, from)) && Reaches(person, goal) && person.WalkTo(goal, jog))
                {
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
                person.StopOver(0.2f);   // the staff's arm comes down; a cup in the other hand stays
            }
            _leaving.Add(person);
            if (CitySpots.PavementRing(person.Position, DepartNear, DepartFar, true, out Vector3 away) || CitySpots.PavementRing(person.Position, DepartNear, DepartFar, false, out away))
            {
                person.WalkTo(away, jog);
            }
        }

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
            _pool.Release(person);
        }

        public void ReleaseAll()
        {
            for (int i = _extras.Count - 1; i >= 0; i--)
            {
                Strip(i);
            }
            _leaving.Clear();
            _pool.ReleaseAll();
        }

        // Moves somebody just taken from the pool on to the navigation mesh the player walks on, at a spot.
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

        /// <summary>Something worn on a bone, fitted as the character stands so far above and ahead of it; two at most a person.</summary>
        public bool Wear(CityPerson person, string id, HumanBodyBones bone, float up, float forward)
        {
            if (person == null || person.Lost)
            {
                return false;
            }
            Animator animator = person.Character.animator;
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.Update(0f);   // fresh from the pool they still have the pose they were put away in
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

        /// <summary>A staff (placard, banner) in the right hand at <see cref="StaffClip"/>'s grip, for the caller to play standing or walking.</summary>
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

        // Takes the staff out of their hand; true if they had one.
        private bool DropStaff(CityPerson person)
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
                _pool.Fallen.Remove(extra.Prop);   // listed as dropped if a car knocked them down
                extra.Prop.SetParent(null, true);
                _props.Return(extra.Prop);
            }
        }
    }
}
