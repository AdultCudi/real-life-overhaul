using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone.Apps.BizMan.StartBusiness;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// BizMan's start screen with the competitors in the city on the cards of the mod's own six kinds of business: the
    /// software studio, the game studio, the IT consultancy, the cybersecurity firm, the AI lab (Tech/Firms/TechTypes) and
    /// the training academy (Careers/AcademyType).
    ///
    /// HOW THE GAME MAKES THE SCREEN (decompiled, UI.Smartphone.Apps.BizMan.StartBusiness). Each time it is shown
    /// (StartBusinessUI.Show, then Load) it lets last time's cards go and makes one copy of its card (StartBusinessTypeUI)
    /// under its private holder ("businessesContainer") for each kind of business the building may hold; each card writes
    /// its competitors as it is set up (Initialize, SetCompetitorsAmount): the label, a localised text with the game's key
    /// ("Neighborhood Competitors"), and the number, the count of the district's buildings whose business is of the card's
    /// kind (StartBusinessUI.GetCompetitors), "-" for a headquarters. For the mod's six it is always 0: no building of the
    /// city holds one but the player's own, as the game's rivals open only the game's kinds, and the mod's twelve tech
    /// companies stand in three towers the game holds as empty buildings (Tech/Sites).
    ///
    /// WHAT IS DONE. A watcher of the mod's on the holder hears the game add its cards (OnTransformChildrenChanged) and wakes
    /// a writer, whose LateUpdate in the same frame, after the game has set every card up, goes over the cards once and
    /// sleeps again. On a card of the mod's own kinds (CompetitorRules.InCity), while its feature is on (the tech sector;
    /// the academy's part of the careers), the label's key is the mod's ("Competitors in the city": a change of language
    /// writes it again in the new one) and the number is the competitors' rule's count for the kind
    /// (TechDirector.InCity: the same rule as the Tech app's Competitors page). The game's own kinds' cards are left
    /// exactly as the game wrote them. The cards are the game's and go at the next opening, which is written again;
    /// nothing is put back and nothing is saved. In a Going Public session another player's business is counted, never
    /// changed, and nothing is sent.
    ///
    /// The holder and the card's three parts are private to the game: they are read through the game contract
    /// (Core/GameContract, part start_competitors). Without them the screen keeps the game's own counts, with one line in
    /// the log.
    ///
    /// Cost: nothing per frame. The writer is switched off but for the frame after the game makes its cards, and the
    /// watcher has no Update; once a second the tech sector's timer tests that the watcher stands on this city's screen.
    /// </summary>
    internal static class StartCards
    {
        public const string LabelKey = "reallife_bizman_competitors_city";

        private static StartCardsWatch _watch;
        private static bool _failed;
        private static string _said;

        /// <summary>Once a second from the tech sector's timer: the watcher is put on the start screen of this city's BizMan if it is not there yet.</summary>
        internal static void Keep()
        {
            if (_watch != null || _failed)
            {
                return;
            }
            global::UI.UIs uis = InstanceBehavior<global::UI.UIs>.Instance;
            Component bizMan = uis != null && uis.fullMenu != null ? uis.fullMenu.bizMan : null;
            if (bizMan == null)
            {
                return;   // (the phone is not built yet)
            }
            try
            {
                if (GameContract.StartCompetitors.Off)
                {
                    _failed = true;
                    return;   // (said once by the contract)
                }
                StartBusinessUI screen = bizMan.GetComponentInChildren<StartBusinessUI>(true);
                Transform holder = screen != null ? GameContract.StartCardsHolder.Field?.GetValue(screen) as Transform : null;
                if (holder == null)
                {
                    _failed = true;
                    TechDirector.Logger?.Warn("Tech: BizMan's start screen is not built as this version expects; its cards keep the game's own count of competitors.");
                    return;
                }
                StartCardsWrite writer = holder.GetComponent<StartCardsWrite>();
                if (writer == null)
                {
                    writer = holder.gameObject.AddComponent<StartCardsWrite>();
                }
                writer.enabled = false;
                StartCardsWatch watch = holder.GetComponent<StartCardsWatch>();
                if (watch == null)
                {
                    watch = holder.gameObject.AddComponent<StartCardsWatch>();
                }
                watch.Writer = writer;
                _watch = watch;
                _said = null;
                if (Meta.Dev.Verbose)
                {
                    TechDirector.Logger?.Info("Tech check (BizMan's start screen): the competitors in the city are written on the mod's own cards each time the screen opens: PASS");
                }
            }
            catch (Exception e)
            {
                _failed = true;
                TechDirector.Logger?.Error(e);
            }
        }

        /// <summary>The screen was hidden: the next opening's check line is written again.</summary>
        internal static void Hidden()
        {
            _said = null;
        }

        /// <summary>
        /// The cards under the holder gone over once, just after the game made them: those of the mod's own kinds say their
        /// competitors in the city. Cards the game is letting go this frame are written too; they go at its end.
        /// </summary>
        internal static void Write(Transform holder)
        {
            TechDirector director = TechDirector.Instance;
            FieldInfo typeField = GameContract.StartCardType.Field, labelField = GameContract.StartCardLabel.Field, amountField = GameContract.StartCardAmount.Field;
            if (director == null || holder == null || typeField == null || labelField == null || amountField == null || SaveGameManager.Current == null)
            {
                return;
            }
            bool tech = Settings.TechSector;
            string academy = TechBridges.AcademyNow();
            bool read = true;
            int game = 0;
            StringBuilder said = Meta.Dev.Verbose ? new StringBuilder(160) : null;
            for (int i = 0; i < holder.childCount; i++)
            {
                Transform child = holder.GetChild(i);
                StartBusinessTypeUI card = child.gameObject.activeSelf ? child.GetComponent<StartBusinessTypeUI>() : null;   // (the template stays inactive)
                if (card == null)
                {
                    continue;
                }
                var type = typeField.GetValue(card) as BusinessType;
                string name = type != null ? type.businessTypeName : null;
                int kind = FirmsGame.KindOf(name);
                if (kind < 0 && academy != null && name == academy)
                {
                    kind = CompetitorRules.Academy;
                }
                if (!CompetitorRules.InCity(kind) || (kind != CompetitorRules.Academy && !tech))
                {
                    game++;
                    continue;
                }
                var label = labelField.GetValue(card) as TextLocalizationComponent;
                var amount = amountField.GetValue(card) as TMP_Text;
                if (label == null || amount == null)
                {
                    continue;
                }
                int count = director.InCity(kind, read);
                read = false;
                UiClone.Key(label, LabelKey);
                amount.text = count.ToString(CultureInfo.InvariantCulture);
                if (said != null)
                {
                    said.Append(said.Length > 0 ? ", " : string.Empty).Append(name).Append(' ').Append(count);
                }
            }
            if (said == null)
            {
                return;
            }
            string line = said.Length > 0
                ? "Tech check (BizMan's start screen): the competitors in the city on the mod's own cards (" + said + "), " + game + " card(s) of the game's own kinds left as the game wrote them: PASS"
                : "Tech check (BizMan's start screen): no card of the mod's own kinds on this building's screen, " + game + " of the game's left as it wrote them: PASS";
            if (line != _said)
            {
                _said = line;
                TechDirector.Logger?.Info(line);
            }
        }
    }

    /// <summary>On the holder of BizMan's start screen's cards: wakes the writer when the game adds or lets go a card. No Update.</summary>
    public sealed class StartCardsWatch : MonoBehaviour
    {
        internal StartCardsWrite Writer;

        private void OnTransformChildrenChanged()
        {
            try
            {
                if (Writer != null)
                {
                    Writer.enabled = true;
                }
            }
            catch (Exception e)
            {
                TechDirector.Logger?.Error(e);
            }
        }

        private void OnDisable()
        {
            StartCards.Hidden();
        }
    }

    /// <summary>On the same holder: goes over the cards once in the frame the game made them, then switches itself off.</summary>
    public sealed class StartCardsWrite : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("tech_sector", "StartCardsWrite", typeof(StartCardsWrite));   // its LateUpdate (Core/Guard)
        private static readonly int HotLateUpdate = HotSpots.Slot("Tech: StartCardsWrite.LateUpdate");

        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            enabled = false;
            StartCards.Write(transform);
        }
    }
}
