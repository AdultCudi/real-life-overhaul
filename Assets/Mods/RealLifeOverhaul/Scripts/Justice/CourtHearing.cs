using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What a hearing came to, for the director to act on once the panel has closed.</summary>
    public sealed class HearingResult
    {
        public JusticeCase Case;
        public CaseOutcome Outcome;
        public JusticeSentence Sentence;
        public int FineDue;             // the fine less what was paid at the roadside
    }

    /// <summary>One hearing on the justice panel. Every chance comes from the case's seed, so a reload replays it the same way.</summary>
    public sealed class CourtHearing
    {
        private enum Beat { Officer, Witness, Camera, Flight, Character, Closing }

        private static readonly string[] Judges = { "reallife_justice_judge_1", "reallife_justice_judge_2", "reallife_justice_judge_3", "reallife_justice_judge_4" };
        private static readonly string[] Prosecutors = { "reallife_justice_prosecutor_1", "reallife_justice_prosecutor_2", "reallife_justice_prosecutor_3" };
        private static readonly string[] Calls = { "reallife_justice_court_call_1", "reallife_justice_court_call_2", "reallife_justice_court_call_3" };
        private static readonly string[] OfficerLines = { "reallife_justice_beat_officer_1", "reallife_justice_beat_officer_2" };
        private static readonly string[] WitnessLines = { "reallife_justice_beat_witness_1", "reallife_justice_beat_witness_2" };
        private static readonly string[] CameraLines = { "reallife_justice_beat_camera_1", "reallife_justice_beat_camera_2" };
        private static readonly string[] FlightLines = { "reallife_justice_beat_flight_1", "reallife_justice_beat_flight_2" };
        private static readonly string[] CharacterLines = { "reallife_justice_beat_character_1", "reallife_justice_beat_character_2" };
        private static readonly string[] ClosingLines = { "reallife_justice_beat_closing_1", "reallife_justice_beat_closing_2" };

        private readonly JusticeDirector _director;
        private readonly JusticeCase _case;
        private readonly bool _remote;
        private readonly int _day;
        private readonly int _priors;
        private readonly bool _probation;
        private readonly string _judge, _prosecutor;
        private readonly List<Beat> _beats = new List<Beat>(4);
        private readonly List<ChargeDef> _convicted = new List<ChargeDef>(4);
        private readonly StringBuilder _text = new StringBuilder(600);

        private float _quality;
        private int _beat;
        private int _choices;           // thousandths the accused's own answers added to (or took off) the case against them
        private float _mitigation;
        private float _factor = 1f;
        private bool _testified, _friendCalled, _footageShown;
        private bool _appointed;
        private string _friend;
        private HearingResult _result;

        public CourtHearing(JusticeDirector director, JusticeCase c, bool remote)
        {
            _director = director;
            _case = c;
            _remote = remote;
            _day = TimeHelper.CurrentDay;
            _priors = Justice.Priors;
            _probation = JusticeState.OnProbation(_day);
            _judge = JusticeText.T(Judges[JusticeLaw.Roll(c.Seed, 1) % Judges.Length]);
            _prosecutor = JusticeText.T(Prosecutors[JusticeLaw.Roll(c.Seed, 2) % Prosecutors.Length]);
        }

        public JusticeCase Case => _case;

        /// <summary>Set when the last page has been answered; null while the hearing is on or if it was cut short.</summary>
        public HearingResult Result => _result;

        private JusticePage Page(int speaker, string body)
        {
            var page = new JusticePage
            {
                Scene = JusticeScene.Court,
                Title = JusticeText.T(_remote ? "reallife_justice_court_title_remote" : "reallife_justice_court_title"),
                Subtitle = JusticeText.T("reallife_justice_court_subtitle", "number", JusticeText.N(_case.Number), "day", JusticeText.N(_day)),
                Left = JusticeFace.Prosecutor, LeftName = _prosecutor,
                Middle = JusticeFace.Judge, MiddleName = _judge,
                Right = JusticeState.Lawyer == LawyerKind.Self || JusticeState.Lawyer == LawyerKind.None ? JusticeFace.None : JusticeFace.Counsel,
                RightName = LegalCounsel.Name(),
                Speaker = speaker,
                SpeakerName = speaker == 0 ? _prosecutor : speaker == 1 ? _judge : speaker == 2 ? LegalCounsel.Name() : string.Empty,
                Body = body
            };
            if (page.Right == JusticeFace.None && speaker == 2)
            {
                page.Speaker = -1;
                page.SpeakerName = JusticeText.T("reallife_justice_court_you");
            }
            return page;
        }

        private static string Continue => JusticeText.T("reallife_justice_continue");

        /// <summary>The first page. The director has already paused the game and holds the player.</summary>
        public void Begin()
        {
            if (JusticeState.Lawyer == LawyerKind.None)
            {
                LegalCounsel.RetainSimple(LawyerKind.PublicDefender, JusticeDirector.Logger);   // nobody faces a court alone unless they chose to
                _appointed = true;
            }
            _quality = LegalCounsel.Quality(_case);
            _text.Length = 0;
            _text.Append(JusticeText.T(Calls[JusticeLaw.Roll(_case.Seed, 3) % Calls.Length], "number", JusticeText.N(_case.Number)));
            if (_case.Has(JusticeLaw.NoShow))
            {
                _text.Append("\n\n").Append(JusticeText.T("reallife_justice_court_call_warrant"));
            }
            if (_appointed)
            {
                _text.Append("\n\n").Append(JusticeText.T("reallife_justice_court_appointed"));
            }
            JusticePanel.Instance.Show(Page(1, _text.ToString()).Add(Continue), _ => Charges());
        }

        private void Charges()
        {
            var withdrawn = new List<string>(2);
            if (!_case.Reviewed)
            {
                _case.Reviewed = true;
                for (int i = 0; i < _case.Charges.Count; i++)
                {
                    JusticeCharge charge = _case.Charges[i];
                    if (!charge.Dropped && charge.Evidence < JusticeLaw.DismissBelow)
                    {
                        charge.Dropped = true;
                        withdrawn.Add(JusticeText.Charge(charge.Id));
                    }
                }
            }
            if (_case.Standing == 0)
            {
                Dismissed();
                return;
            }
            _text.Length = 0;
            _text.Append(JusticeText.T("reallife_justice_court_charges")).Append('\n');
            for (int i = 0; i < _case.Charges.Count; i++)
            {
                JusticeCharge charge = _case.Charges[i];
                ChargeDef def = JusticeLaw.Find(charge.Id);
                if (charge.Dropped || def == null)
                {
                    continue;
                }
                _text.Append("\n  - ").Append(JusticeText.T(def.NameKey)).Append("  (").Append(JusticeText.T(ClassKey(def.Class))).Append(')');
            }
            if (withdrawn.Count > 0)
            {
                _text.Append("\n\n").Append(JusticeText.T("reallife_justice_court_withdrawn", "charges", string.Join(", ", withdrawn)));
            }
            if (_probation)
            {
                _text.Append("\n\n").Append(JusticeText.T("reallife_justice_court_on_probation"));
            }
            JusticePanel.Instance.Show(Page(0, _text.ToString()).Add(Continue), _ => Plea());
        }

        private static string ClassKey(ChargeClass weight)
        {
            return weight == ChargeClass.Grave ? "reallife_justice_class_grave"
                : weight == ChargeClass.Felony ? "reallife_justice_class_felony" : "reallife_justice_class_misdemeanour";
        }

        private void Plea()
        {
            JusticeCharge main = _case.Main;
            float against = JusticeLaw.CaseAgainst(main.Evidence, _priors, _quality, 0) / 1000f;
            BuildBeats();
            _text.Length = 0;
            _text.Append(JusticeText.T("reallife_justice_court_plea"));
            _text.Append("\n\n").Append(JusticeText.T("reallife_justice_court_aside", "charge", JusticeText.Charge(main.Id), "percent", JusticeText.Percent(against)));
            bool deal = _case.DealReady || _case.Remand;   // brought up from the cells with no chance to ring anybody: counsel asks at the door
            JusticePage page = Page(1, _text.ToString())
                .Add(JusticeText.T("reallife_justice_plea_guilty"), JusticeText.T("reallife_justice_plea_guilty_hint"))
                .Add(JusticeText.T("reallife_justice_plea_not_guilty"),
                    JusticeText.T("reallife_justice_plea_not_guilty_hint", "rounds", JusticeText.N(_beats.Count), "percent", JusticeText.Percent(against)));
            if (deal)
            {
                page.Add(JusticeText.T("reallife_justice_plea_deal"), LegalCounsel.DealText(_case, _quality));
            }
            JusticePanel.Instance.Show(page, choice =>
            {
                if (choice == 0)
                {
                    _factor = JusticeLaw.GuiltyPleaFactor;
                    ConvictAll();
                    Sentence(CaseOutcome.Guilty, JusticeText.T("reallife_justice_verdict_guilty_plea"));
                }
                else if (choice == 2)
                {
                    TakeDeal();
                }
                else
                {
                    _beat = 0;
                    ShowBeat();
                }
            });
        }

        private void ConvictAll()
        {
            _convicted.Clear();
            for (int i = 0; i < _case.Charges.Count; i++)
            {
                JusticeCharge charge = _case.Charges[i];
                ChargeDef def = charge.Dropped ? null : JusticeLaw.Find(charge.Id);
                charge.Convicted = def != null;
                if (def != null)
                {
                    _convicted.Add(def);
                }
            }
        }

        private void TakeDeal()
        {
            var drop = new List<JusticeCharge>(4);
            string terms = LegalCounsel.DealText(_case, _quality);
            LegalCounsel.DealPlan(_case, _quality, drop, out JusticeCharge main, out string lesser);
            for (int i = 0; i < drop.Count; i++)
            {
                drop[i].Dropped = true;
            }
            if (lesser != null && main != null)
            {
                main.Id = lesser;
                main.Reduced = true;
            }
            _factor = JusticeLaw.DealFactor;
            ConvictAll();
            Sentence(CaseOutcome.Deal, JusticeText.T("reallife_justice_verdict_deal", "terms", terms));
        }

        private void BuildBeats()
        {
            _beats.Clear();
            _beats.Add(Beat.Officer);
            if (_case.Witnesses > 0)
            {
                _beats.Add(Beat.Witness);
            }
            if (_case.Cameras)
            {
                _beats.Add(Beat.Camera);
            }
            if (_case.Fled && _beats.Count < 3)
            {
                _beats.Add(Beat.Flight);
            }
            if (_beats.Count < 2)
            {
                _beats.Add(Beat.Character);
            }
            _beats.Add(Beat.Closing);
        }

        private static string[] LinesOf(Beat beat)
        {
            switch (beat)
            {
                case Beat.Witness: return WitnessLines;
                case Beat.Camera: return CameraLines;
                case Beat.Flight: return FlightLines;
                case Beat.Character: return CharacterLines;
                case Beat.Closing: return ClosingLines;
                default: return OfficerLines;
            }
        }

        private bool StrongCase => _case.Main != null && _case.Main.Evidence >= 0.6f;

        private void ShowBeat()
        {
            Beat beat = _beats[_beat];
            string[] lines = LinesOf(beat);
            _text.Length = 0;
            _text.Append(JusticeText.T("reallife_justice_beat_round", "round", JusticeText.N(_beat + 1), "rounds", JusticeText.N(_beats.Count))).Append("\n\n");
            _text.Append(JusticeText.T(lines[JusticeLaw.Roll(_case.Seed, 10 + _beat) % lines.Length]));
            JusticePage page = Page(beat == Beat.Closing ? 1 : 0, _text.ToString());
            if (beat == Beat.Closing)
            {
                page.Add(JusticeText.T("reallife_justice_close_speak"),
                        JusticeText.T(StrongCase ? "reallife_justice_close_speak_hint_strong" : "reallife_justice_close_speak_hint_weak"))
                    .Add(JusticeText.T("reallife_justice_close_counsel"),
                        JusticeText.T("reallife_justice_close_counsel_hint", "points", JusticeText.N(Mathf.RoundToInt(_quality * 8f))))
                    .Add(JusticeText.T("reallife_justice_close_nothing"), JusticeText.T("reallife_justice_close_nothing_hint"));
                JusticePanel.Instance.Show(page, Closing);
                return;
            }
            // The first answer testifies, once; after that it objects. The third is whatever the court has not yet seen.
            if (!_testified)
            {
                page.Add(JusticeText.T("reallife_justice_answer_testify"),
                    JusticeText.T(StrongCase ? "reallife_justice_answer_testify_hint_strong" : "reallife_justice_answer_testify_hint_weak"));
            }
            else
            {
                page.Add(JusticeText.T("reallife_justice_answer_object"), JusticeText.T("reallife_justice_answer_object_hint", "percent", JusticeText.Percent(_quality)));
            }
            page.Add(JusticeText.T("reallife_justice_answer_silent"),
                JusticeText.T("reallife_justice_answer_silent_hint", "points", JusticeText.N(Mathf.RoundToInt(_quality * 6f))));
            int special = Special();
            if (special == 1)
            {
                page.Add(JusticeText.T("reallife_justice_answer_friend", "name", _friend), JusticeText.T("reallife_justice_answer_friend_hint"));
            }
            else if (special == 2)
            {
                page.Add(JusticeText.T("reallife_justice_answer_footage"),
                    JusticeText.T(_case.InTheAct ? "reallife_justice_answer_footage_hint_act" : "reallife_justice_answer_footage_hint"));
            }
            JusticePanel.Instance.Show(page, choice => Answer(choice, special));
        }

        // 1: a friend as character witness; 2: the footage and receipts of a business of the player's with cameras; 0: nothing.
        private int Special()
        {
            if (!_friendCalled)
            {
                try
                {
                    if (Justice.Bridges.Social.CloseFriend(out _friend) && !string.IsNullOrEmpty(_friend))
                    {
                        return 1;
                    }
                }
                catch (System.Exception)
                {
                    // a bridge that throws has no friend to offer
                }
            }
            return !_footageShown && OwnCameras() ? 2 : 0;
        }

        private static bool OwnCameras()
        {
            var save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null)
            {
                return false;
            }
            foreach (BuildingRegistration registration in save.BuildingRegistrations)
            {
                if (registration != null && GoingPublic.Mine(registration) && registration.securityLevelPercentage > 0f)
                {
                    return true;
                }
            }
            return false;
        }

        private void Answer(int choice, int special)
        {
            int change;
            string outcome;
            if (choice == 0 && !_testified)
            {
                _testified = true;
                change = StrongCase ? 60 : -80;
                outcome = JusticeText.T(StrongCase ? "reallife_justice_result_testify_bad" : "reallife_justice_result_testify_good");
            }
            else if (choice == 0)
            {
                // Sustained as often as counsel is good.
                bool sustained = JusticeLaw.Roll(_case.Seed, 40 + _beat) < Mathf.RoundToInt(_quality * 1000f);
                change = sustained ? -40 : 10;
                outcome = JusticeText.T(sustained ? "reallife_justice_result_object_sustained" : "reallife_justice_result_object_overruled");
            }
            else if (choice == 2 && special == 1)
            {
                _friendCalled = true;
                change = -80;
                outcome = JusticeText.T("reallife_justice_result_friend", "name", _friend);
                try
                {
                    Justice.Bridges.Social.Stood(_friend, false);
                }
                catch (System.Exception e)
                {
                    JusticeDirector.Logger?.Error(e);
                }
            }
            else if (choice == 2 && special == 2)
            {
                _footageShown = true;
                change = _case.InTheAct ? 0 : -100;
                outcome = JusticeText.T(_case.InTheAct ? "reallife_justice_result_footage_nothing" : "reallife_justice_result_footage_good");
            }
            else
            {
                change = -Mathf.RoundToInt(_quality * 60f);
                outcome = JusticeText.T(_quality >= 0.45f ? "reallife_justice_result_silent_good" : "reallife_justice_result_silent_plain");
            }
            AfterAnswer(change, outcome);
        }

        private void Closing(int choice)
        {
            int change = 0;
            string outcome;
            if (choice == 0)
            {
                if (StrongCase)
                {
                    change = -20;
                    _mitigation += 0.10f;
                    outcome = JusticeText.T("reallife_justice_result_close_remorse");
                }
                else
                {
                    change = 30;
                    outcome = JusticeText.T("reallife_justice_result_close_admission");
                }
            }
            else if (choice == 1)
            {
                change = -Mathf.RoundToInt(_quality * 80f);
                outcome = JusticeText.T("reallife_justice_result_close_counsel");
            }
            else
            {
                outcome = JusticeText.T("reallife_justice_result_close_nothing");
            }
            AfterAnswer(change, outcome);
        }

        private void AfterAnswer(int change, string outcome)
        {
            _choices += change;
            float against = JusticeLaw.CaseAgainst(_case.Main.Evidence, _priors, _quality, _choices) / 1000f;
            _text.Length = 0;
            _text.Append(outcome).Append("\n\n");
            _text.Append(JusticeText.T(change < 0 ? "reallife_justice_result_down" : change > 0 ? "reallife_justice_result_up" : "reallife_justice_result_same",
                "points", JusticeText.N(Mathf.Abs(change) / 10), "percent", JusticeText.Percent(against)));
            JusticePanel.Instance.Show(Page(-1, _text.ToString()).Add(Continue), _ =>
            {
                if (++_beat < _beats.Count)
                {
                    ShowBeat();
                }
                else
                {
                    Verdict();
                }
            });
        }

        private void Verdict()
        {
            _convicted.Clear();
            _text.Length = 0;
            _text.Append(JusticeText.T("reallife_justice_verdict_sum", "record", JusticeText.Percent(JusticeLaw.RecordEffect(_priors)),
                "counsel", Signed(-Mathf.RoundToInt(JusticeLaw.CounselEffect(_quality) * 100f)), "choices", Signed(_choices / 10)));
            _text.Append('\n');
            for (int i = 0; i < _case.Charges.Count; i++)
            {
                JusticeCharge charge = _case.Charges[i];
                ChargeDef def = charge.Dropped ? null : JusticeLaw.Find(charge.Id);
                if (def == null)
                {
                    continue;
                }
                int against = JusticeLaw.CaseAgainst(charge.Evidence, _priors, _quality, _choices);
                int doubt = JusticeLaw.Roll(_case.Seed, 100 + i);
                charge.Convicted = doubt < against;
                if (charge.Convicted)
                {
                    _convicted.Add(def);
                }
                _text.Append('\n').Append(JusticeText.T(charge.Convicted ? "reallife_justice_verdict_line_guilty" : "reallife_justice_verdict_line_not_guilty",
                        "charge", JusticeText.T(def.NameKey), "evidence", JusticeText.Percent(charge.Evidence), "against", JusticeText.N(against / 10))
                    .Replace("{doubt}", JusticeText.N(doubt / 10)));
            }
            _text.Append("\n\n").Append(JusticeText.T("reallife_justice_verdict_rule"));
            string explanation = _text.ToString();
            if (_convicted.Count == 0)
            {
                _result = new HearingResult { Case = _case, Outcome = CaseOutcome.Acquitted };
                JusticePanel.Instance.Show(Page(1, explanation + "\n\n" + JusticeText.T("reallife_justice_verdict_acquitted")).Add(Continue), _ => Finish());
                return;
            }
            _factor = 1f;
            Sentence(CaseOutcome.Convicted, explanation);
        }

        private static string Signed(int value)
        {
            return value > 0 ? "+" + JusticeText.N(value) : JusticeText.N(value);
        }

        private void Sentence(CaseOutcome outcome, string lead)
        {
            float mitigation = _mitigation + (_case.Complied ? 0.15f : 0f);
            float cash = Crime.Cash;
            JusticeSentence sentence = JusticeLaw.Sentence(_convicted, _factor, mitigation, _priors, _probation, JusticeSettings.PrisonCap, JusticeSettings.NoPrison, cash);
            if (sentence.CommunityHours > 0 && !JusticeSettings.Community)
            {
                // Community service switched off: the hours become money, inside the same bound.
                sentence.FineNominal += sentence.CommunityHours * 60;
                sentence.Fine = JusticeLaw.Bound(sentence.FineNominal, cash, JusticeLaw.FineShareOfCash, JusticeLaw.FineFloor);
                sentence.Bounded = sentence.Fine < sentence.FineNominal;
                sentence.CommunityHours = 0;
            }
            int fineDue = Mathf.Max(0, sentence.Fine - _case.PaidAtScene);
            int due = JusticeLaw.ConvictionDue(sentence.Fine, _case.PaidAtScene, _case.Damages);
            // Hours already sat in a cell for this, after the arrest or on remand, are time served.
            int served = Mathf.Min(sentence.JailHours, Mathf.Max(0, _case.HeldHours));
            sentence.JailHours -= served;
            _result = new HearingResult { Case = _case, Outcome = outcome, Sentence = sentence, FineDue = due };

            _text.Length = 0;
            _text.Append(lead).Append("\n\n").Append(JusticeText.T("reallife_justice_sentence_head")).Append('\n');
            _text.Append('\n').Append(JusticeText.T("reallife_justice_sentence_fine", "amount", JusticeText.Money(sentence.Fine)));
            if (sentence.Bounded)
            {
                _text.Append(' ').Append(JusticeText.T("reallife_justice_sentence_fine_bounded", "amount", JusticeText.Money(sentence.FineNominal)));
            }
            if (_case.PaidAtScene > 0)
            {
                _text.Append(' ').Append(JusticeText.T("reallife_justice_sentence_fine_credit", "amount", JusticeText.Money(Mathf.Min(_case.PaidAtScene, sentence.Fine)),
                    "due", JusticeText.Money(fineDue)));
            }
            if (_case.Damages > 0)
            {
                _text.Append(' ').Append(JusticeText.T("reallife_justice_sentence_damages", "amount", JusticeText.Money(_case.Damages), "due", JusticeText.Money(due)));
            }
            if (due > 0)
            {
                _text.Append(' ').Append(due <= cash * JusticeLaw.PayNowShare
                    ? JusticeText.T("reallife_justice_sentence_fine_now")
                    : JusticeText.T("reallife_justice_sentence_fine_plan", "amount", JusticeText.Money((due + JusticeLaw.FinePlanDays - 1) / JusticeLaw.FinePlanDays),
                        "days", JusticeText.N(JusticeLaw.FinePlanDays)));
            }
            if (sentence.PrisonDays > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_sentence_prison", "days", JusticeText.N(sentence.PrisonDays),
                    "cap", JusticeText.N(JusticeSettings.PrisonCap)));
            }
            if (sentence.PrisonCommuted)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_sentence_commuted"));
            }
            if (sentence.JailHours > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_sentence_jail", "hours", JusticeText.N(sentence.JailHours)));
            }
            if (served > 0)
            {
                _text.Append(sentence.JailHours > 0 ? ' ' : '\n').Append(JusticeText.T("reallife_justice_sentence_jail_served", "hours", JusticeText.N(served)));
            }
            if (sentence.CommunityHours > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_sentence_community", "hours", JusticeText.N(sentence.CommunityHours),
                    "days", JusticeText.N(JusticeLaw.CommunityDeadlineDays)));
                if (sentence.FirstOffence)
                {
                    _text.Append(' ').Append(JusticeText.T("reallife_justice_sentence_first"));
                }
            }
            if (sentence.ProbationDays > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_sentence_probation", "days", JusticeText.N(sentence.ProbationDays)));
            }
            if (sentence.SuspensionDays > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_sentence_suspension", "days", JusticeText.N(sentence.SuspensionDays)));
            }
            _text.Append("\n\n").Append(JusticeText.T(_factor <= JusticeLaw.DealFactor ? "reallife_justice_reason_deal"
                : _factor < 1f ? "reallife_justice_reason_guilty" : "reallife_justice_reason_trial"));
            if (_priors > 0)
            {
                _text.Append(' ').Append(JusticeText.T("reallife_justice_reason_record", "count", JusticeText.N(_priors)));
            }
            if (_probation)
            {
                _text.Append(' ').Append(JusticeText.T("reallife_justice_reason_probation"));
            }
            if (_case.Complied)
            {
                _text.Append(' ').Append(JusticeText.T("reallife_justice_reason_complied"));
            }
            if (_mitigation > 0f)
            {
                _text.Append(' ').Append(JusticeText.T("reallife_justice_reason_remorse"));
            }
            if (JusticeSettings.Aftermath)
            {
                _text.Append(' ').Append(JusticeText.T("reallife_justice_reason_appeal", "days", JusticeText.N(JusticeLaw.AppealWindowDays)));
            }
            JusticePanel.Instance.Show(Page(1, _text.ToString()).Add(Continue), _ => Finish());
        }

        // Nothing left to answer: the apology. The director works out and pays the compensation.
        private void Dismissed()
        {
            _result = new HearingResult { Case = _case, Outcome = CaseOutcome.Dismissed };
            int compensation = _director.CompensationFor(_case);
            _text.Length = 0;
            _text.Append(JusticeText.T("reallife_justice_court_dismissed"));
            if (compensation > 0)
            {
                _text.Append("\n\n").Append(JusticeText.T("reallife_justice_court_dismissed_comp", "amount", JusticeText.Money(compensation)));
            }
            JusticePanel.Instance.Show(Page(1, _text.ToString()).Add(Continue), _ => Finish());
        }

        private void Finish()
        {
            _director.HearingOver(this);
        }
    }
}
