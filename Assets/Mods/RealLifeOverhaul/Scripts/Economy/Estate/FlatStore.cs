using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The player's flats in a line of the save's modData of their own, beside the property business's: an older version never reads it, and
    /// this version reads a save without it as no flats. Game-free. "v=" and the version, then "^f:" and the flats divided by ';', their
    /// fields by '|', an applicant's own fields by '.' and applicants by ','. A later version only adds fields at a record's end. A line this
    /// version cannot read is kept as it was read and written back unchanged while no flat of this save has been made.
    /// </summary>
    public static class FlatStore
    {
        public const string Key = "reallife:estate_flats";
        public const int Version = 1;

        private const int Fields = 31;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(FlatBook book)
        {
            if (book.Flats.Count == 0 && book.Unread != null)
            {
                return book.Unread;
            }
            var t = new StringBuilder(32 + book.Flats.Count * 180);
            t.Append("v=").Append(Version).Append("^f:");
            for (int i = 0; i < book.Flats.Count; i++)
            {
                OwnedFlat f = book.Flats[i];
                EstateHolding h = f.Hold;
                EstateUnit u = h.Units[0];
                t.Append(i > 0 ? ";" : string.Empty).Append(EstateStore.Clean(f.Place)).Append('|');
                Ints(t, f.Slot, f.Kind, f.Units, f.Paid, f.BoughtDay, f.Value, f.BilledMonth, h.Cond, h.Income, h.Base, h.Managed ? 1 : 0, h.List, h.Offer, h.OfferLapse, h.SaleDay, h.SalePrice,
                    h.SaleFails ? 1 : 0, h.NextApplicant, h.Paid, h.Spent, u.Ask, u.ToLet ? 1 : 0, u.Tenant, u.Record, u.Rent, u.Deposit, u.Until, u.Arrears, u.ArrearsDays, u.NoticeEnd, u.CourtDay);
                t.Append('|').Append(u.VoidDays.ToString(Inv)).Append('|');
                for (int a = 0; a < u.Applicants.Count; a++)
                {
                    RentApplicant r = u.Applicants[a];
                    t.Append(a > 0 ? "," : string.Empty).Append(r.Id.ToString(Inv)).Append('.').Append(r.Kind.ToString(Inv)).Append('.').Append(r.Offer.ToString(Inv)).Append('.')
                        .Append(r.Record.ToString(Inv)).Append('.').Append(r.Term.ToString(Inv)).Append('.').Append(r.Lapse.ToString(Inv));
                }
                // The landlord's duties and works, at the record's end: a line from before them reads as none.
                t.Append('|');
                Ints(t, h.Manager, h.InspectDay, h.OrderDay, h.Strikes, u.Happy, u.Call, u.CallDay, u.CallCost, u.CallAnswer, u.CallDue, u.Moans, u.Reminded ? 1 : 0, u.PlanAsked ? 1 : 0,
                    u.OnPlan ? 1 : 0, u.PlanEach, u.QuitDay, h.Level, h.Work, h.WorkEnd, h.WorkCost, h.WorkTo, h.RentYear, h.CostYear, h.CallsYear, h.FeesYear, h.FinesYear,
                    h.LastInspectDay, h.LastInspectScore, h.LastInspect);
            }
            return t.ToString();
        }

        /// <summary>The flats of a saved line; false when it cannot be read (the book keeps the line as it was). Nothing is thrown.</summary>
        public static bool Read(FlatBook book, string text)
        {
            book.Flats.Clear();
            book.Unread = null;
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            if (!text.StartsWith("v=", StringComparison.Ordinal))
            {
                book.Unread = text;
                return false;
            }
            try
            {
                string[] parts = text.Split('^');
                for (int p = 1; p < parts.Length; p++)
                {
                    if (parts[p].Length < 3 || parts[p][0] != 'f' || parts[p][1] != ':')
                    {
                        continue;
                    }
                    string body = parts[p].Substring(2);
                    string[] records = body.Length == 0 ? new string[0] : body.Split(';');
                    for (int r = 0; r < records.Length && book.Flats.Count < FlatCatalogue.HeldMost; r++)
                    {
                        OwnedFlat flat = ReadFlat(records[r]);
                        if (flat != null && book.Find(flat.Place, flat.Slot) == null)
                        {
                            book.Flats.Add(flat);
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                book.Flats.Clear();
                book.Unread = text;
                return false;
            }
        }

        private static OwnedFlat ReadFlat(string record)
        {
            string[] c = record.Split('|');
            if (c.Length < 2 + Fields - 1 || c[0].Length == 0)
            {
                return null;
            }
            var f = new OwnedFlat { Place = c[0] };
            int at = 1;
            f.Slot = Math.Max(0, I(c, at++));
            f.Kind = FlatCatalogue.Clamp(I(c, at++), 0, FlatCatalogue.Kinds - 1);
            f.Units = FlatCatalogue.Clamp(I(c, at++), 1, EstateCatalogue.UnitsMost);
            f.Paid = Math.Max(0, I(c, at++));
            f.BoughtDay = Math.Max(0, I(c, at++));
            f.Value = Math.Max(0, I(c, at++));
            f.BilledMonth = Math.Max(0, I(c, at++));
            EstateHolding h = f.Hold;
            h.Place = f.Key;
            h.Use = (int)EstateUse.Homes;
            h.Cond = EstateCatalogue.Clamp(I(c, at++), 0, 1000);
            h.Income = EstateCatalogue.Clamp(I(c, at++), EstateCatalogue.IncomeLeast, EstateCatalogue.IncomeMost);
            h.Base = Math.Max(0, I(c, at++));
            h.Managed = I(c, at++) != 0;
            h.List = Math.Max(0, I(c, at++));
            h.Offer = Math.Max(0, I(c, at++));
            h.OfferLapse = Math.Max(0, I(c, at++));
            h.SaleDay = Math.Max(0, I(c, at++));
            h.SalePrice = Math.Max(0, I(c, at++));
            h.SaleFails = I(c, at++) != 0;
            h.NextApplicant = Math.Max(1, I(c, at++));
            h.Paid = Math.Max(0, I(c, at++));
            h.Spent = Math.Max(0, I(c, at++));
            EstateUnit u = h.Units[0];
            u.Ask = EstateCatalogue.Clamp(I(c, at++), EstateCatalogue.AskLeast, EstateCatalogue.AskMost);
            u.ToLet = I(c, at++) != 0;
            u.Tenant = I(c, at++, -1);
            u.Record = Math.Max(0, I(c, at++));
            u.Rent = Math.Max(0, I(c, at++));
            u.Deposit = Math.Max(0, I(c, at++));
            u.Until = Math.Max(0, I(c, at++));
            u.Arrears = Math.Max(0, I(c, at++));
            u.ArrearsDays = Math.Max(0, I(c, at++));
            u.NoticeEnd = Math.Max(0, I(c, at++));
            u.CourtDay = Math.Max(0, I(c, at++));
            u.VoidDays = Math.Max(0, I(c, at++));
            if (u.Tenant >= EstateCatalogue.TenantKinds)
            {
                u.Tenant = -1;
            }
            string applicants = at < c.Length ? c[at] : string.Empty;
            string[] list = applicants.Length == 0 ? new string[0] : applicants.Split(',');
            for (int a = 0; a < list.Length && u.Applicants.Count < EstateCatalogue.ApplicantsMost; a++)
            {
                string[] q = list[a].Split('.');
                if (q.Length >= 6)
                {
                    u.Applicants.Add(new RentApplicant
                    {
                        Id = I(q, 0), Kind = EstateCatalogue.Clamp(I(q, 1), 0, EstateCatalogue.TenantKinds - 1), Offer = Math.Max(1, I(q, 2)), Record = Math.Max(0, I(q, 3)),
                        Term = Math.Max(1, I(q, 4)), Lapse = I(q, 5)
                    });
                }
            }
            int d = at + 1;
            if (d < c.Length)
            {
                h.Manager = EstateCatalogue.Clamp(I(c, d++), 0, EstateCatalogue.Managers - 1);
                h.InspectDay = Math.Max(0, I(c, d++));
                h.OrderDay = Math.Max(0, I(c, d++));
                h.Strikes = Math.Max(0, I(c, d++));
                u.Happy = I(c, d++, -1);
                u.Call = EstateCatalogue.Clamp(I(c, d++), 0, EstateCatalogue.CallKinds);
                u.CallDay = I(c, d++);
                u.CallCost = Math.Max(0, I(c, d++));
                u.CallAnswer = Math.Max(0, I(c, d++));
                u.CallDue = I(c, d++);
                u.Moans = Math.Max(0, I(c, d++));
                u.Reminded = I(c, d++) != 0;
                u.PlanAsked = I(c, d++) != 0;
                u.OnPlan = I(c, d++) != 0;
                u.PlanEach = Math.Max(0, I(c, d++));
                u.QuitDay = Math.Max(0, I(c, d++));
                h.Level = EstateCatalogue.Clamp(I(c, d++), 0, EstateCatalogue.LevelMost);
                h.Work = Math.Max(0, I(c, d++));
                h.WorkEnd = Math.Max(0, I(c, d++));
                h.WorkCost = Math.Max(0, I(c, d++));
                h.WorkTo = I(c, d++);
                h.RentYear = I(c, d++);
                h.CostYear = I(c, d++);
                h.CallsYear = I(c, d++);
                h.FeesYear = I(c, d++);
                h.FinesYear = I(c, d++);
                h.LastInspectDay = Math.Max(0, I(c, d++));
                h.LastInspectScore = I(c, d++);
                h.LastInspect = EstateCatalogue.Clamp(I(c, d++), 0, 2);
            }
            return f;
        }

        private static void Ints(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : fallback;
        }

        /// <summary>The flats read into a book from the save's data; a save without the line has none.</summary>
        public static void Load(FlatBook book, Dictionary<string, string> data)
        {
            string text;
            Read(book, data != null && data.TryGetValue(Key, out text) ? text : null);
        }

        public static void Save(FlatBook book, Dictionary<string, string> data)
        {
            if (book.Flats.Count == 0 && book.Unread == null)
            {
                data.Remove(Key);   // nothing owned: no line, as a save from before has none
                return;
            }
            data[Key] = Write(book);
        }
    }
}
