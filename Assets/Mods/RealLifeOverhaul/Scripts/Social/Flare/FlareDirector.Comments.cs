using System;
using System.Collections.Generic;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, the comments under posts that are not the player's: who and when from the post's seed, the words
    /// RLO Texts', asked ahead at the lowest priority, an open thread's first; in a session only the host asks. Cost: on
    /// Flare's events a walk over the last day's posts and a dictionary look a comment; nothing per frame.
    /// </summary>
    public sealed partial class FlareDirector
    {
        public const int CommentsWaitingMost = 8;          // comments' background requests waiting at once: a share of the queue (TextQueue.Most 48)
        public const double CommentSeconds = 900.0;        // real seconds a comment's words may take at least: nobody waits for a comment
        public const double CommentThreadSeconds = 60.0;   // real seconds for the open thread's due comments, past the ahead-only notice (TextSpeed.AheadSeconds 30)
        public const long CommentsAheadMinutes = 180;      // game minutes ahead of now a post may go up and have its comments asked already

        private readonly FlareCommentAsks _commentAsks = new FlareCommentAsks();
        private readonly List<FlareComment> _commentPlan = new List<FlareComment>(FlareCommentRules.WrittenMost);
        private long _commentsLooked = -1;
        private int _commentsDay = -1;
        private bool _commentsFreed;   // a request came back since the last walk: the next are asked on the second's look

        /// <summary>A post's written comments, words or not, in the order they come in; none for a post whose comments have no intent.</summary>
        public int CommentsOf(FlarePost p, List<FlareComment> into)
        {
            into.Clear();
            string intent = _file != null && FlareCommentRules.Has(p) ? CommentIntent(p) : null;
            if (intent == null)
            {
                return 0;
            }
            bool city = FlareCommentRules.City(p);
            FlareInfluencer inf = p.Author == FlareAuthor.Influencer ? _file.Influencer(p.Who) : null;
            return FlareCommentRules.Plan(p, FlareLiveRules.CitySeed(_file), _citizens.Count, city ? null : _people, FlareTextsMap.CommentTaste(p, inf != null ? (int)inf.Niche : -1),
                city ? null : _file.Influencers, into);
        }

        /// <summary>A comment's words, or null while it has none (then it is not shown).</summary>
        public string CommentWords(FlarePost p, FlareComment c)
        {
            return FlareBridges.TextKept != null ? FlareBridges.TextKept(FlareCommentRules.Key(p, c)) : null;
        }

        /// <summary>Asks for the words of comments still without them, <paramref name="first"/>'s ahead, while fewer than <see cref="CommentsWaitingMost"/> wait.</summary>
        public void CommentsAhead(FlarePost first)
        {
            if (FlareBridges.TextAsk == null || _file == null || Save == null)
            {
                return;
            }
            long now = Now;
            if (first != null)
            {
                AskComments(first, now, true, true);
                AskComments(first, now, false, false);
            }
            if (now == _commentsLooked && !_commentsFreed)
            {
                return;
            }
            _commentsLooked = now;
            _commentsFreed = false;
            int day = (int)(now / 1440);
            if (day != _commentsDay)
            {
                _commentsDay = day;
                FlareFile file = _file;
                _commentAsks.Prune(id => file.Post(id) != null);   // the comments of posts that are gone, once a game day
                _textAsked.Prune(id => file.Post(id) != null);     // and the record of the words asked for them and the answers under them
                _textAnswers.Prune(id => file.Post(id) != null);
            }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = _file.Posts.Count - 1; i >= 0 && _commentAsks.Background < CommentsWaitingMost; i--)
                {
                    FlarePost p = _file.Posts[i];
                    if (p.At <= now + CommentsAheadMinutes && p.At + 1440 > now)
                    {
                        AskComments(p, now, pass == 0, false);   // the first pass the comments already due, the second those still to come
                    }
                }
            }
        }

        // The second's look: a request came back, so the next comments are asked now, not at the next event.
        private void CommentsSecond()
        {
            if (_commentsFreed)
            {
                CommentsAhead(null);
            }
        }

        private void CommentsForget()
        {
            _commentAsks.Clear();
            _commentsLooked = -1;
            _commentsDay = -1;
            _commentsFreed = false;
        }

        // A post's comments asked: due ones, or those still to come; 'urgent' the open thread's, ahead of the background and past the cap.
        private void AskComments(FlarePost p, long now, bool due, bool urgent)
        {
            if (!FlareCommentRules.Has(p) || p.Shared || (!urgent && _commentAsks.Background >= CommentsWaitingMost))
            {
                return;   // a guest's copy of a shared post: the host's words come
            }
            bool shared = GoingPublic.InSession && FlareNetShare.IsShared(_file, p, Settings.FlareSharePosts);
            if (FlareBridges.TextWants != null && !FlareBridges.TextWants(TextUse.Replies, shared))
            {
                return;
            }
            string intent = CommentIntent(p);
            if (intent == null || CommentsOf(p, _commentPlan) == 0)
            {
                return;
            }
            int citySeed = FlareLiveRules.CitySeed(_file);
            for (int i = 0; i < _commentPlan.Count && (urgent || _commentAsks.Background < CommentsWaitingMost); i++)
            {
                FlareComment c = _commentPlan[i];
                long asked = FlareCommentRules.Asked(p.Id, c.Slot);
                if ((c.At <= now) != due || !_commentAsks.May(asked, urgent))
                {
                    continue;
                }
                string key = FlareCommentRules.Key(p, c), earlier = null;
                if (FlareBridges.TextKept != null && FlareBridges.TextKept(key) != null)
                {
                    _commentAsks.Kept(asked);   // kept from before a load, or sent by the host
                    continue;
                }
                if (i > 0 && FlareTextsMap.AnswersComments(intent) && FlareCommentRules.AnswersEarlier(p, citySeed, c.Slot))
                {
                    long beforeAsked = FlareCommentRules.Asked(p.Id, _commentPlan[i - 1].Slot);
                    earlier = FlareBridges.TextKept != null ? FlareBridges.TextKept(FlareCommentRules.Key(p, _commentPlan[i - 1])) : null;
                    if (earlier == null && (_commentAsks.Waiting(beforeAsked) || _commentAsks.May(beforeAsked, false)))
                    {
                        continue;   // the comment it answers has no words yet: asked later, once it has them or is known to have none
                    }
                }
                TextRequest r = CommentRequest(p, c, intent, now, earlier != null ? _commentPlan[i - 1] : c, earlier, asked, urgent);
                if (r == null)
                {
                    continue;
                }
                r.Key = key;
                r.Keep = !shared;
                r.Shared = shared;
                if (FlareBridges.TextAsk(r))
                {
                    _commentAsks.Asked(asked, urgent);
                }
            }
        }

        // A comment's card: the commenter as the speaker, the post and any earlier comment it answers; null if a value is missing.
        private TextRequest CommentRequest(FlarePost p, FlareComment c, string intent, long now, FlareComment before, string earlier, long asked, bool urgent)
        {
            var r = new TextRequest { Priority = urgent ? TextPriority.Soon : TextPriority.Background, Day = (int)(c.At / 1440) };
            string home;
            if (!Commenter(c, r.Who, out home))
            {
                return null;
            }
            r.Speaker = c.Kind + ":" + c.Who;
            FlareCommentPost post = Poster(p);
            post.Home = home;
            var them = earlier != null ? new TextWho() : null;
            if (them != null && Commenter(before, them, out _))
            {
                post.EarlierFirst = them.First;
                post.EarlierHandle = them.Handle;
                post.EarlierWords = earlier;
            }
            post.Reaction = p.Author == FlareAuthor.Network ? FlareTextsMap.CommentReaction(p, FlareCommentRules.Seed(p, FlareLiveRules.CitySeed(_file)), c.Slot) : null;
            if (!FlareTextsMap.CommentCard(r, intent, post))
            {
                return null;
            }
            r.Seconds = urgent ? CommentThreadSeconds : Math.Max(CommentSeconds, Seconds(c.At - now));
            FlarePost parent = p;
            r.Wanted = () => _file != null && _file.Post(parent.Id) == parent;
            long at = c.At;
            r.Done = words =>
            {
                _commentAsks.Ended(asked, words != null);   // none: free to be asked again
                _commentsFreed = true;
                if (words != null && at <= Now)
                {
                    _view?.ThreadChanged(parent.Id);   // its time has come: shown now if its thread is open
                }
            };
            return r;
        }

        // Who writes a comment, as the card's speaker, and their own district in words.
        private bool Commenter(FlareComment c, TextWho who, out string home)
        {
            home = null;
            switch (c.Kind)
            {
                case 'c':
                    if (c.Who < 0 || c.Who >= _citizens.Count)
                    {
                        return false;
                    }
                    FlareCitizen citizen = _citizens[c.Who];
                    home = FlareGame.DistrictName(citizen.District);
                    FlareTextsMap.Fill(who, citizen, home, FlareLiveRules.CitySeed(_file), When());
                    return true;
                case 'f':
                    if (!Person(c.Who, out FlarePerson person))
                    {
                        return false;
                    }
                    home = string.IsNullOrEmpty(person.District) ? FlareGame.DistrictName(null) : person.District;
                    who.Clear();
                    who.First = who.Name = person.First;
                    who.Handle = FlareText.HandleOf(person.First);
                    who.Age = TextWords.Age(_file.Seed * 13 + c.Who, 21, 48);
                    who.District = home;
                    who.Style = TextWords.CitizenStyle(_file.Seed * 13 + c.Who);
                    who.When = When();
                    return true;
                case 'i':
                    FlareInfluencer inf = _file.Influencer(c.Who);
                    if (inf == null)
                    {
                        return false;
                    }
                    home = FlareGame.DistrictName(inf.District);
                    who.Clear();
                    who.First = who.Name = InfluencerName(inf);
                    who.Handle = inf.Handle;
                    who.Age = TextWords.Age(_file.Seed * 17 + c.Who, 21, 40);
                    who.District = home;
                    who.Style = TextWords.CitizenStyle(_file.Seed * 17 + c.Who);
                    who.When = When();
                    return true;
                default:
                    return false;
            }
        }

        // The post a comment answers, as its card needs it: the poster's name and handle, its words, its district.
        private FlareCommentPost Poster(FlarePost p)
        {
            var post = new FlareCommentPost();
            switch (p.Author)
            {
                case FlareAuthor.Citizen:
                    if (p.Who >= 0 && p.Who < _citizens.Count)
                    {
                        post.First = _citizens[p.Who].First;
                        post.Handle = _citizens[p.Who].Handle;
                    }
                    break;
                case FlareAuthor.Friend:
                    post.First = PersonName(p.Who);
                    post.Handle = post.First != null ? FlareText.HandleOf(post.First) : null;
                    break;
                case FlareAuthor.Influencer:
                    FlareInfluencer inf = _file.Influencer(p.Who);
                    post.First = inf != null ? InfluencerName(inf) : null;
                    post.Handle = inf?.Handle;
                    break;
                case FlareAuthor.Network:
                    post.First = p.AuthorName;
                    post.Handle = FlareText.HandleOf(p.AuthorId);
                    break;
                case FlareAuthor.Official:
                    post.First = FlareText.OfficialName(p.Who);
                    post.Handle = FlareText.OfficialHandle(p.Who);
                    break;
                default:
                    post.First = p.Name;
                    post.Handle = p.Name != null ? FlareText.HandleOf(p.Name) : null;
                    break;
            }
            post.Words = p.Text ?? WordsFor(p, post.First);   // what was typed, else the built line: what the reader sees first
            post.District = string.IsNullOrEmpty(p.District) ? null : FlareGame.DistrictName(p.District);
            post.Player = MyName;
            if (p.Author == FlareAuthor.Business || p.Author == FlareAuthor.Brand || p.Source == FlareSource.Review)
            {
                post.Shop = p.Name;   // data v3's news kinds: the business that posted, or the one reviewed
            }
            if (p.Source == FlareSource.Review)
            {
                post.Stars = FlareRules.Clamp(p.Arg, 1, 5).ToString();
            }
            if (p.Source == FlareSource.Results)
            {
                post.Target = p.Name;
            }
            if (p.Author == FlareAuthor.Tycoon)
            {
                post.Tycoon = p.Name;
                post.Company = FlareTextsMap.TycoonCompanies[FlareRules.Clamp(p.Who, 0, FlareTextsMap.TycoonCompanies.Length - 1)];
                int move = p.Arg - FlareLiveRules.TycoonMoveArg;
                post.Target = move == 3 || move == 4 ? (FlareBridges.TextListed != null ? FlareBridges.TextListed() : null) : null;   // a stake or a bid: in the player's listed company
            }
            return post;
        }

        // The intent of a post's comments (an influencer's by their niche); null for none.
        private string CommentIntent(FlarePost p)
        {
            FlareInfluencer inf = p.Author == FlareAuthor.Influencer ? _file.Influencer(p.Who) : null;
            return FlareTextsMap.Comment(p, inf != null ? (int)inf.Niche : -1, TextData);
        }

        // An influencer's name as their posts show it: a friend's own name if they are one, else the handle without its @.
        private string InfluencerName(FlareInfluencer inf)
        {
            string known = inf.Person >= 0 ? PersonName(inf.Person) : null;
            return known ?? inf.Handle.TrimStart('@');
        }

        /// <summary>A post's words as the feed shows them: the model's if it wrote them in time, else the built line, else its kind's.</summary>
        public string WordsFor(FlarePost p, string author)
        {
            string model = ModelWords(p);
            if (model != null)
            {
                return model;
            }
            string built = FlareText.Built(p, author);
            if (built != null)
            {
                return built;
            }
            if (p.Source == FlareSource.Reply)
            {
                return p.Text ?? string.Empty;
            }
            FlarePerson who = default(FlarePerson);
            bool friend = p.Author == FlareAuthor.Friend && Person(p.Who, out who);
            FlareInfluencer inf = p.Author == FlareAuthor.Influencer && _file != null ? _file.Influencer(p.Who) : null;
            if (inf != null)
            {
                return FlareText.Post(p, null, null, FlareGame.DistrictName(inf.District), (int)inf.Niche);
            }
            return FlareText.Post(p, PersonName(p.Tag), friend ? who.Job : string.Empty, friend ? who.District : string.Empty);
        }
    }
}
