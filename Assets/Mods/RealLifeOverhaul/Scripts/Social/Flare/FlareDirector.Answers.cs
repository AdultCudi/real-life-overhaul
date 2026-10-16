using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The author of a post answers the player's typed reply under it (data v3 of RLO Texts: citizen.answer.reply for a
    /// city person under their own post, official.answer.said, business.answer.said and tycoon.answer.said for an account
    /// under its own). Whether and when is the game's (FlareCommentRules.AuthorAnswer: from the reply's seed, a city
    /// person within an hour or so, an account within two); the words are the model's alone, asked when the reply is posted
    /// and again on Flare's events while the answer is still to come, and kept by the reply's number. Like the comments
    /// under others' posts it has no written line: an answer the model did not word in time is not shown, and with RLO
    /// Texts, its use (Flare comments and replies, official replies, Flare posts, rivals) or a v3 pack missing nothing
    /// changes at all. It is the player's own: shown only in their thread, never saved in Flare's file, never sent in a
    /// Going Public session.
    /// COST: on Flare's events, a few rolls for each reply of the player's still waiting for its answer; one request each;
    /// when a thread is drawn, a few rolls and a look-up for each reply of the player's in it. Nothing per frame.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private readonly FlareTextsAsked<int> _textAnswers = new FlareTextsAsked<int>();   // the answers under the player's replies, by the reply's number

        // An answer still to come under a reply of the player's: its words asked now, its time the deadline.
        private void TextsAnswer(FlarePost reply, long now)
        {
            if (FlareBridges.TextAsk == null || !_textAnswers.May(reply.Id))
            {
                return;
            }
            FlarePost parent = _file.Post(reply.ReplyTo);
            if (!FlareCommentRules.AuthorAnswer(reply, parent, out long at) || at <= now)
            {
                return;
            }
            string key = FlareCommentRules.AnswerKey(reply);
            if (FlareBridges.TextKept != null && FlareBridges.TextKept(key) != null)
            {
                return;
            }
            TextRequest r = AnswerOf(reply, parent);
            if (r == null)
            {
                return;
            }
            int id = reply.Id;
            r.Key = key;
            r.Keep = true;
            r.Day = (int)(at / 1440);
            r.Seconds = Seconds(at - now);
            r.Wanted = () => _file != null && _file.Post(id) == reply && Now < at;   // (words after its time are not used: it never appears late)
            r.Done = words => _textAnswers.Ended(id, words != null);
            if (FlareBridges.TextAsk(r))
            {
                _textAnswers.Asked(id);
            }
        }

        // The answer's card: the post's author as the speaker, their post and the player's reply as the thread.
        private TextRequest AnswerOf(FlarePost reply, FlarePost parent)
        {
            var r = new TextRequest { Intent = FlareTextsMap.Said(parent.Author, TextData), Priority = TextPriority.Soon };
            if (r.Intent == null)
            {
                return null;
            }
            switch (parent.Author)
            {
                case FlareAuthor.Citizen:
                    if (!Citizen(parent.Who, r))
                    {
                        return null;
                    }
                    r.Use = TextUse.Replies;
                    r.Slots.Set("topic", FlareTextsMap.AnswerTopic(parent));
                    break;
                case FlareAuthor.Official:
                    r.Use = TextUse.Official;
                    r.Who.Name = FlareText.OfficialName(parent.Who);
                    r.Who.When = When();
                    r.Speaker = "o:" + parent.Who;
                    break;
                case FlareAuthor.Business:
                case FlareAuthor.Brand:
                    if (string.IsNullOrEmpty(parent.Name))
                    {
                        return null;
                    }
                    r.Use = TextUse.Posts;
                    r.Who.Name = parent.Name;
                    r.Who.Style = TextWords.BusinessStyle(FlareLiveRules.CitySeed(_file) ^ parent.Name.GetHashCode());
                    r.Who.When = When();
                    r.Speaker = "b:" + parent.Name;
                    break;
                case FlareAuthor.Tycoon:
                    if (parent.Who < 0 || string.IsNullOrEmpty(parent.Name))
                    {
                        return null;
                    }
                    r.Use = TextUse.Rivals;
                    Tycoon(parent.Who, parent.Name, r);
                    break;
                default:
                    return null;
            }
            string author = AuthorName(parent);
            r.History.Add(new TextSaid(author, WordsFor(parent, author)));   // (its own post as the reader sees it)
            r.History.Add(new TextSaid(MyName, reply.Text));
            return Use(r, false);   // (the player's own: never the shared feed's)
        }

        /// <summary>The author's answer under a reply of the player's, made for display once its time has come and its words are here; null otherwise.</summary>
        public FlarePost AnswerShown(FlarePost reply, long now)
        {
            if (_file == null || FlareBridges.TextKept == null || reply == null || reply.Author != FlareAuthor.Player || reply.Source != FlareSource.Reply)
            {
                return null;
            }
            FlarePost parent = _file.Post(reply.ReplyTo);
            if (!FlareCommentRules.AuthorAnswer(reply, parent, out long at) || at > now)
            {
                return null;
            }
            string words = FlareBridges.TextKept(FlareCommentRules.AnswerKey(reply));
            if (words == null)
            {
                return null;
            }
            var answer = new FlarePost
            {
                Id = FlareCommentRules.AnswerDisplayId(reply.Id), Author = parent.Author, Who = parent.Who, Name = parent.Name, Place = parent.Place, Source = FlareSource.Reply,
                ReplyTo = parent.Id, Text = words, Reach = 30, Likes = FlareRules.Roll(reply.Seed, reply.Id, 853) % 6
            };
            FlareLiveRules.SetAt(answer, at);
            return answer;
        }
    }
}
