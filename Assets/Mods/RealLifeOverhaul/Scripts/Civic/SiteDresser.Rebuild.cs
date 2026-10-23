using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A rebuild site: hoarding along the door's wall, then the game's own scaffolding up every street side to the roof, the
    /// fabric hung, crews on the first deck and on the pavement, an inspection, and the wrap coming down again. Its stages end
    /// with the wrap down; the building's look itself is never touched here.
    /// </summary>
    public sealed partial class SiteDresser
    {
        private const float DeckReach = 9f;       // metres from the door a deck place is looked for, so the crew is seen where the works are

        // The wrap is laid out once, when the site is set out; the hoarding and notice come from the door's wall (ResolveWall).
        private SiteRefusal ResolveWrap(Dressed dressed)
        {
            SiteState site = dressed.Site;
            CityBuildingController building = CivicCity.Find(site.Street, site.Number);
            dressed.Wrap = dressed.Wrap ?? NewWrap();
            string why = SiteKit.Ready ? dressed.Wrap.Prepare(building, CivicWorks.PlaceText(site)) : "the game's scaffold pieces were not found in this city";
            if (why != null)
            {
                dressed.Wrap.Plan.Clear();
                CivicDirector.Logger?.Info("Civic: site " + site.Id + " (Rebuild) at " + CivicWorks.PlaceText(site) + " shows its hoarding without the game's scaffold: " + why + ".");
            }
            dressed.Area = dressed.Wrap.Plan.Pieces.Count > 0 ? dressed.Wrap.Bounds : new Bounds(dressed.Origin, new Vector3(16f, 8f, 16f));
            return SiteRefusal.None;
        }

        private void BuildRebuild(Dressed dressed)
        {
            int phase = dressed.Phase;
            Vector3 foot = dressed.Kerb, outward = dressed.Out, along = dressed.Along;
            int bays = dressed.BayMax - dressed.BayMin + 1;
            for (int bay = dressed.BayMin; bay <= dressed.BayMax; bay++)
            {
                int order = bay - dressed.BayMin;
                bool notice = bay == 0;
                if ((phase == 4 && !notice && order % 2 == 1) || (dressed.Unsafe && order == bays - 1 && bays > 1))
                {
                    continue;   // coming down with the scaffold; or the panel a careless contractor left out
                }
                Put(dressed, notice ? "site_hoarding_notice" : "site_hoarding", foot + along * (bay * BayWidth) + outward * WallGap, outward);
            }
            if (dressed.HasSkip && phase >= 1 && phase <= 3)
            {
                PutOnRoad(dressed, "skip_bin", dressed.SkipAt, Vector3.Cross(dressed.SkipAlong, Vector3.up));
            }

            SiteWrap wrap = dressed.Wrap;
            if (wrap != null && wrap.Plan.Pieces.Count > 0)
            {
                WrapRules.RebuildStage(phase, wrap.Plan.Lifts, out int lifts, out bool fabric);
                wrap.Want(lifts, fabric);
                Framed = true;
            }

            // People: on the first deck while the scaffold stands, on the pavement in front of the hoarding, and the inspector
            // at the notice board in the inspection stage.
            Vector3 front = foot + outward * 1.6f;
            Vector3 left = foot - along * 3f, right = foot + along * 3f;
            bool deck = wrap != null && wrap.Plan.Pieces.Count > 0 && phase >= 1 && phase <= 3;
            switch (phase)
            {
                case 0:
                    Plan(dressed, foot + along * (dressed.BayMin * BayWidth) + outward * 0.8f, foot + along * (dressed.BayMin * BayWidth), CityLook.RoadWorker, "kneel_work", "arms_crossed");
                    Plan(dressed, front, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 1:
                    OnDeck(dressed, deck, left, "kneel_work", "arms_crossed", CityLook.RoadWorker);
                    Plan(dressed, front, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 2:
                    OnDeck(dressed, deck, left, "kneel_work", "arms_crossed", CityLook.RoadWorker);
                    OnDeck(dressed, deck, right, "paint_roll", null, CityLook.Painter);
                    Plan(dressed, front, foot, CityLook.RoadWorker, "arms_crossed", null);
                    break;
                case 3:
                    if (dressed.HasNotice)
                    {
                        Plan(dressed, dressed.Reader, dressed.NoticeAt, CityLook.Civilian, "clipboard_write", null);   // the inspector, at the notice
                    }
                    OnDeck(dressed, deck, right, "kneel_work", null, CityLook.RoadWorker);
                    break;
                default:
                    Plan(dressed, front, foot, CityLook.RoadWorker, "clipboard_write", null);
                    Plan(dressed, front + along * 1.4f, foot + along * 1.4f, CityLook.RoadWorker, "kneel_work", "arms_crossed");
                    break;
            }
        }

        // One of the crew on the scaffold's first deck, standing on the pavement's mesh under it and lifted to the boards.
        private static void OnDeck(Dressed dressed, bool deck, Vector3 near, string work, string rest, CityLook look)
        {
            if (!deck || !dressed.Wrap.DeckSpot(near, DeckReach, out Vector3 spot, out Vector3 face))
            {
                return;
            }
            for (int i = 0; i < dressed.CrewCount; i++)
            {
                if (CivicCity.Flat(dressed.Crew[i].At - spot) < 1.5f * 1.5f)
                {
                    return;   // the nearest module to both ends of the run is one: one person on it
                }
            }
            // paint_roll is made standing on a ladder's first tread, 0.24 m up: on the boards it is let down by as much.
            float lift = SiteWrap.Deck - (work == "paint_roll" ? 0.24f : 0f);
            Plan(dressed, spot, face, look, work, rest, lift);
        }
    }
}
