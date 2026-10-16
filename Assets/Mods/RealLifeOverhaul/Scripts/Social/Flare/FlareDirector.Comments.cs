using System.Collections.Generic;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The comments under posts that are not the player's (FlareCommentRules): who writes them and when is the game's, from
    /// the post's seed; their words are RLO Texts' alone (the owner's rule, 16 Oct 2026: no written comment lines). The
    /// words are asked early, at the lowest priority, a few at a time: when the city's posts are made and each game hour
    /// (with the other words of Flare), when the feed is shown, and when a thread is opened (that post first). They are
    /// kept by the comment's key, so a load shows the same, and a comment shows only once its words are there and its time
    /// has come, so it never changes in front of the player. In a Going Public session the host asks for the shared feed's
    /// comments and sends them (Texts/TextsShare); a guest only shows what came. With a v3 pack every kind of post has
    /// comments (FlareTextsMap.Comment), and now and then a comment answers the one before it (FlareCommentRules.AnswersEarlier):
    /// it is asked once that one's words are here (or once it is known to have none, then it answers the post alone).
    /// COST: on those events, one walk over the posts (a few hundred) with a plan of at most eight comments for each that
    /// has replies; at most <see cref="CommentsWaitingMost"/> requests waiting at once; nothing per frame.
    /// </summary>
    public sealed partial class FlareDirector
    {
        public const int CommentsWaitingMost = 6;          // comments' requests waiting at once: a share of the background's budget, never all of it
        public const double CommentSeconds = 900.0;        // real seconds a comment's words may take at least: a comment waits for its words, nobody waits for it
        public const long CommentsAheadMinutes = 180;      // game minutes ahead of now a post may go up and have its comments asked already

        private readonly HashSet<long> _commentsAsked = new HashSet<long>();
        private readonly HashSet<string> _commentsPending = new HashSet<string>();   // the keys of comments whose request waits
        private readonly List<FlareComment> _commentPlan = new List<FlareComment>(FlareCommentRules.WrittenMost);
        private int _commentsWaiting;
        private long _commentsLooked = -1;

        /// <summary>The comments a post has (its written ones, whether their words are here or not), in the order they come in; none for a post whose comments have no intent.</summary>
        public int CommentsOf(FlarePost p, List<FlareComment> into)
        {
            into.Clear();
            string intent = _file != null && FlareCommentRules.Has(p) ? CommentIntent(p) : null;
            if (intent == null)
            {
                return 0;
            }
            bool city = FlareCommentRules.City(p);
            return FlareCommentRules.Plan(p, FlareLiveRules.CitySeed(_file), _citizens.Count, city ? null : _people, FlareTextsMap.Taste(intent),
                city ? null : _file.Influencers, into);
        }

        /// <summary>A comment's words, or null while it has none (then it is not shown).</summary>
        public string CommentWords(FlarePost p, FlareComment c)
        {
            return FlareBridges.TextKept != null ? FlareBridges.TextKept(FlareCommentRules.Key(p, c)) : null;
        }

        /// <summary>
        /// Asks for the words of comments still without them: 'first' before the rest (a thread just opened), then the posts
        /// up or going up soon, newest first, while fewer than <see cref="CommentsWaitingMost"/> wait. The walk runs once a
        /// game minute at most.
        /// </summary>
        public void CommentsAhead(FlarePost first)
        {
            if (FlareBridges.TextAsk == null || _file == null || Save == null)
            {
                return;
            }
            long now = Now;
            if (first != null)
            {
                AskComments(first, now);
            }
            if (now == _commentsLooked)
            {
                return;
            }
            _commentsLooked = now;
            for (int i = _file.Posts.Count - 1; i >= 0 && _commentsWaiting < CommentsWaitingMost; i--)
            {
                FlarePost p = _file.Posts[i];
                if (p.At <= now + CommentsAheadMinutes && p.At + 1440 > now)
                {
                    AskComments(p, now);
                }
            }
        }

        // Forgets what was asked (another file, or none).
        private void CommentsForget()
        {
            _commentsAsked.Clear();
            _commentsPending.Clear();
            _commentsLooked = -1;
        }

        private void AskComments(FlarePost p, long now)
        {
            if (!FlareCommentRules.Has(p) || p.Shared || _commentsWaiting >= CommentsWaitingMost)
            {
                return;   // (a guest's copy of a shared post: the host's words come, a guest never asks)
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
            for (int i = 0; i < _commentPlan.Count && _commentsWaiting < CommentsWaitingMost; i++)
            {
                FlareComment c = _commentPlan[i];
                long asked = FlareCommentRules.Asked(p.Id, c.Slot);
                if (_commentsAsked.Contains(asked))
                {
                    continue;
                }
                string key = FlareCommentRules.Key(p, c), earlier = null;
                if (i > 0 && FlareTextsMap.AnswersComments(intent) && FlareCommentRules.AnswersEarlier(p, citySeed, c.Slot))
                {
                    string before = FlareCommentRules.Key(p, _commentPlan[i - 1]);
                    earlier = FlareBridges.TextKept != null ? FlareBridges.TextKept(before) : null;
                    if (earlier == null && (_commentsPending.Contains(before) || !_commentsAsked.Contains(FlareCommentRules.Asked(p.Id, _commentPlan[i - 1].Slot))))
                    {
                        continue;   // (the comment it answers has no words yet: asked on a later look, once it has them or is known to have none)
                    }
                }
                _commentsAsked.Add(asked);   // (asked once a session: kept words stay, a miss is not asked again until a load)
                if (FlareBridges.TextKept != null && FlareBridges.TextKept(key) != null)
                {
                    continue;
                }
                TextRequest r = CommentRequest(p, c, intent, now, earlier != null ? _commentPlan[i - 1] : c, earlier);
                if (r == null)
                {
                    continue;
                }
                r.Key = key;
                r.Keep = !shared;
                r.Shared = shared;
                if (FlareBridges.TextAsk(r))
                {
                    _commentsWaiting++;
                    _commentsPending.Add(key);
                }
            }
        }

        // A comment's card: the commenter as the speaker, the post it answers (and the earlier comment it answers, with words); null if a value is missing.
        private TextRequest CommentRequest(FlarePost p, FlareComment c, string intent, long now, FlareComment before, string earlier)
        {
            var r = new TextRequest { Priority = TextPriority.Background, Day = (int)(c.At / 1440) };
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
            r.Seconds = System.Math.Max(CommentSeconds, Seconds(c.At - now));
            FlarePost parent = p;
            r.Wanted = () => _file != null && _file.Post(parent.Id) == parent;
            string key = FlareCommentRules.Key(p, c);
            r.Done = words =>
            {
                _commentsWaiting = System.Math.Max(0, _commentsWaiting - 1);
                _commentsPending.Remove(key);
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
            post.Words = p.Text ?? WordsFor(p, post.First);   // (what was typed, else the built line: what the reader sees first)
            post.District = string.IsNullOrEmpty(p.District) ? null : FlareGame.DistrictName(p.District);
            post.Player = MyName;
            if (p.Author == FlareAuthor.Business || p.Author == FlareAuthor.Brand || p.Source == FlareSource.Review)
            {
                post.Shop = p.Name;   // (data v3's news kinds: the business that posted, or the one reviewed)
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
                post.Target = move == 3 || move == 4 ? (FlareBridges.TextListed != null ? FlareBridges.TextListed() : null) : null;   // (a stake or a bid: in the player's listed company)
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

        /// <summary>A post's words as the feed shows them: the model's when it wrote them in time, the post's own built line, or its kind's.</summary>
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
