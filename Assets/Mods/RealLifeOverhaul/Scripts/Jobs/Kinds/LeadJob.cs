using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A shift in charge: depot supervisor, crew lead, shift lead, team lead, assistant manager.
    /// The work is other people. Two of the team are out near the office, each doing something
    /// that can be seen from a few steps off: getting on with it (writing up a list), slacking
    /// (a cigarette), or stuck (arms folded, waiting). The lead walks up to each and makes the
    /// call that fits: a word of praise, a word in the ear, or a hand. Then two incidents come
    /// in, each a choice of three. A call made well is a unit of work (paid, and what a review
    /// counts); a bad one is a complaint in the making. Most of the pay is the rank's hourly
    /// salary (Jobs/Career*).
    ///
    /// The team are the jobs' own two characters, put on pavement that was found clear and out
    /// of the player's sight, with the same faces every shift (seeded); they only stand, turn
    /// and, at the end, walk away. If nobody can be put out, the shift is the incidents alone.
    /// </summary>
    public sealed class LeadJob : Job
    {
        private enum Step { Rounds, Asking, Briefing, Incident, Leaving }
        private enum Mood { Fine, Slacking, Stuck }

        private const int Incidents = 2;
        private const float Seconds = 260f;
        private const float StaffReach = 2.4f;
        private const float LeaveReach = 70f;
        private const float AskSeconds = 25f;
        private const float IncidentSeconds = 32f;
        private const string StuckWish = "staff_struggle";

        private static readonly string[] MoodClip = { "take_notes", "smoke", "arms_crossed" };
        private static readonly string[] MoodLine = { "reallife_job_lead_see_fine", "reallife_job_lead_see_slacking", "reallife_job_lead_see_stuck" };
        private static readonly string[] RightLine = { "reallife_job_lead_right_fine", "reallife_job_lead_right_slacking", "reallife_job_lead_right_stuck" };
        private static readonly string[] WrongLine = { "reallife_job_lead_wrong_1", "reallife_job_lead_wrong_2", "reallife_job_lead_wrong_3" };
        private static readonly string[] GoodCall = { "reallife_job_lead_call_good_1", "reallife_job_lead_call_good_2", "reallife_job_lead_call_good_3" };
        private static readonly string[] FairCall = { "reallife_job_lead_call_ok_1", "reallife_job_lead_call_ok_2" };
        private static readonly string[] BadCall = { "reallife_job_lead_call_bad_1", "reallife_job_lead_call_bad_2", "reallife_job_lead_call_bad_3" };

        // what has happened, the three things that can be done about it, which is the best and which the bad one (the third is passable)
        private static readonly (string key, string a, string b, string c, int best, int bad)[] Deck =
        {
            ("reallife_job_lead_inc_late", "reallife_job_lead_inc_late_a", "reallife_job_lead_inc_late_b", "reallife_job_lead_inc_late_c", 0, 2),
            ("reallife_job_lead_inc_row", "reallife_job_lead_inc_row_a", "reallife_job_lead_inc_row_b", "reallife_job_lead_inc_row_c", 0, 1),
            ("reallife_job_lead_inc_short", "reallife_job_lead_inc_short_a", "reallife_job_lead_inc_short_b", "reallife_job_lead_inc_short_c", 1, 2),
            ("reallife_job_lead_inc_spill", "reallife_job_lead_inc_spill_a", "reallife_job_lead_inc_spill_b", "reallife_job_lead_inc_spill_c", 0, 1),
            ("reallife_job_lead_inc_count", "reallife_job_lead_inc_count_a", "reallife_job_lead_inc_count_b", "reallife_job_lead_inc_count_c", 0, 1),
            ("reallife_job_lead_inc_refund", "reallife_job_lead_inc_refund_a", "reallife_job_lead_inc_refund_b", "reallife_job_lead_inc_refund_c", 0, 1)
        };

        private static int _lastIncident = -1;   // across shifts: never the same incident twice running

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Lead);
        private readonly StreetActor[] _staff = new StreetActor[JobCast.Size];
        private readonly Mood[] _mood = new Mood[JobCast.Size];
        private readonly bool[] _checked = new bool[JobCast.Size];
        private readonly string[] _names = new string[JobCast.Size];
        private readonly string[] _prompts = new string[JobCast.Size];
        private readonly string[] _labels = new string[JobCast.Size];

        private Step _step;
        private Vector3 _anchor;
        private Employer _employer;
        private float _endsAt;
        private float _stepEnd;
        private int _target = -1;
        private int _incident;        // incidents dealt with
        private int _deckAt;          // the incident on the table
        private int _good;
        private int _bad;
        private int _calls;
        private bool _paid;
        private int _line = -1;
        private string[] _askChoices;

        public override JobDef Def => _def;
        public override bool QuitIsFailure => _calls > 0 && _good * 2 < _calls;

        protected override bool OnBegin(float now)
        {
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_on_foot";
                return false;
            }
            Vector3 player = Ctx.PlayerPosition;
            _employer = Ctx.Shift != null ? Ctx.Shift.Employer : CareerCatalogue.Find(CareerCatalogue.Retail);
            _anchor = Ctx.Shift != null && Ctx.Shift.HasDepot && JobSpots.Flat(Ctx.Shift.Depot, player) < LeaveReach ? Ctx.Shift.Depot : player;
            _askChoices = new[]
            {
                JobText.T("reallife_job_lead_do_praise"), JobText.T("reallife_job_lead_do_word"), JobText.T("reallife_job_lead_do_help")
            };
            for (int i = 0; i < _staff.Length; i++)
            {
                _names[i] = JobText.T(_employer.Colleagues[i % _employer.Colleagues.Length]);
                _prompts[i] = JobText.T("reallife_job_lead_prompt", "name", _names[i]);
                _labels[i] = _names[i];
                PutStaff(i);
            }
            _endsAt = now + Seconds;
            _step = Step.Rounds;
            NextStaff(now);
            Ctx.Logger?.Info("Jobs: lead shift for " + _employer.Id + ", " + (_staff[0] != null ? 1 : 0) + "+" + (_staff[1] != null ? 1 : 0) + " of the team out.");
            return true;
        }

        // On pavement that is clear, out of sight, a short walk from the office and not on top of the other one.
        private void PutStaff(int index)
        {
            for (int attempt = 0; attempt < 4 && _staff[index] == null; attempt++)
            {
                if (!CitySpots.PavementRing(_anchor, 14f, 34f, true, out Vector3 near) || !CitySpots.Stand(near, 0.45f, out Vector3 spot))
                {
                    continue;
                }
                if (index > 0 && _staff[0] != null && JobSpots.Flat(spot, _staff[0].Position) < 7f)
                {
                    continue;
                }
                StreetActor person = Ctx.Cast.Put(spot, false, _employer.Seed + 11 * (index + 1), (index + _employer.Seed) % 2 == 0, false);
                if (person == null)
                {
                    return;   // nobody to be had: the shift goes on without them
                }
                _mood[index] = (Mood)Random.Range(0, 3);
                person.StandAt(spot, _anchor);
                if (!(_mood[index] == Mood.Stuck && ClipLibrary.Has(StuckWish) && person.Work(StuckWish)))   // the clip made for it, once the bundle has it
                {
                    person.Work(MoodClip[(int)_mood[index]]);
                }
                _staff[index] = person;
            }
        }

        private void NextStaff(float now)
        {
            _target = -1;
            for (int i = 0; i < _staff.Length && _target < 0; i++)
            {
                if (_staff[i] != null && _staff[i].IsValid && !_checked[i])
                {
                    _target = i;
                }
            }
            Prompt = null;
            PromptOver = null;
            Choices = null;
            if (_target < 0)
            {
                Unmark();
                Brief(now);
                return;
            }
            _step = Step.Rounds;
            Mark(_staff[_target].Position, _labels[_target]);
            Show(JobText.T("reallife_job_lead_obj_round", "name", _names[_target]));
        }

        public override void Tick(float now)
        {
            ClockSeconds = Mathf.CeilToInt(_endsAt - now);
            if (Ctx.Driving || JobSpots.Flat(Ctx.PlayerPosition, _anchor) > LeaveReach)
            {
                Ctx.Say("reallife_job_lead_left", 8f);
                Conclude(true);
                return;
            }
            if (now >= _endsAt && _step != Step.Leaving)
            {
                Ctx.Say("reallife_job_lead_time", 8f);
                Wind(now);
                return;
            }
            switch (_step)
            {
                case Step.Rounds:
                    if (_target < 0 || _staff[_target] == null || !_staff[_target].IsValid)
                    {
                        if (_target >= 0)
                        {
                            _checked[_target] = true;   // gone (the street went away round them): nothing to check
                        }
                        NextStaff(now);
                        break;
                    }
                    bool near = JobSpots.Flat(Ctx.PlayerPosition, _staff[_target].Position) < StaffReach;
                    Prompt = near ? _prompts[_target] : null;
                    PromptOver = near ? _staff[_target].Character.transform : null;
                    PromptLift = 2.05f;
                    break;

                case Step.Asking:
                    if (now >= _stepEnd)
                    {
                        Decide(-1, now);   // walked up and said nothing
                    }
                    break;

                case Step.Briefing:
                    if (now >= _stepEnd)
                    {
                        Incident(now);
                    }
                    break;

                case Step.Incident:
                    if (now >= _stepEnd)
                    {
                        Call(-1, now);
                    }
                    break;

                case Step.Leaving:
                    if (now >= _stepEnd || !AnyInView())
                    {
                        Conclude(false);
                    }
                    break;
            }
        }

        public override void Interact(float now)
        {
            if (_step != Step.Rounds || Prompt == null || _target < 0 || _staff[_target] == null)
            {
                return;
            }
            Prompt = null;
            PromptOver = null;
            Unmark();
            StreetActor person = _staff[_target];
            person.Face(Ctx.Player.transform.position);
            Ctx.TurnPlayer(person.Position);
            Ctx.Say(MoodLine[(int)_mood[_target]], 9f);
            Show(JobText.T("reallife_job_lead_obj_ask", "name", _names[_target]));
            Choices = _askChoices;
            ChoiceLit = -1;
            _step = Step.Asking;
            _stepEnd = now + AskSeconds;
            CareerLinks.TellMet(_employer, _target);
        }

        public override void Choose(int choice, float now)
        {
            if (_step == Step.Asking)
            {
                Decide(choice, now);
            }
            else if (_step == Step.Incident)
            {
                Call(choice, now);
            }
        }

        // Praise for the one getting on with it, a word for the slacker, a hand for the one who is stuck.
        private void Decide(int choice, float now)
        {
            int index = _target;
            if (index < 0)
            {
                return;
            }
            StreetActor person = _staff[index];
            bool right = choice == (int)_mood[index];
            _calls++;
            _checked[index] = true;
            Choices = null;
            if (right)
            {
                _good++;
                JobText.Toast(NotificationType.Success, RightLine[(int)_mood[index]], "name", _names[index], 7f);
            }
            else
            {
                if (choice < 0 || _mood[index] == Mood.Stuck)
                {
                    _bad++;   // somebody stuck who got no help, or a lead who walked up and said nothing
                }
                JobText.Toast(NotificationType.Warning, JobText.Pick(WrongLine, ref _line), "name", _names[index], 7f);
            }
            if (person != null && person.IsValid)
            {
                // They are being spoken to, face to face: the reaction is theirs to make.
                Ctx.Animations.Play(person.Character, right ? JobClip.Thank : JobClip.Annoyed);
                Ctx.Cast.Emoji(person, right ? CharacterEmojiName.CommonGreenHappy : CharacterEmojiName.CommonYellowNeutral, 3f);
                if (right && _mood[index] != Mood.Fine)
                {
                    person.Work(MoodClip[(int)Mood.Fine]);   // back to work
                }
            }
            NextStaff(now);
        }

        // A breath between the rounds and the first incident, and between the two.
        private void Brief(float now)
        {
            if (_incident >= Incidents)
            {
                Wind(now);
                return;
            }
            _step = Step.Briefing;
            _stepEnd = now + (_incident == 0 && _calls == 0 ? 2f : 5f);
            Show(JobText.T("reallife_job_lead_obj_wait"));
        }

        private void Incident(float now)
        {
            int pick = Random.Range(0, Deck.Length);
            if (pick == _lastIncident)
            {
                pick = (pick + 1) % Deck.Length;
            }
            _lastIncident = pick;
            _deckAt = pick;
            string key = Deck[pick].key;
            Ctx.Say(key, IncidentSeconds);
            Show(JobText.T("reallife_job_lead_obj_incident", "number", (_incident + 1).ToString(), "count", Incidents.ToString()));
            Choices = new[] { JobText.T(Deck[pick].a), JobText.T(Deck[pick].b), JobText.T(Deck[pick].c) };
            ChoiceLit = -1;
            _step = Step.Incident;
            _stepEnd = now + IncidentSeconds;
        }

        private void Call(int choice, float now)
        {
            int best = Deck[_deckAt].best;
            int bad = Deck[_deckAt].bad;
            _calls++;
            _incident++;
            Choices = null;
            if (choice == best)
            {
                _good++;
                JobText.Toast(NotificationType.Success, JobText.Pick(GoodCall, ref _line), 7f);
            }
            else if (choice == bad || choice < 0)
            {
                _bad++;
                JobText.Toast(NotificationType.Error, choice < 0 ? "reallife_job_lead_call_none" : JobText.Pick(BadCall, ref _line), 7f);
            }
            else
            {
                JobText.Toast(NotificationType.Info, JobText.Pick(FairCall, ref _line), 7f);
            }
            Brief(now);
        }

        // The shift is done: the team walk off, and the job waits a moment for them to be out of sight.
        private void Wind(float now)
        {
            Prompt = null;
            PromptOver = null;
            Choices = null;
            Unmark();
            Vector3 player = Ctx.PlayerPosition;
            for (int i = 0; i < _staff.Length; i++)
            {
                StreetActor person = _staff[i];
                if (person == null || !person.IsValid)
                {
                    continue;
                }
                person.ClearPose();
                if (!StreetSpots.AwayFrom(person.Position, player, 40f, false, out Vector3 off) || !person.MoveTo(off, Pace.Walk))
                {
                    Ctx.Cast.Release(person);
                    _staff[i] = null;
                }
            }
            Show(JobText.T("reallife_job_lead_obj_done"));
            _step = Step.Leaving;
            _stepEnd = now + 9f;
        }

        private bool AnyInView()
        {
            for (int i = 0; i < _staff.Length; i++)
            {
                StreetActor person = _staff[i];
                if (person != null && person.IsValid && CitySpots.InView(person.Position)
                    && JobSpots.Flat(person.Position, Ctx.PlayerPosition) < 45f)
                {
                    return true;
                }
            }
            return false;
        }

        private void Conclude(bool walkedOff)
        {
            Settle();
            Finish(walkedOff || _bad * 2 > Mathf.Max(1, _calls) ? JobEnd.Failed : JobEnd.Done);
        }

        private void Settle()
        {
            if (_paid || _calls <= 0 || Ctx == null)
            {
                return;
            }
            _paid = true;
            int each = UnitPay();
            var slip = new PaySlip
            {
                Units = _good,
                Base = _good * each,
                Bonus = _good == _calls && _calls >= Incidents ? Mathf.RoundToInt(_good * each * 0.1f) : 0,   // every call the right one
                Penalty = _bad * Mathf.RoundToInt(each * 0.5f)
            };
            Ctx.Pay(this, slip, "reallife_job_lead_slip");
        }

        public override void End(JobEnd why)
        {
            if (Ctx == null)
            {
                return;
            }
            Prompt = null;
            PromptOver = null;
            Choices = null;
            for (int i = 0; i < _staff.Length; i++)
            {
                Ctx.Cast.Release(_staff[i]);
                _staff[i] = null;
            }
            if (why != JobEnd.Fault)
            {
                Settle();
            }
        }
    }
}
