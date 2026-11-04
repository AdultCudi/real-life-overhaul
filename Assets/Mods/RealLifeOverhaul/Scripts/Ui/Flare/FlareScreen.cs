using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face over the phone app: the account read into the face's model when the account, the page or the typing
    /// changes, and every target's press done through the director, as the old panel does it. Typing goes through the host's
    /// own text box, into the field the player pressed. Cost: nothing per frame but a comparison of the account's revision;
    /// a fill and a layout when something changed; a list's rows are filled up to their caps and laid only where in view.
    /// </summary>
    public sealed partial class FlareScreen : IUiScreen, IUiTypingSubmit, IUiTypingQuiet, IFlareKitHost
    {
        public const int FeedMost = 100, NoticesMost = FlareDirector.KitNoticesMost, ThreadsMost = 40, BubblesMost = 80, ResultsMost = 30, ReviewsMost = 40,
            InfluencersMost = 30, CardsMost = 24, StoriesMost = FlareFace.MostStories, WhoMost = 5, ExploreWhoMost = 12, TrendsMost = 5, HistoryMost = 8,
            NeedsMost = 4;   // the side drops the rows that do not fit

        private readonly FlareDirector _flare;
        private readonly FlareFaceModel _m = new FlareFaceModel();
        private readonly FlareKitPress _kit;

        // Where the player is, and where Back goes.
        private struct Spot
        {
            public int Page, Post;
            public string Profile, Tag, With;
        }

        private readonly List<Spot> _history = new List<Spot>(HistoryMost);
        private int _page = FlareFace.Home;
        private int _tab;                        // Home's tab
        private int _profileTab;                 // a profile's tab
        private string _profile = "me";          // the account the profile shows
        private string _tag;                     // Explore: the tag open, or null
        private string _with;                    // Messages: the conversation open, or null
        private int _post = -1;                  // the thread's post
        private int _shop;                       // Business: the business chosen
        private int _district;                   // Hire: the district chosen, 0 every district
        private int _hireSel = -1;               // Hire: the influencer chosen in the table (the card under it), -1 the first
        private int _sheet;                      // the sheet open over the page
        private int _field = -1;                 // the field the typing fills

        // The words typed.
        private string _search = string.Empty, _inbox = string.Empty, _message = string.Empty, _reply = string.Empty;

        private int _revision = -1;
        private int _askedFrame = -1;
        private bool _dirty = true;
        private bool _moved;                     // only a list moved (the wheel, a row revealed) or a picture came: laid again, not filled
        private long _seenBefore = long.MaxValue;   // Notifications: seen up to here when the page opened, so what came since stays marked while it shows

        public FlareScreen(FlareDirector flare)
        {
            _flare = flare;
            _kit = new FlareKitPress(this) { AdTargets = FlareCityCatalogue.AdTargets, AdSizes = FlareCityCatalogue.AdReach.Length, AdDays = FlareCityCatalogue.AdDays.Length };
            PicturesOn();
            TakeKitPage();
            TakeWantedPhoto();
        }

        public string Id => "flare";

        public bool Dirty
        {
            get
            {
                int frame = UnityEngine.Time.frameCount;
                if (_askedFrame >= 0 && frame - _askedFrame > 2)
                {
                    Shown();   // asked again after a gap: the screen was closed and is shown again
                }
                _askedFrame = frame;
                return _dirty || _moved || (_flare != null && _flare.Revision != _revision) || PicturesCame || PickerCame || Stale;   // Stale: the times shown moved on (ClockStep)
            }
        }

        // The app is opened again: a test's page taken, the notifications' marks read afresh.
        private void Shown()
        {
            TakeKitPage();
            TakeWantedPhoto();
            if (_page == FlareFace.Notices)
            {
                OpenNotices();
            }
            _dirty = true;
        }

        private void TakeKitPage()
        {
            if (_flare == null || _flare.KitPage < 0)
            {
                return;
            }
            int asked = _flare.KitPage;
            _flare.KitPage = -1;
            _history.Clear();
            switch (asked)
            {
                case 5: Show(FlareFace.Business); break;   // the old test numbers: 5 business, 6 the city, 7 the composer, 8 hiring, 10 the studio
                case 6: Show(FlareFace.City); break;
                case 7: Show(FlareFace.Home); OpenCompose(-1); break;
                case 8: Show(FlareFace.Hire); break;
                case 10: Show(FlareFace.Studio); break;
                default: Show(Math.Max(0, Math.Min(FlareFace.Pages - 1, asked))); break;
            }
        }

        // ------------------------------------------------------------------------------------------------ typing

        public int TypingCommand => FlareFace.FieldCommand;

        public string TypingText
        {
            get
            {
                if (_field == FlareFace.SearchField && _page != FlareFace.Explore)
                {
                    Show(FlareFace.Explore);   // the side's search: its words show in Explore's field as they are typed
                }
                return Typing(_field) ?? string.Empty;
            }
        }

        public int TypingLimit => _field == FlareFace.ThreadField ? FlareLiveRules.DmTextMost : _field == FlareFace.SearchField || _field == FlareFace.InboxField ? 40
            : _field == FlareFace.BioField ? FlareDeskRules.BioMost : FlareLiveRules.TextMost;

        private string Typing(int field)
        {
            switch (field)
            {
                case FlareFace.ComposeField: return _compose.Text;
                case FlareFace.SearchField: return _search;
                case FlareFace.InboxField: return _inbox;
                case FlareFace.ThreadField: return _message;
                case FlareFace.ReplyField: return _reply;
                case FlareFace.PickerField: return _pickQuery.Search;
                case FlareFace.BioField: return _kit.Bio;
                default: return null;
            }
        }

        public void Typed(string text)
        {
            text = text ?? string.Empty;
            switch (_field)
            {
                case FlareFace.ComposeField: _compose.Text = text; break;
                case FlareFace.SearchField: _search = text; _tag = null; break;
                case FlareFace.InboxField: _inbox = text; break;
                case FlareFace.ThreadField: _message = text; break;
                case FlareFace.ReplyField: _reply = text; break;
                case FlareFace.PickerField: _pickQuery.Search = text.Length == 0 ? null : text; _pickBuilt = false; _m.Picker.Photos.Offset = 0f; break;
                case FlareFace.BioField: _kit.Bio = text.Length > FlareDeskRules.BioMost ? text.Substring(0, FlareDeskRules.BioMost) : text; break;
                default: return;
            }
            _dirty = true;
        }

        public void Submitted(string text)
        {
            Typed(text);
            if (_field == FlareFace.ThreadField || _field == FlareFace.ReplyField)
            {
                Send();   // Enter sends a message or a reply; a post waits for its Post button, a search searches as it is typed
            }
        }

        // ------------------------------------------------------------------------------------------------ the host's calls

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            // A scroll or a picture come in lays the rows filled already (their heights kept): only what changed is read again.
            bool fill = _dirty || _flare == null || _flare.Revision != _revision || Stale;
            if (_flare != null)
            {
                _revision = _flare.Revision;   // read first: what the fill itself changes (a message read) shows on the next layout
            }
            PicturesLaid();
            if (fill)
            {
                Fill();
            }
            FlareFace.Lay(s, frame, dark, keys, UiText.Words, _m);
            if (_sheet == FlareFace.PhotosSheet)
            {
                PickerLaid();
            }
            _dirty = _moved = false;
        }

        public void Focused(int command, int arg)
        {
            if (command == FlareFace.FieldCommand)
            {
                _field = arg;   // the field the typing will fill once it is pressed
            }
        }

        public bool Press(int command, int arg)
        {
            bool done;
            if (command != FlareFace.ButtonCommand)
            {
                _kit.EndArmed = -1;   // asked to end a campaign, then did something else: the question lapses
            }
            try
            {
                done = Pressed(command, arg);
            }
            catch (Exception e)
            {
                FlareDirector.Logger?.Error(e);
                done = false;
            }
            _dirty |= done;
            return done;
        }

        private bool Pressed(int command, int arg)
        {
            switch (command)
            {
                case FlareFace.NavCommand: return arg >= 0 && arg < FlareFace.Pages && Nav(arg);
                case FlareFace.PostCommand:
                case FlareFace.ComposerCommand: return OpenCompose(-1);
                case FlareFace.ChipCommand: return OpenCompose(arg);
                case FlareFace.AccountCommand: return OpenProfile("me");
                case FlareFace.StudioLinkCommand: return Nav(FlareFace.Studio);
                case FlareFace.TabCommand: return SetTab(arg);
                case FlareFace.StoryCommand: return OpenStory(arg);
                case FlareFace.SearchCommand: return Nav(FlareFace.Explore);
                case FlareFace.TrendCommand: return arg >= 0 && arg < _trendTags.Count && OpenTag(_trendTags[arg]);
                case FlareFace.WhoCommand: return arg >= 0 && arg < _whoKeys.Count && OpenProfile(_whoKeys[arg]);
                case FlareFace.WhoFollowCommand: return arg >= 0 && arg < _whoKeys.Count && ToggleFollow(_whoKeys[arg]);
                case FlareFace.RowCommand: return OpenRow(arg);
                case FlareFace.AuthorCommand: return RefAt(arg).Key != null && OpenProfile(RefAt(arg).Key);
                case FlareFace.LikeCommand: return Like(arg);
                case FlareFace.RepostCommand: return Repost(arg);
                case FlareFace.CommentCommand: return Comment(arg);
                case FlareFace.ShareCommand: return Share(arg);
                case FlareFace.TagCommand: return TagOfRow(arg);
                case FlareFace.FollowCommand: return RefAt(arg).Key != null && ToggleFollow(RefAt(arg).Key);
                case FlareFace.ButtonCommand: return RowButton(arg);
                case FlareFace.PickCommand: return Pick(arg);
                case FlareFace.BackCommand: return Back();
                case FlareFace.ProfileFollowCommand: return _profile != "me" && ToggleFollow(_profile);
                case FlareFace.DetailCommand: return _with != null && ProfileOfThread(_with);
                case FlareFace.QuickCommand: return Quick(arg);
                case FlareFace.SendCommand: return Send();
                case FlareFace.OptionCommand: return SheetOption(arg);
                case FlareFace.SheetChipCommand: return SheetChip(arg / FlareFace.GroupStride, arg % FlareFace.GroupStride);
                case FlareFace.SheetConfirmCommand: return SheetConfirm();
                case FlareFace.SheetCancelCommand: return CloseSheet();
                case FlareFace.PickerPhotoCommand: return PickPhoto(arg);
                case FlareFace.PickerAlbumCommand: return PickAlbum(arg);
                case FlareFace.MarkAllCommand:
                case FlareFace.SortCommand:
                case FlareFace.NicheCommand: return KitPress(command, arg);
                case FlareFace.PromoteCommand: return OpenAd(arg);
                case FlareFace.EditCommand: return OpenEdit();
                default: return false;
            }
        }

        public bool Back()
        {
            if (_sheet != FlareFace.NoSheet)
            {
                return CloseSheet();
            }
            if (_history.Count > 0)
            {
                Spot s = _history[_history.Count - 1];
                _history.RemoveAt(_history.Count - 1);
                _profile = s.Profile ?? "me";
                _tag = s.Tag;
                _with = s.With;
                _post = s.Post;
                Show(s.Page);
                return true;
            }
            if (_page == FlareFace.Explore && (_tag != null || _search.Length > 0))
            {
                _tag = null;
                _search = string.Empty;
                _dirty = true;
                return true;
            }
            if (_page == FlareFace.Messages && _with != null)
            {
                _with = null;
                _dirty = true;
                return true;
            }
            if (_page != FlareFace.Home)
            {
                return Nav(FlareFace.Home);
            }
            return false;   // Home: Back closes the app
        }

        public void Tab(int step)
        {
            if (_sheet != FlareFace.NoSheet)
            {
                return;
            }
            if (_page == FlareFace.Home)
            {
                SetTab((_tab + step + 3) % 3);
            }
            else if (_page == FlareFace.Profile)
            {
                SetTab((_profileTab + step + 3) % 3);
            }
            else if (_page == FlareFace.Notices)
            {
                SetTab((_kit.NoticeTab + step + FlareKitPress.NoticeTabs) % FlareKitPress.NoticeTabs);
            }
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt && _sheet == FlareFace.NoSheet)
            {
                return Press(FlareFace.PostCommand, 0);
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            FlareList l = _m.List.Index == list ? _m.List : _m.Detail.Index == list ? _m.Detail : null;
            if (l != null)
            {
                l.Reveal = index;
                _moved = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            FlareList l = _m.List.Clip == clip ? _m.List : _m.Detail.Clip == clip ? _m.Detail : null;
            if (l == null)
            {
                return;
            }
            l.Offset = Math.Max(0f, l.Offset - notches * UiTokens.S16 * 2f);
            _moved = true;
        }

        // ------------------------------------------------------------------------------------------------ going places

        // A page of the nav: where Back went before is forgotten, the page opens at its top.
        private bool Nav(int page)
        {
            _history.Clear();
            _profile = "me";
            _tag = null;
            _with = page == FlareFace.Messages ? _with : null;
            Show(page);
            return true;
        }

        // Where the player is now, kept for Back before they go further.
        private void Push()
        {
            if (_history.Count >= HistoryMost)
            {
                _history.RemoveAt(0);
            }
            _history.Add(new Spot { Page = _page, Post = _post, Profile = _profile, Tag = _tag, With = _with });
        }

        private void Show(int page)
        {
            _page = page;
            _kit.EndArmed = -1;
            _sheet = FlareFace.NoSheet;
            _m.List.Offset = 0f;
            _m.List.Footed = false;
            _m.Detail.Offset = 0f;
            _m.Detail.Footed = false;
            if (page == FlareFace.Notices)
            {
                OpenNotices();
            }
            _dirty = true;
        }

        // The notifications open: what was seen before stays the line between new and old while the page shows; the badge goes.
        private void OpenNotices()
        {
            if (_flare == null)
            {
                return;
            }
            _seenBefore = _flare.NoticesSeenAt;
            _flare.NoticesSeen();
        }

        private bool OpenProfile(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            if (_page == FlareFace.Profile && key == _profile)
            {
                return true;
            }
            Push();
            _profile = key;
            _profileTab = 0;
            Show(FlareFace.Profile);
            return true;
        }

        private bool OpenTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }
            Push();
            Show(FlareFace.Explore);
            _tag = tag.TrimStart('#');
            _search = string.Empty;
            return true;
        }

        private bool OpenThread(int post)
        {
            FlareFile file = _flare != null ? _flare.File : null;
            FlarePost p = file != null ? file.Post(post) : null;
            if (p == null)
            {
                return false;
            }
            Push();
            _post = p.ReplyTo >= 0 && file.Post(p.ReplyTo) != null ? p.ReplyTo : post;   // a reply opens the thread of the post it answers
            _reply = string.Empty;
            Show(FlareFace.Thread);
            return true;
        }

        private bool OpenConversation(string with)
        {
            if (string.IsNullOrEmpty(with))
            {
                return false;
            }
            if (_page != FlareFace.Messages)
            {
                Push();
                Show(FlareFace.Messages);
            }
            _with = with;
            _message = string.Empty;
            _m.Detail.Offset = 0f;
            _m.Detail.Footed = false;
            _flare?.ReadDms(with);
            _dirty = true;
            return true;
        }

        private bool SetTab(int tab)
        {
            if (_page == FlareFace.Notices)
            {
                _m.List.Offset = 0f;
                return _kit.SetNoticeTab(tab);
            }
            if (tab < 0 || tab > 2)
            {
                return false;
            }
            if (_page == FlareFace.Profile)
            {
                _profileTab = tab;
            }
            else
            {
                _tab = tab;
            }
            _m.List.Offset = 0f;
            _dirty = true;
            return true;
        }

        private bool Pick(int arg)
        {
            if (arg < 0)
            {
                return false;
            }
            if (_page == FlareFace.Business)
            {
                _shop = arg;
            }
            else if (_page == FlareFace.Hire)
            {
                if (arg > _hireDistricts.Count)
                {
                    return false;
                }
                _district = arg;
            }
            else
            {
                return false;
            }
            _m.List.Offset = 0f;
            return true;
        }
    }
}
