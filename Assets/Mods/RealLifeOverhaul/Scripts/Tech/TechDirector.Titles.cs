using System;
using System.Collections.Generic;
using System.Text;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The names of the player's products and games (<see cref="TitleRules"/>): typed on a page with the game's own text box,
    /// or made up to fit the kind or the genre where the box is left empty, and changed at any time until the product is
    /// withdrawn, at no cost, since nothing reads a name but the pages, notices and news that show it. Only the firms' own file
    /// is ever named: another player's products in a Going Public session are never touched, and nothing here is sent.
    /// Runs on a button or a load, never per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        private const int RivalTitlesMost = 4;   // names on a rival's page: its newest, the rest are in the markets' tables

        private readonly HashSet<string> _titleSet = new HashSet<string>(StringComparer.Ordinal);
        private int _titleTurn;   // suggestions asked for on the concept's page this session: each press offers the next

        // Every product and game name in use in the city, as names are compared: the player's own products (but one), the names typed
        // for the other studios' concepts, the rivals' products and the rival studios' games. Made afresh for each question.
        private HashSet<string> TitleSet(int exceptProduct, string exceptDraft)
        {
            _titleSet.Clear();
            for (int i = 0; _firmsFile != null && i < _firmsFile.Products.Count; i++)
            {
                if (_firmsFile.Products[i].Id != exceptProduct)
                {
                    _titleSet.Add(TitleRules.Key(_firmsFile.Products[i].Name));
                }
            }
            for (int i = 0; _studioOn && _studio != null && i < _studio.Drafts.Count; i++)
            {
                if (_studio.Drafts[i].Place != exceptDraft)
                {
                    _titleSet.Add(TitleRules.Key(_studio.Drafts[i].Name));
                }
            }
            for (int i = 0; _studioOn && _studio != null && i < _studio.Rivals.Count; i++)
            {
                _titleSet.Add(TitleRules.Key(_studio.Rivals[i].Name));
            }
            for (int i = 0; _sectorOn && _sector != null && i < _sector.Rivals.Count; i++)
            {
                _titleSet.Add(TitleRules.Key(_sector.Rivals[i].Title));
            }
            _titleSet.Remove(string.Empty);
            return _titleSet;
        }

        /// <summary>The style a product of the player's is named in: a studio's game by its genre, any other by its kind and its buyers.</summary>
        internal int TitleStyleOf(Product p)
        {
            GameDev g = _studioOn && _studio != null && p != null && p.K == ProductKind.Game ? _studio.GameOf(p.Id) : null;
            return g != null ? TitleRules.StyleOfGenre(g.Genre) : FirmsRules.StyleOf(_firmsFile, p);
        }

        /// <summary>
        /// The name offered for a product about to be started, from the save's seed and the id it will get: the page shows it and
        /// <see cref="NewProduct"/> gives it when the box was left empty. Each <paramref name="turn"/> offers the next free one.
        /// </summary>
        internal string SuggestTitle(ProductKind kind, int segment, int turn)
        {
            if (!_firmsOn || _firmsFile == null)
            {
                return string.Empty;
            }
            int genre = kind == ProductKind.Game ? StudioRules.GenreOfName(null, _firmsFile.NextId, _firmsFile.Seed) : 0;
            HashSet<string> taken = TitleSet(0, null);
            return TitleRules.Suggest(_firmsFile.Seed, _firmsFile.NextId, TitleRules.StyleOf(kind, segment, genre), turn, n => taken.Contains(TitleRules.Key(n)));
        }

        /// <summary>
        /// A typed name for a product not yet started, cleaned into <paramref name="name"/>: <see cref="TitleRules.Ok"/>, or why not
        /// (said in a notice). An empty box is no refusal: the name is then the suggestion.
        /// </summary>
        internal int TypedTitle(string typed, out string name)
        {
            name = TitleRules.Clean(typed);
            if (name.Length == 0)
            {
                return TitleRules.Ok;
            }
            HashSet<string> taken = TitleSet(0, null);
            if (taken.Contains(TitleRules.Key(name)))
            {
                TellTitle(TitleRules.Taken, name);
                name = string.Empty;
                return TitleRules.Taken;
            }
            return TitleRules.Ok;
        }

        /// <summary>
        /// A product or game of the player's renamed: allowed while it is being built or on sale, refused (with a notice saying
        /// why) for an empty name, one another product or a rival's bears, or a withdrawn product. What was already printed
        /// keeps the old name, as a paper would.
        /// </summary>
        internal int RenameProduct(int id, string typed)
        {
            Product p = _firmsOn && _firmsFile != null ? _firmsFile.ProductOf(id) : null;
            if (p == null || p.Stage >= 2)
            {
                TellTitle(TitleRules.Closed, p != null ? p.Name : string.Empty);
                return TitleRules.Closed;
            }
            string name = TitleRules.Clean(typed);
            HashSet<string> taken = TitleSet(id, null);
            int why = TitleRules.Check(name, n => taken.Contains(TitleRules.Key(n)));
            if (why != TitleRules.Ok)
            {
                TellTitle(why, name);
                return why;
            }
            if (!string.Equals(name, p.Name, StringComparison.Ordinal))
            {
                string old = p.Name;
                p.Name = name;
                ChangedTech();
                if (Verbose)
                {
                    Logger?.Info("Tech check (names): product " + id + " renamed from '" + old + "' to '" + name + "', " + name.Length + " of "
                                 + TitleCatalogue.Most + " characters, no other product or rival bears it: PASS");
                }
            }
            return TitleRules.Ok;
        }

        /// <summary>
        /// A studio's concept named: the typed name kept in its draft, or forgotten for an empty box (the studio then names the
        /// game itself: after the game it follows, else a suggestion of its genre). Refused with a notice for a name in use.
        /// </summary>
        internal int NameDraft(string place, string typed)
        {
            GameDraft d = StudioDraft(place);
            if (d == null)
            {
                return TitleRules.Closed;
            }
            string name = TitleRules.Clean(typed);
            if (name.Length > 0 && TitleSet(0, place).Contains(TitleRules.Key(name)))
            {
                TellTitle(TitleRules.Taken, name);
                return TitleRules.Taken;
            }
            d.Name = name;
            ChangedTech();
            return TitleRules.Ok;
        }

        // Another suggestion for a studio's concept, of its genre's words and not the one its page shows now; kept in the draft as a typed name is.
        private void SuggestDraft(string place)
        {
            GameDraft d = StudioDraft(place);
            if (d == null || _firmsFile == null)
            {
                return;
            }
            string shown = TitleRules.Key(DraftTitle(d, out _));
            HashSet<string> taken = TitleSet(0, place);
            taken.Add(shown);
            d.Name = TitleRules.Suggest(_firmsFile.Seed, _firmsFile.NextId, TitleRules.StyleOfGenre(d.Genre), ++_titleTurn, n => taken.Contains(TitleRules.Key(n)));
        }

        /// <summary>
        /// What a concept's game will be called, and how the name came (StudioViews.Title*): the name typed for it while nothing
        /// else bears it; else, for a sequel, the name of the game it follows with the next number; else a suggestion of its
        /// genre's words. The concept's page shows it and the pitch gives it.
        /// </summary>
        internal string DraftTitle(GameDraft d, out int from)
        {
            from = StudioViews.TitleSuggested;
            if (d == null || _firmsFile == null)
            {
                return string.Empty;
            }
            HashSet<string> taken = TitleSet(0, d.Place);
            string typed = TitleRules.Clean(d.Name);
            if (typed.Length > 0 && !taken.Contains(TitleRules.Key(typed)))
            {
                from = StudioViews.TitleTyped;
                return typed;
            }
            Product prior = d.Sequel > 0 ? _firmsFile.ProductOf(d.Sequel) : null;
            string sequel = prior != null ? TitleRules.Sequel(prior.Name, n => taken.Contains(TitleRules.Key(n))) : string.Empty;
            if (sequel.Length > 0)
            {
                from = StudioViews.TitleSequel;
                return sequel;
            }
            return TitleRules.Suggest(_firmsFile.Seed, _firmsFile.NextId, TitleRules.StyleOfGenre(d.Genre), 0, n => taken.Contains(TitleRules.Key(n)));
        }

        /// <summary>A product's name by its id, for the box a rename opens with; empty for none.</summary>
        internal string TitleOf(int id) => NameOf(id);

        /// <summary>The name typed for a studio's concept, for the box that asks for it; empty while the studio names it.</summary>
        internal string DraftTyped(string place)
        {
            GameDraft d = _studioOn && _studio != null && place != null ? _studio.DraftAt(place) : null;
            return d != null ? TitleRules.Clean(d.Name) : string.Empty;
        }

        // A save from before names could be typed: a product with no name, or the second bearer of one, is named once, from the save's
        // seed and its id, and keeps the name from then on (FirmsRules.NameAll). The player's own file only.
        private void NameOldProducts()
        {
            int named = FirmsRules.NameAll(_firmsFile, null, null);
            if (named > 0)
            {
                Logger?.Info("Tech: " + named + " product(s) of an older save had no name of their own and were given one.");
            }
        }

        /// <summary>
        /// What a rival company or startup sells, by name, for its page: its products in the sector's markets and, for a game
        /// studio, its newest games; empty with none known.
        /// </summary>
        internal string RivalTitles(Competitor c)
        {
            if (c == null)
            {
                return string.Empty;
            }
            var b = new StringBuilder(96);
            int shown = 0;
            for (int i = 0; _sectorOn && _sector != null && i < _sector.Rivals.Count && shown < RivalTitlesMost; i++)
            {
                RivalProduct r = _sector.Rivals[i];
                bool theirs = c.Kind == (int)RivalKind.Company ? !r.Startups && r.Company == c.Company : c.Kind == (int)RivalKind.Startup && r.Startup > 0 && r.Startup == c.Startup;
                if (theirs)
                {
                    b.Append(shown++ > 0 ? ", " : string.Empty).Append(r.Title);
                }
            }
            int studio = c.Kind == (int)RivalKind.Company ? Array.IndexOf(StudioCatalogue.RivalNames, c.Name) : -1;
            for (int i = _studioOn && _studio != null && studio >= 0 ? _studio.Rivals.Count - 1 : -1; i >= 0 && shown < RivalTitlesMost; i--)
            {
                if (_studio.Rivals[i].Studio == studio)
                {
                    b.Append(shown++ > 0 ? ", " : string.Empty).Append(_studio.Rivals[i].Name);
                }
            }
            return b.ToString();
        }

        // Why a name was not kept, in a notice: the page stays as it was.
        private void TellTitle(int why, string name)
        {
            string key = why == TitleRules.Taken ? "reallife_tech_name_taken" : why == TitleRules.Closed ? "reallife_tech_name_closed" : "reallife_tech_name_empty";
            Notifications.Show(NotificationType.Warning, key, Data("name", name ?? string.Empty), 8f, null, null, true, false);
        }
    }
}
