using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Extensions;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UI.Smartphone.Apps.Persona;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Persona app's EDUCATION box with the mod's courses after the game's own (Careers/Study/EducationRules).
    ///
    /// HOW THE GAME FILLS IT. The box (UI.Smartphone.Apps.Persona.EducationInfo) is filled each time it is switched on, as
    /// the app opens: it lets go of last time's rows (every copy of its row template, ResetTemplate) and makes one copy of
    /// the template for each of the game's courses (CreateElement), each set up with the course's name, its standing
    /// ("Not started yet", "{n}% completed", "Completed") and a tick when it is completed (EducationInfoEntry.Setup).
    ///
    /// WHAT IS ADDED. A component of the mod's sits on the box. When the box is switched on it waits one frame (so the
    /// game has made its rows and let go of the old ones, the mod's with them) and then makes more copies of the same
    /// template, as the game does: for each group of the mod's courses a heading (its name in bold, the count of its
    /// courses, no row background), then the group's courses, each with its standing in the game's own row: not started
    /// (the game's own words), the classes so far and the next, the exam or retake to sit, an instalment owed, completed
    /// with its grade (and the game's tick), failed with the mark, withdrawn, barred. The rows' texts are keys the game's
    /// localizer fills, so they follow a change of language. The box sits in the game's own scrolling column; if it ever
    /// does not, only the courses begun are listed. Nothing is kept: the game lets the rows go the next time.
    ///
    /// The template and the row's three parts are private to the game's box and row: they are looked up once. If they are
    /// not there (a game that changed them), the box keeps the game's rows alone, with one line in the log.
    ///
    /// GOING PUBLIC. The rows are the player's own studies on this machine; nothing is sent.
    ///
    /// THE COST. Nothing per frame: once a second the director checks the component is on the box (a null test), and the
    /// rows are made once each time the app opens (about fifty copies of the game's row, as many as the game's own lists).
    /// </summary>
    public sealed class PersonaEducation : MonoBehaviour
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static FieldInfo _template, _header, _status, _check;
        private static bool _resolved, _works, _noBox;
        private static PersonaEducation _on;
        private static readonly int HotBuild = HotSpots.Slot("Careers: PersonaEducation.Build");

        private readonly List<EducationRow> _rows = new List<EducationRow>(64);
        private EducationInfo _box;

        /// <summary>Once a second while studying is on: the component is put on the game's education box if it is not there yet.</summary>
        internal static void Keep()
        {
            if (_on != null || _noBox || !Resolve())
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform apps = uis != null && uis.fullMenu != null ? uis.fullMenu.appsContainer : null;
            Transform persona = apps != null ? apps.Find(AppName.Persona.ToString()) : null;
            if (persona == null)
            {
                return;   // (the phone is not built yet)
            }
            EducationInfo box = persona.GetComponentInChildren<EducationInfo>(true);
            if (box == null)
            {
                _noBox = true;
                CareersDirector.Logger?.Warn("Careers: the Persona app has no education box as this version expects; it lists the game's own courses alone.");
                return;
            }
            _on = box.GetComponent<PersonaEducation>();
            if (_on == null)
            {
                _on = box.gameObject.AddComponent<PersonaEducation>();
            }
            _on._box = box;
            if (Meta.Dev.Verbose)
            {
                CareersDirector.Logger?.Info("Careers check (Persona education): the mod's courses are added to the game's education box each time it opens: PASS");
            }
        }

        private static bool Resolve()
        {
            if (_resolved)
            {
                return _works;
            }
            _resolved = true;
            try
            {
                _template = typeof(EducationInfo).GetField("educationEntryTemplate", Hidden);
                _header = typeof(EducationInfoEntry).GetField("headerLabel", Hidden);
                _status = typeof(EducationInfoEntry).GetField("statusLabel", Hidden);
                _check = typeof(EducationInfoEntry).GetField("checkmarkObj", Hidden);
                _works = _template != null && _header != null && _status != null && _check != null;
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
                _works = false;
            }
            if (!_works)
            {
                CareersDirector.Logger?.Warn("Careers: the Persona app's education rows are not as this version expects; it lists the game's own courses alone.");
            }
            return _works;
        }

        private void OnEnable()
        {
            try
            {
                if (_box == null)
                {
                    _box = GetComponent<EducationInfo>();   // (put on while the app was open: this runs before Keep has said which box)
                }
                if (_box != null && isActiveAndEnabled)
                {
                    StartCoroutine(Build());
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
        }

        // One frame after the box is switched on: the game's rows are made and last time's are gone.
        private IEnumerator Build()
        {
            yield return null;
            using var hotSpot = HotSpots.Time(HotBuild);
            try
            {
                Fill();
            }
            catch (Exception e) when (GameMembers.StandsDown(e, "the mod's courses in the Persona app's education box"))
            {
                _works = false;
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
        }

        private void Fill()
        {
            CareersDirector director = CareersDirector.Instance;
            StudyFile f = director != null ? director.Study : null;
            GameInstance save = SaveGameManager.Current;
            if (f == null || save == null || !_works || _box == null)
            {
                return;   // (studying is off: the game's courses alone)
            }
            var template = _template.GetValue(_box) as Component;
            if (template == null || template.transform.parent == null)
            {
                return;
            }
            bool scrolls = _box.GetComponentInParent<ScrollRect>() != null;
            int courses = EducationRules.Rows(f, CareersDirector.Offered, !scrolls, save.Day, _rows);
            int headings = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                EducationRow row = _rows[i];
                Transform made = template.transform.CreateElement();   // (the game's own copy of its row, as its box makes them)
                EducationInfoEntry entry = made.GetComponent<EducationInfoEntry>();
                var header = entry != null ? _header.GetValue(entry) as TextLocalizationComponent : null;
                var status = entry != null ? _status.GetValue(entry) as TextLocalizationComponent : null;
                var check = entry != null ? _check.GetValue(entry) as GameObject : null;
                if (header == null || status == null)
                {
                    Destroy(made.gameObject);
                    continue;
                }
                if (row.Heading)
                {
                    headings++;
                    Set(header, EducationRules.GroupKey(row.Group), null);
                    if (header.TextContainer != null)
                    {
                        header.TextContainer.fontStyle |= FontStyles.Bold;
                    }
                    Set(status, EducationRules.CountKey(row.Count), CareersDirector.Data("count", row.Count.ToString()));
                    Image back = made.GetComponent<Image>();
                    if (back != null)
                    {
                        back.enabled = false;   // a heading, not a course: on the box's own white
                    }
                    check?.SetActive(false);
                    continue;
                }
                CourseDef c = StudyCatalogue.Find(row.Course);
                Set(header, "reallife" + "_study_course_" + c.Key, null);
                Set(status, EducationRules.StatusKey(row.Status), Words(c, row.Status));
                check?.SetActive(row.Status.State == EducationState.Passed);
            }
            if (Meta.Dev.Verbose)
            {
                CareersDirector.Logger?.Info("Careers check (Persona education): after the game's own courses, " + courses + " of the mod's in " + headings + " groups"
                                             + (scrolls ? ", in the game's scrolling column" : ", the begun ones only (the box does not scroll)") + ": PASS");
            }
        }

        // A label of the game's row set as the game sets one: the words to fill it with first, then its key.
        private static void Set(TextLocalizationComponent label, string key, Dictionary<string, string> words)
        {
            label.Arguments = words;
            label.Key = key;
        }

        // What a standing's words are filled with.
        private static Dictionary<string, string> Words(CourseDef c, EducationStatus s)
        {
            switch (s.State)
            {
                case EducationState.Studying:
                case EducationState.FeeOwed:
                    var d = CareersDirector.Data("done", s.Done.ToString(), "of", s.Of.ToString());
                    if (s.NextDay > 0)
                    {
                        d["day"] = CareersText.Day(s.NextDay);
                        d["hour"] = CareersText.Hour(s.NextHour);
                    }
                    return d;
                case EducationState.Exam:
                case EducationState.Retake:
                    return CareersDirector.Data("day", CareersText.Day(s.Day));
                case EducationState.Passed:
                    return CareersDirector.Data("grade", CareersText.Grade(c, s.Grade));
                case EducationState.Failed:
                    return CareersDirector.Data("score", s.Score.ToString());
                default:
                    return null;
            }
        }
    }
}
