// Every check of the Company app's new logic and screens, in the order they were built. Run in the full list, and alone by --company.
internal static partial class Program
{
    private static void CompanyAll()
    {
        CompanyAnswers();   // a bonus and a raise of the limit as answers (CompanyAnswerChecks.cs)
        CompanyDown();      // the managers' grades and a question handed down to the executive (CompanyDownChecks.cs)
        CompanyLate();      // late days and the manager who lets go (CompanyLateChecks.cs)
        CompanyLeases();    // leases, the manager's marketing and lease answers (CompanyLeaseChecks.cs)
        CompanyPlans();     // rota plans: in use, the cap, the floor, copies and the save (CompanyPlanChecks.cs)
    }
}
