using System;
using System.Collections.Generic;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What Flare asks of RLO Texts (Scripts/Texts), wired by Wiring/TextsLinks. Without it Flare keeps its written lines.</summary>
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

        /// <summary>The data version of the running pack (TextsHub.Data): which intents may be asked. Default: v2's.</summary>
        public static Func<int> TextData;

        /// <summary>A friend or the partner of the social life as a card's speaker by their number (false: not known).</summary>
        public static TextFriendOf TextFriend;

        /// <summary>The player's listed company's name, or null (a rival's stake or bid names it).</summary>
        public static Func<string> TextListed;
    }

    /// <summary>Fills a card's speaker from a person of the social life by their number; false if there is none.</summary>
    public delegate bool TextFriendOf(int index, TextWho who, out bool partner);

    /// <summary>
    /// Flare's director, posts worded by RLO Texts: when the game makes a post that goes up later, its card is built and its
    /// words asked at once, its going up the deadline; the words are kept by the post's number (the session's number in the
    /// shared feed) and shown in place of the written line. Words that come after the post went up are not used, so a post
    /// never changes its words in front of the player. In a session only the host asks for the shared feed's words. A
    /// request handed back without words is asked again on the next event (FlareTextsAsked). Cost: on the events that make
    /// posts, one walk over the posts not yet up and the messages; nothing per frame.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private readonly FlareTextsAsked<int> _textAsked = new FlareTextsAsked<int>();         // the posts, by number
        private readonly FlareTextsAsked<string> _textDms = new FlareTextsAsked<string>();     // the answers to the player's messages, by key

        // The data version of the pack in use: a v3 intent is asked only of a v3 pack.
        private static int TextData => FlareBridges.TextData != null ? FlareBridges.TextData() : TextIntents.DataV2;

        /// <summary>The model's words for a post, or null for its written line.</summary>
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
            return m != null && m.Text == null && FlareBridges.TextKept != null ? FlareBridges.TextKept(FlareTextsMap.DmKey(m)) : null;
        }

        // Every post made that goes up later and has an intent, and every answer to a message of the player's still to come:
        // its words asked now, its going up the deadline.
        private void TextsAhead()
        {
            if (FlareBridges.TextAsk == null || _file == null || Save == null)
            {
                return;
            }
            long now = Now;
            bool hostShares = Settings.FlareSharePosts;
            int data = TextData;
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                FlarePost p = _file.Posts[i];
                if (data >= TextIntents.DataV3 && p.Author == FlareAuthor.Player && p.Source == FlareSource.Reply)
                {
                    TextsAnswer(p, now);   // the author's answer to a reply of the player's (FlareDirector.Answers)
                }
                if (p.At <= now || p.Text != null || p.Shared || !_textAsked.May(p.Id))
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
                r.Wanted = () => _file != null && _file.Post(post.Id) == post && Now < post.At;   // words after it went up are not used
                int id = p.Id;
                r.Done = words => _textAsked.Ended(id, words != null);   // kept by the hub under the post's key; none: asked again on the next event
                if (FlareBridges.TextAsk(r))
                {
                    _textAsked.Asked(id);   // not again while it waits
                }
            }
            for (int i = 0; i < _file.Net.Dms.Count; i++)
            {
                FlareDm m = _file.Net.Dms[i];
                if (FlareTextsMap.DmWants(m, now, data))
                {
                    FlareDm asked = FlareTextsMap.DmAsked(_file.Net.Dms, m);
                    TextsDm(m, asked != null ? asked.Text : null);
                }
            }
            CommentsAhead(null);   // a few more of the comments under others' posts
        }

        // The friends' replies of a post of the player's, as its reactors say: asked when it is posted.
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

        // The answer to a message of the player's: asked when it is scheduled, and again on the next event while it is still to
        // come if it came back without words. Its speaker is read from the account; a decided invitation has its own card.
        private void TextsDm(FlareDm answer, string typed)
        {
            int data = TextData;
            if (FlareBridges.TextAsk == null || Save == null || !FlareTextsMap.DmWants(answer, Now, data) || string.IsNullOrEmpty(typed))
            {
                return;
            }
            string key = FlareTextsMap.DmKey(answer);
            if (!_textDms.May(key) || (FlareBridges.TextKept != null && FlareBridges.TextKept(key) != null))
            {
                return;
            }
            FlareDmSpeaker speaker = FlareTextsMap.DmSpeakerOf(answer.With, out int number);
            var r = new TextRequest { Use = TextUse.People, Intent = FlareTextsMap.Dm(answer.Key, speaker, data), Key = key, Keep = true, Day = Save.Day };
            if (!DmWho(answer, speaker, number, r))
            {
                return;
            }
            InviteCard(answer, data, r);   // an invitation's answer: its own intent and values
            r.History.Add(new TextSaid(MyName, typed));
            long at = answer.At;
            r.Seconds = Seconds(at - Now);
            r.Wanted = () => Now < at;
            r.Done = words => _textDms.Ended(key, words != null);   // kept by the hub under its key; none: asked again on the next event
            if (FlareBridges.TextAsk(r))
            {
                _textDms.Asked(key);
            }
        }

        // The speaker of an answer to a message, by the account; false for one the model has no card for (the written line).
        private bool DmWho(FlareDm answer, FlareDmSpeaker speaker, int number, TextRequest r)
        {
            int citySeed = FlareLiveRules.CitySeed(_file);
            switch (speaker)
            {
                case FlareDmSpeaker.Citizen:
                    if (number < 0 || number >= _citizens.Count)
                    {
                        return false;
                    }
                    FlareCitizen c = _citizens[number];
                    FlareTextsMap.Fill(r.Who, c, FlareGame.DistrictName(c.District), citySeed, When());
                    r.Who.Mood = FlareInviteRules.CardMood(answer.Key, FlareTextsMap.Mood(citySeed, number, Save.Day, c.Persona == FlarePersona.Grumpy));
                    r.Speaker = "c:" + number;   // as the city person's posts and replies name them, for the repeat check
                    return true;
                case FlareDmSpeaker.Influencer:
                    FlareInfluencer inf = _file.Influencer(number);
                    if (inf == null)
                    {
                        return false;
                    }
                    r.Who.Name = r.Who.First = answer.Name ?? inf.Handle;
                    r.Who.Handle = inf.Handle;
                    r.Who.Age = TextWords.Age(_file.Seed * 17 + number, 21, 40);
                    r.Who.District = FlareGame.DistrictName(inf.District);
                    r.Who.Style = TextWords.CitizenStyle(_file.Seed * 17 + number);
                    r.Who.Mood = FlareInviteRules.CardMood(answer.Key, FlareTextsMap.Mood(_file.Seed * 17, number, Save.Day, false));
                    r.Who.When = When();
                    r.Speaker = answer.With;
                    return true;
                case FlareDmSpeaker.Friend:
                case FlareDmSpeaker.Partner:
                    if (FlareBridges.TextFriend == null || !FlareBridges.TextFriend(number, r.Who, out bool partner))
                    {
                        return false;
                    }
                    r.Intent = partner ? "partner.dm.answer" : "friend.dm.answer";   // who they are to the player now, whatever the conversation's key
                    r.Speaker = (partner ? "p" : "f") + number;
                    return true;
                case FlareDmSpeaker.Official:
                    r.Use = TextUse.Official;
                    r.Who.Name = FlareText.OfficialName(FlareRules.Clamp(number, 0, (int)FlareOfficial.Bank));
                    r.Who.When = When();
                    r.Speaker = "o:" + number;
                    return true;
                case FlareDmSpeaker.Business:
                    string shop = FlareTextsMap.DmName(answer.With);
                    r.Who.Name = shop;
                    r.Who.Style = TextWords.BusinessStyle(citySeed ^ shop.GetHashCode());
                    r.Who.When = When();
                    r.Speaker = "b:" + shop;
                    return true;
                case FlareDmSpeaker.Tycoon:
                    string name = FlareTextsMap.DmName(answer.With);
                    r.Use = TextUse.Rivals;
                    Tycoon(TycoonVoice(name), name, r);
                    return true;
                default:
                    return false;
            }
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
                    r.Intent = FlareTextsMap.Reply(parent, (FlareReplyKind)p.Arg, TextData);   // its words, its kind, or a comment on its kind: the game chose the reply
                    if (r.Intent == null)
                    {
                        return null;
                    }
                    r.Use = TextUse.Replies;
                    r.Priority = TextPriority.Soon;
                    if (r.Intent == "citizen.reply.words")
                    {
                        r.History.Add(new TextSaid(MyName, parent.Text));
                    }
                    else if (r.Intent.StartsWith("citizen.reply.", StringComparison.Ordinal))
                    {
                        r.Slots.Set("reaction", FlareTextsMap.Reaction((FlareReplyKind)p.Arg, FlareTextsMap.PostKind(parent.Source)));
                        r.History.Add(new TextSaid(MyName, PostWords(parent)));
                    }
                    else
                    {
                        FlareTextsMap.MineSlots(r.Slots, parent, MyName, string.IsNullOrEmpty(parent.District) ? null : FlareGame.DistrictName(parent.District));
                    }
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
                        int move = p.Arg >= FlareLiveRules.TycoonMoveArg ? p.Arg - FlareLiveRules.TycoonMoveArg : -1;
                        r.Intent = FlareTextsMap.Tycoon(move, TextData);
                        Tycoon(p.Who, p.Name, r);
                        TycoonTarget(move, r.Slots);
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
            bool typed = FlareTextsMap.AnswersMention((FlareAnswer)p.Arg) && parent.Author == FlareAuthor.Player && !string.IsNullOrEmpty(parent.Text);
            r.Intent = (typed ? FlareTextsMap.Said(p.Author, TextData) : null) ?? FlareTextsMap.Answer((FlareAnswer)p.Arg, p.Author);   // data v3: what was typed, answered
            typed &= r.Intent != null && r.Intent.EndsWith(".said", StringComparison.Ordinal);
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
                case FlareAuthor.Brand:
                    if (!typed || string.IsNullOrEmpty(p.Name))
                    {
                        return null;   // a brand answers in its own words only what was typed to it
                    }
                    r.Use = TextUse.Posts;
                    r.Who.Name = p.Name;
                    r.Who.Style = TextWords.BusinessStyle(FlareLiveRules.CitySeed(_file) ^ p.Name.GetHashCode());
                    r.Who.When = When();
                    r.Speaker = "b:" + p.Name;
                    return Use(r, shared);
                case FlareAuthor.Tycoon:
                    r.Use = TextUse.Rivals;
                    Tycoon(p.Who, p.Name, r);
                    r.Slots.Set("shop", parent.Name);
                    return p.Who < 0 || (!typed && string.IsNullOrEmpty(parent.Name)) ? null : Use(r, shared);
                default:
                    return null;
            }
        }

        // The request if its use may ask now; null otherwise.
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

        // A rival's stake or bid names the player's listed company; a suit names the player.
        private void TycoonTarget(int move, TextSlots slots)
        {
            if (move == 3 || move == 4)
            {
                string listed = FlareBridges.TextListed != null ? FlareBridges.TextListed() : null;
                if (!string.IsNullOrEmpty(listed))
                {
                    slots.Set("target", listed);   // none: the card is not built, the written line
                }
            }
            else if (move == 6)
            {
                slots.Set("player", MyName);
            }
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
