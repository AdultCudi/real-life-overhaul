using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Photos;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Gallery on the kit: the albums as the window's sections (all, favourites, selfies, businesses, places, posted on Flare), an
    /// album under a heading a day with a search, a sort and filters by place, business and day, select mode (favourite, share, delete),
    /// one photo with its facts, its tags and what Flare did with it, and the editor. The thumbnails are the director's small copies of
    /// the photos in view only (at most 120), the whole photo is made for the viewer and destroyed when the viewer closes. Keys: the
    /// arrows, Q and E step the photos; F posts to Flare, P sets the wallpaper; Esc steps back, from the editor to the photo to the
    /// album, then closes. Cost: a layout when the album, the scroll, a thumbnail or the photo changes; the album is selected and
    /// sorted once a change, not once a layout.
    /// </summary>
    sealed partial class GalleryScreen : IUiScreen, IUiStepping, IUiTyping
    {
        private readonly GalleryApp _app;
        private readonly GalleryQuery _query = new GalleryQuery();
        private readonly List<PhotoEntry> _shown = new List<PhotoEntry>(PhotoStoreRules.Cap + 16);
        private readonly List<string> _picked = new List<string>(8);
        private readonly List<string> _places = new List<string>(16), _businesses = new List<string>(16), _days = new List<string>(16);
        private readonly int[] _counts = new int[(int)GalleryKit.Count];
        private readonly UiAlbumModel _album = new UiAlbumModel();
        private readonly UiPhotoModel _photo = new UiPhotoModel();
        private readonly GalleryTexts _texts = new GalleryTexts();
        private readonly List<string> _links = new List<string>(8);   // what each fact of the open photo links to: "p:" a place, "b:" a business, or null
        private bool _select, _viewing, _force = true, _rebuild = true, _needPosted = true;
        private int _index = -1;
        private int _dirVersion = -1, _thumbVersion = -1;
        private int _window;

        public GalleryScreen(GalleryApp app)
        {
            _app = app;
            _texts.T = UiText.T;
            _texts.Real = PhotosText.Real;
            _texts.Size = SizeOf;
        }

        public string Id => "photo_gallery";

        private PhotosDirector Director => PhotosDirector.Instance;

        public bool Dirty => _force || (_app != null && _app.LatestForKitAsked) || (Director != null && (Director.Version != _dirVersion || Director.Thumbs.Version != _thumbVersion));

        /// <summary>The Gallery app closed: the whole photo, the preview and every small copy are let go.</summary>
        public static void Release()
        {
            GalleryPictures.DropWhole();
            GalleryPictures.DropPreview();
            PhotosDirector director = PhotosDirector.Instance;
            director?.Thumbs.Clear();
            _dropEdit?.Invoke();
        }

        private static Action _dropEdit;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            GalleryPictures.Install();
            _dropEdit = DropEdit;
            PhotosDirector d = Director;
            if (d != null && _needPosted)
            {
                _needPosted = false;
                _texts.TwelveHour = PhotosGame.TwelveHour;
                d.RefreshPosted();
            }
            if (_app != null && _app.TakeLatestForKit() && d != null && d.Count > 0)
            {
                // a photo just taken opens whole, over the whole album, as the old page does
                _query.Clear();
                _viewing = _select = false;
                _rebuild = true;
                Build();
                int newest = GalleryRules.IndexOf(_shown, d.Photos[0].Name);
                if (newest >= 0)
                {
                    _viewing = true;
                    _index = newest;
                }
            }
            Build();
            _force = false;
            _dirVersion = d != null ? d.Version : 0;
            string move = UiText.T("reallife_ui_prompt_move");
            string select = UiText.T("reallife_ui_prompt_select");
            string back = UiText.T("reallife_ui_prompt_back");
            LifePages.Gallery(s, frame, dark, keys, UiText.T("reallife_gallery_app"), Nav(), (int)_query.Kit, _viewing, _viewing ? null : _album, _viewing ? _photo : null, move, select, back,
                _editing ? _editModel : null);
            AskThumbs(d);
            _thumbVersion = d != null ? d.Thumbs.Version : 0;
        }

        // The photos in view (a row either side), or the open photo's neighbours: only those small copies are asked for.
        private void AskThumbs(PhotosDirector d)
        {
            if (d == null || d.Folder == null || _editing)
            {
                return;
            }
            if (_viewing && _index >= 0)
            {
                d.Thumbs.Want(d, d.Folder, _shown, _window, _window + _photo.Neighbours.Length - 1);
            }
            else if (_album.ViewFirst >= 0)
            {
                d.Thumbs.Want(d, d.Folder, _shown, _album.ViewFirst, _album.ViewLast);
            }
        }

        public bool Press(int command, int arg)
        {
            if (_editing && EditPress(command, arg))
            {
                return true;
            }
            if (command == UiAppWindow.NavCommand && arg >= 0 && arg < (int)GalleryKit.Count)
            {
                _query.Kit = GalleryFind.KitAt(arg);
                _query.Place = _query.Business = null;
                _query.Day = 0;
                _viewing = false;
                _select = false;
                _picked.Clear();
                _album.Offset = 0f;
                if (_query.Kit == GalleryKit.Flare)
                {
                    _needPosted = true;
                }
                Changed();
                return true;
            }
            switch (command)
            {
                case UiAlbum.Sort:
                    _query.Sort = GalleryFind.NextSort(_query.Sort);
                    _album.Offset = 0f;
                    Changed();
                    return true;
                case UiAlbum.Select:
                    _select = !_select;
                    _picked.Clear();
                    Changed();
                    return true;
                case UiAlbum.Fav:
                    return FavouritePicked();
                case UiAlbum.Delete:
                    ConfirmDelete();
                    return _picked.Count > 0;
                case UiAlbum.Share:
                    return SharePicked();
                case UiAlbum.Camera:
                    Director?.OpenCamera(false, false);
                    return true;
                case UiAlbum.FilterPlace:
                    _query.Place = GalleryFind.Next(_places, _query.Place);
                    Narrowed();
                    return true;
                case UiAlbum.FilterBusiness:
                    _query.Business = GalleryFind.Next(_businesses, _query.Business);
                    Narrowed();
                    return true;
                case UiAlbum.FilterDay:
                    {
                        string next = GalleryFind.Next(_days, _query.Day > 0 ? _query.Day.ToString(CultureInfo.InvariantCulture) : null);
                        _query.Day = next != null && int.TryParse(next, NumberStyles.None, CultureInfo.InvariantCulture, out int day) ? day : 0;
                        Narrowed();
                        return true;
                    }
                case UiAlbum.FilterClear:
                    _query.Narrow();
                    Narrowed();
                    return true;
                case UiAlbum.Photo:
                    if (arg >= 0 && arg < _shown.Count)
                    {
                        if (_select)
                        {
                            TogglePick(_shown[arg].Name);
                            Changed();
                        }
                        else
                        {
                            _viewing = true;
                            _index = arg;
                            _force = true;
                        }
                        return true;
                    }
                    return false;
                case UiPhotoViewer.Step:
                    return Step(arg < 0 ? -1 : 1);
                case UiPhotoViewer.Neighbour:
                    if (_window + arg >= 0 && _window + arg < _shown.Count)
                    {
                        _index = _window + arg;
                        _asked = -1;
                        _force = true;
                        return true;
                    }
                    return false;
                case UiPhotoViewer.Action:
                    return ActOn(arg);
                case UiPhotoViewer.Fact:
                    return Link(arg);
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_editing)
            {
                LeaveEdit();
                return true;
            }
            if (_viewing)
            {
                _viewing = false;
                GalleryPictures.DropWhole();
                _asked = -1;
                _force = true;
                return true;
            }
            if (_select)
            {
                _select = false;
                _picked.Clear();
                Changed();
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            if (_viewing && !_editing)
            {
                Step(step < 0 ? -1 : 1);
                return;
            }
            if (_viewing || _editing)
            {
                return;
            }
            int next = (int)_query.Kit + (step < 0 ? -1 : 1);
            if (next >= 0 && next < (int)GalleryKit.Count)
            {
                Press(UiAppWindow.NavCommand, next);
            }
        }

        public bool Act(UiAction action)
        {
            if (_editing && action == UiAction.Alt)
            {
                return EditPress(UiPhotoEdit.Save, 0);
            }
            if (_viewing && !_editing && _index >= 0 && _index < _shown.Count)
            {
                if (action == UiAction.Alt)
                {
                    return ActOn(0);
                }
                if (action == UiAction.Option)
                {
                    return ActOn(2);
                }
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            _album.Reveal = index;
            _force = true;
        }

        public void Wheel(int clip, float notches)
        {
            _album.Offset = Math.Max(0f, _album.Offset - notches * 48f);
            _force = true;
        }

        // Left and Right step the photos while one is open, whatever has the focus.
        public bool Steps(int command) => _viewing && !_editing;

        public void Step(int command, int arg, int step)
        {
            Step(step < 0 ? -1 : 1);
        }

        // The search box: its words are the album's search; the keys go to it while it is chosen.
        public int TypingCommand => UiAlbum.Search;

        public string TypingText => _query.Search ?? string.Empty;

        public void Typed(string text)
        {
            _query.Search = string.IsNullOrEmpty(text) ? null : text;
            _album.Offset = 0f;
            Changed();
        }

        // The album is selected again on the next layout.
        private void Changed()
        {
            _rebuild = true;
            _force = true;
        }

        private void Narrowed()
        {
            _album.Offset = 0f;
            Changed();
        }

        private void Build()
        {
            PhotosDirector director = Director;
            if (director == null)
            {
                _shown.Clear();
                FillAlbum(null);
                return;
            }
            _texts.TwelveHour = PhotosGame.TwelveHour;
            if (_rebuild || director.Version != _dirVersion)
            {
                _rebuild = false;
                GalleryFind.Select(director.Photos, _query, director.Favourites, director.Posted, _shown);
                GalleryFind.Counts(director.Photos, director.Favourites, director.Posted, _counts);
                GalleryFind.Values(director.Photos, _query.Kit, director.Favourites, director.Posted, 0, _places);
                GalleryFind.Values(director.Photos, _query.Kit, director.Favourites, director.Posted, 1, _businesses);
                GalleryFind.Values(director.Photos, _query.Kit, director.Favourites, director.Posted, 2, _days);
                for (int i = _picked.Count - 1; i >= 0; i--)
                {
                    if (GalleryRules.IndexOf(director.Photos, _picked[i]) < 0)
                    {
                        _picked.RemoveAt(i);   // deleted
                    }
                }
            }
            if (_index >= _shown.Count)
            {
                _index = _shown.Count - 1;
            }
            if (_viewing && _index < 0)
            {
                _viewing = false;
            }
            if (_editing)
            {
                FillEdit();
            }
            else if (_viewing)
            {
                FillPhoto(director);
            }
            else
            {
                FillAlbum(director);
            }
        }

        private void FillAlbum(PhotosDirector director)
        {
            _album.Selecting = _select;
            GalleryAlbumFill.Fill(_album, _shown, _query, director?.Favourites, director?.Posted, _picked, PhotosGame.Day, _places.Count, _businesses.Count, _days.Count, PhotoThumbCache.Prefix,
                _texts, true);
            _album.Selecting = _select;
            if (director != null)
            {
                string cap = PhotosText.N(PhotoStoreRules.Cap);
                _album.StorageFull = PhotoStoreRules.Full(director.Count);
                _album.StorageLabel = _album.StorageFull
                    ? _texts.T("reallife_gallery_full_line").Replace("{cap}", cap)
                    : _texts.T("reallife_gallery_storage").Replace("{count}", PhotosText.N(director.Count)).Replace("{cap}", cap);
            }
        }

        private void FillPhoto(PhotosDirector director)
        {
            PhotoEntry photo = _shown[_index];
            EnsureWhole(director, photo);
            string title = string.IsNullOrEmpty(photo.Side.Place) ? UiText.T("reallife_ui_life_no_place") : photo.Side.Place;
            if (photo.HasSide && PhotoSidecarRules.Night(photo.Side.Hour) && !string.IsNullOrEmpty(photo.Side.Place))
            {
                title = UiText.Fill("reallife_ui_life_at_night", "place", photo.Side.Place);
            }
            if (!string.IsNullOrEmpty(photo.Side.Business))
            {
                title = photo.Side.Business;
            }
            _photo.Picture = PhotoThumbCache.Prefix + photo.Name;
            _photo.Title = title;
            _photo.Caption = PhotoTimeRules.Clock(photo.Hour, photo.Minute, _texts.TwelveHour, UiText.T("reallife_gallery_hour_only"));
            int onDay = 0, at = 0;
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i].Day == photo.Day)
                {
                    onDay++;
                    if (i <= _index)
                    {
                        at++;
                    }
                }
            }
            _photo.Position = UiText.Fill("reallife_ui_life_of_day", "at", at.ToString(CultureInfo.InvariantCulture), "count", onDay.ToString(CultureInfo.InvariantCulture),
                "day", GalleryAlbumFill.DayWords(photo.Day, PhotosGame.Day, _texts));
            _photo.FactsTitle = UiText.T("reallife_ui_life_facts");
            _photo.TagsTitle = UiText.T("reallife_ui_life_tags");
            _photo.WhereTitle = UiText.T("reallife_ui_life_where");
            string story = null, posted = null;
            Flare.FlareDirector flare = Flare.FlareDirector.Instance;
            if (flare != null && flare.PictureStory(System.IO.Path.Combine(director.Folder, photo.Name), out string business, out int likes, out int came, out long cents))
            {
                string money = (cents >= 0 ? "+" : string.Empty) + (cents / 100).ToString(CultureInfo.InvariantCulture);
                story = UiText.Fill("reallife_ui_life_flare_line", "name", string.IsNullOrEmpty(business) ? UiText.T("reallife_ui_life_a_business") : business,
                    "likes", likes.ToString(CultureInfo.InvariantCulture), "visits", came.ToString(CultureInfo.InvariantCulture), "money", money);
                posted = UiText.Fill("reallife_gallery_flare_posted", "likes", likes.ToString(CultureInfo.InvariantCulture));
            }
            GalleryAlbumFill.Facts(_photo, photo, director.IsFavourite(photo.Name), photo.Name == director.WallpaperName, story, posted, _texts);
            _links.Clear();
            for (int i = 0; i < _photo.Facts.Length; i++)
            {
                _links.Add(_photo.Facts[i].Link ? (_photo.Facts[i].Value == photo.Side.Place ? "p:" + photo.Side.Place : "b:" + photo.Side.Business) : null);
            }
            _photo.CanBack = _index > 0;
            _photo.CanOn = _index + 1 < _shown.Count;
            int neighbours = Math.Min(_shown.Count, 9);
            int first = Math.Max(0, Math.Min(_shown.Count - neighbours, _index - 4));
            _window = first;
            if (_photo.Neighbours == null || _photo.Neighbours.Length != neighbours)
            {
                _photo.Neighbours = new string[neighbours];
            }
            for (int i = 0; i < neighbours; i++)
            {
                _photo.Neighbours[i] = PhotoThumbCache.Prefix + _shown[first + i].Name;
            }
            _photo.NeighbourAt = _index - first;
            bool fav = director.IsFavourite(photo.Name);
            bool wall = PhotosDirector.CanSetWallpaper;
            _photo.Actions = new[]
            {
                new UiActionModel { Label = UiText.T("reallife_gallery_post_flare"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Icon = UiIcon.Send },
                new UiActionModel { Label = UiText.T("reallife_gallery_edit"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Pencil, Key = UiAction.Count },
                new UiActionModel { Label = UiText.T(photo.Name == director.WallpaperName ? "reallife_gallery_wallpaper_off" : "reallife_ui_life_wallpaper"), Kind = UiButtonKind.Secondary,
                    Key = UiAction.Option, Icon = UiIcon.Monitor, Disabled = !wall },
                new UiActionModel { Icon = UiIcon.Heart, Kind = fav ? UiButtonKind.Primary : UiButtonKind.Secondary, Key = UiAction.Count },
                new UiActionModel { Icon = UiIcon.Trash, Kind = UiButtonKind.Destructive, Key = UiAction.Count },
            };
        }

        private int _asked = -1;

        private void EnsureWhole(PhotosDirector director, PhotoEntry photo)
        {
            if (director == null || _asked == _index)
            {
                return;
            }
            _asked = _index;
            GalleryPictures.DropWhole();
            string path = System.IO.Path.Combine(director.Folder, photo.Name);
            int index = _index;
            string name = photo.Name;
            director.StartCoroutine(Guard.Routine(PhotosGame.Load(path, made =>
            {
                if (made == null || !_viewing || _index != index || _editing)
                {
                    if (made != null)
                    {
                        UnityEngine.Object.Destroy(made);
                    }
                    return;
                }
                GalleryPictures.SetWhole(name, made);
                _force = true;
            }, () => _viewing && _index == index && !_editing)));
        }

        private bool Step(int delta)
        {
            int to = _index + delta;
            if (!_viewing || to < 0 || to >= _shown.Count)
            {
                return false;
            }
            _index = to;
            _asked = -1;
            _force = true;
            return true;
        }

        // A place or a business of the facts: the album of that place or that business.
        private bool Link(int fact)
        {
            if (fact < 0 || fact >= _links.Count || _links[fact] == null)
            {
                return false;
            }
            string link = _links[fact];
            _query.Narrow();
            if (link.StartsWith("p:", StringComparison.Ordinal))
            {
                _query.Place = link.Substring(2);
            }
            else
            {
                _query.Business = link.Substring(2);
            }
            _query.Kit = GalleryKit.All;
            _viewing = false;
            GalleryPictures.DropWhole();
            _asked = -1;
            _album.Offset = 0f;
            Changed();
            return true;
        }

        private bool ActOn(int arg)
        {
            if (_index < 0 || _index >= _shown.Count)
            {
                return false;
            }
            PhotoEntry photo = _shown[_index];
            PhotosDirector director = Director;
            if (director == null)
            {
                return false;
            }
            string path = System.IO.Path.Combine(director.Folder, photo.Name);
            switch (arg)
            {
                case 0:
                    FlareScreen.WantPhoto(path);
                    if (PhotosBridges.ShareToFlare == null || !PhotosBridges.ShareToFlare(path, PhotoThumbs.FileOf(path)))
                    {
                        PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_note_flare_off");
                    }
                    else
                    {
                        director.RefreshPosted();
                    }
                    return true;
                case 1:
                    BeginEdit(director, photo);
                    return true;
                case 2:
                    if (!PhotosDirector.CanSetWallpaper)
                    {
                        PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_note_wallpaper_off");
                        return false;
                    }
                    director.SetWallpaper(photo.Name == director.WallpaperName ? null : photo.Name);
                    _force = true;
                    return true;
                case 3:
                    director.ToggleFavourite(photo.Name);
                    _force = true;
                    return true;
                case 4:
                    {
                        string name = photo.Name;
                        GameConfirm.Show("reallife_gallery_delete_title", "reallife_gallery_delete_body", () =>
                        {
                            director.StartCoroutine(Guard.Routine(director.Delete(new List<string> { name }, 0)));
                            _viewing = false;
                            GalleryPictures.DropWhole();
                            _asked = -1;
                            _force = true;
                        }, null, "reallife_gallery_delete_yes", "common_cancel", true, true);
                        return true;
                    }
            }
            return false;
        }

        private bool FavouritePicked()
        {
            PhotosDirector director = Director;
            if (director == null || _picked.Count == 0)
            {
                return false;
            }
            director.SetFavourites(_picked, !_album.FavAll);
            Changed();
            return true;
        }

        // Every picked photo is shared, the first picked last so that Flare's sheet opens with it; one post carries one photo.
        private bool SharePicked()
        {
            PhotosDirector director = Director;
            if (director == null || _picked.Count == 0)
            {
                return false;
            }
            bool any = false;
            for (int i = _picked.Count - 1; i >= 0; i--)
            {
                string path = System.IO.Path.Combine(director.Folder, _picked[i]);
                if (i == 0)
                {
                    FlareScreen.WantPhoto(path);
                }
                any |= PhotosBridges.ShareToFlare != null && PhotosBridges.ShareToFlare(path, PhotoThumbs.FileOf(path));
            }
            if (!any)
            {
                PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_note_flare_off");
                return false;
            }
            _select = false;
            _picked.Clear();
            director.RefreshPosted();
            Changed();
            return true;
        }

        private void ConfirmDelete()
        {
            PhotosDirector director = Director;
            if (director == null || _picked.Count == 0)
            {
                return;
            }
            var names = new List<string>(_picked);
            GameConfirm.Show("reallife_gallery_delete_title", "reallife_gallery_delete_body", () =>
            {
                director.StartCoroutine(Guard.Routine(director.Delete(names, 0)));
                _picked.Clear();
                _select = false;
                Changed();
            }, null, "reallife_gallery_delete_yes", "common_cancel", true, true);
        }

        private void TogglePick(string name)
        {
            int at = _picked.IndexOf(name);
            if (at >= 0)
            {
                _picked.RemoveAt(at);
            }
            else
            {
                _picked.Add(name);
            }
        }

        private UiNavEntry[] Nav()
        {
            var nav = new UiNavEntry[(int)GalleryKit.Count];
            UiIcon[] icons = { UiIcon.Image, UiIcon.Heart, UiIcon.SmileyHappy, UiIcon.Store, UiIcon.MapPin, UiIcon.Megaphone };
            for (int i = 0; i < nav.Length; i++)
            {
                nav[i] = new UiNavEntry { Icon = icons[i], Label = UiText.T(GalleryFind.KitKeys[i]), Count = _counts[i].ToString(CultureInfo.InvariantCulture) };
            }
            return nav;
        }

        private static string _sizeOf, _sizeText;

        // The size of a photo's file, read once a photo opened (not on each layout of it).
        private static string SizeOf(PhotoEntry photo)
        {
            if (photo.Name == _sizeOf)
            {
                return _sizeText;
            }
            _sizeOf = photo.Name;
            _sizeText = SizeRead(photo);
            return _sizeText;
        }

        private static string SizeRead(PhotoEntry photo)
        {
            PhotosDirector director = PhotosDirector.Instance;
            if (director == null || string.IsNullOrEmpty(photo.Name))
            {
                return string.Empty;
            }
            string path = System.IO.Path.Combine(director.Folder, photo.Name);
            long bytes = 0;
            try
            {
                if (System.IO.File.Exists(path))
                {
                    bytes = new System.IO.FileInfo(path).Length;
                }
            }
            catch (System.IO.IOException)
            {
                bytes = 0;
            }
            return bytes <= 0 ? string.Empty : (bytes / 1024).ToString(CultureInfo.InvariantCulture) + " KB";
        }
    }
}
