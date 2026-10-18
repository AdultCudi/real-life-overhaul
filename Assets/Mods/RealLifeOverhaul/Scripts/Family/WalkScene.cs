using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// A pushchair walk of a couple of hundred metres wherever the player likes, pushing the modelled pushchair over the game's
    /// own walk; the partner walks along when home and free. The pushchair is folded away however the walk ends.
    /// </summary>
    internal sealed class WalkScene : FamilyScene
    {
        private const float HandleAhead = 0.88f;   // the pushchair's middle ahead of the feet: its handle is then where push_cart has the hands

        private static readonly string[] Sights =
        {
            "reallife_family_walk_sight_1", "reallife_family_walk_sight_2", "reallife_family_walk_sight_3", "reallife_family_walk_sight_4",
            "reallife_family_walk_sight_5", "reallife_family_walk_sight_6"
        };
        private static int _lastSight = -1;

        private readonly bool _test;
        private FamilyDirector _d;
        private Transform _chair;
        private CityPerson _partner;
        private ChildRecord _child;
        private Vector3 _last;
        private float _left;
        private float _nextSight;
        private float _nextFollow;
        private string _shownLeft;
        private int _shownTens = -1;
        private bool _parting;
        private float _partBy;
        private bool _finished;

        private readonly System.Action _endByKey;   // made once: the prompt is asked for every tick

        public WalkScene(bool test)
        {
            _test = test;
            _endByKey = EndByKey;
        }

        public override string Name => "the pushchair walk";

        private void EndByKey()
        {
            Finish(Time.time, _left < FamilyBalance.WalkMetres * 0.5f);
        }

        public override bool Begin(FamilyDirector director, out string refusal)
        {
            _d = director;
            FamilySave s = FamilyState.S;
            refusal = null;
            _child = FamilyState.Smallest();
            if (_child == null)
            {
                refusal = "reallife_family_no_baby";
                return false;
            }
            if (Children.AwayThisWeekend(TimeHelper.CurrentDay))
            {
                refusal = "reallife_family_no_away";
                return false;
            }
            if (!s.Pushchair)
            {
                if (!_test)
                {
                    refusal = "reallife_family_no_pushchair";
                    return false;
                }
                s.Pushchair = true;
                FamilyState.DropErrand(Errands.BuyPushchair);
            }
            if (BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle)
            {
                refusal = "reallife_family_no_walk_indoors";
                return false;
            }
            PlayerController controller = PlayerHelper.PlayerController;
            if (controller == null || controller.Character == null || !ClipLibrary.Has(SceneClips.PushCart))
            {
                refusal = "reallife_family_no_prop";
                return false;
            }
            _chair = director.Cast.Props.Take("pushchair");
            if (_chair == null)
            {
                refusal = "reallife_family_no_prop";
                return false;
            }
            director.Body.Bind(controller);
            Transform body = controller.Character.transform;
            Vector3 scale = body.lossyScale;
            if (Mathf.Abs(scale.x) < 0.001f || Mathf.Abs(scale.y) < 0.001f || Mathf.Abs(scale.z) < 0.001f || !director.Body.Over(SceneClips.PushCart, BodyMask.Arms, 0.3f))
            {
                refusal = "reallife_family_no_prop";
                return false;
            }
            _chair.SetParent(body, false);
            _chair.localPosition = new Vector3(0f, 0f, HandleAhead / scale.z);
            _chair.localRotation = Quaternion.identity;
            _chair.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

            _last = controller.transform.position;
            _left = FamilyBalance.WalkMetres;
            _nextSight = Time.time + 25f;
            Along(controller.transform.position);
            FamilyText.Toast(NotificationType.Info, _partner != null ? "reallife_family_walk_start_both" : "reallife_family_walk_start", Args.Us.Kid(_child.Name), 8f);
            return true;
        }

        // The partner comes too when they are home and free: in from out of view, then beside the player.
        private void Along(Vector3 player)
        {
            FamilySave s = FamilyState.S;
            if (!FamilyState.Together || !s.LivesIn || _d.Cast.Failed || HomeLife.Where(TimeHelper.CurrentDay, TimeHelper.CurrentHour) != Whereabouts.Home)
            {
                return;
            }
            if (!CitySpots.Pavement(player, 3f, out Vector3 near))
            {
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                if (CitySpots.PavementRing(near, 22f, 40f, true, out Vector3 from) && CitySpots.Reachable(from, near))
                {
                    _partner = _d.Cast.Put(0, FamilyLook.Partner, from, near, NavMeshHelperIds.Npc);
                    if (_partner != null)
                    {
                        _partner.WalkTo(near);
                    }
                    return;
                }
            }
        }

        public override void Tick(float now)
        {
            PlayerController controller = PlayerHelper.PlayerController;
            if (_parting)
            {
                // The walk is over; the partner goes on home, and is gone once nobody can see them.
                bool gone = _partner == null || _partner.Lost;
                if (!gone)
                {
                    Vector3 offset = _partner.Position - PlayerHelper.GetPosition();
                    bool unseen = !CitySpots.InView(_partner.Position);
                    gone = (unseen && CitySpots.Flat(offset) > 18f * 18f) || (now >= _partBy && unseen) || now >= _partBy + 40f;
                }
                Done = gone;
                return;
            }
            if (controller == null || _chair == null || PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding)
            {
                Finish(now, false);
                return;
            }
            Vector3 at = controller.transform.position;
            float step = Mathf.Sqrt(CitySpots.Flat(at - _last));
            _last = at;
            if (step < 5f)
            {
                _left -= step;
            }
            if (_left <= 0f)
            {
                Finish(now, true);
                return;
            }
            int tens = Mathf.CeilToInt(_left / 10f);
            if (tens != _shownTens)
            {
                _shownTens = tens;
                _shownLeft = (tens * 10).ToString();
            }
            _d.Objective("reallife_family_objective_title", "reallife_family_objective_walk", _shownLeft, _child.Name);
            if (_left < FamilyBalance.WalkMetres - 30f)
            {
                _d.Prompt("reallife_family_prompt_walk_end", _child.Name, _endByKey);
            }
            if (now >= _nextSight && step > 0.05f)
            {
                _nextSight = now + Random.Range(28f, 50f);
                FamilyText.Toast(NotificationType.Info, FamilyText.Pick(Sights, ref _lastSight), Args.Us.Kid(_child.Name), 6f);
            }
            if (_partner != null && !_partner.Lost && now >= _nextFollow)
            {
                _nextFollow = now + 1f;
                Transform body = controller.Character.transform;
                Vector3 beside = at - body.forward * 0.9f + body.right * 1.0f;
                if (CitySpots.Flat(_partner.Position - beside) > 1.6f * 1.6f && CitySpots.Pavement(beside, 2f, out Vector3 spot))
                {
                    _partner.WalkTo(spot, CitySpots.Flat(_partner.Position - beside) > 7f * 7f);
                }
            }
        }

        private void Finish(float now, bool whole)
        {
            if (_finished)
            {
                return;
            }
            _finished = true;
            FamilySave s = FamilyState.S;
            Fold();
            bool along = _partner != null && !_partner.Lost;
            if (whole)
            {
                bool first = s.LastWalkDay != TimeHelper.CurrentDay;
                s.LastWalkDay = TimeHelper.CurrentDay;
                s.Walks++;
                _child.Closeness = Mathf.Min(100f, _child.Closeness + (first ? 4f : 1f));
                FamilyModifiers.Add(FamilyModifiers.Joy);
                if (along)
                {
                    HomeLife.Shared(first ? 3f : 1f);
                }
                if (s.Walks == 1)
                {
                    FamilyState.Remember("reallife_family_memory_first_walk", _child.Name);
                }
                FamilyText.Good(along ? "reallife_family_walk_done_both" : "reallife_family_walk_done", Args.Us.Kid(_child.Name));
            }
            else
            {
                FamilyText.Toast(NotificationType.Info, "reallife_family_walk_cut_short", Args.Us.Kid(_child.Name), 6f);
            }
            if (!along)
            {
                Done = true;
                return;
            }
            _parting = true;
            _partBy = now + 40f;
            if (!CitySpots.PavementRing(_partner.Position, 26f, 46f, false, out Vector3 away) || !_partner.WalkTo(away))
            {
                _partBy = now + 10f;
            }
        }

        private void Fold()
        {
            _d.Body.StopOver(0.3f);
            if (_chair != null)
            {
                _chair.SetParent(null, true);
                _chair.localScale = Vector3.one;
                _d.Cast.Props.Return(_chair);
                _chair = null;
            }
        }

        public override void End()
        {
            if (_d == null)
            {
                return;
            }
            Fold();
            _d.Body.Release();
            if (_partner != null)
            {
                _d.Cast.Release(_partner);
                _partner = null;
            }
        }
    }
}
