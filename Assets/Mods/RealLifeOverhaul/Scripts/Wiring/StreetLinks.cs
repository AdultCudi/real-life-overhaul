using System;
using Localizor;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Law;
using RealLifeOverhaul.Street;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Street talk (StreetBridges) with how the city sees the player and the day's city facts as Flare reads them, the
        /// police within earshot, a stranger met in the street as one of the social life's cast (their name, look and
        /// traits), an invitation booked as a plan, word of mouth on footfall, and RLO Texts' words (StreetTexts). Nothing
        /// per frame: each answer is asked on a press or a click.
        /// </summary>
        public static class StreetLinks
        {
            private static Func<BuildingRegistration, int> _ambientBefore;
            private static readonly FlareCityFacts City = new FlareCityFacts();

            internal static void Connect()
            {
                StreetBridges.Facts = Facts;
                StreetBridges.PoliceNear = PoliceNear;
                StreetBridges.Noticed = Reputation.Noticed;
                StreetBridges.CanMeet = () => Stranger(-1) != null;
                StreetBridges.Meet = Meet;
                StreetBridges.AgeOf = AgeOf;
                StreetBridges.InviteLook = a => FlareBridges.InviteLook?.Invoke(a);
                StreetBridges.InviteBook = Book;
                StreetBridges.MouthChanged = MouthChanged;
                _ambientBefore = BusinessGame.Ambient;
                BusinessGame.Ambient = Ambient;
                StreetTexts.Connect();
            }

            internal static void Disconnect()
            {
                StreetTexts.Disconnect();
                StreetBridges.Reset();
                BusinessGame.Ambient = _ambientBefore;   // the others' pairings take theirs off next
                _ambientBefore = null;
            }

            private static void Facts(StreetFacts f)
            {
                try
                {
                    f.Standing = LawState.Loaded ? Standing.Public : Reputation.Start;
                    f.Recognition = Settings.Standing ? Standing.Recognition : 0;
                    f.Record = Standing.HasRecord;
                    f.Wanted = Justice.Wanted;
                    f.Unwashed = Settings.HomeNeeds && HomeState.Loaded && HomeState.Hygiene < HomeBalance.HygieneLow;
                    f.SocialOn = Settings.Social && Settings.SocialMeetings && SocialState.Loaded && SocialDirector.Instance != null;
                    if (FlareBridges.CityFacts == null)
                    {
                        return;
                    }
                    City.Candidates.Clear();
                    City.Day = f.Day;
                    City.EventToday = City.NewsDistrict = null;
                    City.Election = false;
                    City.PriceTrend = City.RateMove = 0;
                    FlareBridges.CityFacts(City);
                    f.Election = City.Election;
                    f.Candidates.AddRange(City.Candidates);
                    f.PriceTrend = City.PriceTrend;
                    f.RateMove = City.RateMove;
                    f.EventToday = City.EventToday != null ? City.EventToday.GetLocalization() : null;
                    f.EventWords = City.EventToday != null ? Texts.TextWords.EventWords(City.EventToday) : null;   // RLO Texts' cards are English in every language
                    f.NewsDistrict = City.NewsDistrict != null ? Districts.DistrictGame.Name(City.NewsDistrict) : null;
                }
                catch (Exception e)
                {
                    Fault("StreetLinks.Facts", e);
                }
            }

            private static bool PoliceNear(Vector3 at, float metres)
            {
                try
                {
                    return (PolicePresence.Instance != null && PolicePresence.Instance.CarWithin(at, metres)) || (BeatPatrol.Instance != null && BeatPatrol.Instance.OfficerWithin(at, metres));
                }
                catch (Exception e)
                {
                    Fault("StreetLinks.PoliceNear", e);
                    return false;
                }
            }

            // Who of the cast somebody new would be: one not yet met, not brought by a friend, not a star influencer's, of
            // the sex given (1 a woman, 0 a man, -1 either), else somebody new while there is room; null if nobody is left.
            private static SocialPerson Stranger(int sex)
            {
                if (!Settings.Social || !Settings.SocialMeetings || !SocialState.Loaded || SocialDirector.Instance == null)
                {
                    return null;
                }
                FlareFile file = FlareDirector.Instance != null ? FlareDirector.Instance.File : null;
                SocialPerson either = null;
                for (int i = 0; i < SocialState.Count; i++)
                {
                    SocialPerson p = SocialState.Get(i);
                    if (p == null || p.Met || p.IsFamily || p.Origin == SocialOrigin.OldFriend || SocialDirector.Expected(p) || (file != null && FlareCast.IsOne(file, p.Index)))
                    {
                        continue;
                    }
                    if (sex < 0 || p.Female == (sex == 1))
                    {
                        return p;
                    }
                    either = either ?? p;
                }
                return SocialDirector.Newcomer(SocialOrigin.Street, sex) ?? either;
            }

            // Somebody new met in the street, with their face and clothes.
            private static int Meet(StreetMeet m)
            {
                try
                {
                    SocialPerson p = Stranger(m.Female ? 1 : 0);
                    if (p == null)
                    {
                        return -1;
                    }
                    MeetInStreet(p, m);
                    return p.Index;
                }
                catch (Exception e)
                {
                    Fault("StreetLinks.Meet", e);
                    return -1;
                }
            }

            private static void MeetInStreet(SocialPerson p, StreetMeet m)
            {
                if (!SocialState.NameTaken(m.First + " " + m.Last, p))
                {
                    p.Name = m.First + " " + m.Last;   // one name is one contact: somebody else's stays theirs
                }
                p.Female = m.Female;
                p.Age = Mathf.Clamp(m.Age, 18, 85);
                p.AgeDay = Mathf.Max(1, SocialLife.Day);   // the age they had in the street is true today: a seat rolled long ago adds no birthdays it never had
                p.Job = m.Job;
                p.District = m.District;
                p.Traits = m.Traits;
                p.Origin = SocialOrigin.Street;
                int seed = PersonLooks.Remember(m.Look);   // the same look in the street, on the contact's picture and on Flare
                if (seed != 0)
                {
                    p.Look = m.Look;
                    p.Seed = seed;
                }
                ContactPictures.Person(SocialPhone.IdOf(p), p.Female, p.Age, p.Seed);
                SocialLife.Meet(p, m.Warm);
                SocialDirector.Logger?.Info("Social: " + p.Name + " swapped numbers with the player in the street: met in the street"
                                            + (seed != 0 ? ", with the look they had there." : " (their look could not be read: the seat's own)."));
            }

            // The age People shows for somebody the player knows (SocialTalk.AgeOf), so street talk says the same number.
            private static int AgeOf(int cast)
            {
                try
                {
                    SocialPerson p = SocialState.Loaded ? SocialState.Get(cast) : null;
                    return p != null && p.Met ? SocialTalk.AgeOf(p) : -1;
                }
                catch (Exception e)
                {
                    Fault("StreetLinks.AgeOf", e);
                    return -1;
                }
            }

            // A yes booked as a call books a plan; -1 if it cannot be now (a plan on already, the place gone, nobody left).
            private static int Book(FlareInviteAsk a, StreetMeet m)
            {
                try
                {
                    if (!Settings.Social || !Settings.SocialOutings || !SocialState.Loaded || SocialDirector.Instance == null || SocialState.Plan.Active
                        || !SocialPlaces.Resolve(a.Street, a.Number, out Venue venue))
                    {
                        return -1;
                    }
                    SocialPerson p = a.Cast >= 0 ? SocialState.Get(a.Cast) : null;
                    if (p == null)
                    {
                        return -1;
                    }
                    if (!p.Met)
                    {
                        MeetInStreet(p, m);
                    }
                    if (!SocialLife.MakePlan(p, a.Kind, a.PlanAt, venue, false, out string reason))
                    {
                        SocialDirector.Logger?.Info("Social: the plan agreed in the street with " + p.Name + " could not be made (" + reason + ").");
                        return -1;
                    }
                    return p.Index;
                }
                catch (Exception e)
                {
                    Fault("StreetLinks.Book", e);
                    return -1;
                }
            }

            // The earlier pairings' part with word of mouth on top.
            private static int Ambient(BuildingRegistration reg)
            {
                int before = 0;
                try
                {
                    before = _ambientBefore != null ? _ambientBefore(reg) : 0;
                }
                catch (Exception e)
                {
                    Fault("StreetLinks.Ambient (the pairings before)", e);
                }
                try
                {
                    return before + StreetDirector.FootfallOf(reg);
                }
                catch (Exception e)
                {
                    Fault("StreetLinks.Ambient", e);
                    return before;
                }
            }

            private static void MouthChanged(string address)
            {
                try
                {
                    BuildingRegistration reg = StreetGame.ShopAt(address);
                    if (reg != null)
                    {
                        BusinessDirector.Footfall(reg, true, false, out _, out _);
                    }
                }
                catch (Exception e)
                {
                    Fault("StreetLinks.MouthChanged", e);
                }
            }
        }
    }
}
