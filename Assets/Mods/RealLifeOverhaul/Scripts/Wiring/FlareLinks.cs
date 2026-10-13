using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Law;
using RealLifeOverhaul.Leisure;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Flare, the city's social media app, with the rest of the mod (Social/Flare/FlareBridges). Flare calls no
        /// other feature and none calls it: what they have to do with each other is here, one line each.
        ///
        ///   People        the social life's people the player has met, as Flare reads them: their stage, what they
        ///                 like (their traits), how close they are, their job and part of town (Social/SocialState)
        ///   PublicStanding the law's public reputation, which the account's own is pulled towards (Standing.Public)
        ///   Civic         the election on now: has the player voted, endorsed, and for whom (CivicDirector's state)
        ///   Career        the player's employer and rank (Jobs/CareerState): a rank up is a promotion to post about
        ///   drafts        a picture saved (PhotoMode.Saved), an outing gone to its end (SocialLinks.OutingDone), a good
        ///                 deed that counted (Reputation.Deed) and an arrest (News.Reported, PlayerArrested) become
        ///                 drafts in Flare (FlareDirector.Happened)
        ///
        /// Everything is read from what those features already keep in public or heard through an event they already
        /// raise; nothing of theirs is changed. COST: nothing per frame. A draft is one call when the thing happens;
        /// the people are read when Flare plays its day, makes a post or the app opens (a walk over the cast of eight).
        /// </summary>
        public static class FlareLinks
        {
            internal static void Connect()
            {
                FlareBridges.People = People;
                FlareBridges.PublicStanding = () => LawState.Loaded ? Standing.Public : FlareRules.ReputationStart;
                FlareBridges.Civic = Civic;
                FlareBridges.Career = Career;
                PhotoMode.Saved += PictureSaved;
                SocialLinks.OutingDone = OutingDone;
                Reputation.Deed += Deed;
                News.Reported += Reported;
            }

            internal static void Disconnect()
            {
                FlareBridges.Reset();
                PhotoMode.Saved -= PictureSaved;
                SocialLinks.OutingDone = null;
                Reputation.Deed -= Deed;
                News.Reported -= Reported;
            }

            // The people of the social life the player has met, as Flare sees them.
            private static void People(List<FlarePerson> into)
            {
                if (!Settings.Social || !SocialState.Loaded)
                {
                    return;
                }
                for (int i = 0; i < SocialRules.CastSize; i++)
                {
                    SocialPerson p = SocialState.Get(i);
                    if (p == null || !p.Met)
                    {
                        continue;
                    }
                    into.Add(new FlarePerson
                    {
                        Index = p.Index, First = SocialLife.First(p), Stage = (int)p.Stage, Taste = Taste(p), Closeness = p.Closeness, Partner = p.Together,
                        Job = SocialCatalogue.Jobs[Math.Max(0, Math.Min(p.Job, SocialCatalogue.Jobs.Length - 1))].GetLocalization(), District = SocialPlaces.District(p.District)
                    });
                }
            }

            // The social life's traits as what Flare's friends care about.
            private static FlareTaste Taste(SocialPerson p)
            {
                FlareTaste t = FlareTaste.None;
                t |= p.Has(SocialTrait.Supportive) ? FlareTaste.Cheers : FlareTaste.None;
                t |= p.Has(SocialTrait.Envious) ? FlareTaste.Cool : FlareTaste.None;
                t |= p.Has(SocialTrait.Homebody) ? FlareTaste.Shops : FlareTaste.None;
                t |= p.Has(SocialTrait.Sporty) ? FlareTaste.Sport : FlareTaste.None;
                t |= p.Has(SocialTrait.Foodie) ? FlareTaste.Food : FlareTaste.None;
                t |= p.Has(SocialTrait.NightOwl) ? FlareTaste.Night : FlareTaste.None;
                t |= p.Has(SocialTrait.Flaky) ? FlareTaste.Flaky : FlareTaste.None;
                return t;
            }

            private static int Civic(out string voted, out string endorsed)
            {
                voted = endorsed = null;
                try
                {
                    CivicGovernment gov = CivicDirector.Instance != null && Settings.Civic ? CivicDirector.Instance.Government : null;
                    GovState s = gov != null ? gov.State : null;
                    if (s == null || s.Phase == ElectionPhase.None)
                    {
                        return 0;
                    }
                    int mask = 0;
                    if (s.Voted >= 0 && s.Voted < s.CandidateCount)
                    {
                        mask |= 1;
                        voted = CivicText.Name(s.Candidates[s.Voted]);
                    }
                    if (s.Endorsed >= 0 && s.Endorsed < s.CandidateCount)
                    {
                        mask |= 2;
                        endorsed = CivicText.Name(s.Candidates[s.Endorsed]);
                    }
                    return mask;
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.Civic", e);
                    return 0;
                }
            }

            private static int Career(out string employer)
            {
                employer = null;
                if (!CareerState.Loaded || !CareerState.Employed)
                {
                    return -1;
                }
                employer = CareerState.Employer.NameKey.GetLocalization();
                return CareerState.EmployerIndex * 100 + CareerState.Rank;
            }

            // A picture saved in photo mode: a draft, with the friend in it and the business it was taken in.
            private static void PictureSaved(string path, bool selfie, int person)
            {
                try
                {
                    FlareDirector flare = FlareDirector.Instance;
                    if (flare == null || !flare.Running)
                    {
                        return;
                    }
                    BuildingRegistration inside = FlareGame.Inside();
                    flare.Happened(selfie ? FlareSource.Selfie : FlareSource.Photo, person, inside != null ? FlareGame.Place(inside) : null, System.IO.Path.GetFileName(path), 0);
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.PictureSaved", e);
                }
            }

            // An outing gone to its end: a draft with the friend (the two favours played on the street are not outings to post).
            private static void OutingDone(SocialPerson person, PlanKind kind)
            {
                if (person != null && kind != PlanKind.Lift && kind != PlanKind.Moving)
                {
                    FlareDirector.Instance?.Happened(FlareSource.Outing, person.Index, null, null, (int)kind);
                }
            }

            private static void Deed()
            {
                FlareDirector.Instance?.Happened(FlareSource.GoodDeed, -1, null, null, 0);
            }

            private static void Reported(NewsKind kind, string detail, int amount)
            {
                if (kind == NewsKind.PlayerArrested)
                {
                    FlareDirector.Instance?.Happened(FlareSource.Arrested, -1, null, null, 0);
                }
            }
        }
    }
}
