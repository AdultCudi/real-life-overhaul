using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The Tech app on the phone, and what the player does with a firm's products and contracts there.</summary>
    public sealed partial class TechDirector
    {
        private PhoneApp _techApp;
        private TechApp _techView;
        private bool _techGaveUp;

        private static readonly string[] TechGlyphs = { "Icon-Laptop", "Icon-Building" };

        private void InstallTech()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new TechApp(this);
            GameSprites.Want(TechGlyphs);
            _techApp = PhoneApp.Install(new PhoneAppDef
            {
                Id = TechAppId,
                TitleKey = "reallife_tech_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(TechGlyphs), new Color32(120, 120, 230, 255), new Color32(60, 50, 160, 255), 0.42f, "AppIcon-TechSector"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_techApp == null)
            {
                _techGaveUp = !tryAgain;
                if (_techGaveUp)
                {
                    Logger?.Warn("Tech: the game's phone is not built as this version expects, so the Tech app is left off. "
                                 + "Firms still run each day and the notifications still say what needs an answer.");
                }
                return;
            }
            _techView = view;
            TechBadge();
        }

        public void OpenTech(int tab, string pick)
        {
            if (_techApp != null && _techView != null)
            {
                _techView.Want(tab, pick);
                _techApp.Open();
            }
        }

        internal bool TechOpened()
        {
            if (_broken || Save == null || !_firmsOn)
            {
                return false;
            }
            try
            {
                ReadPeople(Save.Day);
                ReadFirms(Save.Day);
                Crews();
                TechBadge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the Tech app", e);
                return false;
            }
        }

        /// <summary>Offers and tenders waiting for an answer, and games ready to launch.</summary>
        internal int TechWaiting()
        {
            int n = 0;
            if (!_firmsOn)
            {
                return 0;
            }
            for (int i = 0; i < _firmsFile.Contracts.Count; i++)
            {
                n += _firmsFile.Contracts[i].Stage == (int)WorkStage.Offered || _firmsFile.Contracts[i].Stage == (int)WorkStage.Tender ? 1 : 0;
            }
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                n += FirmsRules.CanLaunch(_firmsFile.Products[i]) ? 1 : 0;
            }
            return n;
        }

        private void TechBadge()
        {
            if (_techApp != null)
            {
                _techApp.SetBadge(_techApp.Showing ? 0 : TechWaiting());
            }
        }

        internal void ChangedTech()
        {
            _techView?.Changed();
            TechBadge();
        }

        /// <summary>A notification of the game's that opens the Tech app on a tab with a row picked.</summary>
        internal void TellTech(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 10f, null, Guard.Wrap(() => OpenTech(tab, pick)), true, false);
        }

        internal Product NewProduct(string place, ProductKind kind, Segment segment, int scope)
        {
            FirmPlace firm = FirmOf(place);
            int live = 0;
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                live += _firmsFile.Products[i].Stage < 2 ? 1 : 0;
            }
            if (firm == null || !FirmsRules.Makes(firm.File.K, kind) || live >= FirmsCatalogue.ProductsMost || Save == null)
            {
                return null;
            }
            Product p = FirmsRules.NewProduct(_firmsFile, place, kind, segment, scope, null);
            p.Launched = Save.Day;
            _firmsFile.Products.Add(p);
            ChangedTech();
            return p;
        }

        internal void SetProduct(int id, int tier, int marketing, int share)
        {
            Product p = _firmsOn ? _firmsFile.ProductOf(id) : null;
            if (p == null)
            {
                return;
            }
            if (tier >= 0)
            {
                p.Tier = TalentCatalogue.Clamp(tier, 0, 2);
            }
            if (marketing >= 0)
            {
                p.Marketing = TalentCatalogue.Clamp(marketing, 0, FirmsCatalogue.MarketingDay.Length - 1);
            }
            if (share >= 0)
            {
                p.Share = TalentCatalogue.Clamp(share, 0, 100);
            }
            ChangedTech();
        }

        internal bool Launch(int id)
        {
            Product p = _firmsOn ? _firmsFile.ProductOf(id) : null;
            FirmPlace firm = p != null ? FirmOf(p.Place) : null;
            if (p == null || firm == null || Save == null || !FirmsRules.CanLaunch(p))
            {
                return false;
            }
            FirmsRules.Launch(p, Save.Day);
            TechBridges.Fame?.Invoke(0, 1);
            Story("reallife_tech_news_launch", "product", p.Name, "firm", firm.File.Name);
            TellTech(NotificationType.Success, "reallife_tech_note_launched", Data("product", p.Name), TabProducts, TechApp.ProductRow + p.Id);
            ChangedTech();
            return true;
        }

        internal void Withdraw(int id)
        {
            Product p = _firmsOn ? _firmsFile.ProductOf(id) : null;
            if (p != null && p.Stage < 2)
            {
                p.Stage = 2;
                ChangedTech();
            }
        }

        internal void SetFocus(string place, int focus)
        {
            FirmPlace firm = FirmOf(place);
            if (firm != null)
            {
                firm.File.Focus = TalentCatalogue.Clamp(focus, 0, (int)Focus.Count - 1);
                ChangedTech();
            }
        }

        /// <summary>A year on the data protection register, paid now. False if the cash does not cover it.</summary>
        internal bool Register(string place)
        {
            FirmPlace firm = FirmOf(place);
            if (firm == null || Save == null)
            {
                return false;
            }
            long fee = (long)Prices.At(FirmsCatalogue.RegisterFee / 100) * 100;
            if (!FirmsGame.Pay(fee, FirmsGame.RegisterKey, firm.Reg, firm.File.Name))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            firm.File.RegisteredUntil = Math.Max(firm.File.RegisteredUntil, Save.Day) + GameCalendar.DaysPerYear;
            ChangedTech();
            return true;
        }

        /// <summary>An offer taken by the firm it was made to: under way from today, with a project's first share paid.</summary>
        internal bool TakeContract(int id)
        {
            Contract w = _firmsOn ? _firmsFile.ContractOf(id) : null;
            FirmPlace firm = w != null ? FirmOf(w.Place) : null;
            if (w == null || firm == null || w.Stage != (int)WorkStage.Offered || Save == null)
            {
                return false;
            }
            long upfront = FirmsRules.Take(w, firm.File.Place, Save.Day) * Prices.Level / 1000;
            if (upfront > 0)
            {
                FirmsGame.Takings(upfront, firm.Reg, firm.File.Name, null);
                ChargeClient(w, firm, upfront, null);
            }
            TellIfNobody(w);
            ChangedTech();
            return true;
        }

        internal void DeclineContract(int id)
        {
            Contract w = _firmsOn ? _firmsFile.ContractOf(id) : null;
            if (w != null && (w.Stage == (int)WorkStage.Offered || w.Stage == (int)WorkStage.Tender))
            {
                w.Stage = (int)WorkStage.Declined;
                w.End = Save != null ? Save.Day : w.End;
                ChangedTech();
            }
        }

        /// <summary>A bid on the council's tender by one of the player's firms, at a share of the fair price.</summary>
        internal bool BidTender(int id, string place, int thousandths)
        {
            Contract t = _firmsOn ? _firmsFile.ContractOf(id) : null;
            FirmPlace firm = FirmOf(place);
            if (t == null || firm == null || t.Stage != (int)WorkStage.Tender || !FirmsRules.Takes(firm.File.K, t.K))
            {
                return false;
            }
            t.Bid = FirmsRules.BidAt(t, thousandths);
            t.Place = place;
            t.Stage = (int)WorkStage.Bid;
            ChangedTech();
            return true;
        }

        /// <summary>A contract under way given up: no more is paid, and the firm's reputation suffers.</summary>
        internal void DropContract(int id)
        {
            Contract w = _firmsOn ? _firmsFile.ContractOf(id) : null;
            FirmPlace firm = w != null ? FirmOf(w.Place) : null;
            if (w != null && w.Stage == (int)WorkStage.Active)
            {
                w.Stage = (int)WorkStage.Failed;
                w.End = Save != null ? Save.Day : w.End;
                if (firm != null)
                {
                    firm.File.Reputation = Math.Max(0, firm.File.Reputation - 80);
                }
                ChangedTech();
            }
        }

        /// <summary>What decides whether a firm's people can work on a kind of contract (-1: any of its work), read now.</summary>
        internal FirmFacts FactsOf(FirmPlace firm, int kind)
        {
            return firm == null || Save == null ? default(FirmFacts) : FirmsGame.Facts(firm.Reg, firm.File.K, kind, _people, firm.File.Place, Save.Day);
        }

        /// <summary>The Utilities app on its internet page.</summary>
        internal bool OpenInternet() => TechBridges.Try(TechBridges.OpenInternet);

        internal bool OpenSchedule(FirmPlace firm) => firm != null && FirmsGame.OpenSchedule(firm.Reg);

        /// <summary>The Company app's rota for the firm.</summary>
        internal bool OpenRota(FirmPlace firm) => firm != null && TechBridges.RotaFor(firm.File.Place);
    }
}
