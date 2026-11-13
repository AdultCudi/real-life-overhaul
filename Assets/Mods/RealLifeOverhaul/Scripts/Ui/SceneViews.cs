using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A line someone says in a scene (a note of the page), with who says it.</summary>
    public struct SceneNote
    {
        public string Name;
        public string Detail;
    }

    /// <summary>An answer a scene offers: its id for the page's owner, its words, what it means, and the words it says in the thread.</summary>
    public struct SceneEntry
    {
        public int Id;
        public string Name;
        public string Detail;
        public string Said;   // what the player says by it, where that is not its name (a pitch's answer); null for its name
        public bool Enabled;
        public UiConsequence[] Lines;   // what it costs or brings, in its colours, under its meaning; null for none
        public string Note;             // the time it takes, beside its name; null for none
    }

    /// <summary>A line of a scene's conversation so far: what they said, or the answer the player gave.</summary>
    public struct SceneSaid
    {
        public UiFrom From;
        public string Text;
    }

    /// <summary>
    /// The words a scene's card takes from the game's and the mod's texts, so the views stay game-free: Back where the
    /// player may leave (the window's cancel), Menu where they may not (the cancel key then opens the game's own menu).
    /// </summary>
    public struct SceneWords
    {
        public string Move, Choose, Back, Menu;
        public string Present;   // under the faces of those present, told with their roles: "{count} present"
        public string Answer;    // over the answers in the conversation: "Your answer"; null for none
    }

    /// <summary>
    /// A scene's page as its owner wrote it into its list (a hearing, a fame or office scene, an interview's round, a
    /// pitch's question): the headline, the lines under it (what is said, and the progress the list writes small), the
    /// notes someone says, the answers, and what the owner told of the moment (its look, the place, those present, who speaks).
    /// </summary>
    public sealed class ScenePage
    {
        public string Title;
        public string Info;
        public SceneLook Look;
        public string Kicker;                 // the place or the moment: "Case 114, day 3"
        public UiIcon Emblem = UiIcon.People; // the moment's emblem where no faces are given
        public string[] Faces;                // those present, as the owner names them
        public string[] Roles;                // who each of them is, in the same order; null for none told
        public UiRole[] Rings;                // with the roles, each one's ring: whose side they are on
        public string Speaker;                // who speaks a page of questions and answers
        public string Role;                   // under their name: who they are to the player
        public bool InfoSaid = true;          // the lines under the headline are said; false where they are the moment's caption (its kicker and faces)
        public bool Locked;                   // the player may not leave: no way out but an answer
        public UiLedgerLine[] Statement;      // what the page says as a statement (a file's figures), drawn in place of its notes' words; null for none
        public string StatementWords;         // the words over the statement (a case's hearing); null for none
        public string StatementFoot;          // the words under it (probation, the licence); null for none
        public readonly List<SceneNote> Notes = new List<SceneNote>(4);
        public readonly List<SceneEntry> Entries = new List<SceneEntry>(8);
        public int Stamp;                     // grows with every change, so a screen sees a new page by one number

        /// <summary>A new filling: the headline and its lines; the notes, the answers and the moment's telling gone.</summary>
        public void Reset(string title, string info)
        {
            Title = title;
            Info = info;
            Look = SceneLook.Decision;
            Kicker = null;
            Emblem = UiIcon.People;
            Faces = null;
            Roles = null;
            Rings = null;
            Speaker = null;
            Role = null;
            InfoSaid = true;
            Statement = null;
            StatementWords = null;
            StatementFoot = null;
            Notes.Clear();
            Entries.Clear();
            Stamp++;
        }
    }

    /// <summary>
    /// A scene's page as the kit draws it: a moment (a verdict, a fame or office scene, the end of an interview or a
    /// pitch) as the decision card, with the faces of those present, what is said and the answers numbered with what each
    /// means; questions and answers (an interview's rounds, a pitch's questions) as the conversation docked at the right,
    /// its thread the lines so far. A page whose words outgrow the card (its paragraph, or the screen's height) takes the
    /// conversation, which scrolls. What an answer means is said in whole sentences, each on its line, so a cost is never
    /// cut off. Game-free; checked offline (UiChecks.Dialog). Cost: a few small arrays a page.
    /// </summary>
    public static class SceneViews
    {
        /// <summary>The decision card's paragraph's width: the card's 1040 less its 296 of faces and two gutters of 32.</summary>
        public const float CardWords = 680f;

        /// <summary>Characters a line under the headline takes and still reads as the card's kicker; a longer one is said in its paragraph.</summary>
        public const int KickerMost = 48;

        /// <summary>Lines an answer's meaning takes under its name: every one it needs. A card too tall for the screen becomes the conversation, whose answers scroll.</summary>
        public const int MeaningMost = int.MaxValue;

        /// <summary>Lines the words a player says by an answer (a pitch's) take under its name: said whole, never cut short by an ellipsis.</summary>
        public const int SaidMost = int.MaxValue;

        /// <summary>The answers' scroll in the docked conversation, kept by the screen from one layout to the next.</summary>
        public sealed class AnswerScroll
        {
            public float Offset;
            public short Clip = -1;
        }

        /// <summary>The room a card keeps clear above and below it: the hint bar over the status bar stays seen under it.</summary>
        public const float Clear = UiTokens.S12;

        /// <summary>
        /// What the lines under the headline say, and the progress written small under them; null where the owner told them
        /// as the moment's caption (its kicker and those present), so they are not said again.
        /// </summary>
        public static string SaidOf(ScenePage page, out string progress)
        {
            DialogRules.SplitInfo(page.Info, out string said, out progress);
            return page.InfoSaid ? said : null;
        }

        /// <summary>What the page says: its notes' words.</summary>
        public static string Body(ScenePage page)
        {
            string body = null;
            for (int i = 0; i < page.Notes.Count; i++)
            {
                string words = Clean(page.Notes[i].Detail);
                if (words != null)
                {
                    body = body == null ? words : body + "\n\n" + words;
                }
            }
            return body;
        }

        /// <summary>The look the page takes: the owner's, or the conversation where the card's paragraph cannot hold it.</summary>
        public static SceneLook LookOf(ScenePage page)
        {
            string said = SaidOf(page, out string _);
            int lines = Lines(Body(page)) + (Kicks(page, said) ? 0 : Lines(said));
            return DialogRules.Look(page.Statement != null, lines);   // a statement reads in the conversation's bubble; questions stand on the card too
        }

        /// <summary>The card stands whole over the status bar, a gutter above and below it; one that cannot is a conversation.</summary>
        public static bool CardFits(UiDecisionModel card, float bandY, IUiKeys keys) => UiDecision.Height(card, CardWords, keys) <= bandY - Clear * 2f;

        /// <summary>
        /// The page laid in its look: the decision card for a moment that stands whole on the screen, else the conversation
        /// docked at the right, scrolled to <paramref name="offset"/> (-1 for its newest line). Returns the look laid.
        /// </summary>
        public static SceneLook Lay(UiScene s, ScenePage page, List<SceneSaid> thread, SceneWords words, float offset, UiFrame frame, bool dark, IUiKeys keys) =>
            Lay(s, page, thread, words, offset, frame, dark, keys, null);

        /// <summary>As above, with the answers' scroll kept in <paramref name="answers"/> (null for none): the screen's wheel moves it where the answers outgrow their room.</summary>
        public static SceneLook Lay(UiScene s, ScenePage page, List<SceneSaid> thread, SceneWords words, float offset, UiFrame frame, bool dark, IUiKeys keys, AnswerScroll answers)
        {
            SceneLook look = LookOf(page);
            UiDecisionModel card = look == SceneLook.Decision ? Decision(page, words) : null;
            if (card != null && !CardFits(card, frame.BandY, keys))
            {
                look = SceneLook.Talk;   // taller than the screen holds: the conversation, which scrolls
            }
            if (look == SceneLook.Talk)
            {
                UiConversationModel talk = Talk(page, thread, words, frame.Width);
                talk.Offset = offset;
                talk.ChoiceOffset = answers != null ? answers.Offset : 0f;
                UiConversation.LayDocked(s, talk, frame, dark, keys);
                if (answers != null)
                {
                    answers.Offset = talk.ChoiceOffset;
                    answers.Clip = talk.ChoiceClip;
                }
            }
            else
            {
                UiDecision.Lay(s, card, frame, dark, keys);
                if (answers != null)
                {
                    answers.Clip = -1;
                }
            }
            return look;
        }

        /// <summary>The page as the decision card.</summary>
        public static UiDecisionModel Decision(ScenePage page, SceneWords words)
        {
            string said = SaidOf(page, out string progress);
            string speaker = Speaker(page);
            bool kicks = Kicks(page, said);
            string[] faces = Faces(page, speaker, out string[] roles, out UiRole[] rings);
            int speaking = speaker != null ? System.Array.IndexOf(faces, speaker) : -1;
            var m = new UiDecisionModel
            {
                Kicker = Clean(page.Kicker) ?? (kicks ? said : page.Look == SceneLook.Talk ? Clean(progress) : null),
                Title = Clean(page.Title),
                Emblem = page.Emblem,
                EmblemTint = UiRole.Accent,
                Faces = faces,
                FaceRoles = roles,
                FaceRings = rings,
                Speaking = roles != null ? speaking : -1,
                // Under the faces: how many are present where each is told with who they are, else who of them speaks.
                EmblemLabel = roles != null ? Present(words, faces.Length) : speaking >= 0 ? speaker : null,
                Body = Join(kicks ? null : said, Body(page)),
                Choices = Choices(page, ChoiceLine(CardWords)),
                Buttons = page.Locked
                    ? new UiActionModel[0]
                    : new[] { new UiActionModel { Label = words.Back, Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiDecision.ButtonCommand } },
            };
            m.Selected = Keyed(page, out int _);   // F takes the answer it leads with, chosen; no cap says so, each row shows its number alone
            m.Prompts = Prompts(page, words);
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        /// <summary>The answers F and P take: the first two that can be taken, as their caps say; -1 for none.</summary>
        public static int Keyed(ScenePage page, out int second)
        {
            int main = -1;
            second = -1;
            for (int i = 0; i < page.Entries.Count; i++)
            {
                if (!page.Entries[i].Enabled)
                {
                    continue;
                }
                if (main < 0)
                {
                    main = i;
                }
                else if (second < 0)
                {
                    second = i;
                }
            }
            return main;
        }

        /// <summary>The page as the conversation: the speaker at the head with the progress in a chip, the thread so far, the answers.</summary>
        public static UiConversationModel Talk(ScenePage page, List<SceneSaid> thread, SceneWords words, float frameWidth)
        {
            string said = SaidOf(page, out string progress);
            string named = Speaker(page);
            string speaker = named ?? Clean(page.Title) ?? string.Empty;
            var lines = new List<UiMessageModel>(thread.Count + 4);
            for (int i = 0; i < thread.Count; i++)
            {
                if (!string.IsNullOrEmpty(thread[i].Text))
                {
                    lines.Add(new UiMessageModel { From = thread[i].From, Text = thread[i].Text });
                }
            }
            Said(lines, page, said);
            var m = new UiConversationModel
            {
                Title = Clean(page.Title),
                Name = speaker,
                Hue = UiColour.HueOf(speaker),
                Ring = UiRole.None,
                Icon = named == null ? page.Emblem : UiIcon.None,   // nobody speaks (a file): the moment's emblem in the face's place
                Role = Clean(page.Role) ?? Clean(page.Kicker),
                Status = Clean(progress),
                StatusTone = UiRole.Accent,
                Messages = lines.ToArray(),
                MessageCount = lines.Count,
                Choices = Choices(page, TalkLine(frameWidth)),
                Offset = -1f
            };
            m.Chosen = Keyed(page, out int _);
            m.ChoicesLabel = m.Choices.Length > 0 ? Clean(words.Answer) : null;
            m.Typing = m.Choices.Length == 0;
            m.Prompts = Prompts(page, words);
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        /// <summary>What the page says now, as the thread's lines.</summary>
        public static void Said(List<UiMessageModel> into, ScenePage page, string said)
        {
            if (!string.IsNullOrEmpty(said))
            {
                into.Add(new UiMessageModel { From = UiFrom.Them, Text = said });
            }
            if (page.Statement != null)
            {
                // A file's figures as a statement: its words over it, the figures on one right edge, the rest under it.
                into.Add(new UiMessageModel { From = UiFrom.Them, Text = Clean(page.StatementWords), Table = page.Statement, TableFoot = Clean(page.StatementFoot) });
                return;
            }
            for (int i = 0; i < page.Notes.Count; i++)
            {
                string words = Clean(page.Notes[i].Detail);
                if (words != null)
                {
                    into.Add(new UiMessageModel { From = UiFrom.Them, Text = words });
                }
            }
        }

        /// <summary>The words an answer says in the thread; null for an id the page does not offer.</summary>
        public static string Answer(ScenePage page, int id)
        {
            for (int i = 0; i < page.Entries.Count; i++)
            {
                if (page.Entries[i].Id == id)
                {
                    return Clean(page.Entries[i].Said) ?? Clean(page.Entries[i].Name);
                }
            }
            return null;
        }

        /// <summary>The room a choice's meaning has on a row of this width: after its number, its mark and the gap.</summary>
        public static float ChoiceLine(float rowWidth) =>
            rowWidth - UiDraw.Snap(UiTextRules.Width("9.", UiStyle.BodyStrong) + UiTokens.S2) - UiTokens.IconSmall - UiTokens.S2;

        /// <summary>The room a choice's meaning has in the conversation docked at the right of a screen this wide.</summary>
        public static float TalkLine(float frameWidth)
        {
            float w = System.Math.Max(UiConversation.DockLeast, UiDraw.SnapDown(frameWidth * UiConversation.DockShare));
            return ChoiceLine(w - UiTokens.S5 * 2f - UiTokens.S3 * 2f);
        }

        /// <summary>
        /// What an answer means, in lines that each fit the room: a sentence to a line, and a sentence too long for one
        /// broken between words (between characters in a script without spaces); <see cref="MeaningMost"/> lines at most.
        /// </summary>
        public static UiConsequence[] Meaning(string detail, float width, int most = MeaningMost)
        {
            string words = Clean(detail);
            if (words == null)
            {
                return null;
            }
            var lines = new List<UiConsequence>(3);
            int start = 0;
            string rest = null;
            for (int i = 0; i <= words.Length && rest == null; i++)
            {
                if (i < words.Length && !Ends(words, i))
                {
                    continue;
                }
                int end = i < words.Length ? i + 1 : i;
                string sentence = words.Substring(start, end - start).Trim();
                start = end;
                string left = lines.Count < most ? Fit(sentence, width, lines, most) : sentence;
                if (left.Length > 0)
                {
                    rest = (left + " " + words.Substring(start)).Trim();
                }
            }
            if (!string.IsNullOrEmpty(rest) && lines.Count > 0)
            {
                // More than the row holds: the last line takes the rest, and the kit's ellipsis says there is more.
                UiConsequence last = lines[lines.Count - 1];
                last.Text = last.Text + " " + rest;
                lines[lines.Count - 1] = last;
            }
            return lines.ToArray();
        }

        // A sentence ends at a stop, a question or an exclamation followed by a space, or at a full-width stop; a semicolon
        // keeps its two halves on one line, broken only where the line is too short for them.
        private static bool Ends(string words, int i)
        {
            char c = words[i];
            if (c == '。' || c == '？' || c == '！' || c == '\n')
            {
                return true;
            }
            return (c == '.' || c == '?' || c == '!') && i + 1 < words.Length && words[i + 1] == ' ';
        }

        // A sentence onto lines that fit: whole if it fits, else broken where a space allows, else between characters.
        // Returns what the lines left could not take, empty when it all went on.
        private static string Fit(string sentence, float width, List<UiConsequence> lines, int most)
        {
            while (sentence.Length > 0 && lines.Count < most)
            {
                if (UiTextRules.Fits(sentence, UiStyle.Small, width))
                {
                    lines.Add(new UiConsequence { Text = sentence });
                    return string.Empty;
                }
                int cut = 1;
                for (int j = 1; j < sentence.Length; j++)
                {
                    if (!UiTextRules.Fits(sentence.Substring(0, j + 1), UiStyle.Small, width))
                    {
                        break;
                    }
                    cut = j + 1;
                }
                int space = sentence.LastIndexOf(' ', System.Math.Min(cut, sentence.Length - 1));
                if (space > cut / 2)
                {
                    cut = space;   // between words, where that keeps most of the line
                }
                lines.Add(new UiConsequence { Text = sentence.Substring(0, cut).Trim() });
                sentence = sentence.Substring(cut).Trim();
            }
            return sentence;
        }

        // Who speaks: the owner's speaker, else the first note's.
        private static string Speaker(ScenePage page)
        {
            string named = Clean(page.Speaker);
            if (named != null)
            {
                return named;
            }
            for (int i = 0; i < page.Notes.Count; i++)
            {
                string name = Clean(page.Notes[i].Name);
                if (name != null)
                {
                    return name;
                }
            }
            return null;
        }

        // Those present as the owner named them, else the one who speaks; none for a moment about no one in particular. Who
        // each is and their ring come with them where the owner told any (null otherwise).
        private static string[] Faces(ScenePage page, string speaker, out string[] roles, out UiRole[] rings)
        {
            var faces = new List<string>(4);
            var who = new List<string>(4);
            var ring = new List<UiRole>(4);
            bool told = false;
            for (int i = 0; page.Faces != null && i < page.Faces.Length; i++)
            {
                string name = Clean(page.Faces[i]);
                if (name != null && !faces.Contains(name))
                {
                    faces.Add(name);
                    string role = page.Roles != null && i < page.Roles.Length ? Clean(page.Roles[i]) : null;
                    told |= role != null;
                    who.Add(role ?? string.Empty);
                    ring.Add(page.Rings != null && i < page.Rings.Length ? page.Rings[i] : UiRole.None);
                }
            }
            if (faces.Count == 0 && speaker != null)
            {
                faces.Add(speaker);
            }
            roles = told ? who.ToArray() : null;
            rings = told ? ring.ToArray() : null;
            return faces.ToArray();
        }

        // "4 present", under the faces; none where the words are not given.
        private static string Present(SceneWords words, int count) =>
            string.IsNullOrEmpty(words.Present) ? null : words.Present.Replace("{count}", count.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// An answer's line in the game's own list, where the kit is off: its meaning as the owner wrote it, then what it
        /// costs and the time it takes on a line of their own. The meaning alone where there is nothing more to say.
        /// </summary>
        public static string Joined(string detail, UiConsequence[] lines, string note)
        {
            string costs = null;
            for (int i = 0; lines != null && i < lines.Length; i++)
            {
                string text = Clean(lines[i].Text);
                if (text != null)
                {
                    costs = costs == null ? text : costs + " \u00b7 " + text;
                }
            }
            string time = Clean(note);
            if (time != null)
            {
                costs = costs == null ? time : costs + " \u00b7 " + time;
            }
            return costs == null ? detail : Clean(detail) == null ? costs : detail + "\n" + costs;
        }

        // The meaning's lines with what the owner told the answer costs after them.
        private static UiConsequence[] With(UiConsequence[] meaning, UiConsequence[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                return meaning;
            }
            var all = new List<UiConsequence>((meaning?.Length ?? 0) + lines.Length);
            if (meaning != null)
            {
                all.AddRange(meaning);
            }
            for (int i = 0; i < lines.Length; i++)
            {
                string text = Clean(lines[i].Text);
                if (text != null)
                {
                    all.Add(new UiConsequence { Text = text, Tone = lines[i].Tone, Change = lines[i].Change });
                }
            }
            return all.ToArray();
        }

        private static UiDecisionChoice[] Choices(ScenePage page, float lineWidth)
        {
            var choices = new UiDecisionChoice[page.Entries.Count];
            for (int i = 0; i < choices.Length; i++)
            {
                SceneEntry e = page.Entries[i];
                string said = Clean(e.Said);
                string name = Clean(e.Name) ?? string.Empty, detail = e.Detail;
                if (said != null && said == Clean(e.Detail))
                {
                    // An answer the player says ("Answer 1" over its words): its words are its name, the rest of a long one under it.
                    name = Head(said, lineWidth, out string rest);
                    detail = rest;
                }
                choices[i] = new UiDecisionChoice
                {
                    Key = i < 9 ? (UiAction)((int)UiAction.Pick1 + i) : UiAction.Count,
                    Name = name,
                    Lines = With(Meaning(detail, lineWidth, said != null ? SaidMost : MeaningMost), e.Lines),
                    Note = Clean(e.Note),
                    Disabled = !e.Enabled
                };
            }
            return choices;
        }

        // As much of an answer's words as its name's line holds (its key's cap beside it), broken between words; the rest
        // after it, null for none.
        private static string Head(string words, float width, out string rest)
        {
            rest = null;
            width -= UiTokens.S10;   // the cap of F or P at the line's end
            if (UiTextRules.Fits(words, UiStyle.BodyStrong, width))
            {
                return words;
            }
            int cut = 1;
            for (int j = 1; j < words.Length; j++)
            {
                if (!UiTextRules.Fits(words.Substring(0, j + 1), UiStyle.BodyStrong, width))
                {
                    break;
                }
                cut = j + 1;
            }
            int space = words.LastIndexOf(' ', System.Math.Min(cut, words.Length - 1));
            if (space > cut / 2)
            {
                cut = space;
            }
            rest = Clean(words.Substring(cut));
            return words.Substring(0, cut).Trim();
        }

        // A scene the player may not leave keeps the cancel key for the game, whose own menu it opens (the window leaves
        // it so when locked): the bar says so, and only the answers go on.
        private static UiPrompt[] Prompts(ScenePage page, SceneWords words) => new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = words.Move },
            new UiPrompt { Action = UiAction.Confirm, Label = words.Choose },
            new UiPrompt { Action = UiAction.Back, Label = page.Locked ? words.Menu : words.Back },
        };

        // The line under the headline as the card's kicker: the owner told none, and it is one short line.
        private static bool Kicks(ScenePage page, string said) =>
            page.Look != SceneLook.Talk && Clean(page.Kicker) == null && said != null && said.Length <= KickerMost && said.IndexOf('\n') < 0;

        private static string Join(string a, string b) => a == null ? b : b == null ? a : a + "\n\n" + b;

        private static int Lines(string words) => string.IsNullOrEmpty(words) ? 0 : UiTextRules.Lines(words, UiStyle.Body, CardWords, 0, out float _);

        // The words as the kit sets them: the list's rich text taken off, blank as none.
        private static string Clean(string words)
        {
            string plain = ContactsRules.Plain(words);
            if (plain == null)
            {
                return null;
            }
            plain = plain.Trim();
            return plain.Length == 0 ? null : plain;
        }
    }
}
