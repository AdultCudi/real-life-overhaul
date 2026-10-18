using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The outing's part for helping them move: three boxes carried from the old door to the new one.</summary>
    public sealed partial class OutingScene
    {
        private const int BoxCount = 3;
        private const string CarryClip = "carry_box_hold";

        // The printed moving box is built to be carried as the parcel the clip was made with.
        private static string BoxProp => PropLibrary.Has("moving_box") ? "moving_box" : "parcel_m";

        private readonly Transform[] _boxes = new Transform[BoxCount];
        private readonly Vector3[] _boxAt = new Vector3[BoxCount];
        private readonly Transform[] _delivered = new Transform[BoxCount];
        private Vector3 _drop;
        private int _boxCount;
        private int _boxesDone;
        private int _carrying = -1;
        private string _pickPrompt, _dropPrompt;

        private void StartBoxes(float now)
        {
            _boxCount = 0;
            _boxesDone = 0;
            _carrying = -1;
            bool carry = ClipLibrary.Has(CarryClip);
            bool there = _to.Valid && SocialPlaces.Standing(_to.Front, out _drop);
            if (carry && there && CitySpots.Along(_friend.Position, out Vector3 along))
            {
                for (int side = 0; side < 2 && _boxCount < BoxCount; side++)
                {
                    for (int i = 0; i < BoxCount + 2 && _boxCount < BoxCount; i++)
                    {
                        Vector3 at = _friend.Position + along * ((side == 0 ? 1f : -1f) * (0.85f + 0.55f * i));
                        if (!CitySpots.OnPavement(at, out Vector3 spot) || !CitySpots.ClearQuietly(spot, 0.28f) || JobSpots.Flat(spot, D.PlayerPosition) < 0.6f)
                        {
                            continue;
                        }
                        Transform box = D.Props.Stand(BoxProp, spot, Random.Range(-12f, 12f));
                        if (box == null)
                        {
                            break;
                        }
                        _boxes[_boxCount] = box;
                        _boxAt[_boxCount] = spot;
                        _boxCount++;
                    }
                }
            }
            if (_boxCount == 0)
            {
                // Nothing to carry with, or no room for a box: talked over instead, for half.
                Say(_who, "reallife_social_moving_nothing");
                Wrap(now, false);
                return;
            }
            Mark(_drop, _to.Name);
            ShowBoxes();
            Go(Step.Boxes, now, 480f);
        }

        private void ShowBoxes()
        {
            Dictionary<string, string> data = SocialLife.Data(_who, "to", _to.Name);
            data["count"] = _boxesDone.ToString();
            data["goal"] = _boxCount.ToString();
            Line = SocialLife.T("reallife_social_obj_moving", data);
        }

        private void Boxes(float now)
        {
            Vector3 player = D.PlayerPosition;
            if (now >= _stepEnd)
            {
                PutBack();
                Wrap(now, _boxesDone > 0);
                return;
            }
            if (_carrying >= 0)
            {
                if (!D.OnFoot || D.Body.OverName != CarryClip)
                {
                    PutBack();   // into a car with a box in the arms, or the clip was lost
                    Prompt = null;
                    return;
                }
                if (JobSpots.Flat(player, _drop) < 2.4f)
                {
                    Offer(_dropPrompt, "reallife_social_button_drop", null);
                }
                else
                {
                    Prompt = null;
                }
                return;
            }
            if (!D.OnFoot)
            {
                Prompt = null;
                return;
            }
            if (NearestBox(player) >= 0)
            {
                Offer(_pickPrompt, "reallife_social_button_pick", null);
            }
            else
            {
                Prompt = null;
            }
        }

        private int NearestBox(Vector3 player)
        {
            int best = -1;
            float bestGap = 1.8f;
            for (int i = 0; i < _boxCount; i++)
            {
                if (_boxes[i] == null)
                {
                    continue;
                }
                float gap = JobSpots.Flat(player, _boxAt[i]);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = i;
                }
            }
            return best;
        }

        private void BoxKey(float now)
        {
            Vector3 player = D.PlayerPosition;
            if (_carrying < 0)
            {
                int box = NearestBox(player);
                if (box < 0)
                {
                    return;
                }
                // The box on the ground goes in the instant one like it is in the player's arms.
                D.Props.Return(_boxes[box]);
                _boxes[box] = null;
                if (!D.Body.Over(CarryClip, BodyMask.Arms, 0.25f, BoxProp))
                {
                    _boxes[box] = D.Props.Stand(BoxProp, _boxAt[box], 0f);
                    return;
                }
                _carrying = box;
                Prompt = null;
                if (_boxesDone == 0 && _friend != null && !_friend.Lost && SocialPlaces.Standing(_drop + (_drop - _spot).normalized * 1.6f, out Vector3 beside))
                {
                    _friend.WalkTo(beside);   // they go ahead to the new door and wait there
                }
                return;
            }
            if (JobSpots.Flat(player, _drop) >= 2.4f)
            {
                return;
            }
            D.Body.StopOver();
            Vector3 at = _drop;
            if (CitySpots.Along(_drop, out Vector3 along))
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 want = _drop + along * ((i % 2 == 0 ? 1f : -1f) * (0.5f + 0.5f * (i / 2)));
                    if (CitySpots.OnPavement(want, out Vector3 spot) && CitySpots.ClearQuietly(spot, 0.25f) && JobSpots.Flat(spot, player) > 0.5f)
                    {
                        at = spot;
                        break;
                    }
                }
            }
            _delivered[_boxesDone] = D.Props.Stand(BoxProp, at, Random.Range(-10f, 10f));
            _carrying = -1;
            _boxesDone++;
            Prompt = null;
            ShowBoxes();
            if (_boxesDone >= _boxCount)
            {
                Unmark();
                Wrap(now, true);
                return;
            }
            Say(_who, SocialLines.Pick(SocialCatalogue.MovingTalk));
        }

        // The box in the player's arms back on the ground at the old door.
        private void PutBack()
        {
            if (_carrying < 0)
            {
                return;
            }
            D.Body.StopOver();
            _boxes[_carrying] = D.Props.Stand(BoxProp, _boxAt[_carrying], 0f);
            _carrying = -1;
        }

        private void ReturnBoxes()
        {
            for (int i = 0; i < BoxCount; i++)
            {
                D.Props.Return(_boxes[i]);
                D.Props.Return(_delivered[i]);
                _boxes[i] = null;
                _delivered[i] = null;
            }
            _carrying = -1;
        }
    }
}
