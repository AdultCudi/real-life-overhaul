using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>Where an application stands: the saved number of <see cref="Applicant.Stage"/>.</summary>
    public enum ApplyStage
    {
        New = 0, Shortlisted = 1, Interviewed = 2, Offered = 3, Countered = 4, Accepted = 5, Started = 6, Declined = 7, Rejected = 8, Withdrawn = 9, Lapsed = 10
    }

    /// <summary>One professional looking for work: who they are is a <see cref="Profile"/>, what they want and claim is here.</summary>
    public sealed class Seeker
    {
        public int Id;
        public Profile P = new Profile();
        public string District = string.Empty;
        public bool Employed;        // in work now: asks more and gives notice
        public string Employer = string.Empty;
        public int Ask;              // cents an hour
        public int Want;             // Arrangement
        public int Until;            // the day they find work elsewhere if nobody hires them
        public int Fit;              // how well they would settle, thousandths (found out at an interview)
        public int Claim;            // points the CV claims over the truth
        public int Refs;             // what their references say, 1 to 5 (found out by asking)
        public int Notice;           // days of notice to their employer
        public bool Taken;           // hired: by the player or a rival
        public bool Passive;         // found by a search firm: not on the open market

        /// <summary>The main skill the CV claims, whole points.</summary>
        public int CvMain => System.Math.Min(100, P.Main / 1000 + Claim);
    }

    /// <summary>An ad of the player's on the jobs board, or a search firm's brief (Search). Game-free.</summary>
    public sealed class JobAd
    {
        public int Id;
        public string Place = string.Empty;   // the business the job is at
        public int Role, Level;
        public int PayLeast, PayMost;          // cents an hour
        public int MinSkill;                   // the main skill asked for, whole points
        public int Work;                       // Arrangement offered
        public int From, To;                   // the days it runs
        public bool Boost;
        public int Came;                       // applicants so far
        public bool Search;                    // a search firm's brief, not an ad
        public int Fee;                        // a search's fee, cents
        public int FeePaid;                    // ...of which paid
        public int Due;                        // the day a search delivers
        public int Crew;                       // a data centre's crew search: the technicians briefed for (0 for a senior search)
        public int Hired;                      // ...of whom started so far, each paying a share of the fee

        public bool CrewSearch => Search && Crew > 0;
        public bool Open(int day) => day >= From && day < To;
    }

    /// <summary>A rival's ad: who, for which family and level, until when. Game-free.</summary>
    public sealed class RivalAd
    {
        public string Rival = string.Empty;
        public int Family, Level, To;
        public int PayBp;   // what they offer over the going rate
    }

    /// <summary>One application to one of the player's ads. Game-free.</summary>
    public sealed class Applicant
    {
        public int Id, Seeker, Ad;
        public int Day;                 // the day they applied
        public int Stage;               // ApplyStage
        public bool Refs, Background, Interviewed;
        public int Interest = 500;      // how keen they are on the job, thousandths
        public int Offer, OfferDay;     // cents an hour, and the day it was made
        public int Counter;             // what they came back with, cents an hour
        public int RivalBid;            // another company's offer, cents an hour
        public string RivalBy = string.Empty;
        public int Start;               // the day they start, once accepted
        public int Revealed;            // what the interview found out: 1 the true skill, 2 the experience, 4 the fit, 8 the ask

        public ApplyStage At => (ApplyStage)Stage;

        public bool Live => Stage <= (int)ApplyStage.Accepted;
    }

    /// <summary>A campaign of the game's recruitment office seen by the mod, and the extra candidates it is to bring. Game-free.</summary>
    public sealed class OfficeCampaign
    {
        public string Key = string.Empty;
        public string Place = string.Empty, Agency = string.Empty, Skill = string.Empty;
        public int From, To, Planned, Delivered;
    }

    /// <summary>A file made for a candidate of the game's (by its id) before they are hired: what the mod knows of them from the market or the office.</summary>
    public sealed class CandidateFile
    {
        public string Id = string.Empty;
        public Profile P = new Profile();
        public int Day;
    }

    /// <summary>Everything the hiring part keeps in a save. Game-free.</summary>
    public sealed class HiringFile
    {
        public int Seed, FirstDay, LastDay = -1, NextId = 1;
        public readonly List<Seeker> Seekers = new List<Seeker>(HiringCatalogue.SeekersMost);
        public readonly List<JobAd> Ads = new List<JobAd>(HiringCatalogue.AdsMost);
        public readonly List<RivalAd> RivalAds = new List<RivalAd>(HiringCatalogue.RivalAdsMost);
        public readonly List<Applicant> Applicants = new List<Applicant>(HiringCatalogue.ApplicantsMost);
        public readonly List<OfficeCampaign> Campaigns = new List<OfficeCampaign>(8);
        public readonly List<CandidateFile> Candidates = new List<CandidateFile>(16);

        public Seeker SeekerOf(int id)
        {
            for (int i = 0; i < Seekers.Count; i++)
            {
                if (Seekers[i].Id == id)
                {
                    return Seekers[i];
                }
            }
            return null;
        }

        public JobAd AdOf(int id)
        {
            for (int i = 0; i < Ads.Count; i++)
            {
                if (Ads[i].Id == id)
                {
                    return Ads[i];
                }
            }
            return null;
        }

        public Applicant ApplicantOf(int id)
        {
            for (int i = 0; i < Applicants.Count; i++)
            {
                if (Applicants[i].Id == id)
                {
                    return Applicants[i];
                }
            }
            return null;
        }

        public CandidateFile CandidateOf(string id)
        {
            for (int i = 0; i < Candidates.Count; i++)
            {
                if (Candidates[i].Id == id)
                {
                    return Candidates[i];
                }
            }
            return null;
        }

        public int Next() => NextId++;
    }
}
