using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>Where a page is set, which decides what the picture above the text shows.</summary>
    public enum JusticeScene
    {
        Court,      // the bench, the prosecutor, the defence
        Cell,       // bars; an officer and another inmate
        Office      // a file on a desk: the Legal file, letters, summaries
    }

    /// <summary>Who a portrait frame shows. None hides the frame.</summary>
    public enum JusticeFace
    {
        None,
        Judge,
        Prosecutor,
        Counsel,
        Officer,
        Inmate,
        Visitor,
        Clerk
    }

    /// <summary>One page of the justice system's full-screen panel: what it says and what can be answered.</summary>
    public sealed class JusticePage
    {
        public JusticeScene Scene;
        public string Title;
        public string Subtitle;
        public JusticeFace Left, Middle, Right;
        public string LeftName, MiddleName, RightName;
        public int Speaker = -1;        // 0 left, 1 middle, 2 right; -1 nobody
        public string SpeakerName;      // written above the text
        public string Body;
        public readonly string[] Choices = new string[JusticePanel.MostChoices];
        public readonly string[] Hints = new string[JusticePanel.MostChoices];
        public int Count;

        public JusticePage Add(string choice, string hint = null)
        {
            if (Count < JusticePanel.MostChoices)
            {
                Choices[Count] = choice;
                Hints[Count] = hint;
                Count++;
            }
            return this;
        }
    }

    /// <summary>
    /// The justice system's one full-screen panel: the courtroom, the cell and the Legal file are
    /// all pages of it. A picture drawn with plain shapes (portrait frames for whoever is there,
    /// the one speaking lit), a text, and up to three answers that can be clicked or picked with
    /// the keys 1 to 3. It covers the whole screen and is opaque, so nothing behind it needs
    /// hiding. Built the first time it is shown, kept switched off otherwise; its Update runs only
    /// while it is open (two key tests and one comparison a frame, nothing allocated).
    /// While it is open it keeps an invisible element of its own selected: the game then blocks
    /// its own keyboard shortcuts, and the mod's cameras give the mouse cursor back.
    /// </summary>
    public sealed class JusticePanel : MonoBehaviour
    {
        public const int MostChoices = 3;

        private const float InputDelay = 0.4f;    // seconds after a page appears before an answer counts: no double presses

        private static readonly Color Backdrop = new Color(0.035f, 0.040f, 0.055f, 1f);
        private static readonly Color Plate = new Color(0.085f, 0.095f, 0.125f, 1f);
        private static readonly Color PlateLit = new Color(0.14f, 0.18f, 0.26f, 1f);
        private static readonly Color Accent = new Color(0.86f, 0.70f, 0.32f, 1f);     // brass
        private static readonly Color Ink = new Color(0.05f, 0.05f, 0.07f, 1f);
        private static readonly Color Soft = new Color(0.72f, 0.76f, 0.83f, 1f);
        private static readonly Color Wood = new Color(0.27f, 0.17f, 0.10f, 1f);
        private static readonly Color WoodDark = new Color(0.17f, 0.10f, 0.06f, 1f);
        private static readonly Color Steel = new Color(0.34f, 0.37f, 0.42f, 1f);
        private static readonly Color Paper = new Color(0.86f, 0.84f, 0.78f, 1f);

        private static JusticePanel _instance;

        private Canvas _canvas;
        private TMP_FontAsset _font;
        private Texture2D _discTexture;
        private Sprite _disc;
        private GameObject _focus;

        private TextMeshProUGUI _title, _subtitle, _speaker, _body;
        private GameObject _court, _cell, _office;
        private readonly Portrait[] _portraits = new Portrait[3];
        private readonly ChoiceButton[] _buttons = new ChoiceButton[MostChoices];

        private Action<int> _answer;
        private int _count;
        private float _shownAt;
        private bool _open;

        private sealed class Portrait
        {
            public GameObject Root;
            public Image Frame, Backing, Body, Head, Hair, Detail;
            public TextMeshProUGUI Name;
        }

        private sealed class ChoiceButton
        {
            public GameObject Root;
            public Image Plate;
            public TextMeshProUGUI Label, Hint;
        }

        /// <summary>Created the first time a page is shown and destroyed with the city scene.</summary>
        public static JusticePanel Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Justice Panel").AddComponent<JusticePanel>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        /// <summary>True while a page is on screen.</summary>
        public static bool IsOpen => _instance != null && _instance._open;

        /// <summary>True only if the panel is open and really being drawn: what the watchdog asks.</summary>
        public static bool IsShowing => _instance != null && _instance._open && _instance._canvas != null && _instance._canvas.enabled
                                        && _instance.gameObject.activeInHierarchy;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30000;   // over everything, the game's fade to black included
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            gameObject.layer = 5;   // UI
            _font = Hud.Font;
            Build();
            _canvas.enabled = false;
            enabled = false;
        }

        private void OnDestroy()
        {
            if (_disc != null)
            {
                Destroy(_disc);
            }
            if (_discTexture != null)
            {
                Destroy(_discTexture);
            }
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ---------------------------------------------------------------- showing

        /// <summary>Puts a page up. <paramref name="answer"/> is called with 0, 1 or 2 when the player picks; it shows the next page or closes the panel.</summary>
        public void Show(JusticePage page, Action<int> answer)
        {
            _answer = answer;
            _count = Mathf.Clamp(page.Count, 1, MostChoices);
            _shownAt = Time.unscaledTime;
            _title.SetText(page.Title ?? string.Empty);
            _subtitle.SetText(page.Subtitle ?? string.Empty);
            _speaker.SetText(page.SpeakerName ?? string.Empty);
            _body.SetText(page.Body ?? string.Empty);

            _court.SetActive(page.Scene == JusticeScene.Court);
            _cell.SetActive(page.Scene == JusticeScene.Cell);
            _office.SetActive(page.Scene == JusticeScene.Office);
            Dress(_portraits[0], page.Left, page.LeftName, page.Speaker == 0);
            Dress(_portraits[1], page.Middle, page.MiddleName, page.Speaker == 1);
            Dress(_portraits[2], page.Right, page.RightName, page.Speaker == 2);

            for (int i = 0; i < MostChoices; i++)
            {
                bool has = i < _count;
                _buttons[i].Root.SetActive(has);
                if (has)
                {
                    _buttons[i].Label.SetText(page.Choices[i] ?? string.Empty);
                    _buttons[i].Hint.SetText(page.Hints[i] ?? string.Empty);
                    // One answer sits in the middle; two sit either side of it; three fill the row.
                    float x = _count == 1 ? 0f : _count == 2 ? (i == 0 ? -300f : 300f) : (i - 1) * 590f;
                    ((RectTransform)_buttons[i].Root.transform).anchoredPosition = new Vector2(x, 112f);
                }
            }
            if (!_open)
            {
                _open = true;
                _canvas.enabled = true;
                enabled = true;
            }
        }

        public void Close()
        {
            _answer = null;
            if (!_open)
            {
                return;
            }
            _open = false;
            _canvas.enabled = false;
            enabled = false;
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == _focus)
            {
                events.SetSelectedGameObject(null);
            }
        }

        private void Update()
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != _focus)
            {
                events.SetSelectedGameObject(_focus);
            }
            if (Time.unscaledTime - _shownAt < InputDelay)
            {
                return;
            }
            int choice = JobKeys.ChoiceDown();
            if (choice < 0 && _count == 1 && Keys.Down(KeyAction.Interact))
            {
                choice = 0;
            }
            if (choice >= 0 && choice < _count)
            {
                Pick(choice);
            }
        }

        private void Pick(int choice)
        {
            if (!_open || choice >= _count || Time.unscaledTime - _shownAt < InputDelay)
            {
                return;
            }
            Action<int> answer = _answer;
            if (answer == null)
            {
                Close();
                return;
            }
            _shownAt = Time.unscaledTime;   // whatever the answer shows next gets its own delay
            try
            {
                answer(choice);
            }
            catch (Exception e)
            {
                // An answer that fails must not leave its page up with the player held behind it: the director lets go of everything.
                Close();
                if (JusticeDirector.Instance != null)
                {
                    JusticeDirector.Instance.PanelFault(e);
                }
            }
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            RectTransform back = Box("Backdrop", transform, Vector2.zero, Vector2.zero, Vector2.zero, Backdrop);
            back.anchorMin = Vector2.zero;
            back.anchorMax = Vector2.one;
            back.offsetMin = Vector2.zero;
            back.offsetMax = Vector2.zero;
            back.GetComponent<Image>().raycastTarget = true;   // nothing behind the panel can be clicked

            // What is kept selected while the panel is open (see the class comment). It draws nothing.
            _focus = new GameObject("Focus", typeof(RectTransform));
            _focus.transform.SetParent(transform, false);
            _focus.layer = 5;
            var selectable = _focus.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;
            var none = new Navigation { mode = Navigation.Mode.None };
            selectable.navigation = none;

            Box("Rule", transform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(1500f, 3f), Accent);
            _title = Text("Title", transform, 40f, Color.white, TextAlignmentOptions.Center, false);
            Place(_title.rectTransform, new Vector2(0f, -22f), new Vector2(1500f, 50f), new Vector2(0.5f, 1f));
            _subtitle = Text("Subtitle", transform, 22f, Soft, TextAlignmentOptions.Center, false);
            Place(_subtitle.rectTransform, new Vector2(0f, -76f), new Vector2(1500f, 30f), new Vector2(0.5f, 1f));
            _subtitle.fontStyle = FontStyles.Normal;

            // The picture: 1500 x 300, under the rule.
            RectTransform picture = Box("Picture", transform, new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(1500f, 300f), Plate);
            _disc = MakeDisc();
            BuildCourt(picture);
            BuildCell(picture);
            BuildOffice(picture);
            _portraits[0] = BuildPortrait(picture, -500f);
            _portraits[1] = BuildPortrait(picture, 0f);
            _portraits[2] = BuildPortrait(picture, 500f);

            _speaker = Text("Speaker", transform, 26f, Accent, TextAlignmentOptions.Left, false);
            Place(_speaker.rectTransform, new Vector2(-750f, -446f), new Vector2(1500f, 34f), new Vector2(0.5f, 1f));
            _speaker.rectTransform.pivot = new Vector2(0f, 1f);

            RectTransform sheet = Box("Sheet", transform, new Vector2(0.5f, 1f), new Vector2(0f, -678f), new Vector2(1500f, 380f), Plate);
            _body = Text("Body", sheet, 25f, Color.white, TextAlignmentOptions.TopLeft, true);
            _body.rectTransform.anchorMin = Vector2.zero;
            _body.rectTransform.anchorMax = Vector2.one;
            _body.rectTransform.offsetMin = new Vector2(26f, 18f);
            _body.rectTransform.offsetMax = new Vector2(-26f, -18f);
            _body.fontStyle = FontStyles.Normal;
            _body.lineSpacing = 8f;
            _body.enableAutoSizing = true;   // a long verdict still fits: the text shrinks before it is cut
            _body.fontSizeMin = 16f;
            _body.fontSizeMax = 25f;

            for (int i = 0; i < MostChoices; i++)
            {
                _buttons[i] = BuildButton(i);
            }
        }

        private ChoiceButton BuildButton(int index)
        {
            var button = new ChoiceButton();
            RectTransform plate = Box("Choice " + (index + 1), transform, new Vector2(0.5f, 0f), new Vector2((index - 1) * 590f, 112f), new Vector2(570f, 150f), PlateLit);
            button.Root = plate.gameObject;
            button.Plate = plate.GetComponent<Image>();
            button.Plate.raycastTarget = true;
            var click = plate.gameObject.AddComponent<Button>();
            click.targetGraphic = button.Plate;
            click.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colours = click.colors;
            colours.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colours.colorMultiplier = 1.4f;
            click.colors = colours;
            int captured = index;
            click.onClick.AddListener(() => Pick(captured));   // made once, here: nothing is allocated when a page is shown

            RectTransform tag = Box("Key", plate, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(38f, 38f), Accent);
            TextMeshProUGUI key = Text("Text", tag, 24f, Ink, TextAlignmentOptions.Center, false);
            Fill(key.rectTransform);
            key.SetText(JobKeys.ChoiceLabel(index));

            button.Label = Text("Label", plate, 24f, Color.white, TextAlignmentOptions.TopLeft, true);
            Place(button.Label.rectTransform, new Vector2(60f, -10f), new Vector2(496f, 62f), new Vector2(0f, 1f));
            button.Label.enableAutoSizing = true;
            button.Label.fontSizeMin = 17f;
            button.Label.fontSizeMax = 24f;
            button.Hint = Text("Hint", plate, 18f, Soft, TextAlignmentOptions.TopLeft, true);
            Place(button.Hint.rectTransform, new Vector2(16f, -76f), new Vector2(540f, 68f), new Vector2(0f, 1f));
            button.Hint.fontStyle = FontStyles.Normal;
            button.Hint.enableAutoSizing = true;
            button.Hint.fontSizeMin = 13f;
            button.Hint.fontSizeMax = 18f;
            return button;
        }

        // The courtroom: panelling, the bench with its seal, the rail, a flag.
        private void BuildCourt(Transform picture)
        {
            RectTransform court = Group("Court", picture);
            _court = court.gameObject;
            Box("Panelling", court, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1500f, 220f), WoodDark);
            for (int i = -3; i <= 3; i++)
            {
                Box("Panel", court, new Vector2(0.5f, 0.5f), new Vector2(i * 214f, 40f), new Vector2(4f, 220f), Wood);
            }
            Box("Bench", court, new Vector2(0.5f, 0f), new Vector2(0f, 55f), new Vector2(520f, 110f), Wood);
            Box("Bench Top", court, new Vector2(0.5f, 0f), new Vector2(0f, 114f), new Vector2(560f, 12f), WoodDark);
            Disc("Seal", court, new Vector2(0.5f, 0f), new Vector2(0f, 56f), 62f, Accent);
            Disc("Seal Inner", court, new Vector2(0.5f, 0f), new Vector2(0f, 56f), 46f, Wood);
            Box("Rail", court, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1500f, 8f), Wood);
            Box("Flag Pole", court, new Vector2(0.5f, 0f), new Vector2(330f, 150f), new Vector2(6f, 250f), Accent);
            Box("Flag", court, new Vector2(0.5f, 0f), new Vector2(366f, 232f), new Vector2(66f, 44f), new Color(0.62f, 0.13f, 0.14f, 1f));
            Box("Flag Stripe", court, new Vector2(0.5f, 0f), new Vector2(366f, 232f), new Vector2(66f, 10f), new Color(0.90f, 0.90f, 0.88f, 1f));
        }

        // The cell: a wall, bars, a bunk, a small high window.
        private void BuildCell(Transform picture)
        {
            RectTransform cell = Group("Cell", picture);
            _cell = cell.gameObject;
            Box("Wall", cell, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 300f), new Color(0.20f, 0.21f, 0.22f, 1f));
            Box("Window", cell, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(150f, 70f), new Color(0.42f, 0.55f, 0.68f, 1f));
            for (int i = -2; i <= 2; i++)
            {
                Box("Window Bar", cell, new Vector2(0.5f, 1f), new Vector2(i * 28f, -52f), new Vector2(5f, 70f), Steel);
            }
            Box("Bunk", cell, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(420f, 18f), Steel);
            Box("Blanket", cell, new Vector2(0.5f, 0f), new Vector2(-40f, 64f), new Vector2(300f, 18f), new Color(0.36f, 0.38f, 0.30f, 1f));
            Box("Pillow", cell, new Vector2(0.5f, 0f), new Vector2(160f, 68f), new Vector2(80f, 22f), new Color(0.80f, 0.80f, 0.76f, 1f));
            Box("Leg", cell, new Vector2(0.5f, 0f), new Vector2(-200f, 20f), new Vector2(10f, 40f), Steel);
            Box("Leg", cell, new Vector2(0.5f, 0f), new Vector2(200f, 20f), new Vector2(10f, 40f), Steel);
            for (int i = 0; i < 9; i++)
            {
                Box("Bar", cell, new Vector2(0.5f, 0.5f), new Vector2(-735f + i * 26f, 0f), new Vector2(8f, 300f), Steel);
                Box("Bar", cell, new Vector2(0.5f, 0.5f), new Vector2(735f - i * 26f, 0f), new Vector2(8f, 300f), Steel);
            }
        }

        // The office: a desk top with a file and a lamp's pool of light.
        private void BuildOffice(Transform picture)
        {
            RectTransform office = Group("Office", picture);
            _office = office.gameObject;
            Box("Desk", office, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 300f), WoodDark);
            Disc("Light", office, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), 330f, new Color(0.33f, 0.22f, 0.13f, 1f));
            RectTransform folder = Box("Folder", office, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(360f, 232f), new Color(0.66f, 0.53f, 0.30f, 1f));
            folder.localRotation = Quaternion.Euler(0f, 0f, 3f);
            RectTransform sheet = Box("Sheet", office, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(320f, 214f), Paper);
            sheet.localRotation = Quaternion.Euler(0f, 0f, -2f);
            for (int i = 0; i < 6; i++)
            {
                Box("Line", sheet, new Vector2(0.5f, 1f), new Vector2(i == 0 ? -50f : 0f, -34f - i * 28f), new Vector2(i == 0 ? 160f : 260f, i == 0 ? 8f : 4f), new Color(0.30f, 0.30f, 0.34f, 1f));
            }
            Disc("Stamp", sheet, new Vector2(1f, 0f), new Vector2(-48f, 44f), 58f, new Color(0.62f, 0.13f, 0.14f, 1f));
            Disc("Stamp Inner", sheet, new Vector2(1f, 0f), new Vector2(-48f, 44f), 44f, Paper);
        }

        private Portrait BuildPortrait(Transform picture, float x)
        {
            var portrait = new Portrait();
            RectTransform frame = Box("Portrait", picture, new Vector2(0.5f, 0.5f), new Vector2(x, 12f), new Vector2(190f, 236f), Ink);
            portrait.Root = frame.gameObject;
            portrait.Frame = frame.GetComponent<Image>();
            RectTransform backing = Box("Backing", frame, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(178f, 196f), Plate);
            portrait.Backing = backing.GetComponent<Image>();
            backing.gameObject.AddComponent<RectMask2D>();   // shoulders wider than the frame are cut at its edge
            portrait.Body = Disc("Shoulders", backing, new Vector2(0.5f, 0f), new Vector2(0f, -30f), 200f, Color.gray);
            portrait.Detail = Box("Detail", backing, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(26f, 52f), Color.white).GetComponent<Image>();
            portrait.Hair = Disc("Hair", backing, new Vector2(0.5f, 0f), new Vector2(0f, 128f), 92f, Color.black);
            portrait.Head = Disc("Head", backing, new Vector2(0.5f, 0f), new Vector2(0f, 116f), 82f, Color.white);
            portrait.Name = Text("Name", frame, 18f, Color.white, TextAlignmentOptions.Center, false);
            Place(portrait.Name.rectTransform, new Vector2(0f, 2f), new Vector2(186f, 24f), new Vector2(0.5f, 0f));
            return portrait;
        }

        // Who is in a frame: the clothes, the hair, one detail (a collar, a tie, a badge), the name plate; lit when they speak.
        private static void Dress(Portrait portrait, JusticeFace face, string name, bool speaking)
        {
            if (face == JusticeFace.None)
            {
                portrait.Root.SetActive(false);
                return;
            }
            portrait.Root.SetActive(true);
            Color clothes, hair, detail, skin;
            switch (face)
            {
                case JusticeFace.Judge:
                    clothes = new Color(0.07f, 0.07f, 0.09f); hair = new Color(0.78f, 0.78f, 0.76f); detail = new Color(0.92f, 0.92f, 0.90f); skin = new Color(0.86f, 0.70f, 0.58f);
                    break;
                case JusticeFace.Prosecutor:
                    clothes = new Color(0.12f, 0.17f, 0.30f); hair = new Color(0.20f, 0.13f, 0.09f); detail = new Color(0.62f, 0.13f, 0.14f); skin = new Color(0.78f, 0.60f, 0.47f);
                    break;
                case JusticeFace.Counsel:
                    clothes = new Color(0.30f, 0.31f, 0.34f); hair = new Color(0.10f, 0.09f, 0.08f); detail = new Color(0.20f, 0.44f, 0.62f); skin = new Color(0.60f, 0.43f, 0.32f);
                    break;
                case JusticeFace.Officer:
                    clothes = new Color(0.13f, 0.20f, 0.36f); hair = new Color(0.10f, 0.12f, 0.20f); detail = new Color(0.86f, 0.70f, 0.32f); skin = new Color(0.84f, 0.66f, 0.54f);
                    break;
                case JusticeFace.Inmate:
                    clothes = new Color(0.85f, 0.45f, 0.12f); hair = new Color(0.16f, 0.12f, 0.10f); detail = new Color(0.75f, 0.38f, 0.10f); skin = new Color(0.70f, 0.52f, 0.40f);
                    break;
                case JusticeFace.Visitor:
                    clothes = new Color(0.22f, 0.45f, 0.36f); hair = new Color(0.45f, 0.28f, 0.14f); detail = new Color(0.90f, 0.88f, 0.80f); skin = new Color(0.88f, 0.72f, 0.60f);
                    break;
                default:
                    clothes = new Color(0.42f, 0.36f, 0.30f); hair = new Color(0.30f, 0.22f, 0.16f); detail = new Color(0.90f, 0.90f, 0.86f); skin = new Color(0.82f, 0.65f, 0.52f);
                    break;
            }
            portrait.Body.color = clothes;
            portrait.Hair.color = hair;
            portrait.Detail.color = detail;
            portrait.Head.color = skin;
            portrait.Frame.color = speaking ? Accent : Ink;
            portrait.Backing.color = speaking ? PlateLit : Plate;
            portrait.Name.color = speaking ? Ink : Color.white;
            portrait.Name.SetText(name ?? string.Empty);
            portrait.Root.transform.localScale = speaking ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
        }

        // ---------------------------------------------------------------- building blocks

        private static RectTransform Group(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Fill(rect);
            go.AddComponent<RectMask2D>();   // nothing of a scene is drawn outside the picture
            return rect;
        }

        private static RectTransform Box(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private Image Disc(string objectName, Transform parent, Vector2 anchor, Vector2 position, float diameter, Color color)
        {
            Image image = Box(objectName, parent, anchor, position, new Vector2(diameter, diameter), color).GetComponent<Image>();
            image.sprite = _disc;
            return image;
        }

        private TextMeshProUGUI Text(string objectName, Transform parent, float size, Color color, TextAlignmentOptions alignment, bool wrap)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.overflowMode = TextOverflowModes.Ellipsis;
            // The SDK's TextMeshPro is older than the game's: this property is the one both versions have.
#pragma warning disable CS0618
            text.enableWordWrapping = wrap;
#pragma warning restore CS0618
            if (_font != null)
            {
                text.font = _font;
            }
            return text;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Anchored to a corner or edge of the parent, measured from it.
        private static void Place(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // One soft-edged white disc, 128 pixels: heads, shoulders, the seal. Made once per city load.
        private Sprite MakeDisc()
        {
            const int size = 128;
            _discTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "RealLifeOverhaul Justice Disc" };
            var pixels = new Color32[size * size];
            float radius = size / 2f - 1.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size / 2f;
                    float dy = y + 0.5f - size / 2f;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            _discTexture.SetPixels32(pixels);
            _discTexture.Apply();
            return Sprite.Create(_discTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);   // a plain quad: nothing has to read the texture back
        }
    }
}
