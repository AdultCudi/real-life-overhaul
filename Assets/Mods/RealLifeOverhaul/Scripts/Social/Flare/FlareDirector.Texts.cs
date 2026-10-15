using System;
using System.Collections.Generic;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What Flare asks of RLO Texts (Scripts/Texts), wired by Wiring/TextsLinks. Without it Flare words everything as before.</summary>
    public static partial class FlareBridges
    {
        /// <summary>Asks for words (true: they come through the request's Done; false: the written line).</summary>
        public static Func<TextRequest, bool> TextAsk;

        /// <summary>Words kept for a key, or null.</summary>
        public static Func<string, string> TextKept;

        /// <summary>Whether a use may ask now (shared: the session's shared feed, which only the host or a player alone writes).</summary>
        public static Func<TextUse, bool, bool> TextWants;

        /// <summary>The day and time as a card writes them ("Monday 14:05").</summary>
        public static Func<string> TextWhen;

        /// <summary>Real seconds until something so many game minutes away.</summary>
        public static Func<double, double> TextSeconds;
    }

    /// <summary>
    /// Flare's posts worded by RLO Texts: when the game has made a post that goes up later (the city's posts of the day,
    /// the replies under the player's post over the hours, an account's answer, a tycoon's post, a business's), its card is
    /// built and the words asked at once, with its going up as the deadline; the words are kept by the post's number (in
    /// the session's shared feed, by the session's number) and shown in place of the written line from then on. Words that
    /// come back after the post went up are not used (a post never changes its words in front of the player). A friend's
    /// reply under the player's post and the answer to a message of the player's are the player's own (never shared).
    /// In a Going Public session only the host asks for the shared feed's words; a guest shows what the host sent.
    /// COST: on the events that make posts (the day, the hour, a post of the player's, a message), one walk over the posts
    /// not yet up (a few dozen); a request per post the model words; nothing per frame.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private readonly HashSet<int> _textAsked = new HashSet<int>();
        private readonly HashSet<string> _textAskedKeys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The model's words for a post, or null for its written line: the post's own (its number, or the session's on a guest's copy).</summary>
        public string ModelWords(FlarePost p)
        {
            if (p == null || p.Text != null || FlareBridges.TextKept == null)
            {
                return null;
            }
            return FlareBridges.TextKept(p.Shared && p.NetId > 0 ? TextsShare.PostKey(p.NetId) : "fp:" + p.Id);
        }

        /// <summary>A friend's reply under the player's post: the model's words, or null for its written line.</summary>
        public string FriendWords(int post, int person)
        {
            return FlareBridges.TextKept != null ? FlareBridges.TextKept("fr:" + post + ":" + person) : null;
        }

        /// <summary>The answer to a message of the player's: the model's words, or null for its written line.</summary>
        public string DmWords(FlareDm m)
        {
            return m != null && m.Text == null && FlareBridges.TextKept != null ? FlareBridges.TextKept("fd:" + m.With + ":" + m.At) : null;
        }

        // Every post made that goes up later and has an intent: its words asked now, its going up the deadline.
        private void TextsAhead()
        {
            if (FlareBridges.TextAsk == null || _file == null || Save == null)
            {
                return;
            }
            long now = Now;
            bool hostShares = Settings.FlareSharePosts;
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                FlarePost p = _file.Posts[i];
                if (p.At <= now || p.Text != null || p.Shared || _textAsked.Contains(p.Id))
                {
                    continue;
                }
                bool shared = GoingPublic.InSession && FlareNetShare.IsShared(_file, p, hostShares);
                TextRequest r = PostRequest(p, shared);
                if (r == null)
                {
                    continue;
                }
                r.Key = "fp:" + p.Id;
                r.Keep = !shared;
                r.Shared = shared;
                r.Day = p.Day;
                r.Seconds = Seconds(p.At - now);
                FlarePost post = p;
                r.Wanted = () => _file != null && _file.Post(post.Id) == post && Now < post.At;   // (words after it went up are not used)
                r.Done = words => { };   // (kept by the hub under the post's key; drawn from there)
                if (FlareBridges.TextAsk(r))
                {
                    _textAsked.Add(p.Id);   // (asked: not again; one not asked is looked at again on the next event)
                }
            }
        }

        // The friends' replies of a post of the player's, as its reactors say (FlareLiveRules.Reactors): asked when it is posted.
        private void TextsFriends(FlarePost p)
        {
            string kind = FlareTextsMap.PostKind(p.Source);
            if (FlareBridges.TextAsk == null || kind == null || string.IsNullOrEmpty(p.Reactors))
            {
                return;
            }
            FlareLiveRules.ReadReactors(p.Reactors, _reactions);
            long now = Now;
            for (int i = 0; i < _reactions.Count; i++)
            {
                FlareReaction re = _reactions[i];
                if (re.Kind != 'R' || re.Who.Length < 2 || re.Who[0] != 'f' || !int.TryParse(re.Who.Substring(1), out int index) || !Person(index, out FlarePerson person))
                {
                    continue;
                }
                string reaction = FlareTextsMap.Reaction(FlareRules.ReplyOf(person, p), kind);
                if (reaction == null)
                {
                    continue;
                }
                var r = new TextRequest { Use = TextUse.Replies, Intent = "citizen.reply." + kind, Key = "fr:" + p.Id + ":" + index, Speaker = "f:" + index, Keep = true, Day = p.Day };
                r.Who.First = person.First;
                r.Who.Name = person.First;
                r.Who.Handle = FlareText.HandleOf(person.First);
                r.Who.Age = TextWords.Age(_file.Seed * 13 + index, 21, 48);
                r.Who.District = string.IsNullOrEmpty(person.District) ? FlareGame.DistrictName(null) : person.District;
                r.Who.Style = TextWords.CitizenStyle(_file.Seed * 13 + index);
                r.Who.When = When();
                r.Slots.Set("reaction", reaction);
                r.History.Add(new TextSaid(MyName, PostWords(p)));
                r.Seconds = Seconds(p.At + re.Delay - now);
                long at = p.At + re.Delay;
                r.Wanted = () => Now < at;
                r.Done = words => { };
                FlareBridges.TextAsk(r);
            }
        }

        // The answer to a message of the player's (FlareDirector.SendDm): asked when it is scheduled.
        private void TextsDm(FlareDm answer, string typed)
        {
            string intent = answer != null ? FlareTextsMap.Dm(answer.Key) : null;
            if (FlareBridges.TextAsk == null || intent == null || answer.With == null || answer.With.Length < 3)
            {
                return;
            }
            var r = new TextRequest { Use = TextUse.People, Intent = intent, Key = "fd:" + answer.With + ":" + answer.At, Speaker = answer.With, Keep = true, Day = Save.Day };
            int number;
            int.TryParse(answer.With.Substring(2), out number);
            if (answer.With[0] == 'c' && number >= 0 && number < _citizens.Count)
            {
                FlareCitizen c = _citizens[number];
                FlareTextsMap.Fill(r.Who, c, FlareGame.DistrictName(c.District), FlareLiveRules.CitySeed(_file), When());
            }
            else if (answer.With[0] == 'i' && _file.Influencer(number) is FlareInfluencer inf)
            {
                r.Who.Name = r.Who.First = answer.Name ?? inf.Handle;
                r.Who.Handle = inf.Handle;
                r.Who.Age = TextWords.Age(_file.Seed * 17 + number, 21, 40);
                r.Who.District = FlareGame.DistrictName(inf.District);
                r.Who.Style = TextWords.CitizenStyle(_file.Seed * 17 + number);
                r.Who.When = When();
            }
            else
            {
                return;
            }
            r.History.Add(new TextSaid(MyName, typed));
            long at = answer.At;
            r.Seconds = Seconds(at - Now);
            r.Wanted = () => Now < at;
            r.Done = words => { };
            FlareBridges.TextAsk(r);
        }

        // The card of one post: its speaker, its intent and values, the post it answers. Null for a post with no intent.
        private TextRequest PostRequest(FlarePost p, bool shared)
        {
            var r = new TextRequest { Priority = TextPriority.Background };
            int seed = FlareLiveRules.CitySeed(_file);
            switch (p.Source)
            {
                case FlareSource.Reply:
                    if (p.Author != FlareAuthor.Citizen || !Citizen(p.Who, r))
                    {
                        return null;
                    }
                    FlarePost parent = _file.Post(p.ReplyTo);
                    string kind = parent != null && parent.Author == FlareAuthor.Player ? FlareTextsMap.PostKind(parent.Source) : null;
                    string reaction = FlareTextsMap.Reaction((FlareReplyKind)p.Arg, kind);
                    if (reaction == null)
                    {
                        return null;
                    }
                    r.Use = TextUse.Replies;
                    r.Priority = TextPriority.Soon;
                    r.Intent = "citizen.reply." + kind;
                    r.Slots.Set("reaction", reaction);
                    r.History.Add(new TextSaid(MyName, PostWords(parent)));
                    return Use(r, shared);
                case FlareSource.CityTalk:
                    if (p.Author != FlareAuthor.Citizen || !Citizen(p.Who, r))
                    {
                        return null;
                    }
                    r.Use = TextUse.Posts;
                    r.Intent = FlareTextsMap.Talk(p.Arg / 10);
                    r.Slots.Set("district", r.Who.District);
                    return r.Intent != null ? Use(r, shared) : null;
                case FlareSource.Review:
                    if (p.Author != FlareAuthor.Citizen || !Citizen(p.Who, r) || string.IsNullOrEmpty(p.Name))
                    {
                        return null;
                    }
                    r.Use = TextUse.Posts;
                    r.Intent = "citizen.review";
                    r.Slots.Set("shop", p.Name).Set("stars", FlareRules.Clamp(p.Arg, 1, 5).ToString());
                    return Use(r, shared);
                case FlareSource.Official:
                    if (p.Author != FlareAuthor.Official)
                    {
                        return null;
                    }
                    r.Use = TextUse.Official;
                    r.Intent = FlareTextsMap.Official(p.Arg / 10, p.Arg % 10);
                    r.Who.Name = FlareText.OfficialName(p.Who);
                    r.Who.When = When();
                    r.Speaker = "o:" + p.Who;
                    if (!string.IsNullOrEmpty(p.District))
                    {
                        r.Slots.Set("district", FlareGame.DistrictName(p.District));
                    }
                    return r.Intent != null ? Use(r, shared) : null;
                case FlareSource.Answer:
                    return AnswerRequest(p, r, shared);
                case FlareSource.BusinessPost:
                    if (p.Author == FlareAuthor.Tycoon)
                    {
                        r.Use = TextUse.Rivals;
                        r.Intent = FlareTextsMap.Tycoon(p.Arg >= FlareLiveRules.TycoonMoveArg ? p.Arg - FlareLiveRules.TycoonMoveArg : -1);
                        Tycoon(p.Who, p.Name, r);
                        if (!string.IsNullOrEmpty(p.District))
                        {
                            r.Slots.Set("district", FlareGame.DistrictName(p.District));
                        }
                        return r.Intent != null ? Use(r, shared) : null;
                    }
                    if (p.Author != FlareAuthor.Business || string.IsNullOrEmpty(p.Name))
                    {
                        return null;
                    }
                    r.Use = TextUse.Posts;
                    r.Intent = FlareTextsMap.Business(p.Arg);
                    r.Who.Name = p.Name;
                    r.Who.Style = TextWords.BusinessStyle(seed ^ p.Name.GetHashCode());
                    r.Who.When = When();
                    r.Speaker = "b:" + p.Name;
                    return r.Intent != null ? Use(r, shared) : null;
                default:
                    return null;
            }
        }

        private TextRequest AnswerRequest(FlarePost p, TextRequest r, bool shared)
        {
            FlarePost parent = _file.Post(p.ReplyTo);
            if (parent == null)
            {
                return null;
            }
            r.Priority = TextPriority.Soon;
            r.Intent = FlareTextsMap.Answer((FlareAnswer)p.Arg, p.Author);
            if (r.Intent == null)
            {
                return null;
            }
            string author = AuthorName(parent);
            string said = parent.Text ?? FlareText.Built(parent, author) ?? PostWords(parent);
            r.History.Add(new TextSaid(author, said));
            switch (p.Author)
            {
                case FlareAuthor.Official:
                    r.Use = TextUse.Official;
                    r.Who.Name = FlareText.OfficialName(p.Who);
                    r.Who.When = When();
                    r.Speaker = "o:" + p.Who;
                    string district = FlareGame.DistrictName(p.District);
                    r.Slots.Set("district", district).Set("address", p.Name ?? district).Set("days", p.When > 0 ? p.When.ToString() : "1").Set("shop", p.Name);
                    return Use(r, shared);
                case FlareAuthor.Business:
                    r.Use = TextUse.Posts;
                    r.Who.Name = p.Name;
                    r.Who.Style = TextWords.BusinessStyle(FlareLiveRules.CitySeed(_file) ^ (p.Name ?? string.Empty).GetHashCode());
                    r.Who.When = When();
                    r.Speaker = "b:" + p.Name;
                    return string.IsNullOrEmpty(p.Name) ? null : Use(r, shared);
                case FlareAuthor.Tycoon:
                    r.Use = TextUse.Rivals;
                    Tycoon(p.Who, p.Name, r);
                    r.Slots.Set("shop", parent.Name);
                    return p.Who < 0 || string.IsNullOrEmpty(parent.Name) ? null : Use(r, shared);
                default:
                    return null;
            }
        }

        // The request if its use may ask now; null otherwise (nothing built for nothing).
        private TextRequest Use(TextRequest r, bool shared)
        {
            return FlareBridges.TextWants == null || FlareBridges.TextWants(r.Use, shared) ? r : null;
        }

        // A city person as the card's speaker.
        private bool Citizen(int number, TextRequest r)
        {
            if (number < 0 || number >= _citizens.Count)
            {
                return false;
            }
            FlareCitizen c = _citizens[number];
            FlareTextsMap.Fill(r.Who, c, FlareGame.DistrictName(c.District), FlareLiveRules.CitySeed(_file), When());
            r.Speaker = "c:" + number;
            return true;
        }

        // A rival tycoon as the card's speaker: their name, company and manner by their voice.
        private void Tycoon(int voice, string name, TextRequest r)
        {
            int v = FlareRules.Clamp(voice, 0, FlareTextsMap.TycoonCompanies.Length - 1);
            r.Who.Name = name;
            r.Who.Job = FlareTextsMap.TycoonCompanies[v];
            r.Who.Style = TextsMapRules.TycoonStyles[v];
            r.Who.When = When();
            r.Speaker = "t:" + v;
        }

        // A post's author as a thread line names them.
        private string AuthorName(FlarePost p)
        {
            switch (p.Author)
            {
                case FlareAuthor.Player: return MyName;
                case FlareAuthor.Citizen: return p.Who >= 0 && p.Who < _citizens.Count ? _citizens[p.Who].Handle : "?";
                case FlareAuthor.Network: return p.AuthorName ?? "?";
                case FlareAuthor.Official: return FlareText.OfficialName(p.Who);
                default: return p.Name ?? "?";
            }
        }

        // A post of the player's in words (the typed text, or its built line).
        private string PostWords(FlarePost p)
        {
            if (p == null)
            {
                return string.Empty;
            }
            return p.Text ?? FlareText.Built(p, MyName) ?? FlareText.Post(p, PersonName(p.Tag), string.Empty, string.Empty);
        }

        private static string When() => FlareBridges.TextWhen != null ? FlareBridges.TextWhen() : null;

        private static double Seconds(long gameMinutes) => FlareBridges.TextSeconds != null ? FlareBridges.TextSeconds(gameMinutes) : gameMinutes;
    }
}
