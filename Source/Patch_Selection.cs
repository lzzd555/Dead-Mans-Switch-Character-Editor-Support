using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMSCharacterEditorSupport
{
    /// <summary>
    /// Character Editor 从"地图当前选中项"确定要编辑的小人时，用的是精确类型比较
    /// GetType() == typeof(Pawn)。原版机械体正好是 Pawn，DMS 一家全部是 Pawn 子类，
    /// 因此被判成"不是小人"：选中 DMS 机械体再打开编辑器，显示的是列表里的别人。
    ///
    /// 两处补丁都只在 CE 自己没认出来（选中的是 DMS 子类小人）时补上结果，其余情况不插手：
    ///   - PosTool.FirstPawnFromSelector：编辑器左侧小人列表同步地图选中项；
    ///   - EditorUI.PreselectPawn：打开编辑器时确定初始小人。
    /// </summary>
    public static class Patch_Selection
    {
        public static void Apply(Harmony harmony, PatchResult result)
        {
            CeAccess.TryPatch(harmony, typeof(Patch_Selection),
                CeAccess.Method(CeAccess.PosToolType, "FirstPawnFromSelector", typeof(Selector)),
                "PosTool.FirstPawnFromSelector", result, postfix: nameof(FirstPawnFromSelector_Postfix));
            CeAccess.TryPatch(harmony, typeof(Patch_Selection),
                CeAccess.Method(CeAccess.EditorUiType, "PreselectPawn", typeof(Pawn)),
                "EditorUI.PreselectPawn", result, prefix: nameof(PreselectPawn_Prefix), postfix: nameof(PreselectPawn_Postfix));
        }

        /// <summary>地图上直接选中的 DMS 小人；其余情况返回 null。</summary>
        public static Pawn SelectedPawn(Selector selector)
        {
            return selector?.FirstSelectedObject is Pawn pawn && DmsPawnScope.IsSupported(pawn) ? pawn : null;
        }

        /// <summary>地图上选中的休眠仓一类容器里装着的 DMS 小人；其余情况返回 null。</summary>
        public static Pawn PawnInSelectedCasket(Selector selector)
        {
            return selector?.FirstSelectedObject is Building_Casket casket
                && casket.ContainedThing is Pawn pawn
                && DmsPawnScope.IsSupported(pawn) ? pawn : null;
        }

        // ===== PosTool.FirstPawnFromSelector(this Selector selector) =====

        public static void FirstPawnFromSelector_Postfix(Selector __0, ref Pawn __result)
        {
            if (__result == null)
            {
                __result = SelectedPawn(__0);
            }
        }

        // ===== EditorUI.PreselectPawn(Pawn pawn) =====

        /// <summary>
        /// 把选中的 DMS 小人当作"显式指定"传进去，走 CE 自己的 API.Pawn = pawn 分支；
        /// 原方法里的"看地图选中项"分支因而不必修补。容器里的情形留给 Postfix：
        /// 原方法会顺手把 CE 自己的休眠仓/坟墓登记到 UIContainers[0]（关闭时据此弹出），
        /// 改写成显式指定反而会跳过这一步。
        /// </summary>
        public static void PreselectPawn_Prefix(ref Pawn __0)
        {
            if (__0 != null || CeAccess.InStartingScreen())
            {
                return;
            }
            __0 = SelectedPawn(MapSelectorOrNull()) ?? __0;
        }

        /// <summary>选中的容器里装着 DMS 小人、而原方法没认出来（没设置或设成了别人）时补上。</summary>
        public static void PreselectPawn_Postfix(Pawn __0)
        {
            if (__0 != null || CeAccess.InStartingScreen())
            {
                return;
            }
            Pawn pawn = PawnInSelectedCasket(MapSelectorOrNull());
            if (pawn != null && !ReferenceEquals(CeAccess.EditorPawn(), pawn))
            {
                CeAccess.SetEditorPawn(pawn);
            }
        }

        private static Selector MapSelectorOrNull()
        {
            return Current.Game?.CurrentMap == null ? null : Find.Selector;
        }
    }
}
