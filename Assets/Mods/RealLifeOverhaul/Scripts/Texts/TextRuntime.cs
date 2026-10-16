namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The runtime files of an RLO Texts pack, pinned: llama.cpp release b11662 for Windows x64 (the Vulkan build, which runs on
    /// the processor too), each file's name and SHA-256. A pack is run only if its runtime folder holds exactly these files
    /// with exactly these contents and no other .exe or .dll (TextPacks.Verify, off the game's thread, once a session).
    /// Written by the packs' build (hashes.py beside make_packs.sh); a new llama.cpp release means a new list. Game-free.
    /// </summary>
    public static class TextRuntime
    {
        public const string Release = "b11662";

        /// <summary>Name, then SHA-256 in lower-case hex, for each file.</summary>
        public static readonly string[] Files =
        {
            "llama-server.exe", "8b6f01dbc8a3750e72e7044e4006864ed38e23df3bb62c7da001efe47ceea33c",
            "llama-server-impl.dll", "b3c0457b7d27405e731556ba3fa4cb3b69305d67011f83c155aeff180b88e826",
            "llama-common.dll", "c583c4d112d9456b600d3619841f6361877e287b057185eb86503fcf67b824fc",
            "llama.dll", "404dab3ca43c687ebd3f0ed63dcaa324aab49a2176d72396a068c3ca41c606dc",
            "ggml.dll", "5c203b69fe548f1d1a8788f7ccdb38db00fce4c597592b47b913f66efd7f5507",
            "ggml-base.dll", "46cd361f761c651254e3f85c31342ee1406e082d3f9338d21fb89d8d6cc35947",
            "ggml-vulkan.dll", "6edab2972420c7efa977f274dd128cb7e30df77cec74004334914ca58878c5c9",
            "libomp.dll", "a12116ba72d1d6820407cf30be23da04ce79d6bb8a71a5ee71759c5a1faa6f1c",
            "mtmd.dll", "527930834a2ba43925d4d077681ea1be95b47b9eca3f7190f03584e475060604",
            "ggml-cpu-alderlake.dll", "46b8b5edfa22a30c79f7188f217e145266c542bd4c90c22edf07adf96ce63a98",
            "ggml-cpu-cannonlake.dll", "ec3ed63db0b612fd404b8703e370433ed82f25f41f58a82529786335aae95357",
            "ggml-cpu-cascadelake.dll", "ab2b2fe8f92a402739969ffaed3e047f2a416073928c14878ca7028807a68100",
            "ggml-cpu-cooperlake.dll", "84a8a3df9ad1827a8b3965405e07eba68255ffa3cd6df4de884990e01267d581",
            "ggml-cpu-haswell.dll", "b2420107209e9826871751310de5956314a53aa7f780834f793dc46ce74e9937",
            "ggml-cpu-icelake.dll", "3731a0807cd7a9d369c4e8ce872bda74cc6da0a62c0d79179ae6863437e3ab77",
            "ggml-cpu-ivybridge.dll", "a9529729237d1502f4d7c554093a61ff5fcd275fa444f699309a75158c4732c3",
            "ggml-cpu-piledriver.dll", "260a3ea669807b028218d3b9f74775aa02fad5cd8d5169bf2b7f177c156ddd41",
            "ggml-cpu-sandybridge.dll", "d24e2f3324cd52989e4492c8ae6bbdbf8e8593bb422b33f9fc166ea4b565efae",
            "ggml-cpu-sapphirerapids.dll", "885aba16aa6d54ad5eef1f57d664588c02308179e24c07687df3a41a7775191f",
            "ggml-cpu-skylakex.dll", "1d1b609e7658297eedad619a7db525f78b8b82985f5307efb1c2a2cd332267de",
            "ggml-cpu-sse42.dll", "bc810ce2cc7c22dcbddcda948820099c8096647a16319870dfd9fddfb9e2c59e",
            "ggml-cpu-x64.dll", "d84a4b029ee34f80bb6cd8ce450ab15521d74e7187ab2f5b98a46814ed01f5ed",
            "ggml-cpu-zen4.dll", "843689f83b53eb170925388f31f7818c945f72c1af2196c4f466dd837e04e77f"
        };
    }
}
