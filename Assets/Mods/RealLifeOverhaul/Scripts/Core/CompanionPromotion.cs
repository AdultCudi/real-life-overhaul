namespace RealLifeOverhaul
{
    /// <summary>The active-play projection remains unchanged while save handlers restore native promotion.</summary>
    public sealed class CompanionPromotion
    {
        public bool Known;
        public int Own, Total, Percent, Ambient, Interior;
        public float Multiplier;

        public void Remember(bool saving, int own, int total, int percent, int ambient, int interior, float multiplier)
        {
            if (saving)
            {
                return;
            }
            Known = true;
            Own = own;
            Total = total;
            Percent = percent;
            Ambient = ambient;
            Interior = interior;
            Multiplier = multiplier;
        }
    }
}
