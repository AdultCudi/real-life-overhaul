using System;
using System.Collections.Generic;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.Floors;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part on the screen for show flats and refits: the game's own panel on the show flat's floor while a viewing
    /// is on (GameUi.DoorPanel), the way to the show flat on the game's destination guider (GameUi.GuideMark), and a renovation
    /// asked from a business's Interior tab in the game's own question box (Core/GameConfirm). Once a second while the player is on
    /// the show flat's floor, else on a press; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const float ViewingReach = 3.5f;   // m from where the buyers are met: the panel offers the viewing

        private DoorPanel _viewing;
        private Action _showRound;
        private Func<string> _viewingWhat;
        private int _viewingKey;
        private GuideMark _showMark;

        // Once a second: on the show flat's floor during a viewing, the panel where the buyers are met while the player stands there.
        private void ViewingSecond()
        {
            ProjectFile p = _state.ShowFlat();
            if (p == null || !InShowFlat || !_showPlace.Spot(out Vector3 spot, out _))
            {
                HideViewing("no viewing here now");
                return;
            }
            int hour = ConstructionGame.Hour;
            if (ShowFlatRules.ViewingAt(hour, ShowFlatRules.Viewings(ShowQuality(p), p.ShowStandard)) < 0)
            {
                HideViewing("no viewing this hour");
                return;
            }
            Vector3 player = ConstructionGame.Player;
            Vector3 flat = spot - player;
            flat.y = 0f;
            if (flat.sqrMagnitude > ViewingReach * ViewingReach)
            {
                HideViewing("the player walked away");
                return;
            }
            if (_viewing == null)
            {
                _viewing = new DoorPanel(Logger, "Show flats check (viewing panel)");
                _showRound = _viewing.Press(() => Answer(ShowRound()));
                _viewingWhat = () => "a viewing at the show flat at " + ConstructionGame.Hour + ":00";
            }
            int key = HourKey(ConstructionGame.Day, hour);
            if (_viewingKey != key || !_viewing.Up)
            {
                _viewingKey = key;
                bool shown = p.ShowShown == key;
                WorldPanel panel = _viewing.Panel;
                panel.Lines(ConstructionText.T("reallife_bld_viewing_title", "name", ProjectName(p)),
                    ConstructionText.T(shown ? "reallife_bld_viewing_shown" : "reallife_bld_viewing_line"), null, WorldPanel.White());
                panel.Button(0, shown ? null : "reallife_bld_viewing_show", shown ? null : _showRound, !shown, !shown);
                panel.Button(1, null, null, false, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
                panel.Follow = null;
            }
            _viewing.Show(spot, player, 2.1f, _viewingWhat);
        }

        private void HideViewing(string why)
        {
            _viewing?.Hide(why);
        }

        // A press's answer in the game's own notice.
        private static void Answer(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                Notifications.Show(NotificationType.Info, key, null, 6f, null, null, true, false);
            }
        }

        /// <summary>The way to the show flat's building on the game's destination guider; false while it has no floor. Returns a text key through why.</summary>
        public bool GuideToShow(out string why)
        {
            if (!ShowWhere(out FloorWhere at))
            {
                why = "reallife_bld_answer_show_no_floor";
                return false;
            }
            if (_showMark == null)
            {
                _showMark = new GuideMark("the show flat").Keep(this).While(() => _showPlace != null && !InShowFlat);
            }
            string name = ConstructionText.T("reallife_bld_show_mark", "floor", at.Floor.ToString(), "address", ConstructionGame.AddressText(at.Street, at.Number));
            why = _showMark.Show(at.Door, name) ? "reallife_bld_answer_show_marked" : "reallife_bld_answer_show_mark_busy";
            return true;
        }

        // ------------------------------------------------------------------ a renovation from the Interior tab

        /// <summary>
        /// A renovation asked from a business's Interior tab: a licensed company of the player's makes its refit project and the app
        /// opens on it; otherwise the city's best renovation contractor's quote in the game's own question box.
        /// </summary>
        public void RenovateAsk(string street, int number)
        {
            string block = RefitBlock(street, number);
            if (block != null)
            {
                Answer(block);
                return;
            }
            if (Licensed)
            {
                int made = NewRefit(street, number, out string why);
                if (made > 0)
                {
                    Open(TabProjects, made);
                }
                Answer(made > 0 ? "reallife_bld_answer_refit_planned" : why);
                return;
            }
            ConstructionGame.Business(street, number, out BusinessView b);
            RefitQuote q = QuoteFor(b);
            LanguageChangeEventDataHolder header = "reallife_bld_confirm_refit".Localize();
            LanguageChangeEventDataHolder body = "reallife_bld_confirm_refit_body".Localize(new Dictionary<string, string>
            {
                { "name", b.Name ?? string.Empty }, { "contractor", ConstructionText.T(BuildCatalogue.RivalKeys[q.Rival]) }, { "amount", ConstructionText.Money(q.Price) },
                { "days", q.Days.ToString() }
            });
            GameConfirm.Show(header, body, () => UiClone.Safe(() =>
            {
                int made = ContractorRefit(street, number, false, out string why);
                Answer(made > 0 ? "reallife_bld_answer_refit_ordered" : why);
            }), null, null, null, true, false);
        }
    }
}
