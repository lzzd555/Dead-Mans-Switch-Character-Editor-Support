using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMSCharacterEditorSupport
{
    public sealed class PatchResult
    {
        public int Total;

        public int Patched;

        /// <summary>没打上的目标：找不到，或 Harmony 报错。</summary>
        public List<string> Failed = new List<string>();

        /// <summary>Harmony 打补丁时抛出的原始异常，键为目标名。</summary>
        public List<KeyValuePair<string, Exception>> Errors = new List<KeyValuePair<string, Exception>>();
    }

    /// <summary>
    /// Character Editor 从"地图当前选中项"确定要编辑的小人时，用的是精确类型比较
    /// GetType() == typeof(Pawn)。原版机械体正好是 Pawn，DMS 一家全部是 Pawn 子类，
    /// 因此被判成"不是小人"：选中 DMS 机械体再打开编辑器，显示的是列表里的别人。
    ///
    /// CE 涉及的类型全是 internal，只能按名字反射定位后手动 Patch：
    ///   - PosTool.FirstPawnFromSelector：编辑器左侧小人列表同步地图选中项；
    ///   - EditorUI.PreselectPawn：打开编辑器时确定初始小人。
    /// 两处都只在 CE 自己没认出来（选中的是 DMS 子类小人）时补上结果，其余情况不插手。
    /// </summary>
    public static class Patch_Selection
    {
        private static MethodInfo _apiGetter;

        private static MethodInfo _apiPawnGetter;

        private static MethodInfo _apiPawnSetter;

        private static MethodInfo _inStartingScreenGetter;

        /// <summary>返回 null 表示 Character Editor 未加载。</summary>
        public static PatchResult Apply(Harmony harmony)
        {
            Type ceType = AccessTools.TypeByName("CharacterEditor.CEditor");
            Type posToolType = AccessTools.TypeByName("CharacterEditor.PosTool");
            if (ceType == null && posToolType == null)
            {
                return null;
            }

            var result = new PatchResult();
            Type editorUiType = ceType == null ? null : AccessTools.Inner(ceType, "EditorUI");
            bool editorUiResolved = editorUiType != null;
            if (editorUiResolved)
            {
                _apiGetter = AccessTools.PropertyGetter(ceType, "API");
                _apiPawnGetter = AccessTools.PropertyGetter(ceType, "Pawn");
                _apiPawnSetter = AccessTools.PropertySetter(ceType, "Pawn");
                _inStartingScreenGetter = AccessTools.PropertyGetter(ceType, "InStartingScreen");
                if (_apiGetter == null || _apiPawnGetter == null || _apiPawnSetter == null || _inStartingScreenGetter == null)
                {
                    result.Failed.Add("CEditor.API / Pawn / InStartingScreen");
                }
            }
            else
            {
                result.Failed.Add("CEditor+EditorUI");
            }

            TryPatch(harmony,
                posToolType == null ? null : AccessTools.DeclaredMethod(posToolType, "FirstPawnFromSelector", new[] { typeof(Selector) }),
                "PosTool.FirstPawnFromSelector", null, nameof(FirstPawnFromSelector_Postfix), result);
            TryPatch(harmony,
                editorUiResolved ? AccessTools.DeclaredMethod(editorUiType, "PreselectPawn", new[] { typeof(Pawn) }) : null,
                "EditorUI.PreselectPawn", nameof(PreselectPawn_Prefix), nameof(PreselectPawn_Postfix), result);
            return result;
        }

        private static void TryPatch(Harmony harmony, MethodInfo target, string label, string prefix, string postfix, PatchResult result)
        {
            result.Total++;
            if (target == null)
            {
                result.Failed.Add(label);
                return;
            }
            try
            {
                harmony.Patch(target,
                    prefix == null ? null : new HarmonyMethod(typeof(Patch_Selection), prefix),
                    postfix == null ? null : new HarmonyMethod(typeof(Patch_Selection), postfix));
                result.Patched++;
            }
            catch (Exception e)
            {
                // 保留原始异常交给 ModInit 记日志；另一个目标照常尝试
                result.Failed.Add($"{label}: {e.GetType().Name} {e.Message}");
                result.Errors.Add(new KeyValuePair<string, Exception>(label, e));
            }
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
            if (__0 != null || InStartingScreen())
            {
                return;
            }
            __0 = SelectedPawn(MapSelectorOrNull()) ?? __0;
        }

        /// <summary>选中的容器里装着 DMS 小人、而原方法没认出来（没设置或设成了别人）时补上。</summary>
        public static void PreselectPawn_Postfix(Pawn __0)
        {
            if (__0 != null || InStartingScreen())
            {
                return;
            }
            Pawn pawn = PawnInSelectedCasket(MapSelectorOrNull());
            object api = pawn == null || _apiGetter == null ? null : _apiGetter.Invoke(null, null);
            if (api == null || _apiPawnSetter == null)
            {
                return;
            }
            if (!ReferenceEquals(_apiPawnGetter?.Invoke(api, null), pawn))
            {
                _apiPawnSetter.Invoke(api, new[] { pawn });
            }
        }

        private static Selector MapSelectorOrNull()
        {
            return Current.Game?.CurrentMap == null ? null : Find.Selector;
        }

        private static bool InStartingScreen()
        {
            if (_inStartingScreenGetter == null)
            {
                // 与 CE 原实现的判据一致（开局页面 = 不在 Playing 状态）
                return Current.ProgramState != ProgramState.Playing;
            }
            return (bool)_inStartingScreenGetter.Invoke(null, null);
        }
    }
}
