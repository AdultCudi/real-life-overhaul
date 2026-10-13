using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Items;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Everything the firms part reads from or does to the game.
    ///
    /// READ: the player's businesses of the tech sector's types, and their web development agencies and graphic designers
    /// (the game's own office businesses, which join the same economy); what a game office sold yesterday and its hour's
    /// price (from the game's statement and ItemHelper.GetPrice), so that only the hours its staff had spare go to the
    /// tech sector's work; the player's other businesses, as clients.
    ///
    /// DONE: money as dated transactions of the mod's own at the firm's address (takings counted among the business's
    /// takings in the game's statement through the economy's books; hosting and software as a row of its costs; marketing
    /// in the game's marketing line), the costs marked deductible as the game marks its own; the research and development
    /// relief given to the game's tax as an extra deductible expense with no money moving (TaxHelper.TrackTransaction, as
    /// the economy's books give Empire Motors' costs). A fine is charged at once and is not deductible.
    /// </summary>
    internal static class FirmsGame
    {
        public const string TakingsKey = "reallife_transaction_tech_takings";
        public const string HostingKey = "reallife_transaction_tech_hosting";
        public const string SoftwareKey = "reallife_transaction_tech_software";
        public const string MarketingKey = "reallife_transaction_tech_marketing";
        public const string FineKey = "reallife_transaction_tech_data_fine";
        public const string RegisterKey = "reallife_transaction_tech_register_fee";
        public const string ServicesKey = "reallife_transaction_tech_it_services";
        public const string ReliefKey = "reallife_transaction_tech_relief";
        public const string CostRow = "reallife_books_tech_hosting";
        private const string DeductKey = "reallife_tech_deduct";
        private const string ReliefDeductKey = "reallife_tech_relief_deduct";
        private const string Marketing = "ba:transactioncategory_marketing";
        public const string WebAgency = "ba:businesstype_webdevelopmentagency";
        public const string GraphicDesigner = "ba:businesstype_graphicdesigner";

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>True for a transaction type that is the tech sector's takings (for the economy's books).</summary>
        public static bool IsTakings(string type) => type == TakingsKey;

        /// <summary>The row of a business's costs a transaction type of the tech sector's belongs to; null for none.</summary>
        public static string RowOf(string type) => type == HostingKey || type == SoftwareKey ? CostRow : null;

        /// <summary>The firm kind of a business's type, or -1 for a business that is no tech firm.</summary>
        public static int KindOf(string type)
        {
            int k = TechTypes.KindOf(type);
            if (k >= 0)
            {
                return k;
            }
            return type == WebAgency ? (int)FirmKind.WebAgency : type == GraphicDesigner ? (int)FirmKind.Design : -1;
        }

        /// <summary>The player's tech firms: their businesses of the sector's types and their game office businesses. One pass over the buildings.</summary>
        public static void Firms(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.HasEstablishedBusiness && KindOf(reg.businessTypeName) >= 0)
                {
                    into.Add(reg);
                }
            }
        }

        /// <summary>The player's businesses that are not tech firms, as clients: place and name.</summary>
        public static void Clients(List<KeyValuePair<string, string>> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count && into.Count < 24; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.HasEstablishedBusiness && !string.IsNullOrEmpty(reg.businessTypeName)
                    && reg.businessTypeName != "ba:businesstype_empty" && KindOf(reg.businessTypeName) < 0)
                {
                    into.Add(new KeyValuePair<string, string>(TalentGame.Place(reg), TalentGame.NameOf(reg)));
                }
            }
        }

        /// <summary>
        /// The share of a game office's staff hours that went unsold yesterday, thousandths: its hours worked against what
        /// it sold at its hour's price. All of it for a business of the tech sector's own types, which the game sells nothing for.
        /// </summary>
        public static int Spare(BuildingRegistration reg, int hoursWorked)
        {
            if (reg == null || TechTypes.Is(reg.businessTypeName))
            {
                return 1000;
            }
            try
            {
                float sold = Economy.Books.SalesYesterday(reg.Address);
                string item = reg.cachedAvailableProducts != null && reg.cachedAvailableProducts.Count > 0 ? reg.cachedAvailableProducts[0] : null;
                float price = string.IsNullOrEmpty(item) ? 0f : ItemHelper.GetPrice(item, reg);
                if (price <= 0.01f || hoursWorked <= 0)
                {
                    return hoursWorked > 0 ? 1000 : 0;
                }
                float used = sold / price / hoursWorked;
                return Mathf.Clamp(Mathf.RoundToInt((1f - used) * 1000f), 0, 1000);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // ---------------------------------------------------------------- money

        /// <summary>Takings at the firm, cents, as a dated transaction of the mod's own (counted among its takings in the game's statement).</summary>
        public static void Takings(long cents, BuildingRegistration at, string name, int? day)
        {
            if (cents > 0 && Save != null && at != null)
            {
                GameManager.ChangeMoneySafe(cents / 100f, Info(TakingsKey, name, null), day, at.Address, true);
            }
        }

        /// <summary>A cost of the firm, cents: deductible; marketing in the game's marketing line. Forced (it was used).</summary>
        public static void Cost(long cents, string key, BuildingRegistration at, string name, int? day)
        {
            if (cents <= 0 || Save == null)
            {
                return;
            }
            TransactionInfo info = Info(key, name, key == MarketingKey ? Marketing : null);
            info.SetTaxDeductibleName(DeductKey.GetLocalization());
            GameManager.ChangeMoneySafe(-cents / 100f, info, day, at != null ? at.Address : null, true);
        }

        /// <summary>A charge the player chose to pay now (the register's fee), cents; refused if the cash does not cover it.</summary>
        public static bool Pay(long cents, string key, BuildingRegistration at, string name)
        {
            if (cents <= 0)
            {
                return true;
            }
            TransactionInfo info = Info(key, name, null);
            info.SetTaxDeductibleName(DeductKey.GetLocalization());
            return Save != null && GameManager.ChangeMoneySafe(-cents / 100f, info, null, at != null ? at.Address : null, false);
        }

        /// <summary>A fine, cents: charged at once, not deductible.</summary>
        public static void Fine(long cents, BuildingRegistration at, string name)
        {
            if (cents > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(-cents / 100f, Info(FineKey, name, null), null, at != null ? at.Address : null, true);
            }
        }

        /// <summary>The research and development relief: an extra deductible expense for the game's tax, with no money moving. Returns what was given, whole money.</summary>
        public static float Relief(long cents)
        {
            if (cents <= 0)
            {
                return 0f;
            }
            float amount = Mathf.Round(cents) / 100f;
            var info = new TransactionInfo(ReliefKey);
            info.SetTaxDeductibleName(ReliefDeductKey.GetLocalization());
            TaxHelper.TrackTransaction(new Transaction(info) { amount = -amount });
            return amount;
        }

        // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
        private static TransactionInfo Info(string key, string name, string category)
        {
            var data = new Dictionary<string, string>(2) { { "name", name ?? string.Empty } };
            return category != null ? new TransactionInfo(key, category, data) : new TransactionInfo(key, data);
        }
    }
}
