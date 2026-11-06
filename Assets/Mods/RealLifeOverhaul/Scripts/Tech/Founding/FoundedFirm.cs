using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A co-founder of the player's startup. They hold shares that vest (four years, a one year cliff) and work at the firm on its payroll with their own skills. Game-free.</summary>
    public sealed class CoFounder
    {
        public int No;                      // the number their line of the cap table carries (Holder.Who), from 1
        public string Name = string.Empty;
        public bool Female;
        public int Age;
        public int Origin;                  // CofounderOrigin: a person of the city's population, or a professional of the hiring pool
        public int Person;                  // the population's person, or the hiring pool's seeker; 0 none
        public int Role, Level;             // the work they do (TalentCatalogue)
        public string Profile = string.Empty;   // their file on the payroll ("pro:n")
        public int Day;                     // the day their shares began to vest
        public int Left;                    // the day they left; 0 while they are here
        public long Shares;                 // what they were given
        public long Kept;                   // what they kept on leaving
    }

    /// <summary>A neighbouring niche a firm opened: which, and the day it opened on.</summary>
    public sealed class Expansion
    {
        public int Niche, Day;
    }

    /// <summary>One person offered as a co-founder: from the population's records, or the hiring pool. Game-free.</summary>
    public struct CofounderOffer
    {
        public int Origin, Person;          // CofounderOrigin, and the record's number
        public string Name;
        public bool Female;
        public int Age;
        public int Role, Level;
        public int Main, A, B;              // skills in thousandths of a point
        public int Years;                   // days of experience
    }

    /// <summary>Where a co-founder comes from.</summary>
    public static class CofounderOrigin
    {
        public const int Population = 0, Pool = 1;
    }

    /// <summary>
    /// A startup of the player's founding: the problem and niche it chose, its articles and where its first money came from, its co-founders, and where it stands in going to
    /// market. One for each firm. Game-free.
    /// </summary>
    public sealed class FoundedFirm
    {
        public string Place = string.Empty;
        public int Day;                     // founded, or the day an older save's firm was placed
        public bool Placed;                 // an older save's running firm, put at the stage it already was
        public bool Engaged;                // a placed firm whose player has started to use it: its niche, fit and sessions count from then
        public int Sector;                  // the problem it solves (Venture)
        public int Niche = -1;              // NicheCatalogue; -1 for none
        public int Articles;                // FoundCatalogue.ArticlesStandard or Friendly
        public int Source;                  // FoundCatalogue.SourceSavings and so on
        public long Raised;                 // cents at the start's prices the first source put in
        public long Fee;                    // cents at the start's prices the incorporation cost
        public int Stage;                   // FoundCatalogue.StageDiscovery and so on
        public int Rounds;                  // sessions held at this stage
        public int Learned;                 // thousandths: what the customers said (discovery)
        public int Fit;                     // thousandths: product market fit
        public int PmfDay;                  // the day fit passed its bar; 0 not yet
        public int Channel;                 // FoundCatalogue.ChannelNone and so on
        public int Partner = -1;            // the listed company that resells it (HiringCatalogue.RivalFirms); -1 none
        public int Price = -1;              // the price tier tested; -1 none yet
        public int Wom;                     // thousandths of awareness users' word of mouth added by a session, easing off
        public int WomDay;
        public int Users;                   // the paying customers it had at the last look
        public int WonMonth, LostMonth;     // customers won and lost, easing over a month (thousandths of a customer)
        public int MentorUntil;             // an accelerator's mentoring runs to this day; 0 none
        public int DemoDay;                 // the day of the accelerator's demo day; 0 none
        public bool DemoHeld;
        // read from the sector each day, never saved
        public int AppealNow, SoundNow, ChurnNow;
        public long BuyersNow;
        public readonly List<CoFounder> Cofounders = new List<CoFounder>(2);
        public readonly List<Expansion> Expanded = new List<Expansion>(2);

        /// <summary>Whether the niche, fit and stages count for this firm: a founded firm always, a placed one once its player takes them up.</summary>
        public bool Counts => !Placed || Engaged;
    }

    /// <summary>Everything founding keeps in a save: the firms' records, and the people of the population already taken as co-founders. Game-free.</summary>
    public sealed class FoundedFile
    {
        public int NextNo = 1;
        public readonly List<FoundedFirm> Items = new List<FoundedFirm>(4);
        public readonly List<int> Taken = new List<int>(4);     // population persons already co-founders

        public FoundedFirm Of(string place)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Place == place)
                {
                    return Items[i];
                }
            }
            return null;
        }

        public CoFounder Cofounder(string place, int no)
        {
            FoundedFirm f = Of(place);
            for (int i = 0; f != null && i < f.Cofounders.Count; i++)
            {
                if (f.Cofounders[i].No == no)
                {
                    return f.Cofounders[i];
                }
            }
            return null;
        }
    }
}
