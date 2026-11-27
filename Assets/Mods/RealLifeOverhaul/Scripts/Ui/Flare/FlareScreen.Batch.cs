using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face, "Answer all": the sheet that says what answering every waiting review would do (how many thanks, how many sorry, how many
    /// are left for the player to judge, the rating now and after) for the business on the page or for every business of the player's, and the press that
    /// answers them through the director (FlareDirector.Batch), so the answers go by the rule a hand answer does. Cost: the preview is worked out when the
    /// sheet opens or its scope changes, never per frame.
    /// </summary>
    public sealed partial class FlareScreen
    {
        private const int GroupBatch = 22;
        private int _batchShop;              // the business the sheet was opened from
        private bool _batchAll;              // the sheet's scope: every business, not just that one

        // The business page's button: how many reviews wait, over every business of the player's.
        private string AnswerAllWords()
        {
            int waiting = _flare.ReviewsWaiting(null);
            return waiting > 0 ? FlareText.Fill("reallife_flare_kit_answer_all", "count", waiting.ToString(CultureInfo.InvariantCulture)) : null;
        }

        private bool OpenBatch(int shop)
        {
            List<FlareShop> shops = _flare != null && _flare.Running ? _flare.MyShops() : null;
            if (shops == null || shops.Count == 0 || shop < 0 || shop >= shops.Count)
            {
                return false;
            }
            _batchShop = shop;
            _batchAll = _flare.ReviewsWaiting(shops[shop].Place) == 0;   // a business with nothing waiting opens on all of them
            _sheetWhy = null;
            _sheet = FlareFace.BatchSheet;
            _field = -1;
            _dirty = true;
            return true;
        }

        private void BatchSheet(FlareSheetModel k)
        {
            List<FlareShop> shops = _flare.MyShops();
            if (shops.Count == 0 || _batchShop >= shops.Count)
            {
                _sheet = FlareFace.NoSheet;
                _m.Sheet = FlareFace.NoSheet;
                return;
            }
            FlareShop shop = shops[_batchShop];
            string place = _batchAll ? null : shop.Place;
            FlareBatchSummary sum = _flare.BatchPreview(place);
            k.Title = FlareText.T("reallife_flare_kit_batch_title");
            k.Line = FlareText.T("reallife_flare_kit_batch_line");
            if (shops.Count > 1)
            {
                var groups = new List<string>(1);
                var chips = new List<string[]>(1);
                var chosen = new List<int>(1);
                Group(groups, chips, chosen, GroupBatch, FlareText.T("reallife_flare_kit_batch_scope"), new[] { shop.Name ?? "?", FlareText.T("reallife_flare_kit_batch_all") }, _batchAll ? 1 : 0);
                k.Groups = groups.ToArray();
                k.Chips = chips.ToArray();
                k.Chosen = chosen.ToArray();
            }
            string parts = _flare.BatchParts(sum, true);
            k.Words = parts.Length > 0 ? parts : FlareText.T("reallife_flare_batch_none");
            if (sum.Businesses > 0 && sum.Before > 0)
            {
                k.FigureLabels = new[] { FlareText.T("reallife_flare_kit_fig_rating_now"), FlareText.T("reallife_flare_kit_fig_rating_after") };
                k.Figures = new[] { (sum.Before / 1000f).ToString("0.00", CultureInfo.InvariantCulture), (sum.After / 1000f).ToString("0.00", CultureInfo.InvariantCulture) };
            }
            string note = FlareDirector.BatchLeftLine(sum.Left);
            if (sum.Beyond > 0)
            {
                note = (note.Length > 0 ? note + " " : string.Empty) + FlareText.Fill("reallife_flare_batch_beyond", "count", sum.Beyond.ToString(CultureInfo.InvariantCulture));
            }
            k.Note = note.Length > 0 ? note : FlareText.T("reallife_flare_kit_batch_note");
            if (_sheetWhy != null)
            {
                k.Note = FlareText.T(_sheetWhy);
                k.NoteBad = true;
            }
            k.Confirm = sum.Answers > 0 ? FlareText.Fill("reallife_flare_kit_batch_confirm", "count", sum.Answers.ToString(CultureInfo.InvariantCulture)) : FlareText.T("reallife_flare_kit_batch_confirm_none");
            k.ConfirmOff = sum.Answers == 0;
        }

        private bool BatchChip(int chip)
        {
            _batchAll = chip == 1;
            return true;
        }

        private bool BatchNow()
        {
            List<FlareShop> shops = _flare.MyShops();
            string place = _batchAll || _batchShop >= shops.Count ? null : shops[_batchShop].Place;
            if (_flare.BatchAnswer(place) == null)
            {
                _sheetWhy = "reallife_flare_kit_batch_failed";
                _dirty = true;
                return true;
            }
            CloseSheet();
            return true;
        }
    }
}
