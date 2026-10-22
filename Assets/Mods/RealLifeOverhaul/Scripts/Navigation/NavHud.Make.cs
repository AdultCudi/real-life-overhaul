using System;
using System.Collections.Generic;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>The minimap's part that makes its panel from a copy of the game's task panel.</summary>
    public sealed partial class NavHud
    {
        private NavHud(NavGraph graph, GameObject dormant, GameObject panel, UIs uis, Transform hints, Transform tasks, Transform lineLike, Color32 ground, Color32 road, Color32 routeColour)
        {
            _graph = graph;
            _dormant = dormant;
            _panel = panel;
            _rect = (RectTransform)panel.transform;
            _screen = (RectTransform)uis.playerHUD.transform;
            _tasksUi = uis.tasksUI;
            _tasks = tasks as RectTransform;
            _hints = hints as RectTransform;
            _labels = uis.playerHUD.nearestBuildingText != null ? uis.playerHUD.nearestBuildingText.transform as RectTransform : null;
            _item = uis.playerHUD.itemPanelUI != null ? uis.playerHUD.itemPanelUI.transform.Find("Panel") as RectTransform : null;

            // The copy is only a picture: nothing on it is clicked, scrolled or laid out by the game.
            Take<ScrollRect>(panel);
            Take<ContentSizeFitter>(panel);
            var graphics = new List<Graphic>(16);
            panel.GetComponentsInChildren(true, graphics);
            for (int i = 0; i < graphics.Count; i++)
            {
                graphics[i].raycastTarget = false;
            }
            panel.AddComponent<Canvas>();   // re-batched by itself, with the HUD canvas's order and scale

            _head = (RectTransform)panel.transform.Find("Headline");
            Take<Button>(_head.gameObject);
            Take<LayoutElement>(_head.gameObject);
            _head.anchorMin = new Vector2(0f, 1f);
            _head.anchorMax = new Vector2(1f, 1f);
            _head.pivot = new Vector2(0.5f, 1f);
            _head.offsetMin = new Vector2(Side, -Head);
            _head.offsetMax = new Vector2(-Side, 0f);
            Transform label = _head.Find("Label");
            _title = label != null ? label.GetComponent<TMP_Text>() : null;
            Transform fold = _head.Find("Icon");
            if (fold != null)
            {
                fold.gameObject.SetActive(false);   // the task panel's fold arrows: this panel does not fold
            }
            if (_title is TMP_Text titleText)
            {
                titleText.textWrappingMode = TextWrappingModes.NoWrap;
                titleText.overflowMode = TextOverflowModes.Ellipsis;
            }

            _view = (RectTransform)panel.transform.Find("Viewport");
            _view.anchorMin = new Vector2(0f, 1f);
            _view.anchorMax = new Vector2(1f, 1f);
            _view.pivot = new Vector2(0.5f, 1f);

            RectTransform groundRect = Child("Ground", _view);
            Stretch(groundRect);
            Image groundImage = groundRect.gameObject.AddComponent<Image>();
            groundImage.color = ground;
            groundImage.raycastTarget = false;

            _base = Child("Base", _view);
            _baseImage = _base.gameObject.AddComponent<RawImage>();
            _baseImage.raycastTarget = false;
            _baseImage.enabled = false;   // until the picture is there, and never with its option off

            _roads = Child("Roads", _view);
            _roadsImage = _roads.gameObject.AddComponent<RawImage>();
            _roadsImage.color = road;
            _roadsImage.raycastTarget = false;
            _roadsImage.enabled = false;   // until the picture is there

            _spin = Child("Spin", _view);
            _spin.sizeDelta = Vector2.zero;
            _world = Child("World", _spin);
            _world.sizeDelta = Vector2.zero;
            RectTransform routeRect = Child("Route", _world);
            routeRect.sizeDelta = Vector2.zero;
            _route = routeRect.gameObject.AddComponent<NavRouteLine>();
            _route.color = routeColour;
            _route.raycastTarget = false;

            Sprite blob = NavMarks.Blob;
            for (int i = 0; i < Pool; i++)
            {
                _marker[i] = Marker("Marker", _world, blob, MarkerSize, out _markerBlob[i], out _markerIcon[i]);
                _marker[i].gameObject.SetActive(false);
                _markerShows[i] = -1;
            }
            _goal = Marker("Goal", _view, blob, GoalSize, out _goalBlob, out _goalIcon);
            _goal.gameObject.SetActive(false);

            RectTransform player = Marker("Player", _view, blob, GoalSize, out Image playerBlob, out Image playerIcon);
            _playerIcon = playerIcon.rectTransform;
            if (uis.mapFilters != null)
            {
                playerBlob.color = uis.mapFilters.playerFilterColor;
                playerIcon.sprite = uis.mapFilters.playerIcon;
                playerIcon.enabled = playerIcon.sprite != null;
            }
            player.anchoredPosition = Vector2.zero;

            _line1 = Line("Line 1", lineLike, new Color32(255, 255, 255, 255));
            _line2 = Line("Line 2", lineLike, new Color32(202, 205, 206, 255));   // the grey of the task panel's group labels
            UiClone.Key(_title, "reallife_nav_title");
            _titleShown = null;
        }

        /// <summary>
        /// Copies the game's task panel and puts the minimap in it. Null if it cannot be done; <paramref name="later"/> then
        /// says whether to ask again, the HUD not being there yet, or not, one line having said what was missing.
        /// </summary>
        public static NavHud Build(NavGraph graph, out bool later)
        {
            later = true;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.playerHUD == null || uis.tasksUI == null || uis.mapFilters == null || InstanceBehavior<GlobalReferences>.Instance == null)
            {
                return null;
            }
            later = false;
            Transform tasks = UiClone.Need(uis.tasksUI.transform, "Container/Panel", "the task panel (the minimap is a copy of it)");
            if (tasks == null || tasks.Find("Headline") == null || tasks.Find("Viewport") == null)
            {
                if (tasks != null)
                {
                    UiClone.Fallback("the task panel has no 'Headline' or no 'Viewport': the minimap is left off");
                }
                return null;
            }
            using var piece = SessionWatch.Piece("the minimap's panel");
            NavMarks.Prepare();   // the round base of the game's map markers
            Transform dormant = UiClone.Dormant(uis.playerHUD.transform, DormantName);
            GameObject panel = UiClone.Asleep(tasks.gameObject, dormant, PanelName);
            if (panel == null)
            {
                UnityEngine.Object.Destroy(dormant.gameObject);
                return null;
            }
            // The scroll bar and the task entries go first, then the game's scripts come off what is left.
            UiClone.KeepOnly(panel.transform, "Headline", "Viewport");
            UiClone.KeepOnly(panel.transform.Find("Viewport"));
            UiClone.StripGameScripts(panel, null);
            Transform entry = uis.tasksUI.taskEntryTemplate != null ? uis.tasksUI.taskEntryTemplate.Find("Label") : null;
            Transform lineLike = entry != null ? entry : panel.transform.Find("Headline/Label");
            if (entry == null)
            {
                UiClone.Fallback("the task entry's 'Label' was not found: the GPS's lines use the headline's lettering");
            }
            Colors colours = InstanceBehavior<GlobalReferences>.Instance.colors;
            Color32 ground = colours.midnight;
            ground.a = 150;
            Color32 road = colours.lightGrey;
            road.a = 235;
            Color32 route = colours.blue;
            route.a = 255;
            Transform hints = uis.transform.Find("ControlsHintUIController/Container");
            if (hints == null)
            {
                UiClone.Fallback("the control hints were not found: the minimap stands at the bottom of the left column whether they show or not");
            }
            NavHud hud;
            try
            {
                hud = new NavHud(graph, dormant.gameObject, panel, uis, hints, tasks, lineLike, ground, road, route);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("the minimap could not be put together from the task panel and is left off");
                UnityEngine.Object.Destroy(dormant.gameObject);
                return null;
            }
            // Off, then onto the HUD under everything else there, so a dialog of the game's covers it.
            panel.SetActive(false);
            panel.transform.SetParent(uis.playerHUD.transform, false);
            panel.transform.SetAsFirstSibling();
            hud._rect.anchorMin = Vector2.zero;
            hud._rect.anchorMax = Vector2.zero;
            hud._rect.pivot = Vector2.zero;
            hud._rect.localScale = Vector3.one;
            hud._rect.localRotation = Quaternion.identity;
            UnityEngine.Object.Destroy(dormant.gameObject);
            return hud;
        }

        private static void Take<T>(GameObject from) where T : Component
        {
            T found = from.GetComponent<T>();
            if (found != null)
            {
                UnityEngine.Object.DestroyImmediate(found);
            }
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var made = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)made.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // A marker as the game's map draws one: the round base in the place's colour, the place's picture on it.
        private static RectTransform Marker(string name, Transform parent, Sprite blob, float size, out Image blobImage, out Image iconImage)
        {
            RectTransform rect = Child(name, parent);
            rect.sizeDelta = new Vector2(size, size);
            blobImage = rect.gameObject.AddComponent<Image>();
            blobImage.sprite = blob;
            blobImage.enabled = blob != null;
            blobImage.raycastTarget = false;
            RectTransform icon = Child("Icon", rect);
            icon.sizeDelta = new Vector2(size * 0.56f, size * 0.56f);
            iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            return rect;
        }

        // One of the two lines: a copy of a task entry's text, with nothing left on it but the lettering.
        private TMP_Text Line(string name, Transform like, Color32 colour)
        {
            GameObject copy = like != null ? UiClone.Asleep(like.gameObject, _dormant.transform, name) : null;
            if (copy == null)
            {
                return null;
            }
            UiClone.StripGameScripts(copy, null);
            for (int i = copy.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(copy.transform.GetChild(i).gameObject);
            }
            Take<Button>(copy);
            Take<CanvasGroup>(copy);
            Take<LayoutElement>(copy);
            Take<TextLocalizationComponent>(copy);   // the words are final when they are written; the game's key must not write over them
            var text = copy.GetComponent<TMP_Text>();
            if (text == null)
            {
                UnityEngine.Object.DestroyImmediate(copy);
                return null;
            }
            copy.transform.SetParent(_panel.transform, false);
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.localScale = Vector3.one;
            text.raycastTarget = false;
            text.color = colour;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Left;
            text.margin = Vector4.zero;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = true;
            text.fontSizeMax = 30f;
            text.fontSizeMin = 21f;
            text.text = string.Empty;
            copy.SetActive(false);
            return text;
        }
    }
}
