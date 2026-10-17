using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The staff rota planner (Scripts/Economy/Company): the faults found when the folder was cleaned up, each checked against
// a case the old code got wrong. A choice in a save whose rota line could not be read is kept; a fault in a page's
// redraw stops no job; an agreed raise works the sheet out again; a confirmed plan is written only as confirmed, and a
// fill that writes nothing says so; a week half written is put back.
internal static partial class Program
{
    private static void RotaFixes()
    {
        RotaFixState();
    }

    private static void RotaFixState()
    {
        // A save whose rota line is of a version this one cannot read: kept as it is, until the player chooses afresh.
        var data = new Dictionary<string, string> { { RotaStore.Key, "v=9^1:Main Street~12" } };
        var modes = new Dictionary<string, int>();
        bool read = RotaStore.Read(data[RotaStore.Key], modes);
        SaveLines.Unreadable(data, RotaStore.Key, "its version is not one this version of the mod reads");
        data[RotaStore.Key] = "v=1^2:Harbour Road~4";
        SaveLines.PutBack(data);
        Check(!read && SaveLines.IsKept(data, RotaStore.Key) && data[RotaStore.Key] == "v=9^1:Main Street~12", "rota state: a rota line this version cannot read is kept and put back at a save");
        SaveLines.Release(data, RotaStore.Key);
        data[RotaStore.Key] = "v=1^2:Harbour Road~4";
        Check(SaveLines.PutBack(data) == 0 && data[RotaStore.Key] == "v=1^2:Harbour Road~4" && !SaveLines.IsKept(data, RotaStore.Key),
            "rota state: once the player chooses, the choice is what the save keeps, not the line put back over it");
        SaveLines.Release(data, "reallife:nothing_kept");
        SaveLines.Release(null, RotaStore.Key);
        string rota = CompanySource("CompanyDirector.Rota.cs");
        Check(InOrder(CompanyMethod(rota, "public void RotaChoose(string place, int mode)"), "if (_rotaModesUnread)", "SaveLines.Release(Save.modData, RotaStore.Key)",
                "_rotaModesUnread = false", "RotaSave(Save)"),
            "rota state: a choice in a save whose line could not be read lets the kept line go before it is written");

        // An agreed raise is in the wages the sheet is read with, so it is in the stamp the sheet is kept by.
        var staff = new List<StaffFile>
        {
            new StaffFile { Id = "a", Place = "High%20Street~1" }, new StaffFile { Id = "b", Place = "High%20Street~1" }, new StaffFile { Id = "c", Place = "Low%20Road~2" }
        };
        int before = StaffRules.RaisesAt(staff, "High%20Street~1");
        staff[2].Raise = 150;
        int elsewhere = StaffRules.RaisesAt(staff, "High%20Street~1");
        staff[1].Raise = 150;
        int raised = StaffRules.RaisesAt(staff, "High%20Street~1");
        Check(elsewhere == before && raised != before && raised == StaffRules.RaisesAt(staff, "High%20Street~1") && StaffRules.RaisesAt(null, "x") == StaffRules.RaisesAt(staff, "nowhere"),
            "rota state: a raise agreed at a business changes its figure, one at another business does not, and the same raises give the same figure");
        Check(CompanyMethod(rota, "private int RotaDays(RotaPlace place)").Contains("StaffRules.RaisesAt(_state.Staff, place.Place)") && CompanyMethod(rota, "private int RotaDays(RotaPlace place)").Contains("raises << 2"),
            "rota state: the raises are mixed into the stamp's extra, clear of its two low bits (the permit is read from them)");

        // The job's coroutine always lets the next start; a page that fails to redraw ends no job.
        string jobs = CompanySource("CompanyDirector.RotaJobs.cs");
        Check(InOrder(CompanyMethod(jobs, "private IEnumerator RotaWork()"), "try", "while (RotaSlice())", "finally", "_rotaWorking = false"),
            "rota state: however the planner's coroutine ends, a fault included, the next job starts it again");
        Check(InOrder(CompanyMethod(jobs, "private bool RotaSlice()"), "if (done)", "try", "_view.Changed();", "catch (Exception e)"),
            "rota state: a fault while the page is written again is caught where the job ends, not by the coroutine");

        // A confirmed plan is written only as it was confirmed; a fill that writes nothing says so.
        string fill = CompanySource("CompanyDirector.RotaFill.cs");
        Check(InOrder(CompanyMethod(fill, "public void FillRota(string place, int rung)"), "long stamp = sheet.Stamp", "RotaConfirmed(place, can != null ? JobFillHours : kind, can != null ? 1 : -1, stamp)",
                "RotaConfirmed(place, kind, -1, stamp)", "RotaConfirmed(place, kind, rung, stamp)")
              && CompanyMethod(fill, "private void RotaConfirmed(").Contains("RotaAsk(place, kind, false, rung, stamp)"),
            "rota state: what is put to the player is remembered with the job it starts");
        string step = CompanyMethod(jobs, "private bool RotaStep(RotaJob job)");
        Check(InOrder(step, "default:", "if (job.Stamp != 0 && sheet.Stamp != job.Stamp)", "return RotaUnwritten(", "if (job.Kind == JobFillHours && rung == null)", "return RotaUnwritten(",
                "return RotaWrite(", "|| RotaUnwritten("),
            "rota state: a plan worked out again after the confirm is not written unless it is the one confirmed, and a write that cannot be made is said");
        Check(InOrder(CompanyMethod(jobs, "private bool RotaToFill(RotaJob job, RotaSheet sheet)"), "if (job.Kind == JobShow)", "return RotaUnwritten("),
            "rota state: a fill refused after it was asked for is said, not dropped");
        Check(InOrder(CompanyMethod(jobs, "private bool RotaUnwritten("), "Logger?.Info(", "if (!job.Test)", "\"reallife_rota_changed\""), "rota state: a fill that writes nothing is in the log and in a notice");

        // A fault in the middle of a write puts the week kept before it back.
        Check(InOrder(CompanyMethod(fill, "private bool RotaWrite("), "_rotaWriting = place;", "RotaGame.Write(", "_rotaWriting = null;")
              && InOrder(CompanyMethod(jobs, "private bool RotaSlice()"), "catch (Exception e)", "RotaFault(", "if (_rotaWriting == job.Place)", "RotaRescue(job.Place)")
              && InOrder(CompanyMethod(jobs, "private void RotaRescue(string place)"), "RotaPutBack(place)", "\"reallife_rota_note_back\"", "catch (Exception e)"),
            "rota state: a week half written when the planner stops on a fault is put back as it was, and the player told");
    }
}
