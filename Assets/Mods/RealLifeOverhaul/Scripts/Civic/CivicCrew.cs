using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A handful of people at work at a place (a site's crew, a canvasser, a rally), never seen appearing or vanishing: they
    /// walk in and off out of view. Cost: nothing per frame; one step a second.
    /// </summary>
    public sealed class CivicCrew
    {
        private enum Doing { Coming, Working, Leaving }

        private sealed class Member
        {
            public CityPerson Person;
            public Vector3 Goal, Face;
            public string Work, Rest;
            public Doing Doing;
            public bool Resting;
            public float Until;
            public float Lift;         // metres they stand above the pavement (on a box)
            public bool Lifted;
            public float BaseOffset;
            public int Walks;
            public bool Borrowed;      // Rest is only a copy of Work, set so that a one-off has something to come back to
        }

        private readonly CivicCast _cast;
        private readonly List<Member> _members = new List<Member>(CivicCast.Most);
        private readonly Stack<Member> _spare = new Stack<Member>(CivicCast.Most);

        public CivicCrew(CivicCast cast)
        {
            _cast = cast;
        }

        public int Count => _members.Count;

        /// <summary>How many are at their place and at it.</summary>
        public int Present
        {
            get
            {
                int present = 0;
                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].Doing == Doing.Working)
                    {
                        present++;
                    }
                }
                return present;
            }
        }

        /// <summary>The first of them who is at their place; null if none is.</summary>
        public CityPerson First => _members.Count > 0 && _members[0].Doing == Doing.Working && !_members[0].Person.Lost ? _members[0].Person : null;

        /// <summary>True if one of them was knocked down or lost since they were added: whoever owns the crew should send the rest away.</summary>
        public bool Disturbed { get; private set; }

        /// <summary>One more person. False (and nobody taken) if nobody is free or there is no way for them to get there unseen.</summary>
        /// <param name="lift">Metres above the pavement they stand once there (the speaker on the box).</param>
        public bool Add(CityLook look, Vector3 goal, Vector3 face, string work, string rest, float lift = 0f)
        {
            if (_members.Count == 0)
            {
                Disturbed = false;   // a new crew: what happened to the last one is over
            }
            Vector3 from = goal;
            bool walkIn = CitySpots.InView(goal);
            if (walkIn)
            {
                bool found = false;
                for (int attempt = 0; attempt < 3 && !found; attempt++)
                {
                    found = CitySpots.PavementRing(goal, 24f, 46f, true, out from) && CitySpots.ClearQuietly(from, 0.4f) && CitySpots.Reachable(from, goal);
                }
                if (!found)
                {
                    return false;
                }
            }
            CityPerson person = _cast.Take(look, from, walkIn ? goal : face);
            if (person == null)
            {
                return false;
            }
            Member member = _spare.Count > 0 ? _spare.Pop() : new Member();
            member.Person = person;
            member.Goal = goal;
            member.Face = face;
            member.Work = work;
            member.Rest = rest;
            member.Resting = false;
            member.Lift = lift;
            member.Lifted = false;
            member.Borrowed = false;
            member.Walks = 0;
            member.Until = Time.time + Random.Range(8f, 16f);
            if (walkIn)
            {
                if (!person.WalkTo(goal))
                {
                    _cast.Release(person);
                    _spare.Push(member);
                    return false;
                }
                member.Doing = Doing.Coming;
            }
            else
            {
                Settle(member);
            }
            _members.Add(member);
            return true;
        }

        public void Tick(float now)
        {
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                Member member = _members[i];
                CityPerson person = member.Person;
                if (person.Lost)
                {
                    Disturbed = true;
                    Remove(i, false);   // the casualty system has them (or the scene went): theirs to put away
                    continue;
                }
                switch (member.Doing)
                {
                    case Doing.Coming:
                        if (person.Arrived)
                        {
                            if (person.Near(member.Goal, 0.6f))
                            {
                                person.Stand();
                                person.Face(member.Face);
                                Settle(member);
                            }
                            else if (++member.Walks > 3 || !person.WalkTo(member.Goal))
                            {
                                Leave(member);   // could not get there: off they go
                            }
                        }
                        break;

                    case Doing.Working:
                        if (member.Rest != null && now >= member.Until)
                        {
                            if (person.Work(member.Resting ? member.Work : member.Rest, _cast.Tools, 0.5f))
                            {
                                member.Resting = !member.Resting;
                                if (!member.Resting && member.Borrowed)
                                {
                                    // Back from a one-off (applause, a leaflet held out): they have no resting clip of their own to alternate with.
                                    member.Rest = null;
                                    member.Borrowed = false;
                                }
                            }
                            member.Until = now + (member.Resting ? Random.Range(4f, 8f) : Random.Range(9f, 18f));
                        }
                        break;

                    default:
                        if (member.Lifted)
                        {
                            // Up on a deck: they are not seen climbing down. They stay as they are until nobody is looking.
                            if (!CitySpots.InView(person.Position))
                            {
                                Remove(i, true);
                            }
                        }
                        else if (!CitySpots.InView(person.Position) && (person.Arrived || CitySpots.Flat(person.Position - member.Goal) > 38f * 38f))
                        {
                            Remove(i, true);
                        }
                        else if (person.Arrived && ++member.Walks <= 4)
                        {
                            SendOff(member);
                        }
                        break;
                }
            }
        }

        /// <summary>Plays a clip once on everyone who is at their place (a round of applause), then they go back to what they were doing.</summary>
        public void AllPlay(string clip, int fromIndex = 0)
        {
            for (int i = fromIndex; i < _members.Count; i++)
            {
                Member member = _members[i];
                if (member.Doing == Doing.Working && !member.Person.Lost)
                {
                    member.Person.Work(clip, _cast.Tools, 0.3f);
                    member.Resting = member.Rest != null;   // so that the next change puts the working clip back
                    member.Until = Time.time + Random.Range(4f, 7f);
                    if (member.Rest == null)
                    {
                        member.Rest = member.Work;   // a clip to come back to
                        member.Borrowed = true;
                    }
                }
            }
        }

        /// <summary>One of them (by the order they were added) plays a clip once and goes back to their own; false if they are not at their place.</summary>
        public bool Once(int index, string clip, float seconds)
        {
            if (index < 0 || index >= _members.Count || _members[index].Doing != Doing.Working || _members[index].Person.Lost)
            {
                return false;
            }
            Member member = _members[index];
            if (!member.Person.Work(clip, _cast.Tools, 0.3f))
            {
                return false;
            }
            if (member.Rest == null)
            {
                member.Rest = member.Work;
                member.Borrowed = true;
            }
            member.Resting = true;
            member.Until = Time.time + seconds;
            return true;
        }

        /// <summary>Changes what one of them does from now on (a speaker starts speaking once there is somebody to speak to).</summary>
        public void SetWork(int index, string clip)
        {
            if (index < 0 || index >= _members.Count || _members[index].Work == clip)
            {
                return;
            }
            Member member = _members[index];
            member.Work = clip;
            if (member.Borrowed)
            {
                member.Rest = clip;
            }
            if (member.Doing == Doing.Working && !member.Person.Lost && !member.Resting)
            {
                member.Person.Work(clip, _cast.Tools, 0.4f);
            }
        }

        /// <summary>Puts the tool of a second clip in the first person's other hand (the leaflet a canvasser holds out).</summary>
        public void Carry(string clip)
        {
            if (First != null)
            {
                First.Carry(clip, _cast.Tools);
            }
        }

        /// <summary>Everybody walks off; each is taken back once out of view. Safe when empty.</summary>
        public void Dismiss()
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Doing != Doing.Leaving)
                {
                    Leave(_members[i]);
                }
            }
        }

        /// <summary>Everybody back in the pool now. Safe at any time, any number of times.</summary>
        public void Release()
        {
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                Remove(i, true);
            }
            Disturbed = false;
        }

        private void Settle(Member member)
        {
            member.Doing = Doing.Working;
            member.Resting = false;
            if (member.Lift > 0f && !member.Lifted)
            {
                NavMeshAgent agent = member.Person.Character != null ? member.Person.Character.navmeshAgent : null;
                if (agent != null && agent.enabled)
                {
                    member.BaseOffset = agent.baseOffset;
                    agent.baseOffset = member.BaseOffset + member.Lift;
                    member.Lifted = true;
                }
            }
            if (member.Work != null)
            {
                member.Person.Work(member.Work, _cast.Tools, 0.35f, ClipEnd.Return, true, Random.Range(0f, 1.5f));
            }
        }

        private void Leave(Member member)
        {
            member.Doing = Doing.Leaving;
            member.Walks = 0;
            if (member.Lifted && member.Lift > 0.5f)
            {
                return;   // see Tick: taken back from where they stand, out of view
            }
            Lower(member);
            SendOff(member);
        }

        private static void SendOff(Member member)
        {
            CityPerson person = member.Person;
            if (CitySpots.PavementRing(person.Position, 38f, 70f, true, out Vector3 away) || CitySpots.PavementRing(person.Position, 38f, 70f, false, out away))
            {
                person.WalkTo(away);
            }
        }

        private static void Lower(Member member)
        {
            if (!member.Lifted)
            {
                return;
            }
            member.Lifted = false;
            NavMeshAgent agent = member.Person.Valid ? member.Person.Character.navmeshAgent : null;
            if (agent != null)
            {
                agent.baseOffset = member.BaseOffset;
            }
        }

        private void Remove(int index, bool release)
        {
            Member member = _members[index];
            _members.RemoveAt(index);
            Lower(member);
            if (release)
            {
                _cast.Release(member.Person);
            }
            member.Person = null;
            _spare.Push(member);
        }
    }
}
