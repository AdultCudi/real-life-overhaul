using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Photos;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face, its photo picker: the compose sheet's "Gallery" chip opens the whole Gallery as a grid of the player's photos
    /// under a heading a day, with the albums as chips and a search, and a photo picked goes back into the compose sheet (a photo of one
    /// of the player's businesses tags that business). The photos are the Gallery's list in memory (the same selection rules, no cap
    /// of five), their small copies the director's cache of the photos in view only. Nothing changes but what the player picks.
    /// Cost: a selection when the album or the search changes, a layout of the rows in view.
    /// </summary>
    public sealed partial class FlareScreen
    {
        private const int GalleryPhoto = -2;   // _photo's value while the photo is one picked from the Gallery

        private readonly GalleryQuery _pickQuery = new GalleryQuery();
        private readonly List<PhotoEntry> _pickShown = new List<PhotoEntry>(PhotoStoreRules.Cap + 16);
        private readonly GalleryTexts _pickTexts = new GalleryTexts();
        private readonly int[] _pickCounts = new int[(int)GalleryKit.Count];
        private string _chosenPhoto;               // "f:" and the path of the Gallery photo picked, or null
        private int _pickThumbs = -1, _pickVersion = -1;
        private bool _pickBuilt;

        // The picker's small copies changed since it was last laid out.
        private bool PickerCame
        {
            get
            {
                PhotosDirector d = PhotosDirector.Instance;
                return _sheet == FlareFace.PhotosSheet && d != null && (d.Thumbs.Version != _pickThumbs || d.Version != _pickVersion);
            }
        }

        private bool OpenPicker()
        {
            PhotosDirector d = PhotosDirector.Instance;
            if (d == null || d.Folder == null)
            {
                return false;
            }
            _pickQuery.Clear();
            _pickBuilt = false;
            _pickTexts.T = UiText.T;
            _pickTexts.Real = photo => "-";
            d.RefreshPosted();
            _m.Picker.Photos.Offset = 0f;
            _sheet = FlareFace.PhotosSheet;
            _field = -1;
            _dirty = true;
            return true;
        }

        // The picker's words and photos, written into its model.
        private void FillPicker()
        {
            PhotosDirector d = PhotosDirector.Instance;
            FlarePickerModel k = _m.Picker;
            k.Title = FlareText.T("reallife_flare_kit_picker_title");
            k.SearchHint = FlareText.T("reallife_flare_kit_picker_search");
            k.Search = _pickQuery.Search;
            k.Cancel = FlareText.T("reallife_flare_kit_cancel");
            if (d == null)
            {
                _sheet = FlareFace.ComposeSheet;
                _m.Sheet = FlareFace.ComposeSheet;
                return;
            }
            if (!_pickBuilt || d.Version != _pickVersion)
            {
                _pickBuilt = true;
                GalleryFind.Select(d.Photos, _pickQuery, d.Favourites, d.Posted, _pickShown);
                GalleryFind.Counts(d.Photos, d.Favourites, d.Posted, _pickCounts);
            }
            _pickVersion = d.Version;
            if (k.Albums.Length != (int)GalleryKit.Count)
            {
                k.Albums = new string[(int)GalleryKit.Count];
            }
            for (int i = 0; i < k.Albums.Length; i++)
            {
                k.Albums[i] = UiText.T(GalleryFind.KitKeys[i]) + " " + _pickCounts[i].ToString(CultureInfo.InvariantCulture);
            }
            k.Album = (int)_pickQuery.Kit;
            GalleryAlbumFill.Fill(k.Photos, _pickShown, _pickQuery, d.Favourites, d.Posted, null, PhotosGame.Day, 0, 0, 0, PhotoThumbCache.Prefix, _pickTexts, false);
        }

        // After the layout: the small copies of the photos in view are asked for, and only those.
        private void PickerLaid()
        {
            PhotosDirector d = PhotosDirector.Instance;
            if (d == null || d.Folder == null)
            {
                return;
            }
            UiAlbumModel album = _m.Picker.Photos;
            if (album.ViewFirst >= 0)
            {
                d.Thumbs.Want(d, d.Folder, _pickShown, album.ViewFirst, album.ViewLast);
            }
            _pickThumbs = d.Thumbs.Version;
        }

        private bool PickAlbum(int album)
        {
            if (album < 0 || album >= (int)GalleryKit.Count)
            {
                return false;
            }
            _pickQuery.Kit = GalleryFind.KitAt(album);
            _pickBuilt = false;
            _m.Picker.Photos.Offset = 0f;
            return true;
        }

        // A photo of the grid picked: it goes into the compose sheet, and a business seen in it is tagged.
        private bool PickPhoto(int index)
        {
            PhotosDirector d = PhotosDirector.Instance;
            if (d == null || d.Folder == null || index < 0 || index >= _pickShown.Count)
            {
                return false;
            }
            PhotoEntry photo = _pickShown[index];
            string path = System.IO.Path.Combine(d.Folder, photo.Name);
            _chosenPhoto = "f:" + path;
            _photo = GalleryPhoto;
            TagBusiness(photo.HasSide ? photo.Side.BusinessPlace : null);
            _sheet = FlareFace.ComposeSheet;
            _sheetWhy = null;
            return true;
        }

        // A photo of one of the player's own businesses is that business's post.
        private void TagBusiness(string place)
        {
            if (string.IsNullOrEmpty(place) || _place >= 0)
            {
                return;
            }
            List<FlareShop> shops = _flare.MyShops();
            for (int i = 0; i < shops.Count; i++)
            {
                if (shops[i].Place == place)
                {
                    _place = i;
                    return;
                }
            }
        }

        private static string _wanted;

        /// <summary>The Gallery's "Post to Flare": the next time Flare shows, its compose sheet opens with this photo attached.</summary>
        public static void WantPhoto(string path)
        {
            _wanted = path;
        }

        // Flare is shown with a photo wanted: the compose sheet opens with it, tagged with its business.
        private void TakeWantedPhoto()
        {
            if (_wanted == null || _flare == null || !_flare.Running)
            {
                return;
            }
            string path = _wanted;
            _wanted = null;
            _history.Clear();
            Show(FlareFace.Home);
            if (!OpenCompose(0))
            {
                return;
            }
            _chosenPhoto = "f:" + path;
            _photo = GalleryPhoto;
            PhotoSidecarFile.Read(path, out PhotoSidecar side);
            TagBusiness(side.BusinessPlace);
        }
    }
}
