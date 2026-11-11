using System;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, the City Ledger's society column: a line somebody else of the mod has written whole (a rival seen about town, RivalsSeen through
    /// Wiring.SeenLinks) goes up on the City Ledger's own account, as its morning headlines do, with the same author and source, so nothing of Flare's
    /// records, texts or shared feed is new. Never while this machine follows another player's feed. COST: nothing per frame; a post on a press of the hour.
    /// </summary>
    public sealed partial class FlareDirector
    {
        public const string SocietyTopic = "cityledger society";

        /// <summary>A society line on the City Ledger's account at an hour and minute of today. True if it went up.</summary>
        public bool Society(string words, int hour, int minute)
        {
            try
            {
                if (!Running || Save == null || _file == null || Following || string.IsNullOrEmpty(words))
                {
                    return false;
                }
                int id = _file.NextId++;
                _file.Posts.Add(new FlarePost
                {
                    Id = id, Day = Save.Day, Hour = FlareRules.Clamp(hour, 0, 23), Minute = FlareRules.Clamp(minute, 0, 59), Author = FlareAuthor.Ledger, Source = FlareSource.Headline,
                    Name = words, Topic = SocietyTopic, Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000, Reach = 6000, Likes = 40 + FlareRules.Roll(_file.Seed, id, 5) / 8
                });
                ShareChanged(null);
                FlareRules.Prune(_file);
                Changed();
                return true;
            }
            catch (Exception e)
            {
                Fault("a society line", e);
                return false;
            }
        }
    }
}
