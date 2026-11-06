namespace RealLifeOverhaul
{
    /// <summary>The heavy goods paper: its own pool, so a car paper's questions never fill it. The keys are in HeavyRules, checked offline.</summary>
    internal static class HeavyQuestions
    {
        public static readonly QuestionPool Pool = new QuestionPool(HeavyRules.QuestionKeys, HeavyRules.QuestionGroups);
    }
}
