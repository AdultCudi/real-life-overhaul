using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Ui;
using UnityEngine;

namespace RealLifeOverhaul.Districts
{
    /// <summary>
    /// The district view, part: the page of the city map's dock on the RLO interface (Ui/Map). The view keeps tinting the game's zones and reading the figures once a day; this part
    /// fills the dock's page from them (the ten layers, the city's average, a donut where one helps, the key on the kit's ramp, the district pointed at or kept, every district ranked
    /// with how it moved this month and how much of it stands empty) and answers its presses. The month's change is kept in the save (<see cref="DistrictMonths"/>), each layer's
    /// figures for each district as the first read of the month found them. With the page off (its switch, or the kit not holding the map's dock) the old window stays as it was.
    /// Cost: a page filled when it changes; the month's figures kept once a month; nothing a frame.
    /// </summary>
    public sealed partial class DistrictView
    {
        private static readonly UiIcon[] LayerIcons =
        {
            UiIcon.Wallet, UiIcon.Coin, UiIcon.Home, UiIcon.People, UiIcon.ShieldCheck, UiIcon.Heart, UiIcon.Lightning, UiIcon.Globe, UiIcon.Store, UiIcon.Flag
        };

        /// <summary>Set by the map's screen on the kit: true while its dock draws the layer, so the old window stays away.</summary>
        internal static Func<bool> KitDraws;

        private DistrictMonths _months = new DistrictMonths();
        private bool _monthsKept = true;       // false while the line in the save is one this version cannot read: it is left as it was read
        private int _kitStamp;
        private bool _wasKit;
        private readonly List<string> _ids = new List<string>(16);
        private readonly List<long[]> _figures = new List<long[]>(16);
        private readonly List<bool[]> _has = new List<bool[]>(16);
        private readonly List<MapDistrict> _ranked = new List<MapDistrict>(16);
        private readonly List<HudFollower> _tags = new List<HudFollower>(12);
        private int _tagsStamp = -1;
        private bool _tagsBroken;
        private string _tagChosen;   // the district the page is about: its tag is the cyan one

        private bool OnKit => KitDraws != null && KitDraws();

        /// <summary>Changes whenever what the page would say changes: a district pointed at or kept, a layer, a day's figures.</summary>
        internal int KitStamp => unchecked(_kitStamp * 31 + Layer * 7 + _readDay + (_showing ? 1 : 0));

        /// <summary>True while a layer shows on the plain map and the view has districts to say.</summary>
        internal bool KitReady => !_broken && Layer >= 0 && _showing && _districts.Count > 0;

        // ------------------------------------------------------------------------------------------------- the month

        // The months' line in the save, read with the crime log's.
        private void KitLoad(GameInstance save)
        {
            string line = null;
            if (save.modData != null)
            {
                save.modData.TryGetValue(DistrictMonths.Key, out line);
            }
            _monthsKept = true;
            if (!DistrictMonths.Readable(line))
            {
                SaveLines.Unreadable(save.modData, DistrictMonths.Key, "its version is not one this version of the mod reads");   // (kept as it was read: Core/SaveLines)
                _monthsKept = false;
                _months = new DistrictMonths();
                return;
            }
            _months = DistrictMonths.Read(line);
        }

        private void KitSave(GameInstance save)
        {
            if (!_monthsKept)
            {
                return;
            }
            string line = _months.Write();
            if (line != null)
            {
                save.modData[DistrictMonths.Key] = line;
            }
            else
            {
                save.modData.Remove(DistrictMonths.Key);
            }
        }

        // The figures as the month finds them, kept the first time the month is read: a month's start is the first day the view is read in it, so the change it
        // shows is from then. (Nothing runs when the day turns for it: the view reads when it is shown.)
        private void KitSnapshot(int today)
        {
            if (!_monthsKept)
            {
                return;
            }
            _ids.Clear();
            _figures.Clear();
            _has.Clear();
            for (int i = 0; i < _districts.Count; i++)
            {
                _ids.Add(_districts[i].Id);
                _figures.Add(_districts[i].Figure);
                _has.Add(_districts[i].HasFigure);
            }
            if (_months.Take(today, GameCalendar.DaysPerYear, _ids, _figures, _has) && Meta.Dev.Verbose)
            {
                Logger?.Info("District view: this month's starting figures were kept for " + _ids.Count + " district(s), day " + today + " (" + _months.Count + " month(s) in the save).");
            }
        }

        // ------------------------------------------------------------------------------------------------- the page

        private static bool HasEmpty(int layer) => layer == (int)DistrictLayer.Wealth || layer == (int)DistrictLayer.Rent || layer == (int)DistrictLayer.Property
            || layer == (int)DistrictLayer.Footfall || layer == (int)DistrictLayer.Competition || layer == (int)DistrictLayer.Council;

        // A layer measured in money or an index changes by per cent; one of counts and scores, by how many.
        private static bool ByPercent(int layer) => layer == (int)DistrictLayer.Wealth || layer == (int)DistrictLayer.Rent || layer == (int)DistrictLayer.Property
            || layer == (int)DistrictLayer.Footfall || layer == (int)DistrictLayer.Council;

        // The city's figure for a layer is a sum where a district's share of it is a count (reports, businesses), else the districts' mean.
        private static bool IsTotal(int layer) => layer == (int)DistrictLayer.Crime || layer == (int)DistrictLayer.Competition;

        private static string Stem(string stem, int layer) => T(Mod + "_map_" + stem + "_" + DistrictRules.Ids[layer]);

        /// <summary>Fills the dock's page for the layer shown; false when there is nothing to say yet.</summary>
        internal bool KitFill(MapDistrictModel m)
        {
            int layer = Layer;
            if (!KitReady)
            {
                return false;
            }
            var which = (DistrictLayer)layer;
            string id = DistrictRules.Ids[layer];
            int today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
            int year = GameCalendar.DaysPerYear;
            m.Title = UiText.Fill("reallife_map_district_title", "layer", T(Mod + "_dv_row_" + id));
            m.Icon = LayerIcons[layer];
            m.FiltersLabel = UiText.T("reallife_map_filters_title");
            if (m.Layers.Length != DistrictRules.Layers)
            {
                m.Layers = new MapRailItem[DistrictRules.Layers];
            }
            for (int i = 0; i < DistrictRules.Layers; i++)
            {
                m.Layers[i] = new MapRailItem { Icon = LayerIcons[i], Name = T(Mod + "_dv_row_" + DistrictRules.Ids[i]) };
            }
            m.Layer = layer;
            m.About = T(Mod + "_dv_about_" + id);
            Rank(layer);
            MapDistrict high = null, low = null;
            long sum = 0;
            int known = 0;
            for (int i = 0; i < _ranked.Count; i++)
            {
                MapDistrict d = _ranked[i];
                if (!d.HasFigure[layer])
                {
                    continue;
                }
                sum += d.Figure[layer];
                known++;
                high = high == null || d.Figure[layer] > high.Figure[layer] ? d : high;
                low = low == null || d.Figure[layer] < low.Figure[layer] ? d : low;
            }
            KitAverage(m, which, layer, today, year, known, sum, high, low);
            KitChart(m, layer);
            KitKey(m, which, layer, known, high, low);
            MapDistrict carded = Carded();
            KitChosen(m, which, layer, carded);
            m.TableTitle = Stem("ranked", layer);
            m.ColDistrict = UiText.T("reallife_map_col_district");
            m.ColFigure = UiText.Fill(Mod + "_map_col_" + id, "unit", DistrictGame.AreaUnit());
            m.ColMonth = UiText.T("reallife_map_col_month");
            m.ColEmpty = HasEmpty(layer) ? UiText.T("reallife_map_col_empty") : null;
            if (m.Rows.Length != _ranked.Count)
            {
                m.Rows = new MapDistrictRow[_ranked.Count];
            }
            for (int i = 0; i < _ranked.Count; i++)
            {
                MapDistrict d = _ranked[i];
                bool has = d.HasFigure[layer];
                var row = new MapDistrictRow
                {
                    Name = d.Name, Swatch = has && d.Band[layer] >= 0 ? UiTokens.Ramp(d.Band[layer]) : UiRole.None,
                    Figure = has ? ShortFigure(which, d.Figure[layer]) : UiText.T("reallife_map_no_value"),
                    Chosen = carded != null && d.Id == carded.Id
                };
                KitMonth(ref row, which, layer, d, today, year);
                row.Empty = HasEmpty(layer) && d.LetArea > 0 ? Percent(Tenths(d.EmptyArea, d.LetArea), false) : null;
                m.Rows[i] = row;
            }
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") }, new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_map_hide_view") },
            };
            m.PromptCount = m.Prompts.Length;
            return true;
        }

        // The districts in order, the highest figure first and those with no figure last, as the old window listed them.
        private void Rank(int layer)
        {
            _ranked.Clear();
            _ranked.AddRange(_districts);
            _ranked.Sort((a, b) =>
            {
                if (a.HasFigure[layer] != b.HasFigure[layer])
                {
                    return a.HasFigure[layer] ? -1 : 1;
                }
                int by = b.Figure[layer].CompareTo(a.Figure[layer]);
                return by != 0 ? by : string.CompareOrdinal(a.Name, b.Name);
            });
        }

        // The city's block: its figure for the layer, the highest and lowest district, the share standing empty, and the change this month.
        private void KitAverage(MapDistrictModel m, DistrictLayer which, int layer, int today, int year, int known, long sum, MapDistrict high, MapDistrict low)
        {
            var rows = new List<UiInfoRow>(5);
            m.AverageTitle = UiText.T("reallife_map_average");
            if (known > 0)
            {
                long city = IsTotal(layer) ? sum : (sum + known / 2) / known;
                rows.Add(new UiInfoRow { Label = Stem("avg", layer), Value = Figure(which, city) });
                if (known > 1)
                {
                    rows.Add(new UiInfoRow { Label = UiText.T("reallife_map_highest"), Value = high.Name + ", " + ShortFigure(which, high.Figure[layer]) });
                    rows.Add(new UiInfoRow { Label = UiText.T("reallife_map_lowest"), Value = low.Name + ", " + ShortFigure(which, low.Figure[layer]) });
                }
            }
            if (HasEmpty(layer))
            {
                long let = 0, empty = 0;
                for (int i = 0; i < _districts.Count; i++)
                {
                    let += _districts[i].LetArea;
                    empty += _districts[i].EmptyArea;
                }
                if (let > 0)
                {
                    rows.Add(new UiInfoRow { Label = UiText.T("reallife_map_empty"), Value = Percent(Tenths(empty, let), false) });
                }
            }
            rows.Add(CityChange(which, layer, today, year));
            m.Average = rows.ToArray();
        }

        private UiInfoRow CityChange(DistrictLayer which, int layer, int today, int year)
        {
            string label = UiText.T("reallife_map_change");
            long delta = 0, start = 0;
            int n = 0;
            for (int i = 0; i < _districts.Count; i++)
            {
                MapDistrict d = _districts[i];
                if (_months.Change(today, year, d.Id, layer, d.Figure[layer], d.HasFigure[layer], out long was, out int _))
                {
                    delta += d.Figure[layer] - was;
                    start += was;
                    n++;
                }
            }
            if (n == 0)
            {
                return new UiInfoRow { Label = label, Value = UiText.T("reallife_map_no_change"), Ink = UiRole.Muted };
            }
            string words = ByPercent(layer) ? Percent(Tenths(delta, start), true) : Count(delta);
            return new UiInfoRow { Label = label, Value = words, Ink = ToneText(layer, Math.Sign(delta)) };
        }

        // The ink of a change: green where it is good for the player, red where bad, plain where a rise is neither (a rent).
        private static UiRole Tone(int layer, int sign)
        {
            LayerSense sense = DistrictRules.Sense((DistrictLayer)layer);
            if (sense == LayerSense.Neutral || sign == 0)
            {
                return UiRole.None;
            }
            return (sign > 0) == (sense == LayerSense.HighGood) ? UiRole.Ok : UiRole.Danger;
        }

        private static UiRole ToneText(int layer, int sign)
        {
            UiRole tone = Tone(layer, sign);
            return tone == UiRole.Ok ? UiRole.OkText : tone == UiRole.Danger ? UiRole.DangerText : UiRole.None;
        }

        private void KitMonth(ref MapDistrictRow row, DistrictLayer which, int layer, MapDistrict d, int today, int year)
        {
            if (!d.HasFigure[layer] || !_months.Change(today, year, d.Id, layer, d.Figure[layer], true, out long was, out int tenths))
            {
                return;
            }
            long delta = d.Figure[layer] - was;
            row.Month = ByPercent(layer) ? Percent(tenths, true) : Count(delta);
            row.MonthSign = Math.Sign(delta);
            row.MonthTone = Tone(layer, row.MonthSign);
        }

        // The donut: who the city's homes are for (classes), what stands to let by kind, who runs the businesses. Layers a share would mean nothing for have none.
        private void KitChart(MapDistrictModel m, int layer)
        {
            m.ChartShares = new float[0];
            m.ChartLabels = new string[0];
            m.ChartValues = new string[0];
            m.ChartTitle = m.ChartCentre = m.ChartCentreSub = null;
            switch ((DistrictLayer)layer)
            {
                case DistrictLayer.Wealth:
                {
                    double working = 0, middle = 0, upper = 0;
                    long people = 0;
                    for (int i = 0; i < _districts.Count; i++)
                    {
                        MapDistrict d = _districts[i];
                        double weight = Math.Max(1, d.Buildings);
                        working += d.Working * weight;
                        middle += d.Middle * weight;
                        upper += d.Upper * weight;
                        people += Math.Max(0, d.Residents);
                    }
                    m.ChartTitle = UiText.T("reallife_map_classes");
                    Slices(m, new[] { UiText.T("reallife_map_class_working"), UiText.T("reallife_map_class_middle"), UiText.T("reallife_map_class_upper") }, new[] { working, middle, upper });
                    if (people > 0)
                    {
                        m.ChartCentre = Compact(people);
                        m.ChartCentreSub = UiText.T("reallife_map_residents_lower");
                    }
                    break;
                }
                case DistrictLayer.Rent:
                {
                    var kinds = new double[4];
                    for (int i = 0; i < _districts.Count; i++)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            kinds[k] += _districts[i].Vacant[k];
                        }
                    }
                    double total = kinds[0] + kinds[1] + kinds[2] + kinds[3];
                    if (total > 0)
                    {
                        m.ChartTitle = UiText.T("reallife_map_for_rent_by_type");
                        Slices(m, new[]
                        {
                            UiText.T("reallife_map_kind_retail"), UiText.T("reallife_map_kind_office"), UiText.T("reallife_map_kind_home"), UiText.T("reallife_map_kind_warehouse")
                        }, kinds);
                        m.ChartCentre = Whole((int)total);
                        m.ChartCentreSub = UiText.T("reallife_map_for_rent");
                    }
                    break;
                }
                case DistrictLayer.Competition:
                {
                    double mine = 0, rivals = 0, all = 0;
                    for (int i = 0; i < _districts.Count; i++)
                    {
                        mine += _districts[i].Mine;
                        rivals += _districts[i].Rivals;
                        all += _districts[i].Businesses;
                    }
                    if (all > 0)
                    {
                        m.ChartTitle = UiText.T("reallife_map_run_by");
                        Slices(m, new[] { UiText.T("reallife_map_run_you"), UiText.T("reallife_map_run_rivals"), UiText.T("reallife_map_run_city") },
                            new[] { mine, rivals, Math.Max(0, all - mine - rivals) });
                        m.ChartCentre = Whole((int)all);
                        m.ChartCentreSub = UiText.T("reallife_map_open_word");
                    }
                    break;
                }
            }
        }

        private static void Slices(MapDistrictModel m, string[] labels, double[] values)
        {
            double total = 0;
            for (int i = 0; i < values.Length; i++)
            {
                total += values[i];
            }
            if (total <= 0)
            {
                return;
            }
            m.ChartLabels = labels;
            m.ChartShares = new float[values.Length];
            m.ChartValues = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                m.ChartShares[i] = (float)(values[i] / total);
                m.ChartValues[i] = ((int)Math.Round(values[i] * 100.0 / total)).ToString(CultureInfo.CurrentCulture) + "%";
            }
        }

        // The key: the ramp from the lowest figure shown to the highest, as the map is tinted.
        private void KitKey(MapDistrictModel m, DistrictLayer which, int layer, int known, MapDistrict high, MapDistrict low)
        {
            m.LegendTitle = null;
            if (known == 0)
            {
                return;
            }
            m.LegendTitle = UiText.T("reallife_map_key");
            m.ScaleLabel = T(Mod + "_dv_row_" + DistrictRules.Ids[layer]);
            m.ScaleLow = ShortFigure(which, low.Figure[layer]);
            m.ScaleHigh = ShortFigure(which, high.Figure[layer]);
            m.ScaleSteps = null;   // the kit's own ramp
        }

        // The district pointed at, kept or the player's: its place in the ranking, its figure, the people and the businesses there.
        private void KitChosen(MapDistrictModel m, DistrictLayer which, int layer, MapDistrict d)
        {
            m.HasChosen = d != null;
            if (d == null)
            {
                return;
            }
            m.ChosenName = d.Name;
            m.ShowLabel = UiText.T("reallife_map_show_on_map");
            int rank = _ranked.IndexOf(d) + 1, count = 0;
            for (int i = 0; i < _ranked.Count; i++)
            {
                count += _ranked[i].HasFigure[layer] ? 1 : 0;
            }
            m.ChosenRank = d.HasFigure[layer] && rank > 0 ? UiText.Fill("reallife_map_rank", "n", Whole(rank), "m", Whole(count)) : null;
            var rows = new List<UiInfoRow>(3)
            {
                new UiInfoRow { Label = Stem("avg", layer), Value = d.HasFigure[layer] ? Figure(which, d.Figure[layer]) : UiText.T("reallife_map_no_value") }
            };
            if (d.Residents >= 0)
            {
                rows.Add(new UiInfoRow { Label = UiText.T("reallife_map_residents"), Value = Whole(d.Residents) });
            }
            if (d.Businesses > 0)
            {
                rows.Add(new UiInfoRow { Label = UiText.T("reallife_map_businesses"), Value = Whole(d.Businesses) });
            }
            m.Chosen = rows.ToArray();
            // Every other figure of the district, the layer shown first, said as the old card says them: the dock's block had only the shown layer's, the residents and the businesses.
            _notes = new List<string>(16);
            try
            {
                CardLines(d, which);
                m.ChosenNotes = _notes.ToArray();
            }
            finally
            {
                _notes = null;
            }
        }

        // ------------------------------------------------------------------------------------------------- the words

        // A layer's figure in the table's narrow column: the unit is in the column's heading.
        private static string ShortFigure(DistrictLayer layer, long value)
        {
            switch (layer)
            {
                case DistrictLayer.Rent:
                    return Money(DistrictGame.PerArea(value / 100f));
                case DistrictLayer.Property:
                    return Money(DistrictGame.PerArea(value));
                default:
                    return Figure(layer, value);
            }
        }

        private static int Tenths(long part, long whole) => whole > 0 ? (int)Math.Max(-9999L, Math.Min(9999L, (part * 1000L + whole / 2) / whole)) : 0;

        // 18 as "+1.8%", -4 as "-0.4%"; the sign only where it is asked for.
        private static string Percent(int tenths, bool signed)
        {
            string number = (Math.Abs(tenths) / 10f).ToString("0.0", CultureInfo.CurrentCulture) + "%";
            return !signed ? number : tenths > 0 ? "+" + number : tenths < 0 ? "-" + number : number;
        }

        private static string Count(long delta) => delta > 0 ? "+" + Whole((int)Math.Min(int.MaxValue, delta)) : delta < 0 ? "-" + Whole((int)Math.Min(int.MaxValue, -delta)) : "0";

        // 18400 as "18.4K": the donut's centre has room for four characters.
        private static string Compact(long n) => n >= 1000000 ? (n / 100000 / 10f).ToString("0.#", CultureInfo.CurrentCulture) + "M"
            : n >= 1000 ? (n / 100 / 10f).ToString("0.#", CultureInfo.CurrentCulture) + "K" : n.ToString(CultureInfo.CurrentCulture);

        // ------------------------------------------------------------------------------------------------- the presses

        /// <summary>Another layer ticked, as the player would tick its row in the map's filters.</summary>
        internal bool KitPick(int layer)
        {
            if (_choice == null || layer < 0 || layer >= DistrictRules.Layers)
            {
                return false;
            }
            _choice.Pick(layer);
            _kitStamp++;
            return true;
        }

        /// <summary>The ranked table's row: the district kept, as a click on it on the map keeps it.</summary>
        internal bool KitKeep(int row)
        {
            if (row < 0 || row >= _ranked.Count)
            {
                return false;
            }
            _kept = _ranked[row].Id;
            _kitStamp++;
            KitTagChosen();
            return true;
        }

        /// <summary>The map flown to the district the page is about.</summary>
        internal bool KitShow()
        {
            MapDistrict d = Carded();
            if (d == null)
            {
                return false;
            }
            DistrictGame.FlyTo(d.Centre);
            return true;
        }

        /// <summary>The view switched off, as its close button did.</summary>
        internal void KitClose() => _choice?.Pick(-1);

        /// <summary>The pointer rests on a row of the table (or left it with -1): the card is about that district while it does, as the old window's list did.</summary>
        internal void KitPoint(int row)
        {
            string id = row >= 0 && row < _ranked.Count ? _ranked[row].Id : null;
            if (id != _listHover)
            {
                _listHover = id;
                _kitStamp++;
                KitTagChosen();
            }
        }

        // ------------------------------------------------------------------------------------------------- the names on the map

        // Each district's name with the layer's figure, a small piece of the kit's beside the district's middle on the open map: one when the layer or the day's figures change,
        // moved a frame by the HUD's host as the map's camera moves (a projection each). With the HUD's host not there, or after a fault, there are none.
        private void KitTags()
        {
            int layer = Layer;
            HudHost hud = HudHost.Instance;
            if (_tagsBroken || layer < 0 || !_showing || !OnKit || hud == null)
            {
                KitTagsClear();
                return;
            }
            int stamp = unchecked(layer * 1000003 + _readDay);
            if (stamp == _tagsStamp && _tags.Count > 0)
            {
                KitTagChosen();
                return;
            }
            KitTagsClear();
            _tagsStamp = stamp;
            _tagChosen = Carded()?.Id;
            try
            {
                var which = (DistrictLayer)layer;
                for (int i = 0; i < _districts.Count; i++)
                {
                    MapDistrict d = _districts[i];
                    if (!d.HasFigure[layer])
                    {
                        continue;
                    }
                    string name = d.Name, figure = ShortFigure(which, d.Figure[layer]);
                    UiRole swatch = d.Band[layer] >= 0 ? UiTokens.Ramp(d.Band[layer]) : UiRole.None;
                    UiMapDistrict.TagSize(name, figure, out float w, out float h);
                    Vector3 at = d.Centre;
                    string id = d.Id;
                    _tags.Add(hud.Follow(w, h, s => UiMapDistrict.Tag(s, UiAt.Screen, 0f, 0f, w, h, name, figure, swatch, id == _tagChosen), () => at, 0f, 0f, UiMapFilters.DockRight, 10000f));
                }
            }
            catch (Exception e)
            {
                _tagsBroken = true;
                Logger?.Error(e);
                Logger?.Warn("District view: the names over the districts could not be made after the error above; the page and the tint go on without them.");
                KitTagsClear();
            }
        }

        // The page is about another district now (the pointer moved to one, the player kept one): the tags are laid out again, a few small pieces, so the cyan one moves.
        private void KitTagChosen()
        {
            string now = Carded()?.Id;
            if (now == _tagChosen)
            {
                return;
            }
            _tagChosen = now;
            for (int i = 0; i < _tags.Count; i++)
            {
                _tags[i].Dirty = true;
            }
        }

        private void KitTagsClear()
        {
            _tagsStamp = -1;
            if (_tags.Count == 0)
            {
                return;
            }
            HudHost hud = HudHost.Instance;
            for (int i = 0; i < _tags.Count; i++)
            {
                try
                {
                    hud?.Unfollow(_tags[i]);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            _tags.Clear();
        }
    }
}
