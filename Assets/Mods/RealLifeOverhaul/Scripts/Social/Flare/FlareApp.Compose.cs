using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The Flare app's compose sheet: the built part and its chips, tone, mention, tags, typed words and picture, with the preview.</summary>
    public sealed partial class FlareApp
    {
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
            FlareKit.Set(_why, why ?? Note(file, c) ?? string.Empty);
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
            _compose.Arg = 0;
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
            var b = new StringBuilder(tags.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Length > 0 && all[i] != tag)
                {
                    b.Append(b.Length > 0 ? " " : string.Empty).Append(all[i]);
                }
            }
            return b.Length > 0 ? b.ToString() : null;
        }

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

        private string Why(FlareFile file, FlareCompose c)
        {
            switch (c.Kind)
            {
                case FlareSource.Reply:
                    return c.Text == null ? FlareText.T("reallife_flare_compose_need_words") : null;
                case FlareSource.Report:
                    if (FlareBridges.Ask(FlareBridges.PlayerDistrict, null) == null)
                    {
                        return FlareText.T("reallife_flare_compose_need_district");
                    }
                    return c.Arg == (int)FlareReport.Business && c.Place == null ? FlareText.T("reallife_flare_compose_stand_in") : null;
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
            return null;
        }

        // What a post that can go will do less of, said quietly beside the Post button; null when it does all.
        private string Note(FlareFile file, FlareCompose c)
        {
            int day = _director.CurrentDay;
            switch (c.Kind)
            {
                case FlareSource.Reply:
                    return null;
                case FlareSource.Report:
                    string district = c.District ?? FlareBridges.Ask(FlareBridges.PlayerDistrict, null);
                    return FlareLiveRules.MovedToday(file.Net, day, FlareLiveRules.MoveKey("me", FlareSource.Report, c.Arg, district, c.Place))
                        ? FlareText.T("reallife_flare_compose_report_again") : null;
                case FlareSource.Review:
                    if (FlareLiveRules.MovedToday(file.Net, day, FlareLiveRules.MoveKey("me", FlareSource.Review, c.Arg, null, c.Place)))
                    {
                        return FlareText.T("reallife_flare_compose_review_again");
                    }
                    break;
            }
            return FlareRules.Counts(file, day) ? null : FlareText.Fill("reallife_flare_compose_counted", "count", FlareRules.PostsCounted.ToString());
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
    }
}
