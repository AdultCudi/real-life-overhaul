using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A line someone says in a scene (a note of the page), with who says it.</summary>
    public struct SceneNote
    {
        public string Name;
        public string Detail;
    }

    /// <summary>An answer a scene offers: its id for the page's owner, its words and what it means.</summary>
    public struct SceneEntry
    {
        public int Id;
        public string Name;
        public string Detail;
        public bool Enabled;
    }

    /// <summary>A line of a scene's conversation so far: what they said, or the answer the player gave.</summary>
    public struct SceneSaid
    {
        public UiFrom From;
        public string Text;
    }

    /// <summary>The words a scene's card takes from the game's and the mod's texts, so the views stay game-free.</summary>
    public struct SceneWords
    {
        public string Move, Choose, Back;
    }

    /// <summary>
    /// A scene's page as its owner wrote it into its list (a hearing, a fame or office scene, an interview's round, a
    /// pitch's question): the headline, the lines under it (what is said, and the progress the list writes small), the
    /// notes someone says, the answers, and what the owner told of the moment (its look, the place, those present).
    /// </summary>
    public sealed class ScenePage
    {
        public string Title;
        public string Info;
        public SceneLook Look;
        public string Kicker;                 // the place or the moment: "Court", "Your office"
        public UiIcon Emblem = UiIcon.People; // the moment's emblem where no faces are given
        public string[] Faces;                // those present, as the owner names them
        public bool Locked;                   // the player may not leave: no way out but an answer
        public readonly List<SceneNote> Notes = new List<SceneNote>(4);
        public readonly List<SceneEntry> Entries = new List<SceneEntry>(8);
        public int Stamp;                     // grows with every change, so a screen sees a new page by one number

        /// <summary>A new filling: the headline and its lines, the notes and answers gone, the moment as it was told.</summary>
        public void Reset(string title, string info)
        {
            Title = title;
            Info = info;
            Notes.Clear();
            Entries.Clear();
            Stamp++;
        }
    }

    /// <summary>
    /// A scene's page as the kit draws it: a moment (a verdict, a fame or office scene, the end of an interview or a
    /// pitch) as the decision card, with the faces of those present, what is said and the answers numbered with what each
    /// means; questions and answers (an interview's rounds, a pitch's questions) as the conversation docked at the right,
    /// its thread the lines so far. A page whose words outgrow the card's paragraph takes the conversation too.
    /// Game-free; checked offline (UiChecks.Dialog). Cost: a few small arrays a page.
    /// </summary>
    public static class SceneViews
    {
        /// <summary>The decision card's paragraph's width: the card's 1040 less its 296 of faces and two gutters of 32.</summary>
        public const float CardWords = 680f;

        /// <summary>Characters a line under the headline takes and still reads as the card's kicker; a longer one is said in its paragraph.</summary>
        public const int KickerMost = 48;

        /// <summary>What the page says: its notes' words, each speaker's line after the first named.</summary>
        public static string Body(ScenePage page)
        {
            string body = null;
            for (int i = 0; i < page.Notes.Count; i++)
            {
                string words = Clean(page.Notes[i].Detail);
                if (words == null)
                {
                    continue;
                }
                body = body == null ? words : body + "\n\n" + words;
            }
            return body;
        }

        /// <summary>
        /// What the lines under the headline say, and the progress written small under them. A page whose owner named its
        /// place or those present (its list's Scene) has told its lines that way: they are not said again.
        /// </summary>
        public static string SaidOf(ScenePage page, out string progress)
        {
            DialogRules.SplitInfo(page.Info, out string said, out progress);
            bool told = page.Kicker != null || (page.Faces != null && page.Faces.Length > 0);
            return told ? null : said;
        }

        /// <summary>The look the page takes: the owner's, or the conversation where the card's paragraph cannot hold it.</summary>
        public static SceneLook LookOf(ScenePage page)
        {
            string said = SaidOf(page, out string _);
            int lines = Lines(Body(page)) + (Kicks(page, said) ? 0 : Lines(said));
            return DialogRules.Look(page.Look == SceneLook.Talk, lines);
        }

        /// <summary>The page as the decision card.</summary>
        public static UiDecisionModel Decision(ScenePage page, SceneWords words)
        {
            string said = SaidOf(page, out string _);
            string speaker = Speaker(page);
            bool kicks = Kicks(page, said);
            var m = new UiDecisionModel
            {
                Kicker = page.Kicker ?? (kicks ? said : null),
                Title = Clean(page.Title),
                Emblem = page.Emblem,
                EmblemTint = UiRole.Accent,
                Faces = Faces(page, speaker),
                EmblemLabel = speaker,
                Body = Join(kicks ? null : said, Body(page)),
                Choices = Choices(page, true),
                Buttons = page.Locked ? new UiActionModel[0] : new[] { new UiActionModel { Label = words.Back, Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiDecision.ButtonCommand } },
            };
            m.Prompts = Prompts(page, words);
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        /// <summary>The page as the conversation: the speaker at the head with the progress beside them, the thread so far, the answers.</summary>
        public static UiConversationModel Talk(ScenePage page, List<SceneSaid> thread, SceneWords words)
        {
            string said = SaidOf(page, out string progress);
            string speaker = Speaker(page) ?? Clean(page.Title);
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
                Name = speaker ?? string.Empty,
                Hue = UiColour.HueOf(speaker ?? string.Empty),
                Ring = UiRole.None,
                Role = page.Kicker,
                Status = progress,
                StatusTone = UiRole.Accent,
                Messages = lines.ToArray(),
                MessageCount = lines.Count,
                Choices = Choices(page, false),
            };
            m.Typing = m.Choices.Length == 0;
            m.Prompts = Prompts(page, words);
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        /// <summary>What the page says now, as the lines the thread takes once an answer is given.</summary>
        public static void Said(List<UiMessageModel> into, ScenePage page, string said)
        {
            if (!string.IsNullOrEmpty(said))
            {
                into.Add(new UiMessageModel { From = UiFrom.Them, Text = said });
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

        /// <summary>The answer an entry's id stands for, in its words; null for none.</summary>
        public static string Answer(ScenePage page, int id)
        {
            for (int i = 0; i < page.Entries.Count; i++)
            {
                if (page.Entries[i].Id == id)
                {
                    return Clean(page.Entries[i].Name);
                }
            }
            return null;
        }

        // Who speaks: the first note's speaker.
        private static string Speaker(ScenePage page)
        {
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

        // Those present as the owner named them, else the one who speaks; none for a moment about no one in particular.
        private static string[] Faces(ScenePage page, string speaker)
        {
            var faces = new List<string>(4);
            for (int i = 0; page.Faces != null && i < page.Faces.Length; i++)
            {
                string name = Clean(page.Faces[i]);
                if (name != null && !faces.Contains(name))
                {
                    faces.Add(name);
                }
            }
            if (faces.Count == 0 && speaker != null)
            {
                faces.Add(speaker);
            }
            return faces.ToArray();
        }

        private static UiDecisionChoice[] Choices(ScenePage page, bool keyed)
        {
            var choices = new UiDecisionChoice[page.Entries.Count];
            for (int i = 0; i < choices.Length; i++)
            {
                SceneEntry e = page.Entries[i];
                string detail = Clean(e.Detail);
                choices[i] = new UiDecisionChoice
                {
                    Key = keyed && i < 9 ? (UiAction)((int)UiAction.Pick1 + i) : UiAction.Count,
                    Name = Clean(e.Name) ?? string.Empty,
                    Lines = detail != null ? new[] { new UiConsequence { Text = detail } } : null,
                    Disabled = !e.Enabled
                };
            }
            return choices;
        }

        private static UiPrompt[] Prompts(ScenePage page, SceneWords words)
        {
            var prompts = new List<UiPrompt>(3)
            {
                new UiPrompt { Action = UiAction.Move, Label = words.Move },
                new UiPrompt { Action = UiAction.Confirm, Label = words.Choose },
            };
            if (!page.Locked)
            {
                prompts.Add(new UiPrompt { Action = UiAction.Back, Label = words.Back });
            }
            return prompts.ToArray();
        }

        // The line under the headline as the card's kicker: the owner told none, and it is one short line.
        private static bool Kicks(ScenePage page, string said) =>
            page.Kicker == null && said != null && said.Length <= KickerMost && said.IndexOf('\n') < 0;

        private static string Join(string a, string b) => a == null ? b : b == null ? a : a + "\n\n" + b;

        private static int Lines(string words) => string.IsNullOrEmpty(words) ? 0 : UiTextRules.Lines(words, UiStyle.Body, CardWords, 0, out float _);

        // The words as the kit sets them: the list's rich text taken off, the case it forces dropped, blank as none.
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
