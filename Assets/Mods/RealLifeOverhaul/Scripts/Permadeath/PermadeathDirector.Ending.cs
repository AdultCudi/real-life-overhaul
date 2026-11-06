using System;
using System.Collections;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>The end of a life: the story gathered, the death written and saved, the obituary printed, the main menu.</summary>
    public partial class PermadeathDirector
    {
        // The life as it stands now, for the obituary: the game's figures, the street, the businesses, the mod's records.
        private void Gather(LifeStory s, DeathCause cause)
        {
            s.Clear();
            PermadeathGame.Fill(s);
            s.Cause = cause;
            s.Place = cause == DeathCause.Illness || cause == DeathCause.Starvation || cause == DeathCause.Test || cause == DeathCause.None
                ? string.Empty : PermadeathGame.Street();
            _keys.Clear();
            _names.Clear();
            try
            {
                PermadeathBridges.Businesses?.Invoke(_keys, _names);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            for (int i = 0; i < _keys.Count; i++)
            {
                PermadeathState.See(_keys[i]);
            }
            for (int i = 0; i < _names.Count; i++)
            {
                LifeStory.Keep(s.Businesses, _names[i], LifeStory.BusinessesMost);
            }
            s.Owned = _keys.Count;
            s.Founded = Mathf.Max(s.Founded, Mathf.Max(PermadeathState.Seen.Count, s.Owned));
            try
            {
                PermadeathBridges.Story?.Invoke(s);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // A death, or the rehearsal (not real: nothing is saved and the character lives on).
        private IEnumerator Dying(DeathCause cause, bool real)
        {
            _ending = true;
            PermadeathGame.CloseQuestion();
            try
            {
                Gather(_story, cause);
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // the page is written from what could be read
            }
            GameInstance save = PermadeathGame.Save;
            if (real)
            {
                // Written down before anything is shown: in the save's data, and on this machine for the older saves.
                PermadeathState.Dead = true;
                PermadeathState.Armed = true;
                PermadeathState.Record.CopyFrom(_story);
                PermadeathGame.Memorial.Keep(PermadeathGame.CharacterId, _story);
                if (save != null)
                {
                    PermadeathState.Write(Data(save));
                }
                Sync(false);
                Logger?.Info("Permadeath: " + (_story.Name.Length > 0 ? _story.Name : "the character") + " has died (" + cause + ") on day " + _story.Day + ", aged " + _story.Age + ".");
            }
            // Walking is held at once, the game's own death only once the screen is black, so a fall or a crash is seen
            // into the fade.
            PermadeathGame.Hold(true, false);
            yield return Safe(PermadeathGame.Fade(1.6f, "reallife_pd_blackout"));
            if (real)
            {
                PermadeathGame.Hold(true, true);
                yield return SaveIt();
            }
            yield return Safe(PermadeathGame.OutOfVehicle());
            yield return Print(_story, !real);
            if (real)
            {
                yield return Safe(PermadeathGame.Fade(0.6f, null));
                Logger?.Info("Permadeath: the obituary was put down; back to the main menu. The save's files are kept.");
                PermadeathGame.MainMenu();
                yield break;
            }
            PermadeathGame.Hold(false, false);
            Notifications.Show(NotificationType.Info, "reallife_pd_test_over", null, 10f);
            _ending = false;
        }

        // The game's own save, at once, tried again a few times if the game will not save this second.
        private IEnumerator SaveIt()
        {
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                bool saved = false;
                try
                {
                    saved = PermadeathGame.SaveNow();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                if (saved)
                {
                    float until = Time.realtimeSinceStartup + 30f;
                    while (PermadeathGame.Saving && Time.realtimeSinceStartup < until)
                    {
                        yield return null;
                    }
                    Logger?.Info("Permadeath check (the death is saved): the game's save "
                                 + (PermadeathGame.Saving ? "is still being written after 30 s: FAIL" : "is on disk at attempt " + attempt + ": PASS") + ".");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(1f);
            }
            Logger?.Warn("Permadeath: the game would not save (five tries). "
                         + "The death is kept on this machine, so this character's saves from a time Permadeath was on are read as ended.");
        }

        // A save that has ended: its obituary, then the main menu. Nothing of it is played.
        private IEnumerator Ended()
        {
            _ending = true;
            PermadeathGame.CloseQuestion();
            PermadeathGame.Hold(true, true);
            LifeStory story = _story;
            if (!_elsewhere)
            {
                if (PermadeathState.Record.Name.Length > 0)
                {
                    story.CopyFrom(PermadeathState.Record);
                }
                else
                {
                    Gather(story, PermadeathState.Record.Cause);   // no readable record: written from what the save says now
                }
            }
            Logger?.Info("Permadeath: this save has ended" + (_elsewhere ? " (the character died in another save since)" : string.Empty) + "; its obituary, then the main menu.");
            yield return Safe(PermadeathGame.Fade(0.5f, "reallife_pd_ended"));
            yield return Safe(PermadeathGame.OutOfVehicle());
            yield return Print(story, false);
            yield return Safe(PermadeathGame.Fade(0.6f, null));
            PermadeathGame.MainMenu();
        }

        // A step of the game's (a fade, leaving a car) that faults is logged and passed over, so nobody is left behind a
        // black screen.
        private static IEnumerator Safe(IEnumerator step)
        {
            while (true)
            {
                object current;
                try
                {
                    if (step == null || !step.MoveNext())
                    {
                        yield break;
                    }
                    current = step.Current;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    yield break;
                }
                yield return current;
            }
        }

        // The obituary on the City Ledger's page, or as a message if that cannot be had, until it is put down.
        private IEnumerator Print(LifeStory story, bool test)
        {
            ObituaryPage page = PermadeathText.Page(story, test);
            _closed = false;
            bool shown = false;
            float until = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < until)
            {
                // The game's menu will not open over a question of its own: it closes the question instead.
                PermadeathGame.CloseQuestion();
                bool asked = false;
                try
                {
                    asked = PermadeathBridges.ShowObituary != null && PermadeathBridges.ShowObituary(page, _onClosed);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                if (asked)
                {
                    float open = Time.realtimeSinceStartup + 2f;
                    while (!PermadeathGame.MenuOpen && Time.realtimeSinceStartup < open)
                    {
                        yield return null;
                    }
                    if (PermadeathGame.MenuOpen)
                    {
                        shown = true;
                        break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            if (!shown)
            {
                Logger?.Warn("Permadeath: the City Ledger's page could not be had; the obituary is shown as a message.");
                Meta.MetaUi.Message(page.Header, page.Plain(), new KeyValuePair<string, Action>("reallife_meta_ok", _onClosed));
            }
            else
            {
                Logger?.Info("Permadeath check (the obituary): on the City Ledger's page, " + page.Texts.Count + " sections, "
                             + (page.BoxLines.Count > 0 ? page.BoxLines[0].Count : 0) + " figures: PASS.");
            }
            _pageUp = shown;
            if (UI.UiFader.isFading)
            {
                yield return Safe(PermadeathGame.UnFade(0.8f));
            }
            while (!_closed)
            {
                yield return null;
            }
            _pageUp = false;
        }

        private void PageClosed()
        {
            _closed = true;
        }
    }
}
