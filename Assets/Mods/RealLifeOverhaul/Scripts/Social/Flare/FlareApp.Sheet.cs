using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The sheet over Flare's screens: writing a post, or hiring influencers for a business.
    ///
    /// WRITING A POST: what it is about first (the built part, which decides what the post does): now, a moment from
    /// today (a draft: a picture, an outing, a promotion...), a report to the city, a review of a business, an
    /// announcement for a business of the player's, or a reply. Then its choices as chips (the report's kind, the
    /// business, the stars, the event's hour), the tone, an account to call on, tags, and what the player types (the
    /// game's own text box, at most 200 characters: it decides nothing) and a picture (the camera, a selfie, the latest
    /// photo). The words it will read as are shown before it goes; the Post button says when it can go if not now.
    ///
    /// HIRING: the deal (a post, a series, an ambassador, a plan, a launch, a giveaway), who (one of the city's
    /// influencers), what the company would charge and bring, and the Hire button: the company's own campaign of that
    /// kind, booked and paid as before.
    ///
    /// Made once, written again on every choice (a few dozen chips, from a pool).
    /// </summary>
    public sealed partial class FlareApp
    {
        private const float SheetMost = 1400f, ChipHeight = 60f;
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
        private readonly List<FlareInfluencer> _active = new List<FlareInfluencer>(FlareCast.Most);
        private readonly List<string> _suggest = new List<string>(8);

        private sealed class ChipPart
        {
            public Image Back;
            public TextMeshProUGUI Label;
        }

        // ---------------------------------------------------------------- building

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

        // ---------------------------------------------------------------- opening and closing

        /// <summary>The compose sheet: with a draft (a moment) or for a reply to a post, or blank.</summary>
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

        /// <summary>A picture for the compose sheet (the camera's shot): attached, the sheet opened if it is not.</summary>
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

        // ---------------------------------------------------------------- writing the sheet

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

        private float WriteCompose(FlareFile file, float width)
        {
            FlareCompose c = _compose;
            int day = _director.CurrentDay;
            FlareKit.Show(_field, true);
            FlareKit.Show(_counter, true);
            FlareKit.Set(_sheetTitle, FlareText.T(c.Kind == FlareSource.Reply ? "reallife_flare_compose_reply" : "reallife_flare_compose_title"));
            float y = 0f;
            FlarePost parent = c.ReplyTo >= 0 ? file.Post(c.ReplyTo) : null;
            if (parent == null && c.Kind == FlareSource.Reply)
            {
                c.Kind = FlareSource.Life;
            }
            bool reply = parent != null;
            if (!reply)
            {
                y = Label(FlareText.T("reallife_flare_compose_about"), y, width);
                float x = 0f;
                Chip(FlareText.T("reallife_flare_kind_now"), c.Kind == FlareSource.Life && c.Draft < 0, () => SetKind(FlareSource.Life), ref x, ref y, width);
                if (file.Drafts.Count > 0)
                {
                    Chip(FlareText.T("reallife_flare_kind_moment"), c.Draft >= 0, () => SetKind(FlareSource.Photo), ref x, ref y, width);
                }
                Chip(FlareText.T("reallife_flare_kind_report"), c.Kind == FlareSource.Report, () => SetKind(FlareSource.Report), ref x, ref y, width);
                Chip(FlareText.T("reallife_flare_kind_review"), c.Kind == FlareSource.Review, () => SetKind(FlareSource.Review), ref x, ref y, width);
                if (FlareGame.Businesses().Count > 0)
                {
                    Chip(FlareText.T("reallife_flare_kind_announce"), c.Kind == FlareSource.Announce, () => SetKind(FlareSource.Announce), ref x, ref y, width);
                }
                y += ChipHeight + 24f;
            }
            else
            {
                string name, handle, key;
                AuthorOf(parent, out name, out handle, out key);
                y = Label(FlareText.Fill("reallife_flare_replying_to", "handle", handle) + "\n" + Cut(parent.Text ?? WordsOf(parent, name)), y, width);
            }
            if (c.Draft >= 0)
            {
                y = Moments(file, c, y, width);
            }
            else if (c.Kind == FlareSource.Report)
            {
                y = ReportChoices(c, y, width);
            }
            else if (c.Kind == FlareSource.Review)
            {
                y = ReviewChoices(c, y, width);
            }
            else if (c.Kind == FlareSource.Announce)
            {
                y = AnnounceChoices(c, day, y, width);
            }
            if (c.Kind != FlareSource.Report)
            {
                FlareSource source = c.Draft >= 0 && file.Draft(c.Draft) != null ? file.Draft(c.Draft).Source : c.Kind;
                int tone = c.ToneChosen ? (int)c.Tone : (int)FlareRules.BestTone(source);
                y = Label(FlareText.T("reallife_flare_tone_label"), y, width);
                float x = 0f;
                for (int t = 0; t < FlareText.ToneKeys.Length; t++)
                {
                    int n = t;
                    Chip(FlareText.Tone(t), t == tone, () =>
                    {
                        _compose.Tone = (FlareTone)n;
                        _compose.ToneChosen = true;
                        WriteSheet();
                    }, ref x, ref y, width);
                }
                y += ChipHeight + 24f;
            }
            y = Mentions(file, c, y, width);
            y = Tags(file, c, y, width);
            // What the player types: the game's own text box.
            string typed = c.Text;
            float fh = Mathf.Max(150f, FlareKit.Height(_fieldText, typed ?? "M", width - 64f) + 56f);
            FlareKit.Place(_field.rectTransform, 0f, y, width, fh);
            FlareKit.Set(_fieldText, typed ?? FlareText.T(reply ? "reallife_flare_reply_placeholder" : "reallife_flare_whats_happening"));
            _fieldText.color = typed != null ? _look.Text : _look.Muted;
            FlareKit.Place(_fieldText.rectTransform, 32f, 28f, width - 64f, fh - 56f);
            y += fh + 6f;
            FlareKit.Set(_counter, FlareText.Fill("reallife_flare_compose_count", "count", (typed != null ? typed.Length : 0).ToString(), "most", FlareLiveRules.TextMost.ToString()));
            FlareKit.Place(_counter.rectTransform, width - 300f, y, 300f, 36f);
            y += 44f;
            // The picture, and how to take one.
            Texture2D pic = c.Pic != null ? _pictures.Of(c.Pic) : null;
            FlareKit.Show(_picBack, c.Pic != null);
            if (c.Pic != null)
            {
                float pw = Mathf.Min(width, 720f), ph = pic != null ? pw * pic.height / Mathf.Max(1f, pic.width) : 140f;
                FlareKit.Place(_picBack.rectTransform, 0f, y, pw, ph);
                FlareKit.Show(_picture, pic != null);
                if (pic != null)
                {
                    _picture.texture = pic;
                    FlareKit.Stretch(_picture.rectTransform, 0f, 0f, 0f, 0f);
                }
                FlareKit.Place(_picRemove.rectTransform, pw - 76f, 16f, 60f, 60f);
                y += ph + 18f;
            }
            float tx = 0f;
            FlareKit.Show(_camera, !reply);
            FlareKit.Show(_selfie, !reply && FlareBridges.TakeSelfie != null);
            FlareKit.Show(_latest, !reply && LatestPhoto(file) != null);
            if (!reply)
            {
                FlareKit.Place(_camera.rectTransform, tx, y, 76f, 76f);
                tx += 92f;
                if (_selfie.gameObject.activeSelf)
                {
                    FlareKit.Place(_selfie.rectTransform, tx, y, 76f, 76f);
                    tx += 92f;
                }
                if (_latest.gameObject.activeSelf)
                {
                    FlareKit.Place(_latest.rectTransform, tx, y, 76f, 76f);
                }
                if (_director.Shooting)
                {
                    y = Label(FlareText.T("reallife_flare_compose_shooting"), y + 84f, width) - 84f;
                }
                y += 92f;
            }
            // The words as they will read, and whether it can go now.
            string preview = Preview(file, c);
            FlareKit.Show(_preview, preview != null);
            if (preview != null)
            {
                float ph = FlareKit.Height(_preview, preview, width);
                FlareKit.Set(_preview, preview);
                FlareKit.Place(_preview.rectTransform, 0f, y, width, ph + 4f);
                y += ph + 16f;
            }
            string why = Why(file, c);
            FlareKit.Set(_why, why ?? string.Empty);
            _why.color = why != null ? _look.Bad : _look.Muted;
            FlareKit.Set(_sendLabel, FlareText.T(reply ? "reallife_flare_reply_button" : "reallife_flare_post_button"));
            _send.color = why == null ? _look.Accent : _look.Chip;
            return y;
        }

        private void SetKind(FlareSource kind)
        {
            FlareFile file = _director.File;
            _compose.Kind = kind;
            _compose.Draft = -1;
            _compose.Place = _compose.Name = null;
            _compose.Arg = kind == FlareSource.Review ? 0 : 0;
            if (kind == FlareSource.Photo && file != null && file.Drafts.Count > 0)
            {
                FlareDraft d = file.Drafts[file.Drafts.Count - 1];
                _compose.Kind = d.Source;
                _compose.Draft = d.Id;
                if ((d.Source == FlareSource.Photo || d.Source == FlareSource.Selfie) && !string.IsNullOrEmpty(d.Name))
                {
                    _compose.Pic = "f:" + d.Name;
                }
            }
            WriteSheet();
        }

        private float Moments(FlareFile file, FlareCompose c, float y, float width)
        {
            y = Label(FlareText.T("reallife_flare_compose_moment"), y, width);
            float x = 0f;
            for (int i = file.Drafts.Count - 1; i >= 0; i--)
            {
                FlareDraft d = file.Drafts[i];
                int id = d.Id;
                Chip(FlareText.Draft(d, _director.PersonName(d.Person)), c.Draft == id, () =>
                {
                    FlareDraft pick = _director.File != null ? _director.File.Draft(id) : null;
                    if (pick != null)
                    {
                        _compose.Draft = id;
                        _compose.Kind = pick.Source;
                        _compose.Pic = (pick.Source == FlareSource.Photo || pick.Source == FlareSource.Selfie) && !string.IsNullOrEmpty(pick.Name) ? "f:" + pick.Name : _compose.Pic;
                        WriteSheet();
                    }
                }, ref x, ref y, width);
            }
            y += ChipHeight + 24f;
            FlareDraft draft = file.Draft(c.Draft);
            if (draft != null && draft.Person >= 0 && _director.PersonName(draft.Person) != null)
            {
                x = 0f;
                Chip(FlareText.Fill("reallife_flare_tag_yes", "friend", _director.PersonName(draft.Person)), c.Tag, () =>
                {
                    _compose.Tag = !_compose.Tag;
                    WriteSheet();
                }, ref x, ref y, width);
                y += ChipHeight + 24f;
            }
            return y;
        }

        private float ReportChoices(FlareCompose c, float y, float width)
        {
            y = Label(FlareText.T("reallife_flare_compose_report"), y, width);
            float x = 0f;
            for (int k = 0; k < FlareText.ReportKeys.Length; k++)
            {
                int n = k;
                Chip(FlareText.T(FlareText.ReportKeys[k]), c.Arg == k, () =>
                {
                    _compose.Arg = n;
                    WriteSheet();
                }, ref x, ref y, width);
            }
            y += ChipHeight + 16f;
            string district = FlareBridges.Ask(FlareBridges.PlayerDistrict, null);
            y = Label(district != null ? FlareText.Fill("reallife_flare_compose_where", "district", FlareText.District(district)) : FlareText.T("reallife_flare_compose_need_district"), y, width);
            if (c.Arg == (int)FlareReport.Business)
            {
                BuildingRegistration inside = FlareLiveGame.InsideAny();
                if (inside != null && !GoingPublic.Mine(inside))
                {
                    c.Place = FlareGame.Place(inside);
                    c.Name = FlareGame.Name(inside);
                    y = Label(FlareText.Fill("reallife_flare_compose_about_business", "name", c.Name), y, width);
                }
                else
                {
                    c.Place = c.Name = null;
                    y = Label(FlareText.T("reallife_flare_compose_stand_in"), y, width);
                }
            }
            return y + 8f;
        }

        private float ReviewChoices(FlareCompose c, float y, float width)
        {
            y = Label(FlareText.T("reallife_flare_compose_which"), y, width);
            float x = 0f;
            BuildingRegistration inside = FlareLiveGame.InsideAny();
            if (inside != null && !GoingPublic.Mine(inside))
            {
                string place = FlareGame.Place(inside), name = FlareGame.Name(inside);
                Chip(FlareText.Fill("reallife_flare_compose_here", "name", name), c.Place == place, () => Pick(place, name), ref x, ref y, width);
            }
            IReadOnlyList<FlareBiz> shops = _director.CityShops;
            for (int i = 0; i < shops.Count && i < 8; i++)
            {
                FlareBiz b = shops[i];
                Chip(b.Name, c.Place == b.Place, () => Pick(b.Place, b.Name), ref x, ref y, width);
            }
            y += ChipHeight + 16f;
            y = Label(FlareText.T("reallife_flare_compose_stars"), y, width);
            x = 0f;
            for (int s = 1; s <= 5; s++)
            {
                int n = s;
                Chip(FlareText.Fill("reallife_flare_stars", "stars", s.ToString()), c.Arg == s, () =>
                {
                    _compose.Arg = n;
                    WriteSheet();
                }, ref x, ref y, width);
            }
            return y + ChipHeight + 24f;
        }

        private void Pick(string place, string name)
        {
            _compose.Place = place;
            _compose.Name = name;
            WriteSheet();
        }

        private float AnnounceChoices(FlareCompose c, int day, float y, float width)
        {
            y = Label(FlareText.T("reallife_flare_compose_which_mine"), y, width);
            float x = 0f;
            List<BuildingRegistration> mine = FlareGame.Businesses();
            for (int i = 0; i < mine.Count; i++)
            {
                string place = FlareGame.Place(mine[i]), name = FlareGame.Name(mine[i]);
                Chip(name, c.Place == place, () => Pick(place, name), ref x, ref y, width);
            }
            y += ChipHeight + 16f;
            y = Label(FlareText.T("reallife_flare_compose_announce"), y, width);
            x = 0f;
            for (int k = 0; k < FlareText.AnnounceKeys.Length; k++)
            {
                int n = k;
                Chip(FlareText.T(FlareText.AnnounceKeys[k]), c.Arg == k, () =>
                {
                    _compose.Arg = n;
                    _compose.When = n == (int)FlareAnnounce.Event ? (day + 1) * 24 + 19 : -1;
                    WriteSheet();
                }, ref x, ref y, width);
            }
            y += ChipHeight + 16f;
            if (c.Arg == (int)FlareAnnounce.Event)
            {
                y = Label(FlareText.T("reallife_flare_compose_when"), y, width);
                x = 0f;
                int[] hours = { day * 24 + 19, (day + 1) * 24 + 12, (day + 1) * 24 + 19, (day + 2) * 24 + 19 };
                int nowHour = day * 24 + (SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0);
                for (int i = 0; i < hours.Length; i++)
                {
                    int when = hours[i];
                    if (when <= nowHour)
                    {
                        continue;
                    }
                    Chip(FlareText.Hour(when), c.When == when, () =>
                    {
                        _compose.When = when;
                        WriteSheet();
                    }, ref x, ref y, width);
                }
                y += ChipHeight + 16f;
            }
            return y + 8f;
        }

        // An account to call on: the City Office and the others, a brand, a player of the session. One at most.
        private float Mentions(FlareFile file, FlareCompose c, float y, float width)
        {
            y = Label(FlareText.T("reallife_flare_compose_mention"), y, width);
            float x = 0f;
            for (int i = 0; i <= (int)FlareOfficial.Bank; i++)
            {
                string key = "o:" + i;
                Chip(FlareText.OfficialHandle(i), c.Mention == key, () => Mention(key), ref x, ref y, width);
            }
            IReadOnlyList<string> brands = _director.BrandAccounts;
            for (int i = 0; i < brands.Count && i < 3; i++)
            {
                string key = "b:" + brands[i];
                Chip(FlareText.HandleOf(brands[i]), c.Mention == key, () => Mention(key), ref x, ref y, width);
            }
            if (GoingPublic.InSession)
            {
                Players(file);
                for (int i = 0; i < _players.Length; i++)
                {
                    if (_players[i] != null)
                    {
                        string key = "p:" + _players[i];
                        Chip(FlareText.HandleOf(_players[i]), c.Mention == key, () => Mention(key), ref x, ref y, width);
                    }
                }
            }
            return y + ChipHeight + 24f;
        }

        private void Mention(string key)
        {
            _compose.Mention = _compose.Mention == key ? null : key;
            WriteSheet();
        }

        // Tags to add: what it is about, the district, and the day's Trending.
        private float Tags(FlareFile file, FlareCompose c, float y, float width)
        {
            _suggest.Clear();
            switch (c.Kind)
            {
                case FlareSource.Report: _suggest.Add(FlareLiveRules.ReportTag((FlareReport)FlareRules.Clamp(c.Arg, 0, (int)FlareReport.Utilities))); break;
                case FlareSource.Review: _suggest.Add("review"); break;
                case FlareSource.Announce: _suggest.Add("shoplocal"); break;
            }
            string district = FlareBridges.Ask(FlareBridges.PlayerDistrict, null);
            if (district != null)
            {
                string word = FlareText.HandleOf(FlareText.District(district)).Substring(1);
                if (word.Length > 1 && !_suggest.Contains(word))
                {
                    _suggest.Add(word);
                }
            }
            FlareLiveRules.Trending(file.Posts, _director.Now, 4, _tags, _tagCounts);
            for (int i = 0; i < _tags.Count && _suggest.Count < 6; i++)
            {
                if (!_suggest.Contains(_tags[i]))
                {
                    _suggest.Add(_tags[i]);
                }
            }
            if (_suggest.Count == 0)
            {
                return y;
            }
            y = Label(FlareText.T("reallife_flare_compose_tags"), y, width);
            float x = 0f;
            for (int i = 0; i < _suggest.Count; i++)
            {
                string tag = _suggest[i];
                bool on = HasTag(c.Tags, tag);
                Chip(FlareText.Tag(tag), on, () =>
                {
                    _compose.Tags = on ? Without(_compose.Tags, tag) : string.IsNullOrEmpty(_compose.Tags) ? tag : _compose.Tags + " " + tag;
                    WriteSheet();
                }, ref x, ref y, width);
            }
            return y + ChipHeight + 24f;
        }

        private static string Without(string tags, string tag)
        {
            if (string.IsNullOrEmpty(tags))
            {
                return null;
            }
            string[] all = tags.Split(' ');
            var b = new System.Text.StringBuilder(tags.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Length > 0 && all[i] != tag)
                {
                    b.Append(b.Length > 0 ? " " : string.Empty).Append(all[i]);
                }
            }
            return b.Length > 0 ? b.ToString() : null;
        }

        // The post's built part, as it will read.
        private string Preview(FlareFile file, FlareCompose c)
        {
            if (c.Kind == FlareSource.Reply)
            {
                return null;
            }
            FlareDraft d = c.Draft >= 0 ? file.Draft(c.Draft) : null;
            int tone = c.ToneChosen ? (int)c.Tone : (int)FlareRules.BestTone(d != null ? d.Source : c.Kind);
            var p = new FlarePost
            {
                Author = FlareAuthor.Player, Source = d != null ? d.Source : c.Kind, Tone = (FlareTone)tone, Name = d != null ? d.Name : c.Name, Place = d != null ? d.Place : c.Place,
                Arg = d != null ? d.Arg : c.Arg, When = c.When, District = c.District ?? FlareBridges.Ask(FlareBridges.PlayerDistrict, null), Seed = file.NextId * 7919
            };
            if ((p.Source == FlareSource.Review && (p.Arg < 1 || p.Place == null)) || (p.Source == FlareSource.Announce && p.Place == null))
            {
                return null;
            }
            string tagged = d != null && c.Tag ? _director.PersonName(d.Person) : null;
            string words = FlareText.Built(p, _director.MyName) ?? FlareText.Post(p, tagged, null, null);
            return FlareText.Fill("reallife_flare_compose_preview", "words", words);
        }

        // Why it cannot go now; null when it can.
        private string Why(FlareFile file, FlareCompose c)
        {
            int day = _director.CurrentDay;
            switch (c.Kind)
            {
                case FlareSource.Reply:
                    return c.Text == null ? FlareText.T("reallife_flare_compose_need_words") : null;
                case FlareSource.Report:
                    if (FlareBridges.Ask(FlareBridges.PlayerDistrict, null) == null)
                    {
                        return FlareText.T("reallife_flare_compose_need_district");
                    }
                    if (c.Arg == (int)FlareReport.Business && c.Place == null)
                    {
                        return FlareText.T("reallife_flare_compose_stand_in");
                    }
                    return file.Net.ReportsDay == day && file.Net.ReportsToday >= FlareLiveRules.ReportsPerDay ? FlareText.T("reallife_flare_compose_reported") : null;
                case FlareSource.Review:
                    if (c.Place == null || c.Arg < 1)
                    {
                        return FlareText.T("reallife_flare_compose_need_place");
                    }
                    break;
                case FlareSource.Announce:
                    if (c.Place == null)
                    {
                        return FlareText.T("reallife_flare_compose_need_mine");
                    }
                    break;
            }
            FlareRules.Wait wait = FlareRules.CanPost(file, day, FlareGame.MinuteOfDay);
            if (wait == FlareRules.Wait.None)
            {
                return null;
            }
            int next = FlareRules.NextPostMinute(file, day);
            return wait == FlareRules.Wait.DayFull || next < 0 ? FlareText.T("reallife_flare_wait_day") : FlareText.Fill("reallife_flare_wait_soon", "time", FlareText.Clock(next));
        }

        // ---------------------------------------------------------------- hiring

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

        // ---------------------------------------------------------------- the sheet's parts

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

        private void TypeClicked()
        {
            bool reply = _compose.Kind == FlareSource.Reply;
            AskText(reply ? "reallife_flare_reply_placeholder" : "reallife_flare_whats_happening", "reallife_flare_compose_done", _compose.Text, FlareLiveRules.TextMost, text =>
            {
                _compose.Text = FlareLiveRules.Clip(text);
                WriteSheet();
            });
        }

        private static FlareDraft LatestPhoto(FlareFile file)
        {
            for (int i = file.Drafts.Count - 1; i >= 0; i--)
            {
                FlareDraft d = file.Drafts[i];
                if ((d.Source == FlareSource.Photo || d.Source == FlareSource.Selfie) && !string.IsNullOrEmpty(d.Name))
                {
                    return d;
                }
            }
            return null;
        }

        private void LatestClicked()
        {
            FlareFile file = _director.File;
            FlareDraft d = file != null ? LatestPhoto(file) : null;
            if (d != null)
            {
                _compose.Kind = d.Source;
                _compose.Draft = d.Id;
                _compose.Pic = "f:" + d.Name;
                WriteSheet();
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
