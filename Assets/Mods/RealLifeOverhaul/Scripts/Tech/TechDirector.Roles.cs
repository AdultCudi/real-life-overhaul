using System;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The Open Roles app on the phone, and what the player does with a professional there.</summary>
    public sealed partial class TechDirector
    {
        private PhoneApp _rolesApp;
        private RolesApp _rolesView;
        private bool _rolesGaveUp;
        private int _layoffs, _layoffsDay = -1;

        private void InstallRoles()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new RolesApp(this);
            GameSprites.Want(RolesGlyphs);
            _rolesApp = PhoneApp.Install(new PhoneAppDef
            {
                Id = RolesAppId,
                TitleKey = "reallife_roles_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(RolesGlyphs), new Color32(96, 176, 168, 255), new Color32(40, 112, 120, 255), 0.42f, "AppIcon-OpenRoles"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_rolesApp == null)
            {
                _rolesGaveUp = !tryAgain;
                if (_rolesGaveUp)
                {
                    Logger?.Warn("Tech: the game's phone is not built as this version expects, so the Open Roles app is left off. "
                                 + "Professionals are still worked through each day and the notifications still say what needs an answer.");
                }
                return;
            }
            _rolesView = view;
            RolesBadge();
        }

        private static readonly string[] RolesGlyphs = { "Icon-Employees", "Icon-Building" };

        private static Sprite Glyph(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Sprite sprite = GameSprites.Get(names[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        public void OpenRoles(int tab, string pick)
        {
            if (_rolesApp != null && _rolesView != null)
            {
                _rolesView.Want(tab, pick);
                _rolesApp.Open();
            }
        }

        /// <summary>
        /// The app has opened: everyone is read again so it shows today, and an offer of the sector's whose employee the
        /// game's own rival offer has reached since gives way before it is drawn.
        /// </summary>
        internal bool RolesOpened()
        {
            if (_broken || Save == null || !_peopleOn)
            {
                return false;
            }
            try
            {
                ReadPeople(Save.Day);
                if (GiveWayAll(Save.Day))
                {
                    Changed();
                }
                RolesBadge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        /// <summary>How many of the player's people wait for an answer: a request for a rise, a rival's offer, a promotion open.</summary>
        internal int Waiting()
        {
            int n = 0, day = Save != null ? Save.Day : 0;
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                n += p.RaiseAsk > 0 || TalentRules.OfferOpen(p, day) || TalentRules.CanPromote(p, day, Year, _people[i].MainPoints) ? 1 : 0;
            }
            return n;
        }

        private void RolesBadge()
        {
            if (_rolesApp != null)
            {
                _rolesApp.SetBadge(_rolesApp.Showing ? 0 : Waiting());
            }
        }

        internal Person PersonOf(string id)
        {
            for (int i = 0; i < _people.Count; i++)
            {
                if (_people[i].File.Id == id)
                {
                    return _people[i];
                }
            }
            return null;
        }

        internal void RefuseRise(string id)
        {
            Person person = PersonOf(id);
            if (person != null && person.File.RaiseAsk > 0)
            {
                TalentRules.Refused(person.File);
                TechBridges.Standing?.Invoke(-1);
                Changed();
            }
        }

        internal bool Promote(string id)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || !TalentRules.CanPromote(person.File, Save.Day, Year, person.MainPoints) || Rested(person))
            {
                return false;
            }
            TalentRules.Promote(person.File, Save.Day);
            Raise(person, TalentCatalogue.PromotePayBp, Save.Day);
            TechBridges.Standing?.Invoke(1);
            Tell(NotificationType.Success, "reallife_talent_note_promoted", Data("name", person.Name, "level", TechText.Level(person.File.Level)), TabTeam, Row(person.File));
            Changed();
            return true;
        }

        internal void SetRole(string id, int role)
        {
            Person person = PersonOf(id);
            RoleDef r = TalentCatalogue.Role(role);
            if (person != null && (person.Game == null ? !r.InGame || r.GameSkill == person.Skill : r.GameSkill == person.Skill))
            {
                TalentRules.Move(person.File, role);
                if (!r.Remote && person.File.Work == (int)Arrangement.Remote)
                {
                    person.File.Work = (int)Arrangement.Hybrid;
                }
                Changed();
            }
        }

        internal void SetWork(string id, int work)
        {
            Person person = PersonOf(id);
            if (person == null)
            {
                return;
            }
            Arrangement a = (Arrangement)TalentCatalogue.Clamp(work, 0, 2);
            if (a == Arrangement.Remote && (!person.File.Def.Remote || person.Game != null))
            {
                return;   // the game's employees work in the building: hybrid is as far as they go
            }
            person.File.Work = (int)a;
            Changed();
        }

        /// <summary>A course in one of the role's specialisms, paid now by the business; false if the cash does not cover it.</summary>
        internal bool Course(string id, int slot)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || person.File.OnCourse(Save.Day))
            {
                return false;
            }
            int fee = (int)((long)Prices.At(TalentRules.CourseFee(person.File.Slot(slot) / 1000)) * TechBridges.CourseShare() / 10000);
            if (!TalentGame.Cost(fee * 100, TalentGame.TrainingKey, person.At, person.Name, false, false))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            TalentRules.StartCourse(person.File, slot, Save.Day, -1);
            Changed();
            return true;
        }

        /// <summary>A degree at Careers' university paid by the business. False if on a course, the university is closed or the cash is short.</summary>
        internal bool SponsorDegree(string id)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || person.File.OnCourse(Save.Day) || TechBridges.DegreeFee == null)
            {
                return false;
            }
            int fee = TechBridges.DegreeFee();
            if (fee <= 0)
            {
                return false;
            }
            if (!TalentGame.Cost(fee * 100, TalentGame.TrainingKey, person.At, person.Name, false, false))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            TalentRules.StartDegree(person.File, Save.Day, TalentCatalogue.Scaled(TalentCatalogue.DegreeDays, Year));
            Changed();
            return true;
        }

        /// <summary>An exam for a certificate, paid now; false if the score is short of it or the cash does not cover it.</summary>
        internal bool Exam(string id, int cert)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || person.File.OnCourse(Save.Day) || person.File.Has(cert) || TalentRules.ExamChance(person.File, cert) <= 0)
            {
                return false;
            }
            int fee = Prices.At(TalentCatalogue.Certs[cert].Fee);
            if (!TalentGame.Cost(fee * 100, TalentGame.TrainingKey, person.At, person.Name, false, false))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            int slot = TalentCatalogue.Certs[cert].Skill == person.File.Def.SkillA ? 0 : 1;
            TalentRules.StartCourse(person.File, slot, Save.Day, cert);
            Changed();
            return true;
        }

        /// <summary>A restraint clause added to the contract: they are paid more for it and rivals come for them half as often.</summary>
        internal void AddClause(string id)
        {
            Person person = PersonOf(id);
            if (person != null && !person.File.NonCompete)
            {
                person.File.NonCompete = true;
                Raise(person, TalentCatalogue.NonCompeteBp, Save != null ? Save.Day : person.File.RecognisedDay);
                Changed();
            }
        }

        internal void AnswerRival(string id, bool match)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || !TalentRules.OfferOpen(person.File, Save.Day))
            {
                return;
            }
            if (person.Game != null && TalentRules.GivesWay(person.File, TalentGame.GameOffer(person.Game)))
            {
                GaveWay(person, Save.Day);   // the game's own offer came while this page was open: it is the one to answer
                Changed();
                return;
            }
            if (match)
            {
                Raise(person, person.File.PoachBp, Save.Day);   // an offer comes at most once in PoachRestDays: its own period
            }
            TalentRules.Answer(person.File, match, Save.Day, Year);
            Changed();
        }

        /// <summary>Notice given by the player: a lay-off (with severance at the end) or, where it is fair, a dismissal at once.</summary>
        internal bool LetGo(string id, bool dismiss)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || person.File.NoticeEnd > 0)
            {
                return false;
            }
            if (dismiss && !TalentRules.FairDismissal(person.File, Save.Day, Year))
            {
                return false;
            }
            TalentRules.Notice(person.File, Save.Day, Year, dismiss ? LeaveKind.Dismissed : LeaveKind.LaidOff);
            TechBridges.Standing?.Invoke(dismiss ? 0 : -1);
            if (!dismiss)
            {
                _layoffs = _layoffsDay == Save.Day ? _layoffs + 1 : 1;
                _layoffsDay = Save.Day;
                if (_layoffs == TalentCatalogue.LayoffsNews && person.At != null)
                {
                    Story("reallife_tech_news_layoffs", "business", person.Business, "count", _layoffs.ToString());
                }
            }
            if (dismiss)
            {
                Go(person, Save.Day);
                ReadPeople(Save.Day);
            }
            Changed();
            return true;
        }
    }
}
