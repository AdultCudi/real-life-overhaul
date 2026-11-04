using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the kit face's new presses ask of the game: each is the director's own call, answered with what it did.</summary>
    public interface IFlareKitHost
    {
        /// <summary>The player ends a campaign they hired; false if it is not running.</summary>
        bool EndHire(int id);

        /// <summary>The player buys an ad for a business: its target, size and days by their numbers; false with the reason's key when it cannot be run.</summary>
        bool BuyAd(string place, int target, int size, int days, out string why);

        /// <summary>The player's profile written: the words about them and the banner picture's key (null for the newest picture).</summary>
        bool SaveProfile(string bio, string banner);

        /// <summary>The notifications are marked seen up to now.</summary>
        void MarkNoticesSeen();
    }

    /// <summary>
    /// The state and the presses of the kit face's working pages that the audit added: the notifications' tab and Mark all read, the hire
    /// page's niche and order, a campaign ended (pressed once to ask, again to end), the ad sheet's choices and the profile sheet's. Game-free:
    /// the screen keeps one and routes the face's commands to it; EconomyCheck presses every one of them against a recording host. Cost: nothing
    /// per frame; a press is a few assignments and one call to the host.
    /// </summary>
    public sealed class FlareKitPress
    {
        public const int NoticeTabs = 5;

        private readonly IFlareKitHost _host;

        public int NoticeTab;                        // the notifications' tab (FlareNoticeTab's number)
        public int Niche;                            // Hire: 0 every niche, then one each
        public int Sort;                             // Hire: 0 by followers, 1 by fit to the business
        public int EndArmed = -1;                    // the campaign pressed once to end, asking to be pressed again

        // The ad sheet.
        public int AdTargets = 5, AdSizes = 3, AdDays = 3;   // how many of each the city offers (set by the screen from the catalogue)
        public string AdPlace;
        public int AdShop, AdTarget, AdSize, AdDay;
        public string AdWhy;

        // The profile sheet.
        public string Bio, Banner;

        public FlareKitPress(IFlareKitHost host)
        {
            _host = host;
        }

        /// <summary>The face's commands this part answers; false for any other, and for an argument outside what the page drew.</summary>
        public bool Press(int command, int arg, int niches)
        {
            switch (command)
            {
                case FlareFace.MarkAllCommand: return MarkAll();
                case FlareFace.SortCommand: return SetSort(arg);
                case FlareFace.NicheCommand: return SetNiche(arg, niches);
                default: return false;
            }
        }

        /// <summary>A notifications tab chosen.</summary>
        public bool SetNoticeTab(int tab)
        {
            if (tab < 0 || tab >= NoticeTabs)
            {
                return false;
            }
            NoticeTab = tab;
            return true;
        }

        /// <summary>Mark all read: the notifications seen up to now.</summary>
        public bool MarkAll()
        {
            _host.MarkNoticesSeen();
            return true;
        }

        public bool SetSort(int sort)
        {
            if (sort < 0 || sort > 1)
            {
                return false;
            }
            Sort = sort;
            return true;
        }

        public bool SetNiche(int niche, int niches)
        {
            if (niche < 0 || niche > niches)
            {
                return false;
            }
            Niche = niche;
            return true;
        }

        /// <summary>
        /// End a campaign: the first press asks (the button says so), the second ends it. A press on another campaign asks about that one instead.
        /// Returns whether anything changed.
        /// </summary>
        public bool End(int id)
        {
            if (id < 0)
            {
                return false;
            }
            if (EndArmed != id)
            {
                EndArmed = id;
                return true;
            }
            EndArmed = -1;
            return _host.EndHire(id);
        }

        /// <summary>The ad sheet opens for a business of the player's, with the first choices.</summary>
        public void BeginAd(string place, int shop)
        {
            AdPlace = place;
            AdShop = shop;
            AdTarget = 0;
            AdSize = 1;
            AdDay = 0;
            AdWhy = null;
        }

        /// <summary>A chip of the ad sheet: group 0 the target, 1 the size, 2 the days.</summary>
        public bool AdChip(int group, int chip)
        {
            AdWhy = null;
            switch (group)
            {
                case 0:
                    if (chip < 0 || chip >= AdTargets) { return false; }
                    AdTarget = chip;
                    return true;
                case 1:
                    if (chip < 0 || chip >= AdSizes) { return false; }
                    AdSize = chip;
                    return true;
                case 2:
                    if (chip < 0 || chip >= AdDays) { return false; }
                    AdDay = chip;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The ad is bought. True when the sheet should close (it ran); false leaves it open with the reason in <see cref="AdWhy"/>.</summary>
        public bool AdConfirm()
        {
            if (string.IsNullOrEmpty(AdPlace))
            {
                return false;
            }
            if (_host.BuyAd(AdPlace, AdTarget, AdSize, AdDay, out string why))
            {
                AdWhy = null;
                return true;
            }
            AdWhy = why ?? "reallife_flare_ad_why_off";
            return false;
        }

        /// <summary>The profile sheet opens on what is written now.</summary>
        public void BeginEdit(string bio, string banner)
        {
            Bio = bio ?? string.Empty;
            Banner = banner;
        }

        /// <summary>The profile is saved. True when the sheet should close.</summary>
        public bool EditConfirm()
        {
            return _host.SaveProfile(Bio, Banner);
        }
    }
}
