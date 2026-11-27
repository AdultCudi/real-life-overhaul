using System;
using System.IO;
using System.Threading.Tasks;
using BAModAPI;
using RealLifeOverhaul;
using RealLifeOverhaul.SelfTest;
using UnityEngine;

[assembly: RegisterModClass(typeof(SelfTestEntry))]

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// The self-test's one way in: a developer diagnostic that runs a plan of steps in the game with nobody at the keyboard
    /// (Diagnostics/SelfTest, tools/selftest in the private workspace). It is wholly inert unless the trigger file
    /// <c>&lt;data folder&gt;/RealLifeOverhaul/selftest.json</c> exists when the game starts: one File.Exists here, no object,
    /// no hook, no option, no text. The file is read and removed at once, so a plan runs once and a crashed run leaves no
    /// trigger for the next start; a file older than <see cref="SelfTestRules.StaleMinutes"/> minutes is removed unread. Nothing
    /// a save or the network holds can make one. COST: one File.Exists at the game's start; nothing per frame.
    /// </summary>
    [ModEntryOnInitializationLoad]
    public class SelfTestEntry : IModBigAmbitions
    {
        private static readonly FeatureGuard EntryGuard = Guard.For(null, "SelfTestEntry", typeof(SelfTestEntry));

        public string[] RelativeAssetBundlePaths
        {
            get
            {
                try
                {
                    return Array.Empty<string>();
                }
                catch (Exception e)
                {
                    return Guard.Failed(EntryGuard, e, Array.Empty<string>());
                }
            }
        }

        public Task OnLoadAsync(ModContext context)
        {
            try
            {
                SelfTestRules.Verdict verdict = SelfTestRules.Look(Application.persistentDataPath, File.Exists, File.GetLastWriteTimeUtc, DateTime.UtcNow, out string path);
                if (verdict == SelfTestRules.Verdict.Absent)
                {
                    return Task.CompletedTask;
                }
                if (verdict == SelfTestRules.Verdict.Stale)
                {
                    File.Delete(path);
                    context.Logger.Warn("SelfTest: a plan file left behind long ago was removed unread.");
                    return Task.CompletedTask;
                }
                SelfTestRunner.Begin(path, context.Logger);
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                return Guard.Failed(EntryGuard, e, Task.CompletedTask);
            }
        }

        public Task OnUnloadAsync()
        {
            try
            {
                return Task.CompletedTask;   // a plan in progress outlives the scopes: it is its own object on the game's own lifetime
            }
            catch (Exception e)
            {
                return Guard.Failed(EntryGuard, e, Task.CompletedTask);
            }
        }
    }
}
