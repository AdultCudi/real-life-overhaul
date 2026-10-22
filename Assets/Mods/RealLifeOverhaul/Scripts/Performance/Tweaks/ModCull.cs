namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's own things in the street that stand far from the player (bus stops, station fronts, signs, boards, kiosks)
    /// are not drawn beyond the game's far band of 200 m, as the game's own lamps and benches are measured
    /// (<see cref="ModCulling"/>, <see cref="CullRules"/>). On by default: the far plane is 300 m, so what goes is small and
    /// far, and nothing but drawing changes. Its timer keeps the culling group on the game's camera and player.
    /// </summary>
    public sealed class ModCull : PerfTweak
    {
        public override string Id => "mod_cull";
        public override string LabelKey => "reallife_perf_mod_cull";
        public override bool DefaultOn => true;
        public override float Interval => 2f;

        protected override void Apply() => ModCulling.Enable(true);

        protected override void Tick(float now) => ModCulling.Keep();

        protected override void Restore() => ModCulling.Enable(false);

        public override string Describe() => ModCulling.Describe();
    }
}
