using System;
using System.Collections;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;
using Localizor.LanguageChangeEvent;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A dialog of the mod that is a talk with one person, and so heads its pages with how old they are.</summary>
    public interface ITalkAge
    {
        /// <summary>Who is talking, as their name stands in the header; null when the talk has nobody.</summary>
        string TalkWho { get; }

        /// <summary>Their age as words ("34 years old", "in their thirties"), the exact age once the player knows them or has asked; null for none.</summary>
        string TalkAge { get; }
    }

    /// <summary>
    /// The age in the header of a talk with a person, on the game's own dialogue panel (and so on the kit, which reads the same panel).
    /// The game heads a page only when the page has a header key, and fills it with no values; a page of one of the mod's talks
    /// (Guard.Entry) gets the key of "{who}, {age}" and the values are put in the panel's header label in the frame after the game has made
    /// the page's copy (it fades in over a second, so nothing raw is seen). The kit's conversation does not read this header for
    /// its name: it takes the name as it always did and shows the age under it (<see cref="Of"/>). Cost: one short coroutine per page of
    /// a talk with a person; nothing per frame.
    /// </summary>
    public static class TalkAgeGame
    {
        private static readonly FeatureGuard Fault = Guard.For("social", "TalkAgeGame", typeof(TalkAgeGame));
        private const int Frames = 10;   // frames the page may take to be put up (a street talk waits for the model's words first)
        private static ITalkAge _making;  // the talk whose constructor is showing its first page: the game sets the controller's dialog only after it returns

        /// <summary>Held by a talk's constructor (<c>using var talk = TalkAgeGame.Making(this)</c>) while it shows its first page.</summary>
        public readonly struct Scope : IDisposable
        {
            public void Dispose()
            {
                _making = null;
            }
        }

        public static Scope Making(ITalkAge talk)
        {
            _making = talk;
            return default;
        }

        /// <summary>The age words of the talk the game runs now, null when it is not a talk with a person.</summary>
        public static string Of(object dialog)
        {
            try
            {
                return !Fault.Off && dialog is ITalkAge talk ? talk.TalkAge : null;
            }
            catch (Exception e)
            {
                Guard.Fail(Fault, e);
                return null;
            }
        }

        /// <summary>A page about to be shown (Guard.Entry): headed with who is talking and how old, when the talk is with a person.</summary>
        public static void Mark(DialogEntry page)
        {
            if (page == null || Fault.Off || page.Template != DialogEntry.TemplateType.Text || !string.IsNullOrEmpty(page.headerKey))   // (a form's header is its question)
            {
                return;
            }
            try
            {
                DialogController dialogs = DialogController.current;
                var talk = dialogs != null ? dialogs.dialog as ITalkAge ?? _making : null;
                string who = talk != null ? talk.TalkWho : null, age = talk != null ? talk.TalkAge : null;
                if (string.IsNullOrEmpty(who) || string.IsNullOrEmpty(age))
                {
                    return;
                }
                page.headerKey = TalkAgeRules.HeadKey;
                dialogs.StartCoroutine(Guard.Routine(Put(dialogs, page, who, age)));
            }
            catch (Exception e)
            {
                Guard.Fail(Fault, e);
            }
        }

        // The header's values, once the game has made this page's copy of its template.
        private static IEnumerator Put(DialogController dialogs, DialogEntry page, string who, string age)
        {
            for (int i = 0; i < Frames; i++)
            {
                yield return null;
                if (dialogs == null || Fault.Off)
                {
                    yield break;
                }
                if (Fill(dialogs, page, who, age))
                {
                    yield break;
                }
            }
        }

        private static bool Fill(DialogController dialogs, DialogEntry page, string who, string age)
        {
            if (GameContract.DialogEntryNow.Field == null || GameContract.DialogEntryView.Field == null
                || !ReferenceEquals(GameContract.DialogEntryNow.Field.GetValue(dialogs), page))
            {
                return false;
            }
            var view = GameContract.DialogEntryView.Field.GetValue(dialogs) as CanvasGroup;
            Transform header = view != null ? view.transform.Find("HeaderLabel") : null;
            var label = header != null ? header.GetComponent<TextLocalizationComponent>() : null;
            if (label == null)
            {
                return true;   // a panel with no header label: nothing to put it in
            }
            label.Arguments = new Dictionary<string, string>(2) { { "who", who }, { "age", age } };
            header.gameObject.SetActive(true);
            return true;
        }
    }
}
