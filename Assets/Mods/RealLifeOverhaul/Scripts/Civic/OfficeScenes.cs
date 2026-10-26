using System;
using System.Text;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The scenes of the player's run and office in <see cref="OfficePanel"/>: a door, their rally, the debate, result night and
    /// the council's meetings. The timed ones begin on their hour once the player is free. Ticked once a second; idle otherwise.
    /// </summary>
    public sealed class OfficeScenes
    {
        private enum Scene { None, Door, Rally, Debate, Result, Meeting }

        private readonly CivicOffice _office;
        private readonly CivicGovernment _gov;
        private readonly CivicCity _city;
        private readonly OfficePanel _panel = new OfficePanel();
        private readonly StringBuilder _text = new StringBuilder(512);
        private Scene _scene;
        private int _step;
        private float _gain;

        private int _doorBuilding;
        private string _doorDistrict;
        private int _doorIssue, _doorMood;

        private int _listeners;
        private OfficeAnswer _rallyOpen;
        private bool _rallyPlank;
        private Action<bool> _rallyOver;

        private readonly int[] _questions = new int[2];
        private AttackKind _debateAttack;
        private bool _debateTrue;

        private int _motion;
        private int _passed;
        private string _last;

        public OfficeScenes(CivicOffice office, CivicGovernment government, CivicCity city)
        {
            _office = office;
            _gov = government;
            _city = city;
        }

        public bool Active => _scene != Scene.None;

        private static int Day => TimeHelper.CurrentDay;

        private OfficeState S => _office.State;

        public void Tick(int day, int hour)
        {
            _panel.Watch();
            if (_scene != Scene.None && !_panel.Active)
            {
                Over(false);   // the window went with the game's interface
            }
            if (_scene != Scene.None || !CivicOffice.Enabled)
            {
                return;
            }
            GovState g = _gov.State;
            if (S.Result != 0 && !S.ResultShown)
            {
                bool tonight = (day == S.ResultDay && hour >= CivicCatalogue.ResultHour) || (day == S.ResultDay + 1 && hour < 3);
                if (!tonight && (day > S.ResultDay + 1 || (day == S.ResultDay + 1 && hour >= 3)))
                {
                    _office.ResultSeen();   // result night went by: the City Hall contact still has the count
                }
                else if (tonight && Free())
                {
                    BeginResult();
                    return;
                }
            }
            if (_office.Campaigning && S.Debate == 0 && day == g.ElectionDay - CivicCatalogue.DebateBefore)
            {
                if (hour >= 22)
                {
                    _office.Debated(OfficeRules.EmptyChair, false, day);
                    CivicText.Toast(NotificationType.Warning, "reallife_civic_office_debate_missed", 12f);
                }
                else if (hour >= 19 && Free())
                {
                    BeginDebate();
                    return;
                }
            }
            if (_office.MeetingDue(day, hour) && Free())
            {
                BeginMeeting();
            }
        }

        /// <summary>Free for a scene: nothing of the game's or the mod's holds the player, not at the wheel, no list window open.</summary>
        public static bool Free()
        {
            return LifeEvents.IsSafeForEvent() && !Wiring.Scenes.PlayerHeld && !PlayerHelper.IsUsingVehicle && !GameUi.TravelList.AnyOpen && !ThirdPersonCamera.IsUiOpen();
        }

        public void Clear()
        {
            if (_scene != Scene.None)
            {
                Over(false);
            }
        }

        public void Destroy()
        {
            Clear();
            _panel.Destroy();
        }

        private bool Begin(Scene scene)
        {
            if (_scene != Scene.None || !_panel.Begin())
            {
                return false;
            }
            _scene = scene;
            _step = 0;
            _gain = 0f;
            return true;
        }

        private void Over(bool finished)
        {
            Scene was = _scene;
            _scene = Scene.None;
            if (_panel.Active)
            {
                _panel.End();
            }
            switch (was)
            {
                case Scene.Debate:
                    if (S.Debate == 0)
                    {
                        _office.Debated(finished ? _gain * OfficeRules.DebateReach(Recognition) : OfficeRules.EmptyChair * 0.5f, finished, Day);
                    }
                    break;
                case Scene.Result:
                    _office.ResultSeen();
                    break;
                case Scene.Meeting:
                    // Whatever was not voted on is decided without the player.
                    for (int i = _motion; i < S.Agenda.Count; i++)
                    {
                        _office.Decide(S.Agenda[i], -1, false, Day);
                    }
                    _office.MeetingHeld(Day);
                    break;
                case Scene.Rally:
                    _rallyOver?.Invoke(finished);
                    _rallyOver = null;
                    break;
            }
        }

        private static int Recognition => CivicBridges.Ask(CivicBridges.Recognition, 0, 0, 100);

        private static string T(string key) => key.GetLocalization();

        private static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        private static string Issue(int issue) => T(CivicCatalogue.IssueNames[Mathf.Clamp(issue, 0, CivicCatalogue.Issues - 1)]);

        private static PolicyDef Plank(Candidate c, int issue)
        {
            for (int i = 0; i < c.Planks; i++)
            {
                PolicyDef p = CivicCatalogue.At(c.Platform[i]);
                if (p != null && (int)p.Issue == issue)
                {
                    return p;
                }
            }
            return null;
        }

        /// <summary>The player knocks on a door; somebody answers. False if no scene could be begun.</summary>
        public bool Door(int building, string district, string address)
        {
            if (!Begin(Scene.Door))
            {
                return false;
            }
            _doorBuilding = building;
            _doorDistrict = district;
            int day = Day;
            int roll = OfficeRules.Roll(_gov.State.Seed, day * 131 + building, 1601);
            // What they are worried about: the city's issues, leaned by their district.
            int d = _office.DistrictIndex(district);
            float total = 0f;
            float[] weight = new float[CivicCatalogue.Issues];
            for (int i = 0; i < weight.Length; i++)
            {
                weight[i] = _gov.Weight(i) * (d >= 0 ? _gov.DistrictLean(d, i) : 1f);
                total += weight[i];
            }
            float pick = roll / 1000f * total;
            _doorIssue = 0;
            for (int i = 0; i < weight.Length; i++)
            {
                pick -= weight[i];
                if (pick <= 0f)
                {
                    _doorIssue = i;
                    break;
                }
            }
            int mood = OfficeRules.Roll(_gov.State.Seed, day * 137 + building, 1603);
            int friendly = 250 + Recognition * 3, hostile = 850 - (CivicBridges.Standing() < 0 ? 120 : 0);
            _doorMood = mood < friendly ? 2 : mood >= hostile ? 0 : 1;
            _office.State.LastDoorDay = day;
            DoorPage(address);
            return true;
        }

        private void DoorPage(string address)
        {
            Candidate me = S.Me;
            PolicyDef theirs = Plank(me, _doorIssue), lead = Plank(me, me.Lead);
            string who = T(_doorMood == 2 ? "reallife_civic_office_door_friendly" : _doorMood == 0 ? "reallife_civic_office_door_hostile" : "reallife_civic_office_door_civil");
            string body = Fill("reallife_civic_office_door_open", "who", who, "issue", Issue(_doorIssue));
            string[] names =
            {
                Fill("reallife_civic_office_door_talk_theirs", "issue", Issue(_doorIssue)),
                Fill("reallife_civic_office_door_talk_mine", "issue", Issue(me.Lead)),
                T(_office.Collecting ? "reallife_civic_office_door_ask_sign" : "reallife_civic_office_door_ask_vote")
            };
            string[] hints =
            {
                theirs != null ? Fill("reallife_civic_office_hint_plank", "policy", T(theirs.Name)) : T("reallife_civic_office_hint_no_plank"),
                lead != null ? Fill("reallife_civic_office_hint_plank", "policy", T(lead.Name)) : T("reallife_civic_office_hint_no_plank"),
                T("reallife_civic_office_hint_ask")
            };
            _panel.Show(T("reallife_civic_office_door_title"), address ?? string.Empty, T("reallife_civic_office_door_speaker"), body, names, hints, 3, DoorAnswer);
        }

        private void DoorAnswer(int option)
        {
            if (_scene != Scene.Door)
            {
                return;
            }
            OfficeAnswer answer = option == 0 ? OfficeAnswer.Plan : option == 1 ? OfficeAnswer.Pivot : OfficeAnswer.Ask;
            Candidate me = S.Me;
            int roll = OfficeRules.Roll(_gov.State.Seed, Day * 139 + _doorBuilding, 1605 + option);
            OfficeGain g = OfficeRules.Door(_doorMood, answer, Plank(me, _doorIssue) != null, Plank(me, me.Lead) != null, _office.Collecting, roll);
            _office.Knocked(_doorBuilding, _doorDistrict, g, Day);
            _text.Length = 0;
            _text.Append(T(g.Tone > 0 ? "reallife_civic_office_door_went_well" : g.Tone < 0 ? "reallife_civic_office_door_went_badly" : "reallife_civic_office_door_went_so"));
            if (g.Signatures > 0)
            {
                _text.Append('\n').Append(Fill("reallife_civic_office_door_signed", "count", g.Signatures.ToString(), "total", S.Signatures.ToString(),
                    "needed", OfficeRules.SignaturesNeeded(S.Race).ToString()));
            }
            if (g.Donation > 0)
            {
                _text.Append('\n').Append(Fill("reallife_civic_office_door_gave", "amount", CivicText.Money(Prices.At(g.Donation))));
            }
            _panel.Show(T("reallife_civic_office_door_title"), string.Empty, T("reallife_civic_office_door_speaker"), _text.ToString(),
                new[] { T("reallife_civic_office_leave") }, null, 1, _ => Over(true));
        }

        /// <summary>The player has reached their rally: a crowd of so many. When it is over, the stage is told (true: the speech was given).</summary>
        public bool Rally(int listeners, Action<bool> over)
        {
            if (!Begin(Scene.Rally))
            {
                return false;
            }
            _listeners = listeners;
            _rallyOver = over;
            int top = _gov.TopIssue();
            Candidate me = S.Me;
            Candidate leader = _office.RaceCandidate(_office.Leader());
            PolicyDef plank = Plank(me, top);
            string body = Fill("reallife_civic_office_rally_open", "count", listeners.ToString(), "issue", Issue(top));
            string[] names =
            {
                Fill("reallife_civic_office_rally_speak_city", "issue", Issue(top)),
                Fill("reallife_civic_office_rally_speak_mine", "issue", Issue(me.Lead)),
                Fill("reallife_civic_office_rally_speak_rival", "name", leader != null ? CivicText.Name(leader) : string.Empty)
            };
            string[] hints =
            {
                plank != null ? Fill("reallife_civic_office_hint_plank", "policy", T(plank.Name)) : T("reallife_civic_office_hint_no_plank"),
                T("reallife_civic_office_hint_pivot"),
                T("reallife_civic_office_hint_attack")
            };
            _panel.Show(T("reallife_civic_office_rally_title"), _office.DistrictName(_office.RaceDistrict()), CivicText.PlayerName(), body, names, hints, 3, RallyOpen);
            return true;
        }

        private void RallyOpen(int option)
        {
            if (_scene != Scene.Rally)
            {
                return;
            }
            _rallyOpen = option == 0 ? OfficeAnswer.Plan : option == 1 ? OfficeAnswer.Pivot : OfficeAnswer.Attack;
            _rallyPlank = Plank(S.Me, _gov.TopIssue()) != null;
            PolicyDef promise = S.Me.Planks > 0 ? CivicCatalogue.At(S.Me.Platform[0]) : null;
            string[] names =
            {
                promise != null ? Fill("reallife_civic_office_rally_promise", "policy", T(promise.Name)) : T("reallife_civic_office_rally_promise_none"),
                T("reallife_civic_office_rally_call")
            };
            _panel.Show(T("reallife_civic_office_rally_title"), string.Empty, CivicText.PlayerName(), T("reallife_civic_office_rally_close"), names,
                new[] { T("reallife_civic_office_hint_promise"), T("reallife_civic_office_hint_call") }, 2, RallyClose);
        }

        private void RallyClose(int option)
        {
            if (_scene != Scene.Rally)
            {
                return;
            }
            int roll = OfficeRules.Roll(_gov.State.Seed, Day, 1611);
            OfficeGain g = OfficeRules.Rally(_rallyOpen, _rallyPlank, option == 0 && S.Me.Planks > 0, _listeners, Recognition, roll);
            _office.Rallied(g, Day);
            _panel.Show(T("reallife_civic_office_rally_title"), string.Empty, CivicText.PlayerName(),
                T(g.Tone > 0 ? "reallife_civic_office_rally_went_well" : g.Tone < 0 ? "reallife_civic_office_rally_went_badly" : "reallife_civic_office_rally_went_so"),
                new[] { T("reallife_civic_office_leave") }, null, 1, _ => Over(true));
        }

        private void BeginDebate()
        {
            if (!Begin(Scene.Debate))
            {
                return;
            }
            Candidate me = S.Me;
            _questions[0] = _gov.TopIssue();
            // The second question: what matters most that the player has no plank on, else what matters next.
            int second = -1;
            float best = float.MinValue;
            for (int i = 0; i < CivicCatalogue.Issues; i++)
            {
                if (i == _questions[0])
                {
                    continue;
                }
                float w = _gov.Weight(i) + (Plank(me, i) == null ? 0.5f : 0f);
                if (w > best)
                {
                    best = w;
                    second = i;
                }
            }
            _questions[1] = Mathf.Max(0, second);
            _debateTrue = CivicBridges.HasRecord() || S.Dirty > 0 || S.Missed > 0;
            _debateAttack = CivicBridges.HasRecord() ? AttackKind.Record : S.Dirty > 0 ? AttackKind.Money : S.Missed > 0 ? AttackKind.Absent
                : S.InOffice ? AttackKind.Promises : AttackKind.Inexperience;
            if (_debateAttack == AttackKind.Promises)
            {
                _debateTrue = _office.Unkept(S.Holds == OfficeRace.Mayor ? _gov.State.Mayor : S.Held) > 0;
            }
            else if (_debateAttack == AttackKind.Inexperience)
            {
                _debateTrue = S.Wins == 0;
            }
            DebatePage();
        }

        private void DebatePage()
        {
            Candidate me = S.Me;
            Candidate rival = _office.RaceCandidate(_office.Leader());
            string rivalName = rival != null ? CivicText.Name(rival) : string.Empty;
            string title = T(S.Race == OfficeRace.Mayor ? "reallife_civic_office_debate_title_mayor" : "reallife_civic_office_debate_title_council");
            string progress = Fill("reallife_civic_office_debate_progress", "number", (_step + 1).ToString(), "of", "3");
            if (_step < 2)
            {
                int issue = _questions[_step];
                PolicyDef plank = Plank(me, issue);
                PolicyDef theirs = rival != null ? Plank(rival, issue) : null;
                string body = Fill(_step == 0 ? "reallife_civic_office_debate_q1" : "reallife_civic_office_debate_q2", "issue", Issue(issue));
                string[] names =
                {
                    plank != null ? Fill("reallife_civic_office_debate_plan", "policy", T(plank.Name)) : T("reallife_civic_office_debate_plan_none"),
                    Fill("reallife_civic_office_debate_pivot", "issue", Issue(me.Lead)),
                    Fill("reallife_civic_office_debate_attack", "name", rivalName)
                };
                string[] hints =
                {
                    plank != null ? T("reallife_civic_office_hint_debate_plan") : T("reallife_civic_office_hint_no_plank"),
                    T("reallife_civic_office_hint_pivot"),
                    theirs != null ? Fill("reallife_civic_office_hint_rival_plank", "name", rivalName, "policy", T(theirs.Name)) : Fill("reallife_civic_office_hint_rival_none", "name", rivalName)
                };
                _panel.Show(title, progress, T("reallife_civic_office_debate_host"), body, names, hints, 3, DebateAnswer);
                return;
            }
            if (_step == 2)
            {
                string body = Fill("reallife_civic_office_debate_q3", "name", rivalName, "what", T(CivicOffice.AttackKey(_debateAttack)));
                string[] names = { T("reallife_civic_office_debate_straight"), Fill("reallife_civic_office_debate_turn", "name", rivalName), T("reallife_civic_office_debate_dodge") };
                string[] hints = { T("reallife_civic_office_hint_straight"), T("reallife_civic_office_hint_turn"), T("reallife_civic_office_hint_dodge") };
                _panel.Show(title, progress, T("reallife_civic_office_debate_host"), body, names, hints, 3, DebateAnswer);
                return;
            }
            float gain = _gain * OfficeRules.DebateReach(Recognition);
            string end = T(gain >= 0.04f ? "reallife_civic_office_debate_end_good" : gain < 0f ? "reallife_civic_office_debate_end_bad" : "reallife_civic_office_debate_end_even");
            _panel.Show(title, string.Empty, T("reallife_civic_office_debate_host"), end, new[] { T("reallife_civic_office_leave") }, null, 1, _ => Over(true));
        }

        private void DebateAnswer(int option)
        {
            if (_scene != Scene.Debate || _step > 2)
            {
                return;
            }
            Candidate me = S.Me;
            Candidate rival = _office.RaceCandidate(_office.Leader());
            int roll = OfficeRules.Roll(_gov.State.Seed, Day, 1621 + _step * 3 + option);
            if (_step < 2)
            {
                int issue = _questions[_step];
                OfficeAnswer answer = option == 0 ? OfficeAnswer.Plan : option == 1 ? OfficeAnswer.Pivot : OfficeAnswer.Attack;
                _gain += OfficeRules.DebateAnswer(answer, Plank(me, issue) != null, rival != null && Plank(rival, issue) != null, false, roll);
            }
            else
            {
                OfficeAnswer answer = option == 0 ? OfficeAnswer.Straight : option == 1 ? OfficeAnswer.TurnBack : OfficeAnswer.Dodge;
                _gain += OfficeRules.DebateAnswer(answer, false, false, _debateTrue, roll);
            }
            _step++;
            DebatePage();
        }

        private void BeginResult()
        {
            if (!Begin(Scene.Result))
            {
                return;
            }
            ResultPage();
        }

        private void ResultPage()
        {
            GovState g = _gov.State;
            bool mayor = S.ResultRace == OfficeRace.Mayor;
            string title = T("reallife_civic_office_result_title");
            if (mayor && _step < g.DistrictsCounted && g.PlayerSlot >= 0 && _step < _city.Districts.Count)
            {
                // District by district: the player's votes against the best of the rest there.
                int d = _step;
                int mine = _gov.DistrictVotes(d, g.PlayerSlot), best = -1, bestVotes = -1;
                for (int c = 0; c < g.CandidateCount; c++)
                {
                    int v = _gov.DistrictVotes(d, c);
                    if (c != g.PlayerSlot && v > bestVotes)
                    {
                        bestVotes = v;
                        best = c;
                    }
                }
                string body = Fill(mine >= bestVotes ? "reallife_civic_office_result_district_ahead" : "reallife_civic_office_result_district_behind",
                    "district", CivicCity.Name(_city.Districts[d]), "votes", mine.ToString("N0"), "name", best >= 0 ? CivicText.Name(g.Candidates[best]) : string.Empty,
                    "theirs", Mathf.Max(0, bestVotes).ToString("N0"));
                _panel.Show(title, Fill("reallife_civic_office_result_progress", "number", (d + 1).ToString(), "of", g.DistrictsCounted.ToString()),
                    T("reallife_civic_office_result_speaker"), body, new[] { T("reallife_civic_office_result_next") }, null, 1, _ => { _step++; ResultPage(); });
                return;
            }
            if (!mayor && _step == 0)
            {
                _text.Length = 0;
                _text.Append(Fill("reallife_civic_office_result_council_head",
                    "district", _office.DistrictName(_office.DistrictIndex(S.District) >= 0 ? _office.DistrictIndex(S.District) : _office.HomeDistrict())));
                int last = Mathf.Min(S.RaceVotes.Length - 1, OfficeState.Rivals);
                for (int i = 0; i <= last; i++)
                {
                    if (i > 0 && i - 1 >= S.RivalCount && S.RaceVotes[i] == 0)
                    {
                        continue;
                    }
                    Candidate c = i == 0 ? null : S.Rival[i - 1];
                    _text.Append('\n').Append("- ").Append(c == null ? CivicText.PlayerName() : CivicText.Name(c)).Append(": ").Append(S.RaceVotes[i].ToString("N0"));
                }
                _panel.Show(title, string.Empty, T("reallife_civic_office_result_speaker"), _text.ToString(), new[] { T("reallife_civic_office_result_next") }, null, 1,
                    _ => { _step = 100; ResultPage(); });
                return;
            }
            bool won = S.Result == 1;
            string share = (S.ResultShare / 10f).ToString("0.#");
            string verdict = Fill(won ? (mayor ? "reallife_civic_office_result_won_mayor" : "reallife_civic_office_result_won_council") : "reallife_civic_office_result_lost", "share", share);
            string[] names = won ? new[] { T("reallife_civic_office_result_speech_thanks"), T("reallife_civic_office_result_speech_work") }
                : new[] { T("reallife_civic_office_result_speech_concede"), T("reallife_civic_office_result_speech_defiant") };
            _panel.Show(title, string.Empty, T("reallife_civic_office_result_speaker"), verdict, names, null, 2, option =>
            {
                if (!won)
                {
                    CivicBridges.Moved(option == 0 ? 1 : -1, 4);
                }
                else
                {
                    CivicBridges.Moved(0, option == 0 ? 4 : 6);
                }
                Over(true);
            });
        }

        private void BeginMeeting()
        {
            if (!Begin(Scene.Meeting))
            {
                return;
            }
            _motion = 0;
            _passed = 0;
            _last = null;
            if (S.Agenda.Count == 0)
            {
                _panel.Show(T("reallife_civic_office_meeting_title"), string.Empty, T("reallife_civic_office_meeting_speaker"), T("reallife_civic_office_meeting_nothing"),
                    new[] { T("reallife_civic_office_leave") }, null, 1, _ => Over(true));
                return;
            }
            _panel.Show(T("reallife_civic_office_meeting_title"), string.Empty, T("reallife_civic_office_meeting_speaker"),
                Fill("reallife_civic_office_meeting_open", "count", S.Agenda.Count.ToString()),
                new[] { T("reallife_civic_office_meeting_take_seat"), T("reallife_civic_office_meeting_apologies") }, null, 2, option =>
                {
                    if (option == 1)
                    {
                        // Apologies sent: the council decides without the player, as when they do not come.
                        _scene = Scene.None;
                        _panel.End();
                        _office.Skip(Day);
                        return;
                    }
                    MotionPage();
                });
        }

        private void MotionPage()
        {
            if (_motion >= S.Agenda.Count)
            {
                string end = Fill("reallife_civic_office_meeting_end", "count", S.Agenda.Count.ToString(), "passed", _passed.ToString());
                _panel.Show(T("reallife_civic_office_meeting_title"), string.Empty, T("reallife_civic_office_meeting_speaker"), (_last != null ? _last + "\n" : string.Empty) + end,
                    new[] { T("reallife_civic_office_leave") }, null, 1, _ => { _motion = S.Agenda.Count; Over(true); });
                return;
            }
            OfficeMotion m = S.Agenda[_motion];
            bool mayor = S.Holds == OfficeRace.Mayor;
            bool conflicted = _office.Conflicted(m);
            _text.Length = 0;
            if (_last != null)
            {
                _text.Append(_last).Append('\n');
            }
            if (m.Zoning)
            {
                _text.Append(Fill("reallife_civic_office_meeting_zoning", "matter", CivicOffice.MotionWords(m), "by", T(m.By == OfficeMotion.ByPlayer ? "reallife_civic_office_by_you"
                    : m.By == OfficeMotion.ByApplicant ? "reallife_civic_office_by_applicant" : "reallife_civic_office_by_council")));
                if (conflicted && !mayor)
                {
                    _text.Append('\n').Append(T("reallife_civic_office_meeting_conflict"));
                }
            }
            else if (m.Kind == OfficeMotion.KindBudget)
            {
                BudgetWeek week = OfficeRules.Week(_gov.Electorate(), _gov.State.Levers, m.Budget);
                _text.Append(Fill("reallife_civic_office_meeting_budget", "levels", CivicOffice.Levels(m.Budget), "revenue", CivicText.Money(Prices.At(week.Revenue)),
                    "spending", CivicText.Money(Prices.At(week.Spending)), "reserve", CivicText.Money(Prices.At((int)Mathf.Clamp(S.Reserve, int.MinValue / 2, int.MaxValue / 2)))));
            }
            else
            {
                PolicyDef p = CivicCatalogue.At(m.Policy);
                _text.Append(Fill("reallife_civic_office_meeting_motion", "policy", p != null ? T(p.Name) : string.Empty, "effect", CivicText.Full(p),
                    "by", T(m.By == OfficeMotion.ByPlayer ? "reallife_civic_office_by_you" : m.By == OfficeMotion.ByMayor ? "reallife_civic_office_by_mayor"
                        : m.By == OfficeMotion.ByPetition ? "reallife_civic_office_by_petition" : "reallife_civic_office_by_council")));
                if (conflicted && !mayor)
                {
                    _text.Append('\n').Append(T("reallife_civic_office_meeting_conflict"));
                }
            }
            string progress = Fill("reallife_civic_office_meeting_progress", "number", (_motion + 1).ToString(), "of", S.Agenda.Count.ToString());
            string[] names, hints;
            if (mayor)
            {
                names = new[] { T("reallife_civic_office_meeting_speak_for"), T("reallife_civic_office_meeting_speak_against"), T("reallife_civic_office_meeting_silent") };
                hints = new[] { T("reallife_civic_office_hint_mayor_word"), T("reallife_civic_office_hint_mayor_word"), null };
            }
            else if (conflicted)
            {
                names = new[] { T("reallife_civic_office_meeting_aye"), T("reallife_civic_office_meeting_no"), T("reallife_civic_office_meeting_declare") };
                hints = new[] { T("reallife_civic_office_hint_undeclared"), T("reallife_civic_office_hint_undeclared"), T("reallife_civic_office_hint_declare") };
            }
            else
            {
                names = new[] { T("reallife_civic_office_meeting_aye"), T("reallife_civic_office_meeting_no"), T("reallife_civic_office_meeting_abstain") };
                hints = null;
            }
            _panel.Show(T("reallife_civic_office_meeting_title"), progress, T("reallife_civic_office_meeting_speaker"), _text.ToString(), names, hints, 3, Voted);
        }

        private void Voted(int option)
        {
            if (_scene != Scene.Meeting || _motion >= S.Agenda.Count)
            {
                return;
            }
            OfficeMotion m = S.Agenda[_motion];
            bool mayor = S.Holds == OfficeRace.Mayor;
            bool conflicted = _office.Conflicted(m);
            MotionVote result;
            if (mayor)
            {
                result = _office.Decide(m, -1, false, Day, option == 0 ? 1 : option == 1 ? -1 : 0);
            }
            else
            {
                int vote = option == 0 ? 1 : option == 1 ? 0 : 2;
                result = _office.Decide(m, vote, conflicted && option == 2, Day);
            }
            _passed += result.Passed ? 1 : 0;
            _last = Fill(result.Passed ? "reallife_civic_office_meeting_carried" : "reallife_civic_office_meeting_lost", "ayes", result.Ayes.ToString(), "noes", result.Noes.ToString());
            _motion++;
            MotionPage();
        }
    }
}
