using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The Flare app's other cards: a profile's head, a note, a tag, a business, a deal, the prompt and a button.</summary>
    public sealed partial class FlareApp
    {
        private sealed class ProfileCell : Card
        {
            private readonly Image _cover;
            private readonly Avatar _avatar;
            private readonly TextMeshProUGUI _name, _handle, _about, _stats;
            private readonly Pill[] _buttons = new Pill[4];

            public ProfileCell(FlareApp app) : base(app, "Profile", false)
            {
                _cover = FlareKit.Box(Rect, "Cover", Color.white, FlareKit.Radius);
                _avatar = new Avatar(Rect, 150f, null);
                _name = FlareKit.Text(Rect, "Name", 46f, Color.white, FontStyles.Bold);
                _handle = FlareKit.Text(Rect, "Handle", FlareKit.Small + 2f, Color.white);
                _about = FlareKit.Text(Rect, "About", FlareKit.Body, Color.white);
                _about.overflowMode = TextOverflowModes.Overflow;
                _stats = FlareKit.Text(Rect, "Stats", FlareKit.Small + 2f, Color.white);
                for (int i = 0; i < _buttons.Length; i++)
                {
                    int n = i;
                    _buttons[i] = new Pill(Rect, "Button" + i, () => App.ProfileButton(Row, n));
                }
            }

            protected override void Colours()
            {
                _cover.color = Look.AccentSoft;
                _name.color = Look.Text;
                _handle.color = Look.Muted;
                _about.color = Look.Text;
                _stats.color = Look.Muted;
            }

            public override float Bind(FlareRow row, float width)
            {
                string key = row.Key;
                string name = key == "me" ? App._director.MyName : App._director.AccountName(key);
                float pad = FlareKit.Pad;
                FlareKit.Place(_cover.rectTransform, 0f, 0f, width, 170f);
                _avatar.Show(App, key == "me" ? "me" : key, name, pad, 95f, 150f);
                float y = 95f + 150f + 16f;
                FlareKit.Set(_name, name);
                FlareKit.Place(_name.rectTransform, pad, y, width - pad * 2f, 58f);
                y += 58f;
                FlareKit.Set(_handle, key == "me" ? App._director.MyHandle : App.Handle(key, name));
                FlareKit.Place(_handle.rectTransform, pad, y, width - pad * 2f, 36f);
                y += 46f;
                string about = row.Text;
                float ah = FlareKit.Height(_about, about, width - pad * 2f);
                FlareKit.Set(_about, about);
                FlareKit.Place(_about.rectTransform, pad, y, width - pad * 2f, ah + 4f);
                y += ah + 14f;
                string stats = App.StatsOf(key);
                float sh = FlareKit.Height(_stats, stats, width - pad * 2f);
                FlareKit.Set(_stats, stats);
                FlareKit.Place(_stats.rectTransform, pad, y, width - pad * 2f, sh + 4f);
                y += sh + 20f;
                float x = pad;
                int shown = 0;
                for (int i = 0; i < _buttons.Length; i++)
                {
                    string label;
                    bool filled;
                    if (App.ProfileButtonOf(key, i, out label, out filled))
                    {
                        x += _buttons[i].Show(Look, label, filled, x, y) + 16f;
                        shown++;
                    }
                    else
                    {
                        _buttons[i].Hide();
                    }
                }
                y += shown > 0 ? 58f + pad : pad - 10f;
                return y;
            }
        }

        private sealed class NoteCell : Card
        {
            private readonly TextMeshProUGUI _text;

            public NoteCell(FlareApp app) : base(app, "Note", false)
            {
                _text = FlareKit.Text(Rect, "Text", FlareKit.Body, Color.white);
                _text.overflowMode = TextOverflowModes.Overflow;
            }

            public override void Paint(FlareLook look)
            {
                Look = look;
                Back.color = new Color(0f, 0f, 0f, 0f);
                Colours();
            }

            protected override void Colours()
            {
            }

            public override float Bind(FlareRow row, float width)
            {
                bool title = row.Number == 1;
                _text.fontSize = title ? 34f : FlareKit.Body - 2f;
                _text.fontStyle = title ? FontStyles.Bold : FontStyles.Normal;
                _text.alignment = title ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Top;
                _text.color = title ? Look.Text : Look.Muted;
                float pad = title ? 8f : FlareKit.Pad;
                float h = FlareKit.Height(_text, row.Text, width - pad * 2f);
                FlareKit.Set(_text, row.Text);
                FlareKit.Place(_text.rectTransform, pad, title ? 16f : pad, width - pad * 2f, h + 4f);
                return h + (title ? 30f : pad * 2f);
            }
        }

        private sealed class TagCell : Card
        {
            private readonly TextMeshProUGUI _rank, _tag, _count;

            public TagCell(FlareApp app) : base(app, "Tag", true)
            {
                _rank = FlareKit.Text(Rect, "Rank", FlareKit.Small, Color.white);
                _tag = FlareKit.Text(Rect, "Tag", 36f, Color.white, FontStyles.Bold);
                _count = FlareKit.Text(Rect, "Count", FlareKit.Small, Color.white);
            }

            protected override void Clicked()
            {
                App.Open(FlareScreen.Tag, 0, -1, Row.Key);
            }

            protected override void Colours()
            {
                _rank.color = Look.Muted;
                _tag.color = Look.Text;
                _count.color = Look.Muted;
            }

            public override float Bind(FlareRow row, float width)
            {
                float pad = FlareKit.Pad;
                FlareKit.Set(_rank, FlareText.Fill("reallife_flare_trend_rank", "rank", (row.Number / 100000 + 1).ToString()));
                FlareKit.Place(_rank.rectTransform, pad, pad - 6f, width - pad * 2f, 32f);
                FlareKit.Set(_tag, FlareText.Tag(row.Key));
                FlareKit.Place(_tag.rectTransform, pad, pad + 26f, width - pad * 2f, 46f);
                FlareKit.Set(_count, FlareText.Fill("reallife_flare_trend_count", "count", FlareText.N(row.Number % 100000 * 37)));
                FlareKit.Place(_count.rectTransform, pad, pad + 74f, width - pad * 2f, 32f);
                return pad * 2f + 104f;
            }
        }

        private sealed class ShopCell : Card
        {
            private readonly TextMeshProUGUI _name, _lines, _stars;
            private readonly Image[] _starIcons = new Image[5];
            private readonly Pill _announce, _promote;

            public ShopCell(FlareApp app) : base(app, "Business", false)
            {
                _name = FlareKit.Text(Rect, "Name", 36f, Color.white, FontStyles.Bold);
                for (int i = 0; i < _starIcons.Length; i++)
                {
                    _starIcons[i] = FlareKit.Box(Rect, "Star" + i, Color.white);
                }
                _stars = FlareKit.Text(Rect, "Stars", FlareKit.Small, Color.white, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                _lines = FlareKit.Text(Rect, "Lines", FlareKit.Small + 2f, Color.white);
                _lines.overflowMode = TextOverflowModes.Overflow;
                _announce = new Pill(Rect, "Announce", () => App.AnnounceClicked(Row));
                _promote = new Pill(Rect, "Promote", () => App.PromoteClicked(Row));
            }

            protected override void Colours()
            {
                _name.color = Look.Text;
                _stars.color = Look.Muted;
                _lines.color = Look.Muted;
            }

            public override float Bind(FlareRow row, float width)
            {
                float pad = FlareKit.Pad;
                FlareKit.Set(_name, row.Text);
                FlareKit.Place(_name.rectTransform, pad, pad - 4f, width - pad * 2f, 48f);
                int overall = row.Number;
                int halves = overall < 0 ? -1 : FlareRules.Clamp((overall + 5) / 10, 0, 10);
                for (int i = 0; i < _starIcons.Length; i++)
                {
                    FlareKit.Show(_starIcons[i], halves >= 0);
                    _starIcons[i].sprite = FlareIcons.Get(halves >= (i + 1) * 2 - 1 ? FlareGlyph.StarFull : FlareGlyph.Star);
                    _starIcons[i].color = new Color(0.98f, 0.75f, 0.15f, 1f);
                    FlareKit.Place(_starIcons[i].rectTransform, pad + i * 40f, pad + 52f, 36f, 36f);
                }
                FlareKit.Set(_stars, halves >= 0 ? (halves / 2).ToString() + (halves % 2 == 1 ? ".5" : string.Empty) : string.Empty);
                FlareKit.Place(_stars.rectTransform, pad + 210f, pad + 50f, 200f, 40f);
                string lines = App.ShopLines(row);
                float lh = FlareKit.Height(_lines, lines, width - pad * 2f);
                FlareKit.Set(_lines, lines);
                FlareKit.Place(_lines.rectTransform, pad, pad + 100f, width - pad * 2f, lh + 4f);
                float y = pad + 100f + lh + 16f;
                float x = pad + _announce.Show(Look, FlareText.T("reallife_flare_shop_announce"), true, pad, y) + 16f;
                _promote.Show(Look, FlareText.T("reallife_flare_shop_hire"), false, x, y);
                return y + 58f + pad;
            }
        }

        private sealed class DealCell : Card
        {
            private readonly TextMeshProUGUI _title, _lines;
            private readonly Pill _end;

            public DealCell(FlareApp app) : base(app, "Deal", false)
            {
                _title = FlareKit.Text(Rect, "Title", 32f, Color.white, FontStyles.Bold);
                _lines = FlareKit.Text(Rect, "Lines", FlareKit.Small + 2f, Color.white);
                _lines.overflowMode = TextOverflowModes.Overflow;
                _end = new Pill(Rect, "End", () => App.EndDealClicked(Row));
            }

            protected override void Colours()
            {
                _title.color = Look.Text;
                _lines.color = Look.Muted;
            }

            public override float Bind(FlareRow row, float width)
            {
                float pad = FlareKit.Pad;
                float th = FlareKit.Height(_title, row.Key, width - pad * 2f);
                FlareKit.Set(_title, row.Key);
                FlareKit.Place(_title.rectTransform, pad, pad - 4f, width - pad * 2f, th + 4f);
                float lh = FlareKit.Height(_lines, row.Text, width - pad * 2f);
                FlareKit.Set(_lines, row.Text);
                FlareKit.Place(_lines.rectTransform, pad, pad + th + 6f, width - pad * 2f, lh + 4f);
                float y = pad + th + lh + 20f;
                if (row.Number > 0)
                {
                    _end.Show(Look, FlareText.T("reallife_flare_hire_end"), false, pad, y);
                    y += 58f + 10f;
                }
                else
                {
                    _end.Hide();
                }
                return y + pad - 10f;
            }
        }

        private sealed class PromptCell : Card
        {
            private readonly Avatar _avatar;
            private readonly Image _field, _camera;
            private readonly TextMeshProUGUI _placeholder;

            public PromptCell(FlareApp app) : base(app, "Prompt", true)
            {
                _avatar = new Avatar(Rect, FlareKit.Avatar, null);
                _field = FlareKit.Box(Rect, "Field", Color.white, 0f, true);
                _placeholder = FlareKit.Text(_field.transform, "Placeholder", FlareKit.Body + 2f, Color.white, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                FlareKit.Stretch(_placeholder.rectTransform, 36f, 0f, 36f, 0f);
                _camera = FlareKit.Box(Rect, "Camera", Color.white, 0f, false, true);
                _camera.sprite = FlareIcons.Get(FlareGlyph.Camera);
                FlareKit.Click(_camera, () => App._director.CameraPressed());
            }

            protected override void Clicked()
            {
                App.OpenCompose(-1, -1);
            }

            protected override void Colours()
            {
                _field.color = Look.Chip;
                _placeholder.color = Look.Muted;
                _camera.color = Look.Accent;
            }

            public override float Bind(FlareRow row, float width)
            {
                float pad = FlareKit.Pad;
                _avatar.Show(App, "me", App._director.MyName, pad, pad, FlareKit.Avatar);
                float x0 = pad + FlareKit.Avatar + FlareKit.Gap;
                FlareKit.Place(_field.rectTransform, x0, pad + 4f, width - x0 - pad - 90f, 76f);
                FlareKit.Set(_placeholder, FlareText.T("reallife_flare_whats_happening"));
                FlareKit.Place(_camera.rectTransform, width - pad - 66f, pad + 14f, 56f, 56f);
                return pad * 2f + FlareKit.Avatar;
            }
        }

        private sealed class ButtonCell : Card
        {
            private readonly Pill _pill;

            public ButtonCell(FlareApp app) : base(app, "Button", false)
            {
                _pill = new Pill(Rect, "Pill", () => App.RowButton(Row));
            }

            public override void Paint(FlareLook look)
            {
                Look = look;
                Back.color = new Color(0f, 0f, 0f, 0f);
            }

            protected override void Colours()
            {
            }

            public override float Bind(FlareRow row, float width)
            {
                float w = _pill.Show(Look, row.Text, true, 0f, 6f, 72f);
                FlareKit.Place(_pill.Back.rectTransform, (width - w) / 2f, 6f, w, 72f);
                return 84f;
            }
        }
    }
}
