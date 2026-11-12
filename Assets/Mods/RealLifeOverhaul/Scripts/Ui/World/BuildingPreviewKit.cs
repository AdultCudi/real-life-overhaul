using BigAmbitions.InputSystem;
using TMPro;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The bar of a building's preview (UI.BuildingPreview, UIs.buildingPreview: a building from BizMan, or a design from
    /// the preview terminal) drawn as the kit's small panel at the top of the screen while the preview runs: over the
    /// world, taking no key of its own, so the game's Cancel still ends the preview. Game side.
    /// </summary>
    public sealed class BuildingPreviewKit : IUiGameScreen
    {
        internal const string Container = "Container";

        public string Id => UiWorld.PreviewId;

        public string Name => "the preview's bar";

        public string Title => UiText.T("buildingpreview_cancel_preview");

        public UiIcon Icon => UiIcon.Eye;

        public UiGameKind Kind => UiGameKind.Overlay;

        public Component Find()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.buildingPreview : null;
        }

        public string Check(Component found)
        {
            if (GameContract.UiWorldPreview.Off)
            {
                return GameContract.UiWorldPreview.Why;
            }
            var preview = (BuildingPreview)found;
            return found.transform.Find(Container) == null || preview.wallsButtonImage == null ? "its container or its walls button is not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal) => conceal.Add(found.transform.Find(Container));

        public IUiScreen Make(Component found) => new BuildingPreviewScreen((BuildingPreview)found);
    }

    /// <summary>
    /// The preview's bar as the kit's tool panel at the top centre: what is previewed (the address and its size, or the
    /// design) on its strip with the walls' state at its right, then the game's Toggle walls beside Cancel preview on the
    /// cyan, the game's Cancel key on its cap. Both press the game's own preview. NPCs preview nothing, so there is no other
    /// side. Cost: a layout as something changes; four times a second a few reads.
    /// </summary>
    public sealed class BuildingPreviewScreen : WorldToolScreen, IUiScreen
    {
        private const float Wide = 480f;   // px: the place's name and its size on one line

        private readonly BuildingPreview _preview;

        public BuildingPreviewScreen(BuildingPreview preview)
        {
            _preview = preview;
        }

        public override string Id => UiWorld.PreviewId;

        protected override string Scene => "building preview";

        protected override float Width => Wide;

        private string Headline()
        {
            var label = GameContract.PreviewHeadline.Field != null ? GameContract.PreviewHeadline.Field.GetValue(_preview) as TMP_Text : null;
            return label != null ? WorldHost.Plain(label.text) : null;
        }

        // The walls as the game's own button shows them: up, down, or cut away to the floor in view.
        private string Walls()
        {
            Sprite shown = _preview.wallsButtonImage != null ? _preview.wallsButtonImage.sprite : null;
            return UiText.T(shown != null && shown == _preview.wallsVisibleSprite ? "reallife_ui_world_preview_walls_up"
                : shown != null && shown == _preview.wallsHiddenSprite ? "reallife_ui_world_preview_walls_down" : "reallife_ui_world_preview_walls_cut");
        }

        protected override long Signature()
        {
            string headline = Headline();
            long seen = headline != null ? headline.GetHashCode() : 0;
            Sprite shown = _preview.wallsButtonImage != null ? _preview.wallsButtonImage.sprite : null;
            return Mix(seen, shown != null ? shown.GetInstanceID() : 0);
        }

        protected override void Read()
        {
            M.Title = Headline() ?? UiText.T("reallife_ui_world_preview_title");
            M.Icon = UiIcon.Eye;
            M.Close = false;
            M.State = Walls();
            ref UiToolLine buttons = ref Add(UiToolKind.Buttons);
            buttons.Label = UiText.T("buildingpreview_cancel_preview");
            buttons.Key = WorldHost.Key(PlayerAction.Cancel);
            buttons.Value = UiText.T("common_toggle_walls");
        }

        // At the top of the screen in its middle, as the game's own bar stands.
        protected override void Place(UiFrame frame, float w, float h, out float x, out float y)
        {
            x = (frame.Width - w) / 2f;
            y = UiTokens.S8;
        }

        protected override bool Line(int line)
        {
            _preview.CancelPreview();
            return true;
        }

        protected override bool Second(int line)
        {
            _preview.ToggleWalls();
            return true;
        }
    }
}
