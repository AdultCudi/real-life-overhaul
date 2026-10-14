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
    /// courses where the status mark would be, no row background and no mark), then the group's courses, each with its
    /// standing in the game's own row: not started (the game's own words), the classes so far and the next, the exam or
    /// retake to sit, an instalment owed, completed with its grade (and the game's tick), failed with the mark, withdrawn,
    /// barred. The rows' texts are keys the game's localizer fills, so they follow a change of language. Nothing is kept:
    /// the game lets the rows go the next time.
    ///
    /// THE BOX'S HEIGHT. Persona's left column (CharacterStats: a ScrollRect whose Content is a VerticalLayoutGroup that
    /// does not control its children's heights, with a ContentSizeFitter) stacks Energy, Hunger, Happiness and Education
    /// by their rectangles. The Education box is a fixed 1320 by 690 (its LayoutElement and its rectangle), made for the
    /// game's five rows; only its inner list (Content: a VerticalLayoutGroup and a ContentSizeFitter) grows. So rows added
    /// to the list were drawn past the box's white, and the column's scroll ended where the box did. After the rows are
    /// made the box is given the height they need (EducationRules.BoxHeight: the game's white under them kept) and the
    /// column is laid out again, so it scrolls to the last row; on each open the box first goes back to the game's own
    /// height. The rows' heights are the template's, so a change of language does not move them. If the rows are ever not
    /// straight in the box, or the box not in a scrolling column, only the courses begun are listed.
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
        private float _baseHeight = -1f;   // the game's own height of the box, as it was before the mod sized it

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
                    Size(-1f);   // the game's own height while the game's rows stand alone (last time's rows go this frame)
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
            var list = template.transform.parent as RectTransform;
            var boxRect = _box.transform as RectTransform;
            float rowsTop = 0f, gameRows = 0f;
            if (list != null && boxRect != null && list.parent == boxRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(list);   // the game's rows alone, laid out as they are drawn
                rowsTop = boxRect.rect.yMax - (list.localPosition.y + list.rect.yMax);
                gameRows = list.rect.height;
            }
            else
            {
                list = null;   // (rows not straight in the box: it cannot be sized to them, so only the begun ones)
            }
            bool scrolls = list != null && _box.GetComponentInParent<ScrollRect>() != null;
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
                    Heading(made as RectTransform, header, status, check);
                    continue;
                }
                CourseDef c = StudyCatalogue.Find(row.Course);
                Set(header, "reallife" + "_study_course_" + c.Key, null);
                Set(status, EducationRules.StatusKey(row.Status), Words(c, row.Status));
                check?.SetActive(row.Status.State == EducationState.Passed);
            }
            float height = -1f;
            if (list != null && _rows.Count > 0)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(list);   // every row now: the list's height as it is drawn
                height = EducationRules.BoxHeight(BaseHeight(), rowsTop, gameRows, list.rect.height);
            }
            Size(height);
            if (Meta.Dev.Verbose)
            {
                CareersDirector.Logger?.Info("Careers check (Persona education): after the game's own courses, " + courses + " of the mod's in " + headings + " groups"
                                             + (scrolls ? ", in the game's scrolling column, the box " + Mathf.RoundToInt(boxRect.rect.height) + " high for its rows"
                                                        : ", the begun ones only (the box does not scroll)") + ": PASS");
            }
        }

        // The box's game height, read the first time before the mod sizes it.
        private float BaseHeight()
        {
            if (_baseHeight < 0f && _box != null && _box.transform is RectTransform r)
            {
                _baseHeight = r.rect.height;
            }
            return _baseHeight;
        }

        // The box made the height its rows need (below 0: the game's own), and Persona's column laid out again to it, so
        // its white holds every row and its scroll reaches the last. The column's layout sizes the box by its rectangle
        // (it does not control its children's heights), and the box's LayoutElement says the same height.
        private void Size(float height)
        {
            if (!(_box != null && _box.transform is RectTransform r))
            {
                return;
            }
            float game = BaseHeight();
            if (height < 0f)
            {
                height = game;
            }
            if (Mathf.Approximately(r.rect.height, height))
            {
                return;
            }
            r.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutElement fixedSize = r.GetComponent<LayoutElement>();
            if (fixedSize != null && fixedSize.preferredHeight >= 0f)
            {
                fixedSize.preferredHeight = height;
            }
            LayoutRebuilder.MarkLayoutForRebuild(r);   // (up to the column's own list, whose fitter sets how far it scrolls)
        }

        // A heading row: no status mark (the grey ring is the mark's background, the tick inside it), the count where the
        // mark stood, and the name given the width the count does not need, so a long group name stays on one line.
        private static void Heading(RectTransform row, TextLocalizationComponent header, TextLocalizationComponent status, GameObject check)
        {
            Transform mark = check != null ? check.transform.parent : null;
            if (mark != null && mark != row)
            {
                mark.gameObject.SetActive(false);
            }
            else
            {
                check?.SetActive(false);
            }
            var name = header.TextContainer != null ? header.TextContainer.rectTransform : null;
            var count = status.TextContainer != null ? status.TextContainer.rectTransform : null;
            if (row == null || name == null || count == null)
            {
                return;
            }
            float right = -count.anchoredPosition.x;
            if (mark is RectTransform m && m != row)
            {
                right = Mathf.Max(0f, -m.anchoredPosition.x - m.rect.width * (1f - m.pivot.x));   // the mark's right edge
            }
            count.anchoredPosition = new Vector2(-right, count.anchoredPosition.y);
            float width = EducationRules.HeadingNameWidth(row.rect.width, name.anchoredPosition.x, right);
            if (width > name.rect.width)
            {
                name.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
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
