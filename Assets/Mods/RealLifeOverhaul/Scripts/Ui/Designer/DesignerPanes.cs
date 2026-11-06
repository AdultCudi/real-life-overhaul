using System.Collections.Generic;
using UI.InteriorDesigner;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's own action panels of the designer, taken once as it opens from the list the game keeps of them (InteriorDesignerActionPanelUI), never
    /// searched for in the scene. The face mirrors what each holds and presses the widgets it made; a panel this version lacks is null and its tool shows
    /// the plain card instead.
    /// </summary>
    internal static class DesignerPanes
    {
        public static FurnitureActionPanelUi Furniture;
        public static InteriorElementActionPanelUI Elements;
        public static TimeOfDayActionPanelUI Daylight;
        public static SaveBlueprintActionPanelUI Blueprint;
        public static ItemActionPanelUI Producer, Queue, Security;

        private static readonly List<ActionPanelUI> Found = new List<ActionPanelUI>(10);

        /// <summary>The panels of this opening.</summary>
        public static void Look(InteriorDesignerUI ui)
        {
            Forget();
            object root = ui != null && GameContract.DesignerActionPanels.Field != null ? GameContract.DesignerActionPanels.Field.GetValue(ui) : null;
            var list = root != null && GameContract.DesignerPanelList.Field != null ? GameContract.DesignerPanelList.Field.GetValue(root) as List<ActionPanelUI> : null;
            if (list == null)
            {
                return;
            }
            Found.Clear();
            Found.AddRange(list);
            for (int i = 0; i < Found.Count; i++)
            {
                switch (Found[i])
                {
                    case FurnitureActionPanelUi furniture:
                        Furniture = furniture;
                        break;
                    case InteriorElementActionPanelUI elements:
                        Elements = elements;
                        break;
                    case TimeOfDayActionPanelUI daylight:
                        Daylight = daylight;
                        break;
                    case SaveBlueprintActionPanelUI blueprint:
                        Blueprint = blueprint;
                        break;
                    case ProducerActionPanelUi producer:
                        Producer = producer;
                        break;
                    case QueueActionPanelUi queue:
                        Queue = queue;
                        break;
                    case SecurityActionPanelUi security:
                        Security = security;
                        break;
                }
            }
            Found.Clear();
        }

        /// <summary>Every reference let go: a new city, or the interface off.</summary>
        public static void Forget()
        {
            Furniture = null;
            Elements = null;
            Daylight = null;
            Blueprint = null;
            Producer = Queue = Security = null;
        }

        /// <summary>The game's panel of a list tool (producers, counters, cameras), or null.</summary>
        public static ItemActionPanelUI ForList(UiDesignerTool tool)
        {
            switch (tool)
            {
                case UiDesignerTool.Producer:
                    return Producer;
                case UiDesignerTool.Queue:
                    return Queue;
                case UiDesignerTool.Security:
                    return Security;
                default:
                    return null;
            }
        }
    }
}
