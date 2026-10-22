using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Family;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Permadeath;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Permadeath with the rest of the mod (Permadeath/PermadeathBridges). Permadeath calls no other feature: the life
        /// an obituary tells, the businesses a save runs and the page it is printed on are all read here, from what each
        /// feature already keeps in public, once, at a death or a test (and the businesses once a game day).
        ///
        ///   Story          the people of the social life (friends, close friends, the family they knew), the partner and
        ///                  the children of family life, the police record and the court's convictions (Standing), Flare's
        ///                  followers and the elections voted in (Civic)
        ///   Businesses     the running businesses, by the address the books key them by, and their names (Economy/Books)
        ///   ShowObituary   the City Ledger's page, a front page of its own (Ledger/LedgerSpecial), whether the paper runs or not
        ///   Flu            the test button's flu, through Home's health
        /// </summary>
        public static class PermadeathLinks
        {
            private static readonly List<BuildingRegistration> Owned = new List<BuildingRegistration>(8);

            internal static void Connect()
            {
                PermadeathBridges.Story = Story;
                PermadeathBridges.Businesses = Businesses;
                PermadeathBridges.ShowObituary = ShowObituary;
                PermadeathBridges.Flu = Flu;
            }

            internal static void Disconnect()
            {
                PermadeathBridges.Reset();
            }

            private static void Story(LifeStory s)
            {
                People(s);
                CareersLinks.Obituary(s);
                try
                {
                    if (FamilyState.Loaded && FamilyState.Together && !string.IsNullOrEmpty(FamilyState.S.PName))
                    {
                        s.Partner = LifeStory.Clean(FamilyState.S.PName);
                        s.Bond = FamilyState.Bond == Bond.Married ? 3 : FamilyState.Bond == Bond.Engaged ? 2 : 1;
                    }
                    if (FamilyState.Loaded)
                    {
                        for (int i = 0; i < FamilyState.S.Children.Count; i++)
                        {
                            ChildRecord child = FamilyState.S.Children[i];
                            if (child != null)
                            {
                                LifeStory.Keep(s.Children, child.Name);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("PermadeathLinks.Family", e);
                }
                try
                {
                    int arrests = 0;
                    for (int i = 0; i < Law.Record.Count; i++)
                    {
                        Law.RecordEntry entry = Law.Record.Entry(i);
                        arrests += entry.Hours > 0 || entry.Outcome == Law.Outcome.Held ? 1 : 0;
                    }
                    s.Arrests = arrests;
                    s.Convictions = Standing.Convictions;
                }
                catch (Exception e)
                {
                    Fault("PermadeathLinks.Record", e);
                }
                try
                {
                    Flare.FlareFile flare = Flare.FlareDirector.Instance != null ? Flare.FlareDirector.Instance.File : null;
                    s.Followers = flare != null ? Math.Max(0, flare.Followers) : 0;
                    CivicGovernment government = CivicDirector.Instance != null ? CivicDirector.Instance.Government : null;
                    s.Elections = government != null ? Math.Max(0, government.State.VotesCast) : 0;
                }
                catch (Exception e)
                {
                    Fault("PermadeathLinks.City", e);
                }
            }

            // The people of the social life the player has met: the family they knew, a partner (when family life has none),
            // their friends and, by name, their close friends.
            private static void People(LifeStory s)
            {
                try
                {
                    if (!SocialState.Loaded)
                    {
                        return;
                    }
                    int friends = 0;
                    for (int i = 0; i < SocialState.Count; i++)
                    {
                        SocialPerson p = SocialState.People[i];
                        if (p == null || !p.Met || string.IsNullOrEmpty(p.Name))
                        {
                            continue;
                        }
                        if (p.IsFamily)
                        {
                            LifeStory.Keep(s.Family, p.Name);
                        }
                        else if (p.Together)
                        {
                            if (s.Partner.Length == 0)
                            {
                                s.Partner = LifeStory.Clean(p.Name);
                                s.Bond = 1;
                            }
                        }
                        else if (p.Stage >= SocialStage.Friend)
                        {
                            friends++;
                            if (p.Stage == SocialStage.CloseFriend)
                            {
                                LifeStory.Keep(s.Close, p.Name);
                            }
                        }
                    }
                    s.Friends = friends;
                }
                catch (Exception e)
                {
                    Fault("PermadeathLinks.People", e);
                }
            }

            private static void Businesses(List<string> keys, List<string> names)
            {
                try
                {
                    Books.Businesses(Owned);
                    for (int i = 0; i < Owned.Count; i++)
                    {
                        keys.Add(Books.Key(Owned[i].Address));
                        names.Add(Owned[i].GetDisplayName());
                    }
                }
                catch (Exception e)
                {
                    Fault("PermadeathLinks.Businesses", e);
                }
                Owned.Clear();
            }

            private static bool ShowObituary(ObituaryPage page, Action closed)
            {
                try
                {
                    LedgerDirector paper = LedgerDirector.Instance;
                    if (paper == null || page == null)
                    {
                        return false;
                    }
                    var special = new LedgerSpecial
                    {
                        Day = page.Day,
                        Header = page.Header,
                        EarLeftLabel = page.EarLeftLabel,
                        EarLeft = page.EarLeft,
                        EarRightLabel = page.EarRightLabel,
                        EarRight = page.EarRight,
                        DateMiddle = page.DateMiddle,
                        DateRight = page.DateRight,
                        Kicker = page.Kicker,
                        Headline = page.Headline,
                        Stand = page.Stand,
                        Byline = page.Byline,
                        Body = page.Body,
                        NoticeHead = page.NoticeHead,
                        Notice = page.Notice
                    };
                    for (int i = 0; i < page.Labels.Count && i < page.Texts.Count; i++)
                    {
                        special.Section(page.Labels[i]).AddRange(page.Texts[i]);
                    }
                    for (int i = 0; i < page.BoxLabels.Count && i < page.BoxLines.Count; i++)
                    {
                        special.Box(page.BoxLabels[i]).AddRange(page.BoxLines[i]);
                    }
                    return paper.ShowSpecial(special, closed);
                }
                catch (Exception e)
                {
                    Fault("PermadeathLinks.ShowObituary", e);
                    return false;
                }
            }

            private static bool Flu(float hoursAgo)
            {
                try
                {
                    if (!HomeState.Loaded || !HomeHealth.Catch(Illness.Flu, "reallife_health_cause_going_round", true))
                    {
                        return false;
                    }
                    HomeState.IllSince = TimeHelper.NowInMinutes() - hoursAgo * 60f;
                    HomeState.IllTreated = false;
                    return HomeState.Ill == Illness.Flu;
                }
                catch (Exception e)
                {
                    Fault("PermadeathLinks.Flu", e);
                    return false;
                }
            }
        }
    }
}
