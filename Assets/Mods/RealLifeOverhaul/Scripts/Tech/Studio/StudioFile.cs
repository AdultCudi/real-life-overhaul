using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A concept a studio has drafted on its concept screen and not yet pitched at the desk: one a studio. The pitch (the
    /// concept desk session) turns it into a game.
    /// </summary>
    public sealed class GameDraft
    {
        public string Place = string.Empty;   // the studio
        public int Genre, Setting;            // Genre; the setting's slot among the genre's four (StudioCatalogue.GenreSettings)
        public int Platform;                  // Platform: PC, console or mobile (ports add more later)
        public int Budget = 2;                // the step of StudioCatalogue.Budgets
        public int Price = 2;                 // the step of StudioCatalogue.PriceSteps
        public int Model;                     // GameModel
        public int Engine;                    // GameEngine
        public int Sequel;                    // the product id of the studio's game it follows; 0 for a new name
        public string Name = string.Empty;    // empty: one is made from the save's seed
    }

    /// <summary>
    /// One game of the player's studio, by its product's id (the firms' list keeps its name, place, stage, copies sold and
    /// takings, as for every product): the concept, the plan, the parts of its quality as built, bugs, hype, the review,
    /// sales, piracy and its life after launch. Game-free.
    /// </summary>
    public sealed class GameDev
    {
        public int Id;                        // Product.Id
        public string Place = string.Empty;   // the studio that makes it
        // the concept
        public int Genre, Setting, Platforms; // Platforms: one bit per Platform it is sold on
        public int Model, Engine, Scope;      // GameModel, GameEngine, Scope
        public long Budget;                   // cents at the start's prices, as planned at the greenlight
        public long Price;                    // cents: a copy, a subscription's month, or 0 for a free game
        public bool Drm, Published;           // copy protection; a publisher's advance taken
        public int Sequel;                    // the id of the game it follows; 0 none
        public int Ambition = 1;              // 0 lean, 1 as planned, 2 ambitious
        // the plan and production
        public int Phase;                     // GamePhase
        public long Work, Plan;               // work points the game takes now, and the plan its parts were set by (a cut or a delay moves Work only)
        public long Done;                     // work points built
        public readonly long[] PartDone = new long[(int)Part.Count];    // work points into each part
        public readonly long[] PartCraft = new long[(int)Part.Count];   // ...and the craft they were built with, summed point by point
        public int Hidden, Found;             // bugs nobody has found, and bugs found and not yet fixed
        public long BugCarry;                 // a two-thousandth of a bug carried from one day's work to the next
        public int Fix = StudioCatalogue.FixShareBuild;   // thousandths of the points that fix found bugs (a playtest's choice)
        public int Started;                   // the day of the greenlight
        public int Milestone = -1;            // the phase whose milestone review waits (0 or 1), -1 none...
        public int MilestoneDay;              // ...since this day
        public int CrunchUntil, BoostUntil;   // days a crunch and contractors last until (not counted)
        public int CertUntil;                 // a console game's certification holds it back until this day
        public long Spent;                    // cents at the start's prices: the team's pay while it was made, licences, marketing, contractors
        public long Pledged;                  // cents its crowdfunding raised from the studio's followers (Flare)
        // hype and marketing
        public int Hype;                      // thousandths of the audience excited
        public int LastNews;                  // the day of the last news (hype fades after a quiet spell)
        public int Beats;                     // bits: 1 announced, 2 previews held, 4 an expo this year
        public int LastTrailer = -1000, LastExpo = -1000;
        public int Campaign;                  // the step of StudioCatalogue.CampaignShare
        public long CampaignSpent;            // cents of the campaign spent so far
        public int ReleaseDay;                // the day it is to launch; 0: as soon as it is ready
        // the launch and the review
        public int Launched = -1;             // the day it went on sale; -1 not yet
        public int Score = -1;                // the critics' score, 0 to 100; -1 before the reviews
        public int Quality;                   // the quality it shipped with, 0 to 100
        public int Reach;                     // thousandths of its audience that know of it, set at launch
        public bool Early;                    // on sale in early access, before its full release
        // sales
        public long Sold, Pirated;            // copies, or players of a free game, or subscribers ever
        public long Gross, Net;               // cents at the start's prices: what buyers paid, and what the studio kept
        public long Subscribers;              // a subscription's paying players now
        public long Active;                   // players still playing (for servers and support)
        public long Carry;                    // millionths of a copy carried from one day's sales to the next
        public int WindowStart = -1;          // the day the newest launch window opened (the launch, a port, an update)...
        public long WindowUnits;              // ...and the copies it sells over its days
        public int SaleUntil;                 // the day a sale ends (not counted)
        public int Lift = 1000;               // thousandths of its life's sales its free updates and localisation have brought it to
        public long NetMonth;                 // cents of the net kept since the leads were last paid
        public bool Converted;                // a game of an older save: it sells on from where it stood, not from the audience
        public long Left;                     // a converted game's copies still to come
        // after launch
        public int Reported;                  // bugs players have reported and no patch has fixed
        public int Asks;                      // bits: the fan requests open (FanAsk)
        public int Project;                   // what the team builds after launch: 0 nothing, 1 a free update, 2 an add-on, 3 a port, 4 localisation
        public int ProjectArg;                // ...the request (FanAsk) or the platform (Platform)
        public long ProjectDone, ProjectWork; // ...work points built and needed
        public int Updates, AddOns;           // free updates and add-ons released
        public long AddOnSold;                // add-on copies sold
        public long AddOnWindow;              // ...and those its window still sells
        public int AddOnStart = -1;           // ...since this day
        public bool Localised;
        public bool DrmRemoved;
        public long Advance, Royalties;       // a publisher's advance, and the royalties the studio has earned against it
        public long Fans;                     // buyers who follow a sequel (set at launch for its own; its sequel reads them)
        public bool Burnt;                    // a weak sequel burnt the name: its fans halved
        public bool Award;                    // won the year's award
        public long RoyaltyGross;             // cents of gross a licensed engine's free first million has used
        // bonuses
        public int Bonus;                     // the step of StudioCatalogue.BonusPool chosen
        public int Leads;                     // the step of StudioCatalogue.LeadRoyalty chosen
        public bool BonusDone;                // the bonus day has passed (paid, or none)
        public long LeadsPaid;                // cents at the start's prices paid to the leads
        public int LastLeadsPay;              // the day the leads were last paid

        public GameModel M => (GameModel)Model;
        public bool Live => Phase == (int)GamePhase.Live;
        public bool Building => Phase <= (int)GamePhase.Polish;               // an early access game builds on while it sells
        public bool OnSale => Launched >= 0 && Phase != (int)GamePhase.Ended;
        public bool On(Platform p) => (Platforms & (1 << (int)p)) != 0;
    }

    /// <summary>A game of a rival studio: released by the same rules and selling to the same audience, for the market and the paper.</summary>
    public sealed class RivalGame
    {
        public int Id;
        public int Studio;                    // its studio, an index of StudioCatalogue.RivalNames
        public string Name = string.Empty;
        public int Genre, Platforms, Scope;
        public int Score;
        public int Launched;
        public long Sold;
        public long Units;                    // copies it sells over its life, set at its release
        public bool Award;
    }

    /// <summary>A professional's experience of each genre, person-days, kept by their file's id; the studio's craft grows with it.</summary>
    public sealed class GenreExperience
    {
        public string Id = string.Empty;
        public readonly int[] Days = new int[(int)Genre.Count];
    }

    /// <summary>A studio's own engine: the work built and whether it is done. One a studio that has started one.</summary>
    public sealed class StudioEngine
    {
        public string Place = string.Empty;
        public long Done;
        public bool Ready;
        public bool Building;                 // the studio is putting points into it
    }

    /// <summary>
    /// Everything the studio part keeps in a save: its seed, the games, the drafts, the people's genre experience, the
    /// studios' engines, and the rival studios' market (on a Going Public guest, the host's, applied from the shared city).
    /// </summary>
    public sealed class StudioFile
    {
        public int Seed, FirstDay, LastDay = -1;
        public bool Converted;                // the older save's games have been taken into this model
        public int NextRival = 1;
        public int TrendGenre = -1, TrendUntil;   // the genre a hit made hot, and until when
        public int AwardYear = -1;            // the calendar year whose award was last given
        public readonly int[] RivalNext = new int[StudioCatalogue.RivalNames.Length];   // the day each rival studio releases its next game
        public readonly List<GameDev> Games = new List<GameDev>(16);
        public readonly List<GameDraft> Drafts = new List<GameDraft>(4);
        public readonly List<GenreExperience> People = new List<GenreExperience>(32);
        public readonly List<StudioEngine> Engines = new List<StudioEngine>(2);
        public readonly List<RivalGame> Rivals = new List<RivalGame>(StudioCatalogue.RivalGamesKept);
        public readonly Dictionary<string, int> SnubUntil = new Dictionary<string, int>(2);   // a studio whose hit paid no bonus, and the day rivals stop pulling at its people

        public GameDev GameOf(int id)
        {
            for (int i = 0; i < Games.Count; i++)
            {
                if (Games[i].Id == id)
                {
                    return Games[i];
                }
            }
            return null;
        }

        public GameDraft DraftAt(string place)
        {
            for (int i = 0; i < Drafts.Count; i++)
            {
                if (Drafts[i].Place == place)
                {
                    return Drafts[i];
                }
            }
            return null;
        }

        public StudioEngine EngineAt(string place)
        {
            for (int i = 0; i < Engines.Count; i++)
            {
                if (Engines[i].Place == place)
                {
                    return Engines[i];
                }
            }
            return null;
        }

        public GenreExperience ExperienceOf(string id)
        {
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Id == id)
                {
                    return People[i];
                }
            }
            return null;
        }
    }

    /// <summary>What one person brings to a studio's day: their points, their craft (0 to 100 a part) and their experience of the game's genre.</summary>
    public struct StudioHand
    {
        public string Id;
        public int Family;                    // Family
        public bool Tester;
        public int Points;                    // the day's work points (output, hours and the week's meeting already counted)
        public int Skill;                     // 0 to 100: their main skill and specialisms together
        public int Level;                     // TalentCatalogue's level
        public int Hours;                     // hours worked yesterday
    }

    /// <summary>What a studio's day came to for the director to book and tell: money in the start's prices, and what happened.</summary>
    public struct StudioDay
    {
        public long Takings, Costs, Marketing, Servers, Outsourcing, Royalties, Advance;   // cents at the start's prices
        public long Units;                    // copies sold today across the studio's games
        public int Milestones, Ready, Launched, Reviews, Released;   // milestones reached, games finished, launched, reviewed, post-launch projects released
    }
}
