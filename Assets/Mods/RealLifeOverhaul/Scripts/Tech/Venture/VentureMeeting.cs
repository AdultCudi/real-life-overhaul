using System;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A pitch meeting in the game's own list window (<see cref="TravelList"/> through a <see cref="DeskList"/>), in the
    /// style of the investors' pitch (Economy/Ventures/PitchMeeting): three rounds of questions, the window locked and the
    /// player standing still until it is over. Two kinds:
    ///
    ///   founders     a startup pitches the player: ask about its revenue (the deck's figure against the truth), the
    ///                team (the founders' quality), the market (its sector, the AI wave, the rival), its burn (the runway)
    ///                or the terms; the last page is the term sheet: take it as an angel or from the fund, ask for a lower
    ///                value, think it over or pass
    ///   the player   the player pitches one of their own firms to a venture firm: each question is answered with the
    ///                true figure, a stretch or a bold claim; the firm's diligence may catch an untrue one, which ends the
    ///                meeting; otherwise the score sets the value on its term sheet
    ///
    /// Nothing runs by itself: a click does it all. The director looks once a second whether the window is still there.
    /// </summary>
    internal sealed class VentureMeeting
    {
        private const NavigationBlocker Blocker = (NavigationBlocker)7317;
        private const float InputDelay = 0.4f;
        private const int Leave = 20, TakeAngel = 21, TakeFund = 22, Haggle = 23, Think = 24, Pass = 25, Done = 26;
        private const string ButtonAsk = "reallife_vc_meet_ask";
        private const string ButtonDo = "reallife_vc_meet_do";

        // the founders' meeting: what the player can ask
        public const int TopicRevenue = 0, TopicTeam = 1, TopicMarket = 2, TopicBurn = 3, TopicTerms = 4, Topics = 5;
        private static readonly string[] TopicKeys = { "reallife_vc_topic_revenue", "reallife_vc_topic_team", "reallife_vc_topic_market", "reallife_vc_topic_burn", "reallife_vc_topic_terms" };

        // the player's pitch: what a firm asks, and the three answers to each
        private static readonly string[][] PitchKeys =
        {
            new[] { "reallife_vc_pitch_revenue_true", "reallife_vc_pitch_revenue_stretch", "reallife_vc_pitch_revenue_bold" },
            new[] { "reallife_vc_pitch_growth_true", "reallife_vc_pitch_growth_stretch", "reallife_vc_pitch_growth_bold" },
            new[] { "reallife_vc_pitch_team_true", "reallife_vc_pitch_team_stretch", "reallife_vc_pitch_team_bold" }
        };
        private static readonly string[] PitchQuestions = { "reallife_vc_pitch_revenue", "reallife_vc_pitch_growth", "reallife_vc_pitch_team" };

        private readonly TechDirector _director;
        private readonly DeskList _window = new DeskList("RealLifeOverhaul Venture", 1250f, 150f);
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;

        private Startup _startup;
        private string _place;          // the player's firm, for a pitch of the player's
        private int _vc;
        private long _revenue, _growth;
        private int _people;
        private PlayerController _held;
        private int _round, _asked, _score;
        private string _heard = string.Empty;
        private bool _over, _caught, _leaving;
        private float _shownAt;

        public VentureMeeting(TechDirector director)
        {
            _director = director;
            _onPick = Picked;
            _onClosed = Closed;
        }

        public bool Active { get; private set; }

        /// <summary>A startup's founders pitch the player.</summary>
        public bool Founders(Startup s)
        {
            _startup = s;
            _place = null;
            _heard = TechText.T("reallife_vc_meet_founders_open", "founder", s.Founder, "name", s.Name);
            return Begin();
        }

        /// <summary>The player pitches a firm of theirs to a venture firm, with its true figures handed in.</summary>
        public bool Player(string place, int vc, long revenue, long growthMille, int people)
        {
            _startup = null;
            _place = place;
            _vc = vc;
            _revenue = revenue;
            _growth = growthMille;
            _people = people;
            _score = 500;
            _heard = TechText.T("reallife_vc_meet_player_open", "firm", VentureCatalogue.Firms[TalentCatalogue.Clamp(vc, 0, VentureCatalogue.FirmCount - 1)].Name);
            return Begin();
        }

        private bool Begin()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (Active || player == null || _window.Failed)
            {
                return false;
            }
            _round = 0;
            _asked = 0;
            _over = false;
            _caught = false;
            Active = true;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.fullMenu != null && FullMenu.IsOpen)
            {
                uis.fullMenu.Toggle(false);
            }
            if (!Show())
            {
                Active = false;
                return false;
            }
            _held = player;
            player.ResetNavigation();
            player.SetNavigationBlocker(Blocker);
            return true;
        }

        private string Title => _startup != null ? TechText.T("reallife_vc_meet_title_founders", "name", _startup.Name)
            : TechText.T("reallife_vc_meet_title_player", "firm", VentureCatalogue.Firms[TalentCatalogue.Clamp(_vc, 0, VentureCatalogue.FirmCount - 1)].Name);

        private bool Show()
        {
            TravelList list = _window.Take(this);
            if (list == null)
            {
                return false;
            }
            string progress = TechText.T("reallife_hire_meet_progress", "number", (_round + 1).ToString(), "of", VentureCatalogue.MeetingQuestions.ToString());
            string question = _startup != null ? string.Empty : TechText.T(PitchQuestions[TalentCatalogue.Clamp(_round, 0, PitchQuestions.Length - 1)]) + "\n";
            list.Begin(Title, _heard + "\n" + question + "<size=80%>" + progress + "</size>");
            if (_startup != null)
            {
                for (int t = 0; t < Topics; t++)
                {
                    if ((_asked & (1 << t)) == 0)
                    {
                        list.Add(t, TechText.T(TopicKeys[t]), string.Empty, ButtonAsk, true);
                    }
                }
            }
            else
            {
                string[] answers = PitchAnswers(_round);
                for (int a = 0; a < answers.Length; a++)
                {
                    list.Add(a, TechText.T("reallife_vc_meet_answer", "number", (a + 1).ToString()), answers[a], ButtonAsk, true, list.HeightFor(answers[a].Length, true));
                }
            }
            list.Add(Leave, TechText.T("reallife_vc_meet_leave"), string.Empty, ButtonDo, true);
            Open(list);
            return true;
        }

        // The player's three answers to a firm's question: the truth, a stretch, a bold claim.
        private string[] PitchAnswers(int round)
        {
            string[] q = PitchKeys[TalentCatalogue.Clamp(round, 0, PitchKeys.Length - 1)];
            switch (round)
            {
                case 0:
                    return new[]
                    {
                        TechText.T(q[0], "amount", TechText.Cents(_revenue)), TechText.T(q[1], "amount", TechText.Cents(_revenue * 13 / 10)),
                        TechText.T(q[2], "amount", TechText.Cents(Math.Max(_revenue * 2, 100000)))
                    };
                case 1:
                    return new[]
                    {
                        TechText.T(q[0], "growth", TechText.Mille((int)_growth)), TechText.T(q[1], "growth", TechText.Mille((int)Math.Max(_growth * 3 / 2, 50))),
                        TechText.T(q[2], "growth", TechText.Mille((int)Math.Max(_growth * 3, 200)))
                    };
                default:
                    return new[]
                    {
                        TechText.T(q[0], "people", _people.ToString()), TechText.T(q[1], "people", (_people + Math.Max(2, _people / 3)).ToString()),
                        TechText.T(q[2], "people", (_people * 2 + 5).ToString())
                    };
            }
        }

        private void Open(TravelList list)
        {
            list.Open(_onPick, _onClosed);
            list.ScrollToTop();
            list.Locked = true;
            _shownAt = Time.unscaledTime;
        }

        private void Picked(int id)
        {
            try
            {
                if (!Active || Time.unscaledTime - _shownAt < InputDelay)
                {
                    return;
                }
                if (_over)
                {
                    Decided(id);
                    return;
                }
                if (id == Leave)
                {
                    Summary();
                    return;
                }
                if (_startup != null)
                {
                    if (id < 0 || id >= Topics || (_asked & (1 << id)) != 0)
                    {
                        return;
                    }
                    _asked |= 1 << id;
                    _heard = Heard(id);
                }
                else
                {
                    if (id < 0 || id > 2)
                    {
                        return;
                    }
                    if (Answered(id))
                    {
                        return;   // (caught: the meeting is over)
                    }
                }
                _round++;
                if (_round >= VentureCatalogue.MeetingQuestions)
                {
                    Summary();
                }
                else if (!Show())
                {
                    End();
                }
            }
            catch (Exception e)
            {
                End();
                TechDirector.Logger?.Error(e);
            }
        }

        // What the founders' answer showed.
        private string Heard(int topic)
        {
            Startup s = _startup;
            switch (topic)
            {
                case TopicRevenue:
                    s.Revealed |= 1;
                    return TechText.Fill(s.ClaimBp > 1000 ? "reallife_vc_heard_revenue_less" : "reallife_vc_heard_revenue", "claimed", TechText.Cents(VentureRules.Claimed(s)), "true", TechText.Cents(s.Mrr));
                case TopicTeam:
                    s.Revealed |= 2;
                    return TechText.T(s.Quality >= 700 ? "reallife_vc_heard_team_strong" : s.Quality >= 400 ? "reallife_vc_heard_team_fair" : "reallife_vc_heard_team_weak", "founder", s.Founder);
                case TopicMarket:
                    s.Revealed |= 4;
                    return TechText.Fill("reallife_vc_heard_market", "sector", TechText.VentureName((Venture)s.Sector), "hype", s.Sector == (int)Venture.Ai ? TechText.Mille(_director.HypeNow) : "-");
                case TopicBurn:
                    int runway = VentureRules.Runway(s.Cash, s.Burn, s.Mrr);
                    return TechText.Fill("reallife_vc_heard_burn", "burn", TechText.Cents(s.Burn), "days", runway == int.MaxValue ? "-" : runway.ToString());
                default:
                    return TechText.T(s.Quality >= 600 ? "reallife_vc_heard_terms_firm" : "reallife_vc_heard_terms_soft", "founder", s.Founder);
            }
        }

        // The player's answer to a firm: the truth earns its points, a stretch or a bold claim more unless the firm's diligence catches it.
        private bool Answered(int option)
        {
            VcDef vc = VentureCatalogue.Firms[TalentCatalogue.Clamp(_vc, 0, VentureCatalogue.FirmCount - 1)];
            int strength = _round == 0 ? (int)Math.Min(1000, _revenue / 50000) : _round == 1 ? (int)Math.Min(1000, _growth * 4) : Math.Min(1000, _people * 40);
            int points = 100 + strength / 4;
            if (option > 0)
            {
                int catchChance = vc.Diligence * (option == 1 ? 45 : 90) / 100;
                if (UnityEngine.Random.Range(0, 1000) < catchChance)
                {
                    _caught = true;
                    _director.VcCaught(_place, _vc);
                    Caught();
                    return true;
                }
                points += option == 1 ? 80 : 160;
            }
            _score += points - 120;
            _heard = TechText.T(points >= 250 ? "reallife_vc_react_good" : points >= 150 ? "reallife_vc_react_fair" : "reallife_vc_react_poor", "firm", vc.Name);
            return false;
        }

        private void Caught()
        {
            TravelList list = _window.Take(this);
            if (list == null)
            {
                End();
                return;
            }
            list.Begin(Title, TechText.T("reallife_vc_meet_caught"));
            string body = TechText.T("reallife_vc_meet_caught_body", "firm", VentureCatalogue.Firms[TalentCatalogue.Clamp(_vc, 0, VentureCatalogue.FirmCount - 1)].Name);
            list.AddNote(TechText.T("reallife_vc_meet_caught"), body, list.HeightFor((int)(body.Length * 1.3f), false));
            list.Add(Done, TechText.T("reallife_hire_meet_close"), string.Empty, ButtonDo, true);
            _over = true;
            Open(list);
        }

        private void Summary()
        {
            TravelList list = _window.Take(this);
            if (list == null)
            {
                End();
                return;
            }
            list.Begin(Title, TechText.T("reallife_vc_meet_over"));
            if (_startup != null && _startup.Ask != null)
            {
                TermSheet a = _startup.Ask;
                string terms = TechText.Fill("reallife_vc_meet_terms", "amount", TechText.Cents(_director.Level(a.Amount)), "pre", TechText.Cents(_director.Level(a.Pre)), "share", TechText.Mille(a.EquityMille),
                    "day", a.Lapse.ToString());
                list.AddNote(_startup.Name, terms, list.HeightFor((int)(terms.Length * 1.3f), false));
                list.Add(TakeAngel, TechText.T("reallife_vc_meet_take_angel"), TechText.T("reallife_vc_meet_take_angel_detail"), ButtonDo, true);
                if (_director.FundOpen)
                {
                    list.Add(TakeFund, TechText.T("reallife_vc_meet_take_fund"), TechText.T("reallife_vc_meet_take_fund_detail", "cash", TechText.Cents(_director.FundCash)), ButtonDo, true);
                }
                list.Add(Haggle, TechText.T("reallife_vc_meet_haggle"), TechText.T("reallife_vc_meet_haggle_detail", "cut", TechText.Mille(VentureCatalogue.HaggleCut)), ButtonDo, true);
                list.Add(Think, TechText.T("reallife_vc_meet_think"), string.Empty, ButtonDo, true);
                list.Add(Pass, TechText.T("reallife_vc_meet_pass"), string.Empty, ButtonDo, true);
            }
            else if (_startup != null)
            {
                list.AddNote(_startup.Name, TechText.T("reallife_vc_meet_no_round"), 160f);
                list.Add(Done, TechText.T("reallife_hire_meet_close"), string.Empty, ButtonDo, true);
            }
            else
            {
                string said = _director.VcSheet(_place, _vc, _score);
                list.AddNote(VentureCatalogue.Firms[TalentCatalogue.Clamp(_vc, 0, VentureCatalogue.FirmCount - 1)].Name, said, list.HeightFor((int)(said.Length * 1.3f), false));
                list.Add(Done, TechText.T("reallife_hire_meet_close"), string.Empty, ButtonDo, true);
            }
            _over = true;
            Open(list);
        }

        private void Decided(int id)
        {
            Startup s = _startup;
            End();
            if (s == null)
            {
                return;
            }
            switch (id)
            {
                case TakeAngel: _director.Back(s.Id, false); break;
                case TakeFund: _director.Back(s.Id, true); break;
                case Haggle: _director.Haggle(s.Id); break;
                case Pass: _director.Pass(s.Id); break;
            }
        }

        private void Closed()
        {
            if (Active && !_leaving)
            {
                Free();
            }
        }

        public void Watch()
        {
            if (Active && !_window.OpenFor(this))
            {
                Free();
            }
        }

        public void End()
        {
            _leaving = true;
            if (_window.OpenFor(this))
            {
                _window.Close();
            }
            _leaving = false;
            Free();
        }

        private void Free()
        {
            Active = false;
            PlayerController player = _held;
            _held = null;
            try
            {
                if (player != null)
                {
                    player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                TechDirector.Logger?.Error(e);
            }
            _director.ChangedVenture();
        }

        public void Dispose()
        {
            End();
            _window.Destroy(e => TechDirector.Logger?.Error(e));
        }
    }
}
