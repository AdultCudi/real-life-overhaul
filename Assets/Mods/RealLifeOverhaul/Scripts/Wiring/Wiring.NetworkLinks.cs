using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.Networking;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The networking life with the social life, the careers and the investors:
        ///
        ///   people          who the cast's people are, the save's seed, a text from them (under the social life's limits)
        ///                   and a first line in their thread (Network/NetworkBridges)
        ///   meetings        somebody met (SocialLinks.Met), who brought them, and what is said of their work while they are
        ///                   met (SocialLinks.Position); the day's round (SocialLinks.DayDone)
        ///   calls           the favour's button and answer on a call, the People page's lines (SocialPhone.Network*)
        ///   favours         a job lead (CareersDirector.Lead), freelance work (CareersDirector.Referred), an investor
        ///                   meeting (VenturesDirector.Introduced)
        ///
        /// Each is called on a meeting, a call or the day's round; a fault is said once (<see cref="Fault"/>) and the default
        /// answer is used: nothing said, no favour.
        /// </summary>
        public static class NetworkLinks
        {
            internal static void Connect()
            {
                NetworkBridges.Person = PersonOf;
                NetworkBridges.CastSeed = () => SocialState.CastSeed;
                NetworkBridges.Text = Text;
                NetworkBridges.Thread = Thread;
                NetworkBridges.JobLead = JobLead;
                NetworkBridges.Freelance = Freelance;
                NetworkBridges.Investor = Investor;
                SocialLinks.Met = Met;
                SocialLinks.NetworkForget = NetworkDirector.Forget;
                SocialLinks.DayDone = DayDone;
                SocialLinks.Position = Position;
                SocialPhone.NetworkLabel = Label;
                SocialPhone.NetworkAsk = Ask;
                SocialPhone.NetworkLine = Line;
                NetworkDirector.TestIntroduce = () => SocialDirector.Instance != null ? SocialDirector.Instance.TestIntroduction() : -1;
                NetworkDirector.TestCall = index => SocialPhone.Call(SocialState.Get(index));
            }

            internal static void Disconnect()
            {
                SocialLinks.Met = null;
                SocialLinks.NetworkForget = null;
                SocialLinks.DayDone = null;
                SocialLinks.Position = null;
                SocialPhone.NetworkLabel = null;
                SocialPhone.NetworkAsk = null;
                SocialPhone.NetworkLine = null;
                NetworkDirector.TestIntroduce = null;
                NetworkDirector.TestCall = null;
                NetworkBridges.Reset();
                NetworkDirector.Reset();
            }

            private static NetPerson Info(SocialPerson p)
            {
                return new NetPerson { Index = p.Index, Seed = p.Seed, Name = p.Name, Origin = (int)p.Origin, Stage = (int)p.Stage, MetDay = p.MetDay, Met = p.Met };
            }

            private static NetVia ViaOf(SocialPerson by)
            {
                return by == null ? NetVia.Met : by.IsFamily ? NetVia.Family : NetVia.Friend;
            }

            private static bool PersonOf(int index, out NetPerson person)
            {
                person = default;
                try
                {
                    SocialPerson p = SocialState.Loaded ? SocialState.Get(index) : null;
                    if (p != null)
                    {
                        person = Info(p);
                    }
                    return p != null;
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.PersonOf", e);
                    return false;
                }
            }

            private static bool Text(int index, string key, Dictionary<string, string> data)
            {
                try
                {
                    SocialPerson p = SocialState.Get(index);
                    return p != null && p.Met && Settings.SocialContact && SocialLife.SendText(p, key, data);
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.Text", e);
                    return false;
                }
            }

            private static void Thread(int index, string key, Dictionary<string, string> data)
            {
                try
                {
                    Contact contact = SocialPhone.ContactOf(SocialState.Get(index));
                    contact?.SendMessage(new TextMessage(key, data), notify: false);
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.Thread", e);
                }
            }

            private static NetResult JobLead(string from, int salt)
            {
                try
                {
                    CareersDirector careers = CareersDirector.Instance;
                    string why = "reallife_social_net_why_no_lead";
                    Opening o = careers != null ? careers.Lead(from, salt, out why) : null;
                    return o != null ? NetResult.Yes(CareersText.Title(o.Prof, o.Rank), CareersText.Employer(o.Employer)) : NetResult.No(why);
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.JobLead", e);
                    return NetResult.No("reallife_social_net_why_no_lead");
                }
            }

            private static NetResult Freelance(int salt)
            {
                try
                {
                    CareersDirector careers = CareersDirector.Instance;
                    string why = "reallife_social_net_why_no_contract";
                    Contract c = careers != null ? careers.Referred(salt, out why) : null;
                    return c != null ? NetResult.Yes(c.Client, CareersText.Money(Prices.At((int)c.Fee))) : NetResult.No(why);
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.Freelance", e);
                    return NetResult.No("reallife_social_net_why_no_contract");
                }
            }

            private static NetResult Investor(int salt, string by)
            {
                try
                {
                    VenturesDirector ventures = VenturesDirector.Instance;
                    string why = "reallife_social_net_why_no_investor";
                    Economy.Ventures.Investor i = ventures != null ? ventures.Introduced(salt, by, out why) : null;
                    return i != null ? NetResult.Yes(i.Name) : NetResult.No(why);
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.Investor", e);
                    return NetResult.No("reallife_social_net_why_no_investor");
                }
            }

            private static void Met(SocialPerson person, SocialPerson by)
            {
                try
                {
                    if (person != null)
                    {
                        NetworkDirector.Met(Info(person), ViaOf(by), false);
                    }
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.Met", e);
                }
            }

            private static void DayDone()
            {
                try
                {
                    NetworkDirector.Daily(SocialState.Count);
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.DayDone", e);
                }
            }

            // What the friend says of them ("Dana is a recruiter at ..."), or what they say of themselves.
            private static string Position(SocialPerson person, SocialPerson by, bool byFriend)
            {
                try
                {
                    if (person == null)
                    {
                        return null;
                    }
                    NetRole role = NetworkDirector.Peek(Info(person), ViaOf(by), out string org);
                    if (role == NetRole.None)
                    {
                        return null;
                    }
                    var data = new Dictionary<string, string> { { "org", org ?? string.Empty }, { "name", SocialLife.First(person) } };
                    return SocialLife.T(byFriend ? NetworkCatalogue.IntroKey(role) : NetworkCatalogue.MeetKey(role), data);
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.Position", e);
                    return null;
                }
            }

            private static string Label(SocialPerson p)
            {
                try
                {
                    NetFavour favour = p != null && p.Met ? NetworkDirector.Ready(p.Index) : NetFavour.None;
                    return favour != NetFavour.None ? NetworkCatalogue.ButtonKey(favour) : null;
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.Label", e);
                    return null;
                }
            }

            // The favour done now, or why not, said by them as the call closes.
            private static DialogEntry Ask(SocialPerson p)
            {
                string key = "reallife_social_net_why_not_now";
                Dictionary<string, string> data = SocialLife.Data(p);
                try
                {
                    NetFavour favour = NetworkDirector.Ready(p.Index);
                    NetResult r = NetworkDirector.Ask(p.Index);
                    key = NetworkDirector.AnswerKey(favour, r);
                    data = NetworkDirector.Data(Info(p), NetworkDirector.Of(p.Index));
                    data["a"] = r.A ?? string.Empty;
                    data["b"] = r.B ?? string.Empty;
                    SocialLife.Contacted(p);
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.Ask", e);
                }
                SocialLife.Said(p, key, data);
                return PhoneContacts.Closing(key, data);
            }

            private static string Line(SocialPerson p)
            {
                try
                {
                    return p != null ? NetworkDirector.PeopleLine(p.Index) : null;
                }
                catch (Exception e)
                {
                    Fault("NetworkLinks.Line", e);
                    return null;
                }
            }
        }
    }
}
