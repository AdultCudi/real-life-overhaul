using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The Flare app's sheet over its screens, for writing a post (FlareApp.Compose.cs) or hiring influencers; made once, written again on every choice.</summary>
    public sealed partial class FlareApp
    {
        private const float SheetMost = 1400f, ChipHeight = 60f;   // canvas units
        private const int SheetNone = 0, SheetCompose = 1, SheetHire = 2;

        private Image _shade, _sheet, _close, _field, _picBack, _picRemove, _camera, _selfie, _latest, _send;
        private TextMeshProUGUI _sheetTitle, _fieldText, _counter, _preview, _why, _sendLabel;
        private RawImage _picture;
        private RectTransform _sheetView, _sheetContent;
        private ScrollRect _sheetScroll;
        private readonly List<ChipPart> _chips = new List<ChipPart>(48);
        private readonly List<Action> _chipActions = new List<Action>(48);
        private readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>(8);
        private int _chipsUsed, _labelsUsed;
        private int _sheetKind;
        private readonly FlareCompose _compose = new FlareCompose();
        private string _hirePlace, _hireName;
        private int _hireDeal, _hireWho = -1;
        private readonly List<FlareInfluencer> _active = new List<FlareInfluencer>(FlareCast.Level);
        private readonly List<string> _suggest = new List<string>(8);

        private sealed class ChipPart
        {
            public Image Back;
            public TextMeshProUGUI Label;
        }

        private void BuildSheet()
        {
            _shade = FlareKit.Box(_root, "Shade", Color.black, 0f, false, true);
            FlareKit.Stretch(_shade.rectTransform, 0f, 0f, 0f, 0f);
            FlareKit.Click(_shade, CloseSheet);
            _sheet = FlareKit.Box(_root, "Sheet", Color.white, FlareKit.Radius + 6f, false, true);
            _sheetTitle = FlareKit.Text(_sheet.transform, "Title", 40f, Color.white, FontStyles.Bold);
            _close = FlareKit.Box(_sheet.transform, "Close", Color.white, 0f, false, true);
            _close.sprite = FlareIcons.Get(FlareGlyph.Close);
            FlareKit.Click(_close, CloseSheet);
            _sheetView = FlareKit.Rect(_sheet.transform, "View");
            _sheetView.gameObject.AddComponent<RectMask2D>();
            var catcher = _sheetView.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            _sheetContent = FlareKit.Rect(_sheetView, "Content");
            _sheetScroll = _sheetView.gameObject.AddComponent<ScrollRect>();
            _sheetScroll.viewport = _sheetView;
            _sheetScroll.content = _sheetContent;
            _sheetScroll.horizontal = false;
            _sheetScroll.movementType = ScrollRect.MovementType.Clamped;
            _sheetScroll.scrollSensitivity = 40f;
            _field = FlareKit.Box(_sheetContent, "Field", Color.white, 22f, false, true);
            FlareKit.Click(_field, TypeClicked);
            _fieldText = FlareKit.Text(_field.transform, "Words", FlareKit.Body + 2f, Color.white);
            _fieldText.overflowMode = TextOverflowModes.Overflow;
            _counter = FlareKit.Text(_sheetContent, "Counter", FlareKit.Small, Color.white, FontStyles.Normal, TextAlignmentOptions.TopRight);
            _preview = FlareKit.Text(_sheetContent, "Preview", FlareKit.Small + 2f, Color.white);
            _preview.overflowMode = TextOverflowModes.Overflow;
            _picBack = FlareKit.Box(_sheetContent, "PictureBack", Color.white, 18f);
            _picture = FlareKit.Picture(_picBack.transform, "Picture");
            _picRemove = FlareKit.Box(_picBack.transform, "Remove", Color.white, 0f, true, true);
            FlareKit.Click(_picRemove, () =>
            {
                _compose.Pic = null;
                WriteSheet();
            });
            var x = FlareKit.Box(_picRemove.transform, "X", Color.white);
            x.sprite = FlareIcons.Get(FlareGlyph.Close);
            FlareKit.Stretch(x.rectTransform, 10f, 10f, 10f, 10f);
            _camera = Tool(FlareGlyph.Camera, () => _director.CameraPressed());
            _selfie = Tool(FlareGlyph.Selfie, () => _director.SelfiePressed());
            _latest = Tool(FlareGlyph.Picture, LatestClicked);
            _why = FlareKit.Text(_sheet.transform, "Why", FlareKit.Small, Color.white, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _send = FlareKit.Box(_sheet.transform, "Send", Color.white, 0f, true, true);
            FlareKit.Click(_send, SendClicked);
            _sendLabel = FlareKit.Text(_send.transform, "Label", 34f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            FlareKit.Stretch(_sendLabel.rectTransform, 0f, 0f, 0f, 0f);
            FlareKit.Show(_shade, false);
            FlareKit.Show(_sheet, false);
        }

        private Image Tool(FlareGlyph glyph, Action click)
        {
            Image hit = FlareKit.Box(_sheetContent, "Tool" + glyph, Color.white, 0f, true, true);
            FlareKit.Click(hit, click);
            Image icon = FlareKit.Box(hit.transform, "Icon", Color.white);
            icon.sprite = FlareIcons.Get(glyph);
            FlareKit.Stretch(icon.rectTransform, 16f, 16f, 16f, 16f);
            return hit;
        }

        private void LaySheet(float w, float h)
        {
            float sw = Mathf.Min(SheetMost, w - 160f), sh = Mathf.Min(1360f, h - 100f);
            FlareKit.Place(_sheet.rectTransform, (w - sw) / 2f, (h - sh) / 2f, sw, sh);
            FlareKit.Place(_sheetTitle.rectTransform, 44f, 30f, sw - 160f, 56f);
            FlareKit.Place(_close.rectTransform, sw - 92f, 30f, 52f, 52f);
            FlareKit.Place(_sheetView, 44f, 104f, sw - 88f, sh - 104f - 130f);
            _sheetContent.anchorMin = new Vector2(0f, 1f);
            _sheetContent.anchorMax = new Vector2(1f, 1f);
            _sheetContent.pivot = new Vector2(0.5f, 1f);
            FlareKit.Place(_why.rectTransform, 44f, sh - 104f, sw - 420f, 70f);
            FlareKit.Place(_send.rectTransform, sw - 44f - 300f, sh - 112f, 300f, 84f);
        }

        private void PaintSheet()
        {
            FlareLook l = _look;
            _shade.color = l.Shade;
            _sheet.color = l.Card;
            _sheetTitle.color = l.Text;
            _close.color = l.Text;
            _field.color = l.Raised;
            _counter.color = l.Muted;
            _preview.color = l.Muted;
            _picBack.color = l.Raised;
            _picRemove.color = l.Shade;
            _camera.color = _selfie.color = _latest.color = l.AccentSoft;
            for (int i = 0; i < 3; i++)
            {
                Image tool = i == 0 ? _camera : i == 1 ? _selfie : _latest;
                tool.transform.GetChild(0).GetComponent<Image>().color = l.Accent;
            }
            _why.color = l.Muted;
            _send.color = l.Accent;
            _sendLabel.color = Color.white;
        }

        /// <summary>The compose sheet with a draft (a moment), for a reply to a post, or blank.</summary>
        private void OpenCompose(int draft, int replyTo)
        {
            FlareFile file = _director.File;
            if (!_built || file == null)
            {
                return;
            }
            _compose.Reset();
            if (replyTo >= 0 && file.Post(replyTo) != null)
            {
                _compose.Kind = FlareSource.Reply;
                _compose.ReplyTo = replyTo;
            }
            else if (draft >= 0 && file.Draft(draft) != null)
            {
                FlareDraft d = file.Draft(draft);
                _compose.Kind = d.Source;
                _compose.Draft = d.Id;
                _compose.Pic = (d.Source == FlareSource.Photo || d.Source == FlareSource.Selfie) && !string.IsNullOrEmpty(d.Name) ? "f:" + d.Name : null;
            }
            else if (draft == -1 && file.Drafts.Count > 0 && _composeWanted == -2)
            {
                FlareDraft d = file.Drafts[file.Drafts.Count - 1];
                _compose.Kind = d.Source;
                _compose.Draft = d.Id;
            }
            _sheetKind = SheetCompose;
            ShowSheet();
        }

        private void OpenHire(string place, string name, int influencer)
        {
            if (!_built || _director.File == null)
            {
                return;
            }
            _hirePlace = place;
            _hireName = name;
            _hireDeal = 0;
            _hireWho = influencer;
            _sheetKind = SheetHire;
            ShowSheet();
        }

        private void ShowSheet()
        {
            FlareKit.Show(_shade, true);
            FlareKit.Show(_sheet, true);
            _shade.transform.SetAsLastSibling();
            _sheet.transform.SetAsLastSibling();
            _sheetContent.anchoredPosition = Vector2.zero;
            WriteSheet();
        }

        private void CloseSheet()
        {
            _sheetKind = SheetNone;
            if (_shade != null)
            {
                FlareKit.Show(_shade, false);
                FlareKit.Show(_sheet, false);
            }
        }

        /// <summary>The camera's shot for the compose sheet: attached, the sheet opened if it is not.</summary>
        public void Attach(string pic)
        {
            if (!_built)
            {
                return;
            }
            if (_sheetKind != SheetCompose)
            {
                OpenCompose(-1, -1);
            }
            _compose.Pic = pic;
            WriteSheet();
        }

        private void SheetPicture()
        {
            if (_sheetKind == SheetCompose)
            {
                WriteSheet();
            }
        }

        private void WriteSheet()
        {
            if (_sheetKind == SheetNone)
            {
                return;
            }
            FlareFile file = _director.File;
            if (file == null)
            {
                CloseSheet();
                return;
            }
            _chipsUsed = _labelsUsed = 0;
            float width = _sheetView.rect.width;
            float y = _sheetKind == SheetHire ? WriteHire(file, width) : WriteCompose(file, width);
            for (int i = _chipsUsed; i < _chips.Count; i++)
            {
                FlareKit.Show(_chips[i].Back, false);
            }
            for (int i = _labelsUsed; i < _labels.Count; i++)
            {
                FlareKit.Show(_labels[i], false);
            }
            _sheetContent.sizeDelta = new Vector2(0f, y + 20f);
        }

        private float WriteHire(FlareFile file, float width)
        {
            FlareKit.Set(_sheetTitle, FlareText.Fill("reallife_flare_hire_title", "business", _hireName));
            FlareKit.Show(_field, false);
            FlareKit.Show(_counter, false);
            FlareKit.Show(_picBack, false);
            FlareKit.Show(_camera, false);
            FlareKit.Show(_selfie, false);
            FlareKit.Show(_latest, false);
            float y = Label(FlareText.T("reallife_flare_hire_deal"), 0f, width);
            float x = 0f;
            for (int k = 0; k < FlareText.DealKeys.Length; k++)
            {
                int n = k;
                Chip(FlareText.Deal(k), _hireDeal == k, () =>
                {
                    _hireDeal = n;
                    WriteSheet();
                }, ref x, ref y, width);
            }
            y += ChipHeight + 16f;
            y = Label(FlareText.T(FlareText.DealAboutKeys[_hireDeal]), y, width);
            _active.Clear();
            for (int i = 0; i < file.Influencers.Count; i++)
            {
                if (file.Influencers[i].Active)
                {
                    _active.Add(file.Influencers[i]);
                }
            }
            int who = -1;
            if ((FlareDeal)_hireDeal != FlareDeal.Plan && _active.Count > 0)
            {
                y = Label(FlareText.T("reallife_flare_hire_who"), y, width);
                x = 0f;
                bool known = false;
                for (int i = 0; i < _active.Count; i++)
                {
                    known |= _active[i].Id == _hireWho;
                }
                if (!known)
                {
                    _hireWho = _active[0].Id;
                }
                for (int i = 0; i < _active.Count; i++)
                {
                    FlareInfluencer inf = _active[i];
                    int id = inf.Id;
                    Chip(inf.Handle + "  ·  " + FlareText.Niche(inf.Niche), _hireWho == id, () =>
                    {
                        _hireWho = id;
                        WriteSheet();
                    }, ref x, ref y, width);
                }
                y += ChipHeight + 16f;
                who = _hireWho;
            }
            int cost, percent;
            string why;
            bool can = _director.HireQuote(_hirePlace, (FlareDeal)_hireDeal, who, out cost, out percent, out why);
            FlareCampaign busy;
            string quote = can ? FlareText.Fill("reallife_flare_hire_quote", "cost", FlareText.Money(cost), "percent", percent.ToString())
                : why == FlareDirector.BusyWhy && _director.CampaignAt(_hirePlace, out busy) ? FlareText.Busy(busy, _director.CurrentDay) : FlareText.T(why ?? "reallife_flare_hire_no_one");
            FlareKit.Show(_preview, true);
            float ph = FlareKit.Height(_preview, quote, width);
            FlareKit.Set(_preview, quote);
            FlareKit.Place(_preview.rectTransform, 0f, y, width, ph + 4f);
            y += ph + 16f;
            FlareKit.Set(_why, can ? string.Empty : FlareText.T("reallife_flare_hire_cannot"));
            _why.color = _look.Bad;
            FlareKit.Set(_sendLabel, FlareText.T("reallife_flare_hire_button"));
            _send.color = can ? _look.Accent : _look.Chip;
            return y;
        }

        private float Label(string text, float y, float width)
        {
            TextMeshProUGUI t;
            if (_labelsUsed < _labels.Count)
            {
                t = _labels[_labelsUsed];
            }
            else
            {
                t = FlareKit.Text(_sheetContent, "Label" + _labels.Count, FlareKit.Small + 2f, Color.white, FontStyles.Bold);
                t.overflowMode = TextOverflowModes.Overflow;
                _labels.Add(t);
            }
            _labelsUsed++;
            t.color = _look.Muted;
            float h = FlareKit.Height(t, text, width);
            FlareKit.Set(t, text);
            FlareKit.Place(t.rectTransform, 0f, y, width, h + 4f);
            FlareKit.Show(t, true);
            return y + h + 12f;
        }

        // A chip at x on the row at y, on the next row if it does not fit; x moves on past it.
        private void Chip(string text, bool on, Action click, ref float x, ref float y, float width)
        {
            ChipPart chip;
            int index = _chipsUsed;
            if (index < _chips.Count)
            {
                chip = _chips[index];
                _chipActions[index] = click;
            }
            else
            {
                chip = new ChipPart();
                chip.Back = FlareKit.Box(_sheetContent, "Chip" + index, Color.white, 0f, true, true);
                FlareKit.Click(chip.Back, () => ChipClicked(index));
                chip.Label = FlareKit.Text(chip.Back.transform, "Label", 27f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
                chip.Label.textWrappingMode = TextWrappingModes.NoWrap;
                FlareKit.Stretch(chip.Label.rectTransform, 22f, 0f, 22f, 0f);
                _chips.Add(chip);
                _chipActions.Add(click);
            }
            _chipsUsed++;
            FlareKit.Set(chip.Label, text);
            float w = Mathf.Min(width, chip.Label.GetPreferredValues(text, 4000f, 0f).x + 52f);
            if (x > 0f && x + w > width)
            {
                x = 0f;
                y += ChipHeight + 12f;
            }
            FlareKit.Place(chip.Back.rectTransform, x, y, w, ChipHeight);
            chip.Back.color = on ? _look.Accent : _look.Chip;
            chip.Label.color = on ? Color.white : _look.ChipText;
            chip.Label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            FlareKit.Show(chip.Back, true);
            x += w + 12f;
        }

        private void ChipClicked(int index)
        {
            if (index < _chipActions.Count && index < _chipsUsed)
            {
                _chipActions[index]?.Invoke();
            }
        }

        private void SendClicked()
        {
            if (_sheetKind == SheetHire)
            {
                int who = (FlareDeal)_hireDeal != FlareDeal.Plan ? _hireWho : -1;
                if (_director.Hire(_hirePlace, (FlareDeal)_hireDeal, who))
                {
                    CloseSheet();
                    Open(FlareScreen.Business, 0, -1, null, true);
                }
                else
                {
                    WriteSheet();
                }
                return;
            }
            string why;
            if (_director.Compose(_compose, out why))
            {
                bool reply = _compose.Kind == FlareSource.Reply;
                int to = _compose.ReplyTo;
                CloseSheet();
                if (reply)
                {
                    Open(FlareScreen.Thread, 0, to);
                }
                else
                {
                    Open(FlareScreen.Home, 0, -1, null, true);
                }
            }
            else
            {
                WriteSheet();
            }
        }
    }
}
