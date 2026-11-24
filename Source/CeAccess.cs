using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace DMSCharacterEditorSupport
{
    /// <summary>
    /// 对 Character Editor 的反射入口。CE 的类型全是 internal，用到的还是 private 嵌套类，
    /// 没有可用的公开接口，只能按名字定位。查找结果在这里集中缓存；找不到的记入 PatchResult，
    /// 由 ModInit 写日志，不让游戏启动时报错。
    /// </summary>
    public static class CeAccess
    {
        public static Type PosToolType { get; private set; }

        public static Type EditorUiType { get; private set; }

        public static Type BlockBioType { get; private set; }

        public static Type BackstoryToolType { get; private set; }

        public static Type TraitToolType { get; private set; }

        public static Type MessageToolType { get; private set; }

        private static MethodInfo _apiGetter;

        private static MethodInfo _apiPawnGetter;

        private static MethodInfo _apiPawnSetter;

        private static MethodInfo _inStartingScreenGetter;

        /// <summary>定位 CE 的类型与成员。返回 false 表示 Character Editor 未加载。</summary>
        public static bool Init(PatchResult result)
        {
            Type ceType = AccessTools.TypeByName("CharacterEditor.CEditor");
            PosToolType = AccessTools.TypeByName("CharacterEditor.PosTool");
            if (ceType == null && PosToolType == null)
            {
                return false;
            }

            PosToolType = Require(result, PosToolType, "PosTool");
            BackstoryToolType = Require(result, AccessTools.TypeByName("CharacterEditor.BackstoryTool"), "BackstoryTool");
            TraitToolType = Require(result, AccessTools.TypeByName("CharacterEditor.TraitTool"), "TraitTool");
            MessageToolType = Require(result, AccessTools.TypeByName("CharacterEditor.MessageTool"), "MessageTool");
            EditorUiType = Require(result, Inner(ceType, "EditorUI"), "CEditor+EditorUI");
            // 各个板块（BlockBio 等）嵌套在 EditorUI 里面，不是直接挂在 CEditor 下
            BlockBioType = Require(result, Inner(EditorUiType, "BlockBio"), "CEditor+EditorUI+BlockBio");
            if (ceType == null)
            {
                return true;
            }

            _apiGetter = AccessTools.PropertyGetter(ceType, "API");
            _apiPawnGetter = AccessTools.PropertyGetter(ceType, "Pawn");
            _apiPawnSetter = AccessTools.PropertySetter(ceType, "Pawn");
            _inStartingScreenGetter = AccessTools.PropertyGetter(ceType, "InStartingScreen");
            if (_apiGetter == null || _apiPawnGetter == null || _apiPawnSetter == null || _inStartingScreenGetter == null)
            {
                result.Failed.Add("CEditor.API / Pawn / InStartingScreen");
            }
            return true;
        }

        private static Type Inner(Type ceType, string name)
        {
            return ceType == null ? null : AccessTools.Inner(ceType, name);
        }

        private static Type Require(PatchResult result, Type type, string label)
        {
            if (type == null)
            {
                result.Failed.Add(label);
            }
            return type;
        }

        /// <summary>编辑器当前正在编辑的小人。</summary>
        public static Pawn EditorPawn()
        {
            object api = _apiGetter?.Invoke(null, null);
            return api == null ? null : _apiPawnGetter?.Invoke(api, null) as Pawn;
        }

        public static void SetEditorPawn(Pawn pawn)
        {
            object api = _apiGetter?.Invoke(null, null);
            if (api != null)
            {
                _apiPawnSetter?.Invoke(api, new[] { pawn });
            }
        }

        public static bool InStartingScreen()
        {
            if (_inStartingScreenGetter == null)
            {
                // 与 CE 原实现的判据一致（开局页面 = 不在 Playing 状态）
                return Current.ProgramState != ProgramState.Playing;
            }
            return (bool)_inStartingScreenGetter.Invoke(null, null);
        }

        public static MethodInfo Method(Type type, string name, params Type[] parameters)
        {
            return type == null ? null : AccessTools.DeclaredMethod(type, name, parameters);
        }

        /// <summary>
        /// 给 CE 的一个方法挂补丁。prefix / postfix / finalizer 是 patchClass 里静态方法的名字，
        /// 不需要的省略。
        /// </summary>
        public static void TryPatch(Harmony harmony, Type patchClass, MethodInfo target, string label, PatchResult result,
            string prefix = null, string postfix = null, string finalizer = null)
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
                    Hook(patchClass, prefix),
                    Hook(patchClass, postfix),
                    null,
                    Hook(patchClass, finalizer));
                result.Patched++;
            }
            catch (Exception e)
            {
                // 保留原始异常交给 ModInit 记日志；其余目标照常尝试
                result.Failed.Add($"{label}: {e.GetType().Name} {e.Message}");
                result.Errors.Add(new KeyValuePair<string, Exception>(label, e));
            }
        }

        private static HarmonyMethod Hook(Type patchClass, string method)
        {
            return method == null ? null : new HarmonyMethod(patchClass, method);
        }
    }
}
