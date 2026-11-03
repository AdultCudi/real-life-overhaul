using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Ui;
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

        // What an answer to the plea comes to, worked out as the sentence would be now: the money due and the time.
        private struct Cost
        {
            public int Due;
            public int PrisonDays, JailHours, CommunityHours;
        }

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
        private readonly List<ChargeDef> _weighed = new List<ChargeDef>(4);       // the charges an answer's cost is worked out on
        private readonly List<JusticeCharge> _dealDrops = new List<JusticeCharge>(4);
        private readonly StringBuilder _text = new StringBuilder(600);

        private float _quality;
        private int _beat;
        private int _choices;           // thousandths the accused's own answers added to (or took off) the case against them
        private float _mitigation;
        private float _factor = 1f;
        private float _fineFactor = -1f;   // the fine's own factor where it is not the sentence's (a lost trial's); -1 for the same
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
                Body = body,
                LeftRole = Taxed ? JusticeText.T("reallife_justice_role_inspector") : null,   // a tax case is brought for the city's tax office
                Defendant = true
            };
            if (page.Right == JusticeFace.None && speaker == 2)
            {
                page.Speaker = -1;
                page.SpeakerName = JusticeText.T("reallife_justice_court_you");
            }
            return page;
        }

        private static string Continue => JusticeText.T("reallife_justice_continue");

        // A case about tax kept back: brought for the city's tax office, which takes a settlement.
        private bool Taxed => _case.Main != null && _case.Main.Id == JusticeLaw.TaxEvasion;

        // The words for the sum a charge is about, where the case holds one (TaxCases): null for none.
        private static string FigureKey(string charge) =>
            charge == JusticeLaw.TaxEvasion ? "reallife_justice_court_figure_tax"
            : charge == JusticeLaw.Bribery ? "reallife_justice_court_figure_bribery"
            : charge == JusticeLaw.CampaignFinance ? "reallife_justice_court_figure_campaign"
            : charge == JusticeLaw.BidRigging ? "reallife_justice_court_figure_rigging" : null;

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
                string figure = _case.Amount > 0 ? FigureKey(charge.Id) : null;
                if (figure != null)
                {
                    _text.Append(": ").Append(JusticeText.T(figure, "amount", JusticeText.Money(_case.Amount)));   // the sum it is about
                }
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
            // Each answer's money and time, worked out as the sentence would be now; a lost trial's fine twice a guilty plea's.
            Standing(_weighed);
            Cost guilty = CostOf(JusticeLaw.GuiltyPleaFactor, -1f, _weighed);
            Cost trial = CostOf(1f, JusticeLaw.TrialFineFactor, _weighed);
            bool settle = Taxed && trial.Due > 0;   // the city's tax office takes a settlement in place of a deal
            JusticePage page = Page(1, _text.ToString())
                .Add(JusticeText.T("reallife_justice_plea_guilty"), JusticeText.T("reallife_justice_plea_guilty_hint"));
            Lines(page, guilty, false);
            page.Add(JusticeText.T("reallife_justice_plea_not_guilty"),
                    JusticeText.T("reallife_justice_plea_not_guilty_hint", "rounds", JusticeText.N(_beats.Count), "percent", JusticeText.Percent(against)))
                .Line(Odds(Cleared()), 0);
            Lines(page, trial, true);
            if (settle)
            {
                page.Add(JusticeText.T("reallife_justice_plea_settle"), JusticeText.T("reallife_justice_plea_settle_hint"))
                    .Line(JusticeText.T("reallife_justice_settle_cost", "amount", JusticeText.Money(trial.Due)), UiConsequence.Loss)
                    .Line(JusticeText.T("reallife_justice_settle_gain"), 1);
            }
            else if (deal)
            {
                page.Add(JusticeText.T("reallife_justice_plea_deal"), LegalCounsel.DealText(_case, _quality));
                DealCharges(_weighed);
                Lines(page, CostOf(JusticeLaw.DealFactor, -1f, _weighed), false);
            }
            int settlement = trial.Due;
            JusticePanel.Instance.Show(page, choice =>
            {
                if (choice == 0)
                {
                    _factor = JusticeLaw.GuiltyPleaFactor;
                    ConvictAll();
                    Sentence(CaseOutcome.Guilty, JusticeText.T("reallife_justice_verdict_guilty_plea"));
                }
                else if (choice == 2 && settle)
                {
                    Settle(settlement);
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

        // The charges still standing, as a plea or a verdict on every one would take them.
        private void Standing(List<ChargeDef> into)
        {
            into.Clear();
            for (int i = 0; i < _case.Charges.Count; i++)
            {
                ChargeDef def = _case.Charges[i].Dropped ? null : JusticeLaw.Find(_case.Charges[i].Id);
                if (def != null)
                {
                    into.Add(def);
                }
            }
        }

        // The charges the deal on the table leaves: those it drops gone, the main one the lesser where it is reduced.
        private void DealCharges(List<ChargeDef> into)
        {
            into.Clear();
            LegalCounsel.DealPlan(_case, _quality, _dealDrops, out JusticeCharge main, out string lesser);
            for (int i = 0; i < _case.Charges.Count; i++)
            {
                JusticeCharge charge = _case.Charges[i];
                ChargeDef def = charge.Dropped || _dealDrops.Contains(charge) ? null : JusticeLaw.Find(charge == main && lesser != null ? lesser : charge.Id);
                if (def != null)
                {
                    into.Add(def);
                }
            }
        }

        // The chance to be cleared of every charge standing, before the trial's answers move it: each is weighed on its own.
        private float Cleared()
        {
            float cleared = 1f;
            for (int i = 0; i < _case.Charges.Count; i++)
            {
                JusticeCharge charge = _case.Charges[i];
                if (!charge.Dropped && JusticeLaw.Find(charge.Id) != null)
                {
                    cleared *= 1f - JusticeLaw.CaseAgainst(charge.Evidence, _priors, _quality, 0) / 1000f;
                }
            }
            return cleared;
        }

        // An answer's money and time as the sentence would be worked out now: what was paid at the roadside off the fine,
        // damages held added, the hours already held taken off.
        private Cost CostOf(float factor, float fineFactor, List<ChargeDef> convicted)
        {
            JusticeSentence s = Work(factor, fineFactor, convicted);
            return new Cost
            {
                Due = JusticeLaw.ConvictionDue(s.Fine, _case.PaidAtScene, _case.Damages), PrisonDays = s.PrisonDays,
                JailHours = s.JailHours - Mathf.Min(s.JailHours, Mathf.Max(0, _case.HeldHours)), CommunityHours = s.CommunityHours
            };
        }

        // An answer's cost under it: its money and its time, the time beside it too; a trial's as what losing it brings.
        private static void Lines(JusticePage page, Cost cost, bool ifLost)
        {
            if (!ifLost)
            {
                page.Takes(cost.PrisonDays > 0 ? JusticeText.T("reallife_justice_cost_days", "days", JusticeText.N(cost.PrisonDays))
                    : cost.JailHours > 0 ? JusticeText.T("reallife_justice_cost_hours", "hours", JusticeText.N(cost.JailHours))
                    : cost.CommunityHours > 0 ? JusticeText.T("reallife_justice_cost_hours", "hours", JusticeText.N(cost.CommunityHours)) : null);
            }
            if (cost.Due > 0)
            {
                page.Line(JusticeText.T(ifLost ? "reallife_justice_cost_lost_fine" : "reallife_justice_cost_fine", "amount", JusticeText.Money(cost.Due)), UiConsequence.Loss);
            }
            string prison = ifLost ? "reallife_justice_cost_lost_prison" : "reallife_justice_cost_prison";
            string custody = ifLost ? "reallife_justice_cost_lost_custody" : "reallife_justice_cost_custody";
            string community = ifLost ? "reallife_justice_cost_lost_community" : "reallife_justice_cost_community";
            page.Line(cost.PrisonDays > 0 ? JusticeText.T(prison, "days", JusticeText.N(cost.PrisonDays))
                : cost.JailHours > 0 ? JusticeText.T(custody, "hours", JusticeText.N(cost.JailHours))
                : cost.CommunityHours > 0 ? JusticeText.T(community, "hours", JusticeText.N(cost.CommunityHours)) : null, -1);
        }

        // The chance to be cleared, in words: about so many in ten.
        private static string Odds(float cleared)
        {
            int tenths = JusticeLaw.InTen(cleared);
            return tenths <= 0 ? JusticeText.T("reallife_justice_odds_none")
                : tenths >= 10 ? JusticeText.T("reallife_justice_odds_all")
                : JusticeText.T("reallife_justice_odds", "count", JusticeText.N(tenths));
        }

        // A tax case settled with the city: the settlement paid as a fine is, the charge dropped and nothing on the record.
        private void Settle(int amount)
        {
            for (int i = 0; i < _case.Charges.Count; i++)
            {
                _case.Charges[i].Convicted = false;
            }
            _result = new HearingResult { Case = _case, Outcome = CaseOutcome.Settled, FineDue = amount };
            JusticePanel.Instance.Show(Page(1, JusticeText.T("reallife_justice_verdict_settled", "amount", JusticeText.Money(amount))).Add(Continue), _ => Finish());
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
            _fineFactor = JusticeLaw.TrialFineFactor;   // a lost trial: twice the fine a guilty plea brings
            Sentence(CaseOutcome.Convicted, explanation);
        }

        private static string Signed(int value)
        {
            return value > 0 ? "+" + JusticeText.N(value) : JusticeText.N(value);
        }

        // The sentence for these convictions as the court works it out, with the answers and the record so far.
        private JusticeSentence Work(float factor, float fineFactor, List<ChargeDef> convicted)
        {
            float mitigation = _mitigation + (_case.Complied ? 0.15f : 0f);
            float cash = Crime.Cash;
            JusticeSentence sentence = JusticeLaw.Sentence(convicted, factor, mitigation, _priors, _probation, JusticeSettings.PrisonCap, JusticeSettings.NoPrison, cash,
                fineFactor);
            if (sentence.CommunityHours > 0 && !JusticeSettings.Community)
            {
                // Community service switched off: the hours become money, inside the same bound.
                sentence.FineNominal += sentence.CommunityHours * 60;
                sentence.Fine = JusticeLaw.Bound(sentence.FineNominal, cash, JusticeLaw.FineShareOfCash, JusticeLaw.FineFloor);
                sentence.Bounded = sentence.Fine < sentence.FineNominal;
                sentence.CommunityHours = 0;
            }
            return sentence;
        }

        private void Sentence(CaseOutcome outcome, string lead)
        {
            float cash = Crime.Cash;
            JusticeSentence sentence = Work(_factor, _fineFactor, _convicted);
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
                : _factor < 1f ? "reallife_justice_reason_guilty" : _fineFactor > _factor ? "reallife_justice_reason_trial_fine" : "reallife_justice_reason_trial"));
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
