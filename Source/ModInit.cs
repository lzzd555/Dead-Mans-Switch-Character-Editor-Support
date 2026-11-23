using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace DMSCharacterEditorSupport
{
    [StaticConstructorOnStartup]
    public static class ModInit
    {
        private const string LogPrefix = "[DMSCharacterEditorSupport] ";

        static ModInit()
        {
            var harmony = new Harmony("lzzd.DMS.CharacterEditorSupport");
            PatchResult result = Patch_Selection.Apply(harmony);
            if (result == null)
            {
                // Character Editor 未加载；About 已声明硬依赖，这里不重复提示
                return;
            }

            foreach (KeyValuePair<string, Exception> error in result.Errors)
            {
                Log.Error($"{LogPrefix}Failed to patch {error.Key}:\n{error.Value}");
            }
            if (result.Failed.Count == 0)
            {
                Log.Message($"{LogPrefix}Initialized: patched {result.Patched}/{result.Total} Character Editor target(s).");
            }
            else
            {
                Log.Warning($"{LogPrefix}Character Editor target(s) unavailable: {string.Join(", ", result.Failed)}. "
                    + "This Character Editor build is not supported yet, DMS pawns stay invisible to the editor.");
            }
        }
    }
}
