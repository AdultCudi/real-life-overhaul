using System;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare's director, the City Office's cases and what a post does to a business.</summary>
    public sealed partial class FlareDirector
    {
        // A report reaches the City Office: a case and the week's tally. The same report by the same player again today joins
        // its case, is answered and moves nothing more: null.
        private FlareCase FileCase(FlarePost p, string reporter, float x, float z)
        {
            if (Following || p.District == null)
            {
                return null;
            }
            bool again;
            var kind = (FlareReport)FlareRules.Clamp(p.Arg, 0, (int)FlareReport.Utilities);
            string who = reporter == "me" || reporter.StartsWith("p:", StringComparison.Ordinal) ? reporter : null;
            if (!FlareLiveRules.FirstToday(_file.Net, Save.Day, FlareLiveRules.MoveKey(who, FlareSource.Report, (int)kind, p.District, p.Place)))
            {
                FlareCase open = FlareLiveRules.OpenCase(_file.Net, kind, p.District, p.Place);
                p.Case = open != null ? open.Id : -1;
                RepeatAnswer(p, kind);
                Logger?.Info("Flare: " + reporter + " reported " + kind + " in " + FlareGame.DistrictName(p.District) + " again today: in the feed and answered, with the case filed already.");
                return null;
            }
            bool counted = FlareLiveRules.Counted(_file.Net, Save.Day, who);
            FlareCase c = FlareLiveRules.File(_file.Net, kind, p.District, p.Place, p.Name, x, z, Now, reporter, p.Id, FlareLiveRules.CitySeed(_file), out again, counted);
            p.Case = c.Id;
            if (!counted)
            {
                Logger?.Info("Flare: " + reporter + " has made three reports today: this one is filed and answered, and moves no mood, tally or paper.");
            }
            if (again && c.State == FlareCaseState.Open && FaultsReal)
            {
                c.ReplyAt = FlareLiveRules.Sooner(c.ReplyAt, Now);   // the same real fault reported again: the City Office answers sooner
            }
            if (again && counted)
            {
                FlareLiveRules.Complained(_file.Net, kind, p.District, Save.Day, c.State == FlareCaseState.Acting);
                Escalation(c, Save.Day);
            }
            ShareCaseChanged(c);
            return c;
        }

        // The same report again today: noted, in a reply to this post; the case, the tally and the crews stay as they are.
        private void RepeatAnswer(FlarePost report, FlareReport kind)
        {
            FlareOfficial who = kind == FlareReport.Crime ? FlareOfficial.Police : kind == FlareReport.Transit ? FlareOfficial.Transit
                : kind == FlareReport.Utilities ? FlareOfficial.Utilities : FlareOfficial.CityOffice;
            int id = _file.NextId++;
            var a = new FlarePost
            {
                Id = id, Author = FlareAuthor.Official, Who = (int)who, Source = FlareSource.Answer, Arg = (int)FlareAnswer.Received, ReplyTo = report.Id, Case = report.Case,
                Name = report.Name, District = report.District, Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29),
                Reach = 400 + FlareRules.Roll(_file.Seed, id, 5), Likes = 3 + FlareRules.Roll(_file.Seed, id, 6) % 40, Topic = FlareLiveRules.ReportTag(kind)
            };
            FlareLiveRules.SetAt(a, Math.Max(report.At, Now) + FlareLiveRules.ReplyLeast);
            _file.Posts.Add(a);
            ShareChanged(a);
        }

        // A case's answer is due: what the City Office does, its answer, the tally and the mood.
        private void Answer(FlareCase c, int day)
        {
            // The city's faults judge the report first: nothing there is answered so; a real fault sends the crew to where it is.
            float fx = c.X, fz = c.Z;
            int fault = -1;
            int verdict = Settings.FlareCityOffice && FlareBridges.FaultCheck != null
                ? FlareBridges.FaultCheck(c.Kind, c.District, c.X, c.Z, c.Reporter, c.Id, c.Place, out fx, out fz, out fault)
                : -1;
            c.Verdict = verdict;
            c.Fault = verdict == (int)FaultAnswer.True ? fault : -1;   // kept with the case, so a crew sent later under the paper's eye takes the same fault
            if (FlareLiveRules.FoundNothing(verdict))
            {
                NothingFound(c, day, (FaultAnswer)verdict);
                return;
            }
            bool real = verdict == (int)FaultAnswer.True;
            if (real)
            {
                c.X = fx;
                c.Z = fz;
            }
            bool siteNear = !real && c.Kind == FlareReport.Unsafe && FlareBridges.SiteNear != null && (c.X != 0f || c.Z != 0f) && FlareBridges.SiteNear(c.X, c.Z, 120f);
            FlareAnswer answer;
            FlareOrder order = FlareLiveRules.Decide(_file.Net, c, c.ReplyAt, Settings.FlareCityOffice, siteNear, real, out answer);
            int site = -1, days = 0;
            string address = null;
            if (order == FlareOrder.Works)
            {
                if (FlareBridges.Works == null || !FlareBridges.Works(c.Kind, c.District, c.X, c.Z, FlareLiveRules.CitySeed(_file) + c.Id, out site, out days, out address))
                {
                    order = FlareOrder.Queued;
                    answer = FlareAnswer.Busy;
                }
            }
            else if (order == FlareOrder.SiteCheck)
            {
                FlareBridges.SiteReported?.Invoke(c.X, c.Z);
            }
            FlareLiveRules.Act(_file.Net, c, order, day, site);
            if (order == FlareOrder.Inspection && verdict == (int)FaultAnswer.Clean)
            {
                c.State = FlareCaseState.Done;   // the inspector found the business clean: nothing against it
                answer = FlareAnswer.Clean;
            }
            else if (order == FlareOrder.Inspection)
            {
                Inspected(c, day);
            }
            if (real && (order == FlareOrder.Works || order == FlareOrder.Patrol || order == FlareOrder.Cleanup))
            {
                FlareBridges.FaultActed?.Invoke(c.Id, c.Fault, order == FlareOrder.Works);
            }
            bool acted = order != FlareOrder.Queued && order != FlareOrder.Answered;
            if (c.Counted)
            {
                FlareLiveRules.Complained(_file.Net, c.Kind, c.District, day, acted);   // a report past its player's three a day: no tally, no mood
            }
            if (acted)
            {
                FlareLiveRules.Answered(_file.Net, c.Kind, c.District);
            }
            if (c.Counted)
            {
                _file.MayorMood = FlareRules.Clamp(_file.MayorMood + (acted ? FlareLiveRules.MoodActed : order == FlareOrder.Queued ? FlareLiveRules.MoodQueued : 0), -100, 100);
            }
            FlarePost reply = AnswerPost(c, answer, address, days);
            Logger?.Info("Flare: the City Office answers case " + c.Id + " (" + c.Kind + " in " + FlareGame.DistrictName(c.District) + ", reported by " + c.Reporter + "): " + answer + ", " + order
                         + (order == FlareOrder.Works ? " (site " + site + " at " + address + ", about " + days + " days)" : c.Until >= 0 ? " until day " + c.Until : string.Empty)
                         + "; the feed's mood " + _file.MayorMood + ".");
            if (c.Reporter == "me" && reply != null && Settings.SocialMediaNotices && CaseNote(day))
            {
                int id = reply.Id;
                Tell(NotificationType.Info, "reallife_flare_note_case", () => OpenPost(id), "account", FlareText.OfficialName(reply.Who));
            }
            Escalation(c, day);
            ShareCaseChanged(c);
        }

        private static bool FaultsReal => FlareBridges.FaultsReal != null && FlareBridges.FaultsReal();

        // A report the faults found nothing in: answered so, nothing ignored, and a player's own costs a little reputation by the option.
        private void NothingFound(FlareCase c, int day, FaultAnswer verdict)
        {
            FlareAnswer answer = verdict == FaultAnswer.Barred ? FlareAnswer.Barred : verdict == FaultAnswer.Warned ? FlareAnswer.Warned : FlareAnswer.Nothing;
            FlareLiveRules.Act(_file.Net, c, FlareOrder.Answered, day, -1);
            bool mine = c.Reporter == "me";
            int was = _file.Reputation;
            if (mine && verdict != FaultAnswer.Barred && Settings.CityFaultsFalseCost)
            {
                _file.Reputation = FlareLiveRules.FalseReport(_file.Reputation);
            }
            FlarePost reply = AnswerPost(c, answer, null, 0);
            Logger?.Info("Flare: the City Office looked into case " + c.Id + " (" + c.Kind + " in " + FlareGame.DistrictName(c.District) + ", reported by " + c.Reporter + ") and found nothing: "
                         + answer + (mine && _file.Reputation != was ? "; the player's reputation on Flare " + was + " to " + _file.Reputation : string.Empty) + ".");
            if (mine && reply != null && Settings.SocialMediaNotices && CaseNote(day))
            {
                int id = reply.Id;
                Tell(answer == FlareAnswer.Nothing ? NotificationType.Info : NotificationType.Warning,
                    answer == FlareAnswer.Barred ? "reallife_flare_note_barred" : answer == FlareAnswer.Warned ? "reallife_flare_note_warned" : "reallife_flare_note_nothing", () => OpenPost(id));
            }
            ShareCaseChanged(c);
        }

        /// <summary>One of the city's people reports a real fault of the city's: a post naming where, and a case. False when Flare cannot take it.</summary>
        public bool CityReport(FlareReport kind, string district, string address, float x, float z)
        {
            if (!Running || Following || Save == null || district == null)
            {
                return false;
            }
            EnsureCitizens(_file);
            if (_citizens.Count == 0)
            {
                return false;
            }
            int start = FlareLiveRules.PickCitizen(_file.Seed, _file.NextId, 880) % _citizens.Count;
            FlareCitizen who = _citizens[start];
            for (int i = 0; i < _citizens.Count; i++)
            {
                FlareCitizen c = _citizens[(start + i) % _citizens.Count];
                if (string.Equals(c.District, district, StringComparison.Ordinal))
                {
                    who = c;   // somebody who lives there, if anybody does
                    break;
                }
            }
            int id = _file.NextId++;
            int reach = 60 + FlareRules.Roll(_file.Seed, id, 531) * 3;
            var p = new FlarePost
            {
                Id = id, Author = FlareAuthor.Citizen, Who = who.Index, Source = FlareSource.Report, Arg = (int)kind, District = district, Name = string.IsNullOrEmpty(address) ? null : address,
                Topic = FlareLiveRules.ReportTag(kind), Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29), Reach = reach,
                Likes = reach / 8 + FlareRules.Roll(_file.Seed, id, 533) % 12, Replies = FlareRules.Roll(_file.Seed, id, 534) % 6
            };
            FlareLiveRules.SetAt(p, Now);
            _file.Posts.Add(p);
            FileCase(p, "c" + who.Index, x, z);
            FlareRules.Prune(_file);
            ShareChanged(p);
            Recount();
            Changed();
            return true;
        }

        private bool CaseNote(int day)
        {
            if (_caseNoteDay != day)
            {
                _caseNoteDay = day;
                _caseNotes = 0;
            }
            return _caseNotes++ < CaseNotesPerDay;
        }

        // The answer in character, a reply to the report.
        private FlarePost AnswerPost(FlareCase c, FlareAnswer answer, string address, int days)
        {
            FlarePost report = c.Post >= 0 ? _file.Post(c.Post) : null;
            bool reportedByMe = c.Reporter == "me" || (c.Reporter != null && c.Reporter.StartsWith("p:", StringComparison.Ordinal));
            if (report == null && !reportedByMe)
            {
                return null;
            }
            if (!reportedByMe && answer != FlareAnswer.Works && answer != FlareAnswer.Patrol && answer != FlareAnswer.Cleanup && FlareRules.Roll(c.Id, c.Repeats, 860) >= 350)
            {
                return null;   // the city's people's complaints are answered when something is done, and now and then otherwise
            }
            FlareOfficial who = c.Kind == FlareReport.Crime ? FlareOfficial.Police : c.Kind == FlareReport.Transit ? FlareOfficial.Transit : c.Kind == FlareReport.Utilities ? FlareOfficial.Utilities
                : c.Kind == FlareReport.Unsafe && answer == FlareAnswer.Patrol ? FlareOfficial.Police : FlareOfficial.CityOffice;
            int id = _file.NextId++;
            var p = new FlarePost
            {
                Id = id, Author = FlareAuthor.Official, Who = (int)who, Source = FlareSource.Answer, Arg = (int)answer, ReplyTo = report != null ? report.Id : -1, Case = c.Id,
                Name = address ?? c.Name, District = c.District, When = days, Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000 + FlareRules.Roll(_file.Seed, id, 29),
                Reach = 400 + FlareRules.Roll(_file.Seed, id, 5), Likes = 3 + FlareRules.Roll(_file.Seed, id, 6) % 40, Topic = FlareLiveRules.ReportTag(c.Kind)
            };
            FlareLiveRules.SetAt(p, Math.Max(c.ReplyAt, report != null ? report.At + 5 : c.ReplyAt));
            _file.Posts.Add(p);
            c.Answer = id;
            FlareRules.Prune(_file);
            ShareChanged(p);
            return p;
        }

        // A business complained about and inspected: the talk turns against it for a few days, at the owner's game alone.
        private void Inspected(FlareCase c, int day)
        {
            if (c.Place == null)
            {
                return;
            }
            BuildingRegistration reg = FlareLiveGame.AnyBusiness(c.Place);
            if (reg == null || !FlareLiveRules.EffectHere(FlareSource.Report, GoingPublic.Foreign(reg), GoingPublic.Mine(reg), false))
            {
                return;   // another player's: their own game applies it when the case comes to it
            }
            Buzz(c.Place, FlareLiveRules.ComplaintBuzz, FlareLiveRules.InspectionDays, day, reg);
            Logger?.Info("Flare: an inspector visits " + c.Name + " after a complaint: " + FlareLiveRules.ComplaintBuzz + "% customers for " + FlareLiveRules.InspectionDays + " days.");
        }

        // The week's ignored complaints of a kind in a district: at three it is in the paper, the mood falls, and the office acts.
        private void Escalation(FlareCase c, int day)
        {
            if (!c.Counted || !FlareLiveRules.Escalate(_file.Net, c.Kind, c.District, day, Settings.FlareCityOffice))
            {
                return;
            }
            FlareLiveRules.Story(_file.Net, day + 1, "reallife_flare_news_ignored", "problem", FlareText.ReportWord(c.Kind), "district", FlareGame.DistrictName(c.District),
                "count", FlareLiveRules.Tally(_file.Net, c.Kind, c.District, day).ToString());
            _file.MayorMood = FlareRules.Clamp(_file.MayorMood + FlareLiveRules.MoodEscalated, -100, 100);
            Logger?.Info("Flare: " + FlareLiveRules.Tally(_file.Net, c.Kind, c.District, day) + " complaints about " + c.Kind + " in " + FlareGame.DistrictName(c.District)
                         + " this week, none acted on: in tomorrow's City Ledger; the feed's mood " + _file.MayorMood + ".");
            // Under the paper's eye a crew is found after all, if one of the week's is free.
            FlareOrder order = FlareLiveRules.EscalationOrder(_file.Net, c, Now, Settings.FlareCityOffice);
            if (order == FlareOrder.None)
            {
                return;
            }
            int site = -1, days = 0;
            string address = null;
            if (order == FlareOrder.Works
                && (FlareBridges.Works == null || !FlareBridges.Works(c.Kind, c.District, c.X, c.Z, FlareLiveRules.CitySeed(_file) + c.Id, out site, out days, out address)))
            {
                return;
            }
            FlareLiveRules.Act(_file.Net, c, order, day, site);
            if (order == FlareOrder.Inspection)
            {
                Inspected(c, day);
            }
            if (c.Verdict == (int)FaultAnswer.True && (order == FlareOrder.Works || order == FlareOrder.Patrol || order == FlareOrder.Cleanup))
            {
                FlareBridges.FaultActed?.Invoke(c.Id, c.Fault, order == FlareOrder.Works);   // the fault the case found when it was answered is taken
            }
            FlareLiveRules.Answered(_file.Net, c.Kind, c.District);
            AnswerPost(c, FlareAnswer.Pressure, address, days);
        }

        /// <summary>A review, announcement or complaint moves a business's customers at its owner's game alone, the city's on every machine.</summary>
        private void BusinessEffect(FlarePost p, bool mine)
        {
            if (p.Place == null || Save == null)
            {
                return;
            }
            BuildingRegistration reg = FlareLiveGame.AnyBusiness(p.Place);
            if (reg == null || !FlareLiveRules.EffectHere(p.Source, GoingPublic.Foreign(reg), GoingPublic.Mine(reg), mine))
            {
                return;
            }
            int day = Save.Day;
            if (!FlareLiveRules.FirstToday(_file.Net, day, FlareLiveRules.MoveKey(FlareLiveRules.Mover(p), p.Source, p.Arg, null, p.Place)))
            {
                Logger?.Info("Flare: " + FlareLiveRules.KeyOf(p) + " posted about " + p.Name + " again today (" + p.Source + "): in the feed, its customers moved once today already.");
                return;
            }
            if (p.Source == FlareSource.Review)
            {
                int buzz = FlareLiveRules.ReviewBuzz(p.Arg, p.Reach);
                if (buzz != 0)
                {
                    Buzz(p.Place, buzz, FlareLiveRules.ReviewDays, day, reg);
                    Logger?.Info("Flare: a " + p.Arg + "-star review of " + p.Name + " by " + FlareLiveRules.KeyOf(p) + ": " + (buzz > 0 ? "+" : string.Empty) + buzz + "% customers for "
                                 + FlareLiveRules.ReviewDays + " days.");
                }
                if (p.Author != FlareAuthor.Player && GoingPublic.Mine(reg) && Settings.SocialMediaNotices && p.Arg <= 2 && FlareRules.MayNotify(_file, day, true))
                {
                    int id = p.Id;
                    Tell(NotificationType.Warning, "reallife_flare_note_review", () => OpenPost(id), "business", p.Name, "stars", p.Arg.ToString());
                }
                if (FlareLiveGame.City(reg) && p.Author == FlareAuthor.Player)
                {
                    BusinessAnswer(p);
                }
            }
            else if (p.Source == FlareSource.Announce)
            {
                int buzz = FlareLiveRules.AnnounceBuzz(p.Reach);
                int days = p.Arg == (int)FlareAnnounce.Event && p.When > 0 ? Math.Max(1, p.When / 24 - day + 1) : FlareLiveRules.AnnounceDays;
                Buzz(p.Place, buzz + (p.Arg == (int)FlareAnnounce.Event ? FlareLiveRules.EventCrowd - 1 : 0), days, day, reg);
                if (p.Arg == (int)FlareAnnounce.Hiring)
                {
                    _hiringDay = day;
                }
            }
        }

        // Talk about a business for some days, written into its customers by business pressure's writer.
        private void Buzz(string place, int percent, int days, int day, BuildingRegistration reg)
        {
            FlareRules.AddBuzz(_file, place, percent, day, days);
            if (FlareBridges.Footfall != null && reg != null)
            {
                FlareBridges.Footfall(reg, false);
            }
        }

        private void BusinessAnswer(FlarePost review)
        {
            int id = _file.NextId++;
            var p = new FlarePost
            {
                Id = id, Author = FlareAuthor.Business, Source = FlareSource.Answer, Arg = (int)(review.Arg >= 4 ? FlareAnswer.Thanks : FlareAnswer.Sorry), ReplyTo = review.Id,
                Place = review.Place, Name = review.Name, Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000, Reach = 200, Likes = FlareRules.Roll(_file.Seed, id, 3) % 20
            };
            FlareLiveRules.SetAt(p, review.At + 40 + FlareRules.Roll(_file.Seed, id, 4) % 200);
            _file.Posts.Add(p);
        }

        private void MentionAnswer(FlarePost p)
        {
            string m = p.Mention;
            if (m == null || m.Length < 3 || (m[0] != 'o' && m[0] != 'b' && m[0] != 't') || m[1] != ':')
            {
                return;
            }
            int id = _file.NextId++;
            var a = new FlarePost
            {
                Id = id, Source = FlareSource.Answer, ReplyTo = p.Id, Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000, Reach = 300, Likes = FlareRules.Roll(_file.Seed, id, 3) % 30
            };
            if (m[0] == 'o')
            {
                int who;
                int.TryParse(m.Substring(2), out who);
                a.Author = FlareAuthor.Official;
                a.Who = FlareRules.Clamp(who, 0, (int)FlareOfficial.Bank);
                a.Arg = (int)FlareAnswer.Mention;
            }
            else
            {
                a.Author = m[0] == 'b' ? FlareAuthor.Brand : FlareAuthor.Tycoon;
                a.Name = m.Substring(2);
                a.Arg = (int)(m[0] == 'b' ? FlareAnswer.Brand : FlareAnswer.Tycoon);
                a.Who = m[0] == 'b' ? -1 : TycoonVoice(a.Name);   // a tycoon answers in their own voice
            }
            FlareLiveRules.SetAt(a, p.At + 30 + FlareRules.Roll(_file.Seed, id, 4) % 90);
            _file.Posts.Add(a);
            ShareChanged(a);
        }
    }
}
