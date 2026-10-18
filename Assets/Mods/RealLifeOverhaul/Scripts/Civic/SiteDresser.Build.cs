using System;
using System.Collections.Generic;
using System.Globalization;
using Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>What a site shows in each phase, its closed lane and where its crew stands.</summary>
    public sealed partial class SiteDresser
    {
        private Transform Put(Dressed dressed, string id, Vector3 at, Vector3 forward)
        {
            Transform prop = _props.Stand(id, at, forward);
            if (prop != null)
            {
                dressed.Props.Add(prop);
            }
            return prop;
        }

        // On the carriageway: at the height of the road there, and (unless it lies flat) not where a vehicle stands now.
        private Transform PutOnRoad(Dressed dressed, string id, Vector3 at, Vector3 forward, bool flat = false)
        {
            if (CitySpots.Ground(at, out float ground))
            {
                at.y = ground;
            }
            if (!flat && Physics.CheckSphere(at + Vector3.up * 0.5f, 0.55f, TrafficTools.VehicleMask, QueryTriggerInteraction.Ignore))
            {
                return null;
            }
            return Put(dressed, id, at, forward);
        }

        // The lane beside works in the road: shut while they stand in it (stages 1 to 4), open again for the last.
        private bool Lane(Dressed dressed, float half)
        {
            int phase = dressed.Phase;
            if (phase < 4 && !dressed.LaneShut && dressed.LaneFromKerb < NeedsClosure + 1.5f && dressed.Road.Lane(dressed.Origin, 14f))
            {
                dressed.LaneShut = dressed.Road.Close(dressed.Road.LaneCentre, half + 2.5f);
            }
            else if (phase >= 4 && dressed.LaneShut)
            {
                dressed.Road.Open();
                dressed.LaneShut = false;
            }
            return dressed.LaneShut;
        }

        // What closes a lane to the eye: a barrier across each end, a taper of cones upstream, the sign on the pavement before it.
        private void LaneProps(Dressed dressed, float half)
        {
            Vector3 lane = dressed.Along, outward = dressed.Out;
            Vector3 laneCentre = dressed.Kerb + outward * dressed.LaneFromKerb;
            Vector3 upstream = laneCentre - lane * (half + 1.6f), downstream = laneCentre + lane * (half + 1.6f);
            if (dressed.Unsafe)
            {
                // The barrier at the upstream end has been knocked flat and left, and the taper was never laid.
                if (CitySpots.Ground(upstream, out float ground))
                {
                    upstream.y = ground;
                }
                Transform fallen = Put(dressed, "barrier", upstream + Vector3.up * 0.06f, lane);
                if (fallen != null)
                {
                    fallen.rotation = Quaternion.LookRotation(lane) * Quaternion.Euler(84f, 0f, 0f);
                }
            }
            else
            {
                PutOnRoad(dressed, "barrier", upstream, lane);
                for (int cone = 0; cone < 4; cone++)
                {
                    Vector3 kerbSide = dressed.Kerb + outward * 0.5f, laneSide = laneCentre + outward * 1.4f;
                    PutOnRoad(dressed, "cone", Vector3.Lerp(kerbSide, laneSide, cone / 3f) - lane * (half + 8.5f - cone * 2f), lane);
                }
            }
            PutOnRoad(dressed, "barrier", downstream, lane);
            if (CitySpots.Stand(dressed.Edge - outward * 0.5f - lane * (half + 13f), 0.45f, out Vector3 sign))
            {
                Put(dressed, "road_sign_works", sign, -lane);   // facing the traffic that comes
            }
        }

        private void BuildRoad(Dressed dressed)
        {
            int phase = dressed.Phase;
            Vector3 lane = dressed.Along, outward = dressed.Out, trench = dressed.Origin;
            bool shut = Lane(dressed, HalfTrench);
            if (!shut && phase < 4 && dressed.LaneFromKerb < NeedsClosure)
            {
                return;   // nothing may stand beside a live lane this near: Dress turns the place down
            }

            for (int piece = 0; piece < 4; piece++)
            {
                Vector3 at = trench + lane * (piece * 2f - 3f);
                switch (phase)
                {
                    case 0:
                        PutOnRoad(dressed, "road_marking", at + outward * 0.5f, lane, true);
                        PutOnRoad(dressed, "road_marking", at - outward * 0.45f, lane, true);
                        break;
                    case 1:
                        PutOnRoad(dressed, piece < 2 ? "trench_cover" : "road_marking", piece < 2 ? at : at + outward * 0.5f, lane, true);
                        break;
                    case 2:
                        PutOnRoad(dressed, "trench_cover", at, lane, true);
                        break;
                    default:
                        PutOnRoad(dressed, "asphalt_patch", at, lane, true);
                        break;
                }
            }
            if (phase == 1 || phase == 2)
            {
                PutOnRoad(dressed, "spoil_heap", trench + lane * (HalfTrench + 0.8f), lane);
                if (phase == 2)
                {
                    PutOnRoad(dressed, "spoil_heap", trench - lane * (HalfTrench + 0.8f), -lane);
                }
            }
            if (phase < 4)
            {
                // Cones along the outer edge of the works; with a site left unsafe, most of them are missing.
                for (int cone = 0; cone < 5; cone++)
                {
                    if (dressed.Unsafe && cone != 2)
                    {
                        continue;
                    }
                    PutOnRoad(dressed, "cone", trench + outward * 0.85f + lane * (cone * 2.2f - 4.4f), lane);
                }
                if (shut)
                {
                    LaneProps(dressed, HalfTrench);
                }
                if (CitySpots.Stand(dressed.Edge - outward * 0.8f + lane * 2.4f, 0.3f, out Vector3 box))
                {
                    Put(dressed, "toolbox", box, lane);
                }
            }

            // The crew stands on the pavement's edge and works on the strip in front of it.
            Vector3 edge = dressed.Edge - outward * 0.08f;
            Vector3 near = edge - lane * 1.7f, far = edge + lane * 0.9f, end = edge - lane * (HalfTrench + 3f);
            switch (phase)
            {
                case 0:
                    Plan(dressed, near, near + outward, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 1:
                    Plan(dressed, far, far + outward, CityLook.RoadWorker, "jackhammer", "jackhammer_idle");
                    Plan(dressed, near, near + outward, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    break;
                case 2:
                    Plan(dressed, near, near + outward, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    Plan(dressed, far, far + outward, CityLook.RoadWorker, "shovel_rest", "shovel_dig");
                    break;
                case 3:
                    Plan(dressed, near, near + outward, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    break;
                default:
                    Plan(dressed, near, near + outward, CityLook.RoadWorker, "clipboard_write", null);
                    break;
            }
            if (shut && phase < 4)
            {
                Plan(dressed, end, end - lane, CityLook.RoadWorker, "paddle_hold", null);   // facing the traffic that comes
            }
        }

        private void BuildUtility(Dressed dressed)
        {
            int phase = dressed.Phase;
            Vector3 lane = dressed.Along, outward = dressed.Out;
            bool shut = Lane(dressed, 1.2f);
            if (!shut && phase < 4 && dressed.LaneFromKerb < NeedsClosure)
            {
                return;
            }
            Vector3 hole = dressed.Kerb + outward * 0.8f;
            if (CitySpots.Ground(hole, out float ground))
            {
                hole.y = ground;
            }
            bool open = phase >= 1 && phase <= 3;
            bool tent = phase == 1 || phase == 2;
            Put(dressed, open ? "manhole_open" : "manhole_lid", hole, lane);
            if (open)
            {
                PutOnRoad(dressed, "manhole_lid", hole + lane * 1.25f, lane, true);
            }
            if (tent)
            {
                PutOnRoad(dressed, "works_tent", hole, -outward);   // open towards the pavement, where its people are
            }
            // Cones at the corners; a site left unsafe has an open cover and one cone.
            for (int corner = 0; corner < 4 && phase < 4; corner++)
            {
                if (dressed.Unsafe && corner > 0)
                {
                    continue;
                }
                PutOnRoad(dressed, "cone", hole + lane * (corner % 2 == 0 ? -1.15f : 1.15f) + outward * (corner < 2 ? -0.55f : 0.95f), lane);
            }
            if (shut && phase < 4)
            {
                LaneProps(dressed, 1.2f);
            }
            if (phase == 2 && CitySpots.Stand(dressed.Edge - outward * 0.7f + lane * 1.6f, 0.3f, out Vector3 box))
            {
                Put(dressed, "toolbox", box, lane);
            }
            Vector3 kneel = dressed.Edge - outward * 0.08f, stand = dressed.Edge - outward * 0.5f - lane * 1.3f;
            if (open)
            {
                Plan(dressed, kneel, kneel + outward, CityLook.RoadWorker, "kneel_work", phase == 3 ? "arms_crossed" : null);
            }
            if (phase != 3)
            {
                Plan(dressed, stand, hole, CityLook.RoadWorker, "clipboard_write", null);
            }
        }

        private void BuildScaffold(Dressed dressed)
        {
            int phase = dressed.Phase;
            Vector3 foot = dressed.Kerb, outward = dressed.Out, along = dressed.Along;
            int bays = dressed.BayMax - dressed.BayMin + 1;
            for (int bay = dressed.BayMin; bay <= dressed.BayMax; bay++)
            {
                int order = bay - dressed.BayMin;
                bool partial = (phase == 0 || phase == 4) && order >= (bays + 1) / 2;   // going up, or coming down: half of it has only its first lift
                Vector3 at = foot + along * (bay * BayWidth) + outward * WallGap;
                Put(dressed, "scaffold_bay", at, outward);
                int lifts = partial ? 0 : phase == 0 || phase == 4 ? Mathf.Min(1, dressed.Lifts) : dressed.Lifts;
                for (int lift = 0; lift < lifts; lift++)
                {
                    Vector3 up = at + Vector3.up * (DeckHeight + lift * LiftHeight);
                    Put(dressed, "scaffold_bay_upper", up, outward);
                    if (phase >= 1 && phase <= 3 && !(dressed.Unsafe && order == 0))
                    {
                        Put(dressed, "scaffold_net", up, outward);   // an unsafe site has a bay with its netting off
                    }
                }
                if (phase >= 1 && phase <= 3 && !dressed.Unsafe)
                {
                    Put(dressed, "scaffold_fan", at, outward);       // and no fan over the walkway
                }
            }
            if (dressed.HasSkip && phase >= 1)
            {
                PutOnRoad(dressed, "skip_bin", dressed.SkipAt, Vector3.Cross(dressed.SkipAlong, Vector3.up));   // its long side along the kerb
            }

            // People: on the first deck (a painter, a worker) and on the pavement at the end of the run.
            Vector3 deck = foot + outward * 0.92f;
            Vector3 beside = foot + along * (dressed.BayMin * BayWidth - BayWidth * 0.5f - 1.2f) + outward * 1.5f;
            if (dressed.HasNotice && CivicCity.Flat(dressed.NoticeAt - beside) < 1.6f * 1.6f)
            {
                beside = foot + along * (dressed.BayMax * BayWidth + BayWidth * 0.5f + 1.2f) + outward * 1.5f;
            }
            switch (phase)
            {
                case 0:
                case 4:
                    Plan(dressed, beside, beside - outward, CityLook.RoadWorker, "kneel_work", "arms_crossed");
                    Plan(dressed, beside + along * 1.3f + outward * 0.4f, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 1:
                    Plan(dressed, beside, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 2:
                    Plan(dressed, deck, deck - outward, CityLook.RoadWorker, "kneel_work", "arms_crossed", DeckHeight + 0.02f);
                    Plan(dressed, beside, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                default:
                    Plan(dressed, deck, deck - outward, CityLook.Painter, "paint_roll", null, DeckHeight - 0.24f);   // the clip stands 0.24 up, on a ladder's tread
                    Plan(dressed, beside, foot, CityLook.RoadWorker, "arms_crossed", null);
                    break;
            }
        }

        private void BuildRefit(Dressed dressed)
        {
            int phase = dressed.Phase;
            Vector3 foot = dressed.Kerb, outward = dressed.Out, along = dressed.Along;
            int bays = dressed.BayMax - dressed.BayMin + 1;
            for (int bay = dressed.BayMin; bay <= dressed.BayMax; bay++)
            {
                int order = bay - dressed.BayMin;
                bool notice = bay == 0 && phase >= 2;
                if ((phase == 0 && order >= (bays + 1) / 2) || (phase == 4 && !notice && bays > 1) || (dressed.Unsafe && order == bays - 1 && bays > 1))
                {
                    continue;   // not up yet; coming down; or the panel a careless contractor left out
                }
                Put(dressed, notice ? "site_hoarding_notice" : "site_hoarding", foot + along * (bay * BayWidth) + outward * WallGap, outward);
            }
            if (dressed.HasSkip && phase >= 1 && phase <= 2)
            {
                PutOnRoad(dressed, "skip_bin", dressed.SkipAt, Vector3.Cross(dressed.SkipAlong, Vector3.up));
            }
            Vector3 front = foot + outward * 1.6f;
            int paintBay = dressed.BayMax > 0 ? dressed.BayMax : dressed.BayMin < 0 ? dressed.BayMin : 0;
            Vector3 painter = foot + along * (paintBay * BayWidth) + outward * (WallGap + 0.02f + 0.80f);
            switch (phase)
            {
                case 2:
                    Plan(dressed, foot + along * (dressed.BayMin * BayWidth) + outward * 0.8f, foot + along * (dressed.BayMin * BayWidth), CityLook.RoadWorker, "kneel_work", "arms_crossed");
                    break;
                case 3:
                    // The painter on the first tread of a ladder that stands between them and the hoarding
                    // (the ladder is climbed from its -Z side, so its +Z looks at the hoarding as the painter does).
                    if (CitySpots.Stand(painter, 0.45f, out Vector3 stand) && Put(dressed, "step_ladder", stand - outward * 0.24f, -outward) != null)
                    {
                        Put(dressed, "bucket", stand - outward * 0.15f + along * 0.62f, outward);
                        Plan(dressed, stand, stand - outward, CityLook.Painter, "paint_roll", null);
                    }
                    break;
                default:
                    Plan(dressed, front, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
            }
        }
    }
}
