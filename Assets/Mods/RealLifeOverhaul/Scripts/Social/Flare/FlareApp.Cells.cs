using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The Flare app's cards, made once and bound to row after row as the list scrolls; here a post, a notification, an account and a message.</summary>
    public sealed partial class FlareApp
    {
        private const int CellPost = 1, CellNotice = 2, CellAccount = 3, CellThread = 4, CellBubble = 5, CellProfile = 6, CellNote = 7, CellTag = 8,
            CellShop = 9, CellDeal = 10, CellPrompt = 11, CellButton = 12;

        private FlareCell MakeCell(int kind)
        {
            switch (kind)
            {
                case CellPost: return new PostCell(this);
                case CellNotice: return new NoticeCell(this);
                case CellAccount: return new AccountCell(this);
                case CellThread: return new ThreadCell(this);
                case CellBubble: return new BubbleCell(this);
                case CellProfile: return new ProfileCell(this);
                case CellTag: return new TagCell(this);
                case CellShop: return new ShopCell(this);
                case CellDeal: return new DealCell(this);
                case CellPrompt: return new PromptCell(this);
                case CellButton: return new ButtonCell(this);
                default: return new NoteCell(this);
            }
        }

        // A row's height before its card has shown it: the one measured before, else a guess by its words.
        private float Estimate(FlareRow row, float width)
        {
            float known;
            if (_heights.TryGetValue(HeightKey(row), out known))
            {
                return known;
            }
            switch (row.Kind)
            {
                case CellPost:
                    int chars = (row.Post != null && row.Post.Text != null ? row.Post.Text.Length : 0) + 90;
                    float lines = Mathf.Ceil(chars * FlareKit.Body * 0.55f / Mathf.Max(200f, width - 200f));
                    return 210f + lines * 40f + (row.Post != null && FlarePictures.KeyOf(row.Post) != null ? 420f : 0f);
                case CellNotice: return 150f;
                case CellAccount: return 150f;
                case CellThread: return 130f;
                case CellBubble: return 120f;
                case CellProfile: return 560f;
                case CellNote: return row.Number == 1 ? 70f : 110f;
                case CellTag: return 110f;
                case CellShop: return 300f;
                case CellDeal: return 240f;
                case CellPrompt: return 150f;
                default: return 100f;
            }
        }

        private static long HeightKey(FlareRow row)
        {
            long id = row.Post != null ? row.Post.Id : row.Dm != null ? 1000000L + row.Dm.Id : row.Number;
            return row.Kind * 100000000L + id * 4 + (row.Kind == CellPost && row.Number == 1 ? 1 : 0);
        }

        private void Measured(FlareRow row, float height)
        {
            if (row.Post != null || row.Dm != null)
            {
                if (_heights.Count >= HeightsMost)
                {
                    _heights.Clear();
                }
                _heights[HeightKey(row)] = height;
            }
        }

        private abstract class Card : FlareCell
        {
            protected readonly FlareApp App;
            protected readonly Image Back;
            protected FlareLook Look = FlareLook.Light;

            protected Card(FlareApp app, string name, bool clickable)
            {
                App = app;
                Rect = FlareKit.Rect(app._root, name);
                Back = FlareKit.Box(Rect, "Card", Color.white, FlareKit.Radius, false, clickable);
                FlareKit.Stretch(Back.rectTransform, 0f, 0f, 0f, 0f);
                if (clickable)
                {
                    FlareKit.Click(Back, Clicked);
                }
            }

            protected virtual void Clicked()
            {
            }

            public override void Paint(FlareLook look)
            {
                Look = look;
                Back.color = look.Card;
                Colours();
            }

            protected abstract void Colours();
        }

        // An avatar: the account's picture, a logo in a white disc, an emblem or the initials on its colour; round, no mask.
        private sealed class Avatar
        {
            private const float LogoInset = 0.17f;   // of the disc's size each side: a square in a circle is 0.71 of it across
            private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

            public readonly Image Circle, Glyph;
            public readonly TextMeshProUGUI Letters;
            public readonly RawImage Photo, Logo;

            public Avatar(Transform parent, float size, Action click)
            {
                Circle = FlareKit.Box(parent, "Avatar", Color.white, 0f, true, click != null);
                if (click != null)
                {
                    FlareKit.Click(Circle, click);
                }
                Letters = FlareKit.Text(Circle.transform, "Initials", size * 0.38f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                FlareKit.Stretch(Letters.rectTransform, 0f, 0f, 0f, 0f);
                Glyph = FlareKit.Box(Circle.transform, "Glyph", Color.white);
                FlareKit.Stretch(Glyph.rectTransform, size * 0.22f, size * 0.22f, size * 0.22f, size * 0.22f);
                Photo = FlareKit.Picture(Circle.transform, "Photo");
                FlareKit.Stretch(Photo.rectTransform, 0f, 0f, 0f, 0f);
                Logo = FlareKit.Picture(Circle.transform, "Logo");
                FlareKit.Show(Photo, false);
                FlareKit.Show(Logo, false);
            }

            public void Show(FlareApp app, string key, string name, float x, float y, float size)
            {
                FlareKit.Place(Circle.rectTransform, x, y, size, size);
                FlareAvatarShown shown = app._avatars.Of(key);
                bool photo = shown.Picture != null, logo = !photo && shown.Logo != null;
                int emblem = photo || logo ? -1 : shown.Emblem >= 0 ? shown.Emblem : OfficialOf(key);
                Circle.color = photo ? Clear : logo ? Color.white : FlareLook.AvatarOf(key);
                FlareKit.Show(Photo, photo);
                FlareKit.Show(Logo, logo);
                FlareKit.Show(Glyph, emblem >= 0);
                FlareKit.Show(Letters, !photo && !logo && emblem < 0);
                if (photo)
                {
                    Photo.texture = shown.Picture;
                }
                else if (logo)
                {
                    Logo.texture = shown.Logo;
                    FlareKit.Place(Logo.rectTransform, size * LogoInset, size * LogoInset, size * (1f - LogoInset * 2f), size * (1f - LogoInset * 2f));
                }
                else if (emblem >= 0)
                {
                    Glyph.sprite = FlareIcons.Get(GlyphOfEmblem(emblem));
                }
                else
                {
                    FlareKit.Set(Letters, Initials(name));
                }
            }

            private static int OfficialOf(string key)
            {
                int n;
                return key != null && key.StartsWith("o:", StringComparison.Ordinal) && int.TryParse(key.Substring(2), out n) ? FlareAvatarRules.Official(n) : -1;
            }
        }

        // An emblem's glyph (FlareAvatarRules.Emblem*).
        private static FlareGlyph GlyphOfEmblem(int emblem)
        {
            switch (emblem)
            {
                case FlareAvatarRules.EmblemPolice: return FlareGlyph.Flag;
                case FlareAvatarRules.EmblemTransit: return FlareGlyph.Pin;
                case FlareAvatarRules.EmblemBank:
                case FlareAvatarRules.EmblemBusiness: return FlareGlyph.Store;
                case FlareAvatarRules.EmblemLedger: return FlareGlyph.Mail;
                case FlareAvatarRules.EmblemCompany: return FlareGlyph.Share;
                case FlareAvatarRules.EmblemCandidate: return FlareGlyph.Check;
                case FlareAvatarRules.EmblemTycoon: return FlareGlyph.StarFull;
                default: return FlareGlyph.City;
            }
        }

        // A pill button: filled or quiet, its words, its click made once.
        private sealed class Pill
        {
            public readonly Image Back;
            public readonly TextMeshProUGUI Label;

            public Pill(Transform parent, string name, Action click)
            {
                Back = FlareKit.Box(parent, name, Color.white, 0f, true, true);
                FlareKit.Click(Back, click);
                Label = FlareKit.Text(Back.transform, "Label", 26f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                FlareKit.Stretch(Label.rectTransform, 12f, 0f, 12f, 0f);
            }

            public float Show(FlareLook look, string text, bool filled, float x, float y, float height = 58f)
            {
                FlareKit.Set(Label, text);
                float w = Mathf.Max(140f, Label.GetPreferredValues(text, 2000f, 0f).x + 60f);
                FlareKit.Place(Back.rectTransform, x, y, w, height);
                Back.color = filled ? look.Text : look.Chip;
                Label.color = filled ? look.Card : look.ChipText;
                FlareKit.Show(Back, true);
                return w;
            }

            public void Hide()
            {
                FlareKit.Show(Back, false);
            }
        }

        private sealed class PostCell : Card
        {
            private readonly Avatar _avatar;
            private readonly TextMeshProUGUI _name, _meta, _context, _body, _attach, _going, _case, _replies, _reposts, _likes;
            private readonly Image _check, _attachBox, _attachIcon, _goingBack, _caseBack, _replyIcon, _repostIcon, _likeIcon, _replyHit, _repostHit, _likeHit;
            private readonly RawImage _picture;

            public PostCell(FlareApp app) : base(app, "Post", true)
            {
                _avatar = new Avatar(Rect, FlareKit.Avatar, Author);
                _name = FlareKit.Text(Rect, "Name", FlareKit.Name, Color.white, FontStyles.Bold);
                _name.textWrappingMode = TextWrappingModes.NoWrap;
                _check = FlareKit.Box(Rect, "Check", Color.white);
                _check.sprite = FlareIcons.Get(FlareGlyph.Check);
                _meta = FlareKit.Text(Rect, "Meta", FlareKit.Small, Color.white);
                _meta.textWrappingMode = TextWrappingModes.NoWrap;
                _context = FlareKit.Text(Rect, "Context", FlareKit.Small, Color.white);
                _body = FlareKit.Text(Rect, "Body", FlareKit.Body, Color.white);
                _body.overflowMode = TextOverflowModes.Overflow;
                _attachBox = FlareKit.Box(Rect, "Attach", Color.white, 18f);
                _attachIcon = FlareKit.Box(_attachBox.transform, "Icon", Color.white);
                _attach = FlareKit.Text(_attachBox.transform, "Words", 28f, Color.white);
                _attach.overflowMode = TextOverflowModes.Overflow;
                _picture = FlareKit.Picture(Rect, "Picture");
                _goingBack = FlareKit.Box(Rect, "Going", Color.white, 0f, true, true);
                FlareKit.Click(_goingBack, () => App.GoingClicked(Row));
                _going = FlareKit.Text(_goingBack.transform, "Label", 28f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                FlareKit.Stretch(_going.rectTransform, 0f, 0f, 0f, 0f);
                _caseBack = FlareKit.Box(Rect, "Case", Color.white, 14f);
                _case = FlareKit.Text(_caseBack.transform, "Label", 25f, Color.white, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                FlareKit.Stretch(_case.rectTransform, 18f, 0f, 18f, 0f);
                _replyHit = Action(out _replyIcon, out _replies, FlareGlyph.Reply, () => App.ReplyClicked(Row));
                _repostHit = Action(out _repostIcon, out _reposts, FlareGlyph.Repost, () => App.RepostClicked(Row));
                _likeHit = Action(out _likeIcon, out _likes, FlareGlyph.Heart, () => App.LikeClicked(Row));
            }

            private Image Action(out Image icon, out TextMeshProUGUI count, FlareGlyph glyph, Action click)
            {
                Image hit = FlareKit.Box(Rect, "Action", new Color(1f, 1f, 1f, 0f), 20f, false, true);
                FlareKit.Click(hit, click);
                icon = FlareKit.Box(hit.transform, "Icon", Color.white);
                icon.sprite = FlareIcons.Get(glyph);
                FlareKit.Place(icon.rectTransform, 10f, 9f, 38f, 38f);
                count = FlareKit.Text(hit.transform, "Count", FlareKit.Small, Color.white, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                FlareKit.Place(count.rectTransform, 58f, 0f, 160f, 56f);
                return hit;
            }

            protected override void Clicked()
            {
                App.PostClicked(Row);
            }

            private void Author()
            {
                App.AuthorClicked(Row);
            }

            protected override void Colours()
            {
                _name.color = Look.Text;
                _meta.color = Look.Muted;
                _context.color = Look.Muted;
                _body.color = Look.Text;
                _attachBox.color = Look.Raised;
                _attachIcon.color = Look.Accent;
                _attach.color = Look.Text;
                _check.color = Look.Accent;
                _caseBack.color = Look.AccentSoft;
                _case.color = Look.Text;
                _replies.color = _reposts.color = _likes.color = Look.Muted;
            }

            public override float Bind(FlareRow row, float width)
            {
                FlarePost p = row.Post;
                bool big = row.Number == 1;
                long now = App._director.Now;
                string name, handle, key;
                App.AuthorOf(p, out name, out handle, out key);
                float pad = FlareKit.Pad, av = big ? 100f : FlareKit.Avatar;
                float x0 = pad + av + FlareKit.Gap, tw = width - x0 - pad;
                _avatar.Show(App, key, name, pad, pad, av);
                float y = pad - 4f;
                FlareKit.Set(_name, name);
                float nameW = Mathf.Min(tw * 0.6f, _name.GetPreferredValues(name, 4000f, 0f).x + 4f);
                FlareKit.Place(_name.rectTransform, x0, y, nameW, 42f);
                bool official = p.Author == FlareAuthor.Official || p.Author == FlareAuthor.Ledger;
                FlareKit.Show(_check, official);
                float mx = x0 + nameW + 10f;
                if (official)
                {
                    FlareKit.Place(_check.rectTransform, mx, y + 6f, 30f, 30f);
                    mx += 38f;
                }
                FlareKit.Set(_meta, handle + "  ·  " + FlareText.Ago(p.At, now));
                FlareKit.Place(_meta.rectTransform, mx, y + 6f, Mathf.Max(60f, x0 + tw - mx), 36f);
                y += 46f;
                string context = App.ContextOf(p);
                FlareKit.Show(_context, context != null);
                if (context != null)
                {
                    FlareKit.Set(_context, context);
                    FlareKit.Place(_context.rectTransform, x0, y, tw, 34f);
                    y += 36f;
                }
                string built = App.WordsOf(p, name);
                string body = p.Text ?? built;
                string attach = p.Text != null && p.Source != FlareSource.Reply && p.Source != FlareSource.Life ? built : null;
                _body.fontSize = big ? FlareKit.Body * 1.18f : FlareKit.Body;
                float bh = FlareKit.Height(_body, body, tw);
                FlareKit.Set(_body, body);
                FlareKit.Place(_body.rectTransform, x0, y, tw, bh + 4f);
                y += bh + 12f;
                FlareKit.Show(_attachBox, attach != null);
                if (attach != null)
                {
                    float ah = FlareKit.Height(_attach, attach, tw - 100f);
                    FlareKit.Place(_attachBox.rectTransform, x0, y, tw, ah + 36f);
                    _attachIcon.sprite = FlareIcons.Get(FlareApp.GlyphOf(p));
                    FlareKit.Place(_attachIcon.rectTransform, 22f, 18f, 38f, 38f);
                    FlareKit.Set(_attach, attach);
                    FlareKit.Place(_attach.rectTransform, 76f, 18f, tw - 100f, ah + 4f);
                    y += ah + 36f + 14f;
                }
                Texture2D picture = App.PictureOf(p);
                FlareKit.Show(_picture, picture != null);
                if (picture != null)
                {
                    float pw = Mathf.Min(tw, big ? 1000f : 840f), ph = pw * picture.height / Mathf.Max(1f, picture.width);
                    _picture.texture = picture;
                    FlareKit.Place(_picture.rectTransform, x0, y, pw, ph);
                    y += ph + 14f;
                }
                bool invite = p.Source == FlareSource.Invite || (p.Source == FlareSource.Announce && p.Arg == (int)FlareAnnounce.Event);
                FlareKit.Show(_goingBack, invite);
                if (invite)
                {
                    bool going = App._director.IsGoing(p.Id);
                    FlareKit.Set(_going, FlareText.T(going ? "reallife_flare_going_yes" : "reallife_flare_going"));
                    _goingBack.color = going ? Look.Chip : Look.Accent;
                    _going.color = going ? Look.ChipText : Color.white;
                    FlareKit.Place(_goingBack.rectTransform, x0, y, 260f, 62f);
                    y += 76f;
                }
                string caseLine = App.CaseLineOf(p);
                FlareKit.Show(_caseBack, caseLine != null);
                if (caseLine != null)
                {
                    FlareKit.Set(_case, caseLine);
                    FlareKit.Place(_caseBack.rectTransform, x0, y, tw, 52f);
                    y += 64f;
                }
                int replies = App.RepliesOf(p, now), reposts = FlareLiveRules.Shown(p.Reposts, p, now), likes = FlareLiveRules.Shown(p.Likes, p, now) + p.PlayerLikes + (p.Liked ? 1 : 0);
                float slot = Mathf.Min(260f, tw / 3f);
                bool display = p.Id < 0;   // made for display (a friend's reply, a comment under another's post): it can be liked, not answered or shared
                FlareKit.Show(_replyHit, !display);
                FlareKit.Show(_repostHit, !display);
                FlareKit.Show(_likeHit, !display || p.Slot >= 0);
                FlareKit.Place(_replyHit.rectTransform, x0 - 10f, y, slot, 56f);
                FlareKit.Place(_repostHit.rectTransform, x0 - 10f + slot, y, slot, 56f);
                FlareKit.Place(_likeHit.rectTransform, x0 - 10f + (display ? 0f : slot * 2f), y, slot, 56f);
                FlareKit.Set(_replies, replies > 0 ? FlareText.N(replies) : string.Empty);
                FlareKit.Set(_reposts, reposts > 0 ? FlareText.N(reposts) : string.Empty);
                FlareKit.Set(_likes, likes > 0 ? FlareText.N(likes) : string.Empty);
                _replyIcon.color = Look.Muted;
                _repostIcon.color = p.Reposted ? Look.Share : Look.Muted;
                _reposts.color = p.Reposted ? Look.Share : Look.Muted;
                _likeIcon.sprite = FlareIcons.Get(p.Liked ? FlareGlyph.HeartFull : FlareGlyph.Heart);
                _likeIcon.color = p.Liked ? Look.Like : Look.Muted;
                _likes.color = p.Liked ? Look.Like : Look.Muted;
                y += 56f + pad - 14f;
                y = Mathf.Max(y, pad * 2f + av);
                App.Measured(row, y);
                return y;
            }
        }

        private sealed class NoticeCell : Card
        {
            private readonly Image _icon;
            private readonly Avatar _avatar;
            private readonly TextMeshProUGUI _line, _snippet, _time;

            public NoticeCell(FlareApp app) : base(app, "Notice", true)
            {
                _icon = FlareKit.Box(Rect, "Icon", Color.white);
                _avatar = new Avatar(Rect, 60f, null);
                _line = FlareKit.Text(Rect, "Line", FlareKit.Body, Color.white);
                _snippet = FlareKit.Text(Rect, "Snippet", FlareKit.Small, Color.white);
                _snippet.maxVisibleLines = 2;
                _time = FlareKit.Text(Rect, "Time", FlareKit.Small, Color.white, FontStyles.Normal, TextAlignmentOptions.TopRight);
            }

            protected override void Clicked()
            {
                App.NoticeClicked(Row);
            }

            protected override void Colours()
            {
                _line.color = Look.Text;
                _snippet.color = Look.Muted;
                _time.color = Look.Muted;
            }

            public override float Bind(FlareRow row, float width)
            {
                FlareNotice n = row.Notice;
                string name = App.ReactorName(n.Who);
                float pad = FlareKit.Pad;
                _icon.sprite = FlareIcons.Get(GlyphOf(n.Kind));
                _icon.color = n.Kind == FlareNoticeKind.Like ? Look.Like : n.Kind == FlareNoticeKind.Repost ? Look.Share : Look.Accent;
                FlareKit.Place(_icon.rectTransform, pad, pad + 6f, 44f, 44f);
                _avatar.Show(App, n.Who, name, pad + 64f, pad, 60f);
                float x0 = pad + 64f + 60f + FlareKit.Gap, tw = width - x0 - pad - 150f;
                string line = FlareText.Notice(n.Kind, "<b>" + name + "</b>");
                float lh = FlareKit.Height(_line, line, tw);
                FlareKit.Set(_line, line);
                FlareKit.Place(_line.rectTransform, x0, pad, tw, lh + 2f);
                FlareKit.Set(_time, FlareText.Ago(n.At, App._director.Now));
                FlareKit.Place(_time.rectTransform, width - pad - 140f, pad, 140f, 34f);
                string snippet = App.SnippetOf(n);
                float y = pad + Mathf.Max(60f, lh) + 6f;
                FlareKit.Show(_snippet, snippet != null);
                if (snippet != null)
                {
                    float sh = Mathf.Min(FlareKit.Height(_snippet, snippet, width - x0 - pad), 76f);
                    FlareKit.Set(_snippet, snippet);
                    FlareKit.Place(_snippet.rectTransform, x0, y, width - x0 - pad, sh + 2f);
                    y += sh + 6f;
                }
                Back.color = n.At > App._seenBefore ? Look.Raised : Look.Card;
                return y + pad - 6f;
            }

            private static FlareGlyph GlyphOf(FlareNoticeKind kind)
            {
                switch (kind)
                {
                    case FlareNoticeKind.Like: return FlareGlyph.HeartFull;
                    case FlareNoticeKind.Reply: return FlareGlyph.Reply;
                    case FlareNoticeKind.Repost: return FlareGlyph.Repost;
                    case FlareNoticeKind.Mention: return FlareGlyph.At;
                    case FlareNoticeKind.Case: return FlareGlyph.City;
                    case FlareNoticeKind.Dm: return FlareGlyph.Mail;
                    case FlareNoticeKind.Going: return FlareGlyph.Calendar;
                    default: return FlareGlyph.StarFull;
                }
            }
        }

        private sealed class AccountCell : Card
        {
            private readonly Avatar _avatar;
            private readonly TextMeshProUGUI _name, _handle, _about;
            private readonly Pill _follow;

            public AccountCell(FlareApp app) : base(app, "Account", true)
            {
                _avatar = new Avatar(Rect, 76f, null);
                _name = FlareKit.Text(Rect, "Name", FlareKit.Name, Color.white, FontStyles.Bold);
                _handle = FlareKit.Text(Rect, "Handle", FlareKit.Small, Color.white);
                _about = FlareKit.Text(Rect, "About", FlareKit.Small, Color.white);
                _follow = new Pill(Rect, "Follow", () => App.FollowAccount(Row));
            }

            protected override void Clicked()
            {
                App.AccountClicked(Row);
            }

            protected override void Colours()
            {
                _name.color = Look.Text;
                _handle.color = Look.Muted;
                _about.color = Look.Muted;
            }

            public override float Bind(FlareRow row, float width)
            {
                string key = row.Key;
                string name = App._director.AccountName(key);
                float pad = FlareKit.Pad;
                _avatar.Show(App, key, name, pad, pad, 76f);
                float x0 = pad + 76f + FlareKit.Gap, tw = width - x0 - pad - 230f;
                FlareKit.Set(_name, name);
                FlareKit.Place(_name.rectTransform, x0, pad - 2f, tw, 40f);
                FlareKit.Set(_handle, App.Handle(key, name));
                FlareKit.Place(_handle.rectTransform, x0, pad + 40f, tw, 34f);
                string about = row.Text;
                float ah = about != null ? FlareKit.Height(_about, about, tw) : 0f;
                FlareKit.Set(_about, about);
                FlareKit.Place(_about.rectTransform, x0, pad + 78f, tw, ah + 2f);
                FlareFile file = App._director.File;
                bool followable = file != null && FlareApp.Followable(key);
                if (followable)
                {
                    bool following = FlareCast.Followed(file, key);
                    _follow.Show(Look, FlareText.T(following ? "reallife_flare_follow_on" : "reallife_flare_follow"), !following, width - pad - 200f, pad + 8f);
                }
                else
                {
                    _follow.Hide();
                }
                return Mathf.Max(pad * 2f + 76f, pad + 80f + ah + pad);
            }
        }

        private sealed class ThreadCell : Card
        {
            private readonly Avatar _avatar;
            private readonly TextMeshProUGUI _name, _snippet, _time;
            private readonly Image _dot;

            public ThreadCell(FlareApp app) : base(app, "Conversation", true)
            {
                _avatar = new Avatar(Rect, 76f, null);
                _name = FlareKit.Text(Rect, "Name", FlareKit.Name, Color.white, FontStyles.Bold);
                _snippet = FlareKit.Text(Rect, "Snippet", FlareKit.Small, Color.white);
                _snippet.textWrappingMode = TextWrappingModes.NoWrap;
                _time = FlareKit.Text(Rect, "Time", FlareKit.Small, Color.white, FontStyles.Normal, TextAlignmentOptions.TopRight);
                _dot = FlareKit.Box(Rect, "Unread", Color.white, 0f, true);
            }

            protected override void Clicked()
            {
                App.ConversationClicked(Row);
            }

            protected override void Colours()
            {
                _name.color = Look.Text;
                _snippet.color = Look.Muted;
                _time.color = Look.Muted;
                _dot.color = Look.Accent;
            }

            public override float Bind(FlareRow row, float width)
            {
                FlareDm m = row.Dm;
                float pad = FlareKit.Pad;
                string name = m.Name ?? App._director.AccountName(m.With);
                _avatar.Show(App, m.With, name, pad, pad, 76f);
                float x0 = pad + 76f + FlareKit.Gap;
                FlareKit.Set(_name, name);
                FlareKit.Place(_name.rectTransform, x0, pad - 2f, width - x0 - pad - 170f, 40f);
                FlareKit.Set(_time, FlareText.Ago(m.At, App._director.Now));
                FlareKit.Place(_time.rectTransform, width - pad - 160f, pad, 160f, 34f);
                FlareKit.Set(_snippet, (m.Mine ? FlareText.T("reallife_flare_dm_you") + " " : string.Empty) + (App._director.DmWords(m) ?? FlareText.DmWords(m, name)));
                FlareKit.Place(_snippet.rectTransform, x0, pad + 44f, width - x0 - pad - 40f, 36f);
                bool unread = row.Number > 0;
                FlareKit.Show(_dot, unread);
                FlareKit.Place(_dot.rectTransform, width - pad - 22f, pad + 52f, 20f, 20f);
                _snippet.fontStyle = unread ? FontStyles.Bold : FontStyles.Normal;
                return pad * 2f + 76f;
            }
        }

        private sealed class BubbleCell : Card
        {
            private readonly Image _bubble;
            private readonly TextMeshProUGUI _words, _time;

            public BubbleCell(FlareApp app) : base(app, "Message", false)
            {
                _bubble = FlareKit.Box(Rect, "Bubble", Color.white, 30f);
                _words = FlareKit.Text(_bubble.transform, "Words", FlareKit.Body, Color.white);
                _words.overflowMode = TextOverflowModes.Overflow;
                _time = FlareKit.Text(Rect, "Time", 22f, Color.white);
            }

            public override void Paint(FlareLook look)
            {
                Look = look;
                Back.color = new Color(0f, 0f, 0f, 0f);
                Colours();
            }

            protected override void Colours()
            {
                _time.color = Look.Muted;
            }

            public override float Bind(FlareRow row, float width)
            {
                FlareDm m = row.Dm;
                string words = App._director.DmWords(m) ?? FlareText.DmWords(m, m.Name ?? App._director.AccountName(m.With));
                float most = width * 0.7f;
                float tw = Mathf.Min(most - 56f, _words.GetPreferredValues(words, most - 56f, 0f).x + 4f);
                float th = FlareKit.Height(_words, words, tw);
                float bw = tw + 56f, bh = th + 40f;
                float x = m.Mine ? width - bw : 0f;
                FlareKit.Place(_bubble.rectTransform, x, 0f, bw, bh);
                _bubble.color = m.Mine ? Look.Accent : Look.Raised;
                _words.color = m.Mine ? Color.white : Look.Text;
                FlareKit.Set(_words, words);
                FlareKit.Place(_words.rectTransform, 28f, 20f, tw, th + 2f);
                FlareKit.Set(_time, FlareText.Ago(m.At, App._director.Now));
                _time.alignment = m.Mine ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
                FlareKit.Place(_time.rectTransform, m.Mine ? width - 300f : 10f, bh + 4f, 290f, 30f);
                float h = bh + 38f;
                App.Measured(row, h);
                return h;
            }
        }
    }
}
