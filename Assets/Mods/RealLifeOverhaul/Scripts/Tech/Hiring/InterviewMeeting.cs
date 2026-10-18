using System;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// An interview in the game's own list window (<see cref="TravelList"/> through a <see cref="DeskList"/>), the player standing
    /// still until it is over. Clicks do it all; the director checks once a second that the window is still there.
    /// </summary>
    internal sealed class InterviewMeeting
    {
        private const NavigationBlocker Blocker = (NavigationBlocker)7311;   // a value of our own, so nobody else's is ever lifted
        private const float InputDelay = 0.4f;   // seconds before a click on a new page counts
        private const int Leave = 10, Close = 11;
        private const string ButtonAsk = "reallife_hire_meet_ask";
        private const string ButtonDo = "reallife_hire_meet_do";

        private static readonly string[] TopicKeys =
        {
            "reallife_hire_topic_technical", "reallife_hire_topic_project", "reallife_hire_topic_motive", "reallife_hire_topic_pay", "reallife_hire_topic_team"
        };

        private static readonly string[] TopicDetailKeys =
        {
            "reallife_hire_topic_technical_detail", "reallife_hire_topic_project_detail", "reallife_hire_topic_motive_detail", "reallife_hire_topic_pay_detail",
            "reallife_hire_topic_team_detail"
        };

        private readonly TechDirector _director;
        private readonly DeskList _window = new DeskList("RealLifeOverhaul Interview", 1250f, 150f);
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;

        private Applicant _a;
        private Seeker _s;
        private JobAd _ad;
        private PlayerController _held;
        private int _round, _asked;
        private string _heard = string.Empty;
        private bool _over, _leaving;
        private float _shownAt;

        public InterviewMeeting(TechDirector director)
        {
            _director = director;
            _onPick = Picked;
            _onClosed = Closed;
        }

        public bool Active { get; private set; }

        public bool Begin(Applicant a, Seeker s, JobAd ad)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (Active || player == null || _window.Failed || a == null || s == null)
            {
                return false;
            }
            _a = a;
            _s = s;
            _ad = ad;
            _round = 0;
            _asked = 0;
            _over = false;
            _heard = TechText.T("reallife_hire_meet_opening", "name", s.P.Name);
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

        private string Title => TechText.T("reallife_hire_meet_title", "name", _s.P.Name, "role", TechText.Role(_ad != null ? _ad.Role : _s.P.Role));

        private bool Show()
        {
            TravelList list = _window.Take(this);
            if (list == null)
            {
                return false;
            }
            string progress = TechText.T("reallife_hire_meet_progress", "number", (_round + 1).ToString(), "of", HiringCatalogue.InterviewRounds.ToString());
            list.Begin(Title, _heard + "\n<size=80%>" + progress + "</size>");
            for (int t = 0; t < HiringRules.Topics; t++)
            {
                if ((_asked & (1 << t)) != 0)
                {
                    continue;
                }
                string detail = TechText.T(TopicDetailKeys[t]);
                list.Add(t, TechText.T(TopicKeys[t]), detail, ButtonAsk, true, list.HeightFor(detail.Length, true));
            }
            string leave = TechText.T("reallife_hire_meet_leave_detail");
            list.Add(Leave, TechText.T("reallife_hire_meet_leave"), leave, ButtonDo, true, list.HeightFor(leave.Length, true));
            Open(list);
            return true;
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
                if (_over || id == Close)
                {
                    End();
                    return;
                }
                if (id == Leave)
                {
                    ShowSummary();
                    return;
                }
                if (id < 0 || id >= HiringRules.Topics || (_asked & (1 << id)) != 0)
                {
                    return;
                }
                _asked |= 1 << id;
                int moved = HiringRules.Ask(_a, _s, id);
                string mood = moved > 30 ? "reallife_hire_meet_keen" : moved < 0 ? "reallife_hire_meet_cooler" : "reallife_hire_meet_even";
                _heard = Heard(id) + " " + TechText.T(mood, "name", _s.P.Name);
                _round++;
                if (_round >= HiringCatalogue.InterviewRounds)
                {
                    ShowSummary();
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

        private string Heard(int topic)
        {
            switch (topic)
            {
                case HiringRules.TopicTechnical:
                    return TechText.Fill(_s.Claim > 5 ? "reallife_hire_meet_heard_skill_less" : "reallife_hire_meet_heard_skill", "name", _s.P.Name,
                        "skill", TechText.Percent(_s.P.Main / 1000), "cv", TechText.Percent(_s.CvMain));
                case HiringRules.TopicProject:
                    return TechText.Fill("reallife_hire_meet_heard_years", "name", _s.P.Name, "years", TechText.Years(_s.P.YearsDays), "a", TechText.Skill(_s.P.Def.SkillA),
                        "score", TechText.Score(_s.P.A));
                case HiringRules.TopicMotive:
                    return TechText.T(_s.Fit >= 650 ? "reallife_hire_meet_heard_fit_good" : _s.Fit >= 400 ? "reallife_hire_meet_heard_fit_fair" : "reallife_hire_meet_heard_fit_poor",
                        "name", _s.P.Name);
                case HiringRules.TopicPay:
                    return TechText.Fill("reallife_hire_meet_heard_ask", "name", _s.P.Name, "ask", TechText.Rate(_s.Ask), "work", TechText.Work(_s.Want));
                default:
                    return TechText.T(_s.Fit >= 700 ? "reallife_hire_meet_heard_team_good" : "reallife_hire_meet_heard_team", "name", _s.P.Name, "b", TechText.Skill(_s.P.Def.SkillB));
            }
        }

        private void ShowSummary()
        {
            TravelList list = _window.Take(this);
            if (list == null)
            {
                End();
                return;
            }
            string body = TechText.Fill("reallife_hire_meet_summary", "name", _s.P.Name, "interest", TechText.Mille(_a.Interest),
                "skill", (_a.Revealed & HiringRules.RevealSkill) != 0 ? TechText.Percent(_s.P.Main / 1000) : TechText.T("reallife_hire_unknown"),
                "ask", (_a.Revealed & HiringRules.RevealAsk) != 0 ? TechText.Rate(_s.Ask) : TechText.T("reallife_hire_unknown"));
            list.Begin(Title, TechText.T("reallife_hire_meet_over"));
            list.AddNote(_s.P.Name, body, list.HeightFor((int)(body.Length * 1.3f), false));
            list.Add(Close, TechText.T("reallife_hire_meet_close"), TechText.T("reallife_hire_meet_close_detail"), ButtonDo, true);
            _over = true;
            Open(list);
            _director.Changed();
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
            bool was = Active;
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
            if (was)
            {
                _director.Changed();
                Notifications.Show(NotificationType.Info, "reallife_hire_note_interviewed", TechDirector.Data("name", _s != null ? _s.P.Name : "?"), 6f, null, null, false, false);
            }
        }

        public void Dispose()
        {
            End();
            _window.Destroy(e => TechDirector.Logger?.Error(e));
        }
    }
}
