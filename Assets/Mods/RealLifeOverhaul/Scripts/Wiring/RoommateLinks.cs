using System;
using System.Collections.Generic;
using RealLifeOverhaul.Planner;
using RealLifeOverhaul.Roommates;
using FamilyApi = RealLifeOverhaul.Family.FamilyApi;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The roommate with the features it leans on. The doors that are filled in:
        ///
        ///   RoommateBridges.PartnerLivesIn / PartnerName   family life: whether the player's partner lives in, and what they are called
        ///   RoommateBridges.PartnerBody                    where the partner stands in the home just now: the roommate keeps off that place
        ///   RoommateBridges.Friends / IsPartner            social life: the friends who could be asked to move in, and whether one is the partner
        ///   RoommateBridges.FriendMoved                    a roommate who is one of social life's people: what happens at home moves that person's figures
        ///   RoommateBridges.MakeFriend                     a roommate the player gets on with becomes one of social life's people: the cast's neighbour,
        ///                                                  while the player has not met them, takes the roommate's name, look and job and is met
        ///   RoommateBridges.Introduce                      they bring somebody the player has not met along, as social life's own friends do
        ///   the Planner                                    a source of its own: the next rent day, rent or a loan that is owed, the day they leave,
        ///                                                  the day a notice comes down
        ///
        /// Every adapter answers from what the other feature already keeps and is called on an event (a day, a button);
        /// none has a timer. A fault is said once (<see cref="Fault"/>) and the default answer is given.
        /// </summary>
        public static class RoommateLinks
        {
            private sealed class RoommateSource : IPlannerSource
            {
                public string Id => "roommate";

                public bool Available => RoommateSettings.Enabled && RoommateDirector.Instance != null && RoommateDirector.Instance.Running;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    IReadOnlyList<RoommateAlert> alerts = RoommateDirector.Instance.Alerts;
                    for (int i = 0; i < alerts.Count; i++)
                    {
                        RoommateAlert alert = alerts[i];
                        PlannerEntry e = into.Add(Id, alert.A, 0, alert.Money ? PlannerCategory.Money : PlannerCategory.Social, alert.Day);
                        if (e != null && into.WantText)
                        {
                            e.Title = alert.Title;
                            e.What = alert.What;
                            e.Money = alert.Sum;
                            e.IfMissed = alert.IfMissed;
                        }
                    }
                }
            }

            private static readonly RoommateSource Source = new RoommateSource();

            private static bool PeopleOn => SocialState.Loaded && Settings.Social;

            internal static void Connect()
            {
                RoommateBridges.PartnerLivesIn = PartnerLivesIn;
                RoommateBridges.PartnerName = PartnerName;
                RoommateBridges.PartnerBody = PartnerBody;
                RoommateBridges.Friends = Friends;
                RoommateBridges.FriendMoved = FriendMoved;
                RoommateBridges.MakeFriend = MakeFriend;
                RoommateBridges.Introduce = Introduce;
                RoommateBridges.IsPartner = IsPartner;
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                RoommateBridges.Reset();
            }

            private static bool PartnerLivesIn()
            {
                try
                {
                    return FamilyApi.LivesTogether;
                }
                catch (Exception e)
                {
                    Fault("RoommateLinks.PartnerLivesIn", e);
                    return false;
                }
            }

            private static string PartnerName()
            {
                try
                {
                    return FamilyApi.PartnerName ?? string.Empty;
                }
                catch (Exception e)
                {
                    Fault("RoommateLinks.PartnerName", e);
                    return string.Empty;
                }
            }

            private static UnityEngine.Transform PartnerBody()
            {
                try
                {
                    CityPerson partner = Family.HomeLife.Person;
                    return partner != null && partner.Valid ? partner.Character.transform : null;
                }
                catch (Exception e)
                {
                    Fault("RoommateLinks.PartnerBody", e);
                    return null;
                }
            }

            private static int Friends(List<SocialPerson> into)
            {
                int added = 0;
                try
                {
                    for (int i = 0; PeopleOn && i < SocialRules.CastSize; i++)
                    {
                        SocialPerson p = SocialState.Get(i);
                        if (RoommateRules.FriendWould(p))
                        {
                            into.Add(p);
                            added++;
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("RoommateLinks.Friends", e);
                }
                return added;
            }

            private static SocialPerson Friend(int index)
            {
                try
                {
                    return PeopleOn ? SocialState.Get(index) : null;
                }
                catch (Exception e)
                {
                    Fault("RoommateLinks.Friend", e);
                    return null;
                }
            }

            private static bool IsPartner(int index)
            {
                SocialPerson p = Friend(index);
                return p != null && p.Met && p.Together;
            }

            private static void FriendMoved(int index, float affection, float trust)
            {
                try
                {
                    SocialPerson p = Friend(index);
                    if (p == null || !p.Met)
                    {
                        return;
                    }
                    if (affection >= 0f && trust >= 0f)
                    {
                        SocialLife.Contacted(p);
                    }
                    SocialLife.Change(p, 0f, affection, trust);
                }
                catch (Exception e)
                {
                    Fault("RoommateLinks.FriendMoved", e);
                }
            }

            // The cast has one person for each place somebody can be met, and the neighbour is the one a roommate can be:
            // while the player has not met them, they are nobody yet, and take the roommate's name, look and job.
            private static int MakeFriend(Flatmate mate)
            {
                try
                {
                    if (!PeopleOn || mate == null || mate.Person == null || SocialDirector.Instance == null || SocialDirector.Busy)
                    {
                        return -1;
                    }
                    SocialPerson p = SocialState.ByOrigin(SocialOrigin.Neighbour);
                    if (p == null || p.Met)
                    {
                        return -1;
                    }
                    SocialPerson m = mate.Person;
                    p.Name = m.Name;
                    p.Female = m.Female;
                    p.Age = m.Age;
                    p.Seed = m.Seed;
                    p.Job = m.Job;
                    p.FreeFrom = m.FreeFrom;
                    p.FreeTo = m.FreeTo;
                    SocialTrait traits = SocialTrait.None;
                    if (RoommateRules.Has(mate.Traits, RoomTrait.Reliable)) traits |= SocialTrait.Reliable;
                    if (RoommateRules.Has(mate.Traits, RoomTrait.LateWithRent)) traits |= SocialTrait.Flaky;
                    if (RoommateRules.Has(mate.Traits, RoomTrait.Generous)) traits |= SocialTrait.Generous;
                    if (RoommateRules.Has(mate.Traits, RoomTrait.Tight)) traits |= SocialTrait.Careful;
                    if (RoommateRules.Has(mate.Traits, RoomTrait.NightOwl)) traits |= SocialTrait.NightOwl;
                    if (RoommateRules.Has(mate.Traits, RoomTrait.EarlyBird)) traits |= SocialTrait.Punctual;
                    if (RoommateRules.Has(mate.Traits, RoomTrait.Cooks)) traits |= SocialTrait.Foodie;
                    if (RoommateRules.Has(mate.Traits, RoomTrait.Private)) traits |= SocialTrait.Homebody;
                    if (traits != SocialTrait.None)
                    {
                        p.Traits = traits;
                    }
                    SocialLife.Meet(p, true);
                    p.Familiarity = Math.Max(p.Familiarity, m.Familiarity);
                    p.Affection = Math.Max(p.Affection, m.Affection);
                    p.Trust = Math.Max(p.Trust, m.Trust);
                    p.Shared = Math.Max(p.Shared, SocialRules.FriendShared);   // they have lived together: that is things done together
                    SocialLife.Restage(p);
                    return p.Index;
                }
                catch (Exception e)
                {
                    Fault("RoommateLinks.MakeFriend", e);
                    return -1;
                }
            }

            private static string Introduce(int index)
            {
                try
                {
                    SocialPerson p = Friend(index);
                    SocialPerson stranger = p != null && p.Met ? SocialDirector.NextStranger() : null;
                    if (stranger == null || SocialDirector.Instance == null || !SocialDirector.Instance.QueueIntroduction(p, stranger))
                    {
                        return null;
                    }
                    return stranger.Name;
                }
                catch (Exception e)
                {
                    Fault("RoommateLinks.Introduce", e);
                    return null;
                }
            }
        }
    }
}
