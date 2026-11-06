using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.BizPace;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone.Apps.BizMan.StartBusiness;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The competitors in the city on BizMan's start screen cards of the mod's six kinds of business, written in the frame the
    /// game makes its cards (Core/GameContract, part start_competitors). The game's own cards are left as they are. Cost: nothing per frame.
    /// </summary>
    internal static class StartCards
    {
        public const string LabelKey = "reallife_bizman_competitors_city";

        private static StartCardsWatch _watch;
        private static bool _failed;
        private static string _said;

        /// <summary>Once a second from the tech sector's timer: puts the watcher on this city's start screen if it is not there yet.</summary>
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
                return;   // the phone is not built yet
            }
            try
            {
                if (GameContract.StartCompetitors.Off)
                {
                    _failed = true;
                    return;   // the contract says so once
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
                    TechDirector.Logger?.Info(
                        "Tech check (BizMan's start screen): the competitors in the city are written on the mod's own cards each time the screen opens: PASS");
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

        private static readonly int BizWrite = HotSpots.Slot("Tech: StartCards.Write");

        /// <summary>Goes over the cards once, just after the game made them, those it is letting go this frame too.</summary>
        internal static void Write(Transform holder)
        {
            using var timed = BizPaceWatch.Time(BizWrite);   // a step of BizMan's start screen opening (BizPace)
            TechDirector director = TechDirector.Instance;
            FieldInfo typeField = GameContract.StartCardType.Field, labelField = GameContract.StartCardLabel.Field;
            FieldInfo amountField = GameContract.StartCardAmount.Field;
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
                StartBusinessTypeUI card = child.gameObject.activeSelf ? child.GetComponent<StartBusinessTypeUI>() : null;   // the template stays inactive
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
                ? "Tech check (BizMan's start screen): the competitors in the city on the mod's own cards (" + said + "), " + game
                  + " card(s) of the game's own kinds left as the game wrote them: PASS"
                : "Tech check (BizMan's start screen): no card of the mod's own kinds on this building's screen, " + game + " of the game's left as it wrote them: PASS";
            if (line != _said)
            {
                _said = line;
                TechDirector.Logger?.Info(line);
            }
        }
    }

    /// <summary>On the holder of the start screen's cards: wakes the writer when the game adds or lets go a card. No frame cost: no Update.</summary>
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

    /// <summary>On the same holder: goes over the cards once in the frame the game made them, then switches itself off. Idle off.</summary>
    public sealed class StartCardsWrite : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("tech_sector", "StartCardsWrite", typeof(StartCardsWrite));
        private static readonly int HotLateUpdate = HotSpots.Slot("Tech: StartCardsWrite.LateUpdate");

        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // the feature was switched off after its faults
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
