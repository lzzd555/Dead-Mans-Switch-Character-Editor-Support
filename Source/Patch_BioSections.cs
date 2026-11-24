using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMSCharacterEditorSupport
{
    /// <summary>
    /// Fortified 框架给带 HumanlikeMechExtension 的 DMS 机械体挂了 Pawn_StoryTracker（Synthetic
    /// 的 Armiman / BigSister / Maiden、Synthroid 等，为的是显示体型、头型、发型和装备），
    /// 而 Character Editor 只看 pawn.story 是否为空：只要不为空，就把"经历 / 不能做的事 /
    /// 特性 / 喜爱的颜色"这些只对人类有意义的板块画出来，还允许往机械体里写入背景故事和特性。
    ///
    /// DMS 一家都不是人形种族（intelligence 是 ToolUser），原版生成器从不给它们经历或特性，
    /// 原版机械体在 CE 里也没有这些板块。这里对 DMS 机械体隐藏它们，角色页的版面因此与原版
    /// 机械体完全一致。体型、发型、头型是 DMS 人形机械体本来就有的外观功能，保持不动。
    ///
    /// 技能面板（DrawSkills）同样对 DMS 机械体隐藏：CE 画它时不检查 story 是否为空，没有 story 的
    /// 机械体（原版的和 DMS 的）一画就空引用，所以原版机械体在 CE 里本来就没有技能面板。
    /// 这里直接跳过，DMS 机械体不论有没有 story 都不再报这个错、也都不显示技能。
    /// 原版机械体不经过这些补丁，行为保持 CE 原样。
    /// </summary>
    public static class Patch_BioSections
    {
        public static void Apply(Harmony harmony, PatchResult result)
        {
            Type bio = CeAccess.BlockBioType;
            CeAccess.TryPatch(harmony, typeof(Patch_BioSections),
                CeAccess.Method(bio, "DrawBackstory", typeof(int), typeof(int)),
                "BlockBio.DrawBackstory", result, prefix: nameof(SkipForDmsMech));
            CeAccess.TryPatch(harmony, typeof(Patch_BioSections),
                CeAccess.Method(bio, "DrawIncapableOf", typeof(int), typeof(int)),
                "BlockBio.DrawIncapableOf", result, prefix: nameof(SkipForDmsMech));
            CeAccess.TryPatch(harmony, typeof(Patch_BioSections),
                CeAccess.Method(bio, "DrawTraits", typeof(int), typeof(int), typeof(int)),
                "BlockBio.DrawTraits", result, prefix: nameof(SkipForDmsMech));
            CeAccess.TryPatch(harmony, typeof(Patch_BioSections),
                CeAccess.Method(bio, "DrawFavorite", typeof(int), typeof(int), typeof(int)),
                "BlockBio.DrawFavorite", result, prefix: nameof(SkipForDmsMech));
            CeAccess.TryPatch(harmony, typeof(Patch_BioSections),
                CeAccess.Method(bio, "DrawSkills", typeof(int), typeof(int)),
                "BlockBio.DrawSkills", result, prefix: nameof(SkipForDmsMech));
        }

        // ===== BlockBio.DrawBackstory / DrawIncapableOf / DrawTraits / DrawFavorite / DrawSkills =====

        /// <summary>前缀：当前编辑的是 DMS 机械体时不画原板块。返回 false 跳过原方法。</summary>
        public static bool SkipForDmsMech()
        {
            return !DmsPawnScope.IsMech(CeAccess.EditorPawn());
        }
    }

    /// <summary>
    /// 隐藏板块挡住了界面上的按钮，但 CE 还有不经界面的写入途径（右侧的"随机化"按钮、套用预设）。
    /// CE 里写经历只有 BackstoryTool.SetBackstory 的两个重载，写特性最终都汇到 TraitTool.AddTrait；
    /// 在这三个入口对 DMS 机械体直接跳过，无论从哪里操作，经历和特性都写不进去。
    /// </summary>
    public static class Patch_BioWrites
    {
        public static void Apply(Harmony harmony, PatchResult result)
        {
            Type backstoryTool = CeAccess.BackstoryToolType;
            CeAccess.TryPatch(harmony, typeof(Patch_BioWrites),
                CeAccess.Method(backstoryTool, "SetBackstory", typeof(Pawn), typeof(bool), typeof(bool), typeof(bool), typeof(bool)),
                "BackstoryTool.SetBackstory(Pawn, bool, bool, bool, bool)", result, prefix: nameof(SkipWriteForDmsMech));
            CeAccess.TryPatch(harmony, typeof(Patch_BioWrites),
                CeAccess.Method(backstoryTool, "SetBackstory", typeof(Pawn), typeof(BackstoryDef), typeof(BackstoryDef)),
                "BackstoryTool.SetBackstory(Pawn, BackstoryDef, BackstoryDef)", result, prefix: nameof(SkipWriteForDmsMech));
            CeAccess.TryPatch(harmony, typeof(Patch_BioWrites),
                CeAccess.Method(CeAccess.TraitToolType, "AddTrait", typeof(Pawn), typeof(TraitDef), typeof(TraitDegreeData), typeof(bool), typeof(bool), typeof(Trait)),
                "TraitTool.AddTrait", result, prefix: nameof(SkipWriteForDmsMech));
        }

        /// <summary>前缀：目标小人是 DMS 机械体时不执行原方法。返回 false 跳过。</summary>
        public static bool SkipWriteForDmsMech(Pawn __0)
        {
            return !DmsPawnScope.IsMech(__0);
        }
    }
}
