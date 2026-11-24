using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace DMSCharacterEditorSupport
{
    /// <summary>
    /// Character Editor 的界面和设置代码在五十多处 catch 住异常后交给 MessageTool.DebugException，
    /// 而它只在开发者模式下弹一条屏幕消息，从不写日志：编辑器里出的错在 Player.log 里查不到，
    /// 消息过去之后也无从追查。
    ///
    /// 这里在同样的条件（开发者模式）下把异常补写进日志，而且只记录正在编辑失能机关单位时发生的，
    /// CE 在别处的内部小毛病不会被记成错误。同一个异常 5 秒内重复出现只记一次，
    /// 界面每帧重复抛同一个异常也不会刷屏。
    /// </summary>
    public static class Patch_ExceptionLog
    {
        private static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(5);

        private static readonly Dictionary<string, long> Recent = new Dictionary<string, long>();

        public static void Apply(Harmony harmony, PatchResult result)
        {
            CeAccess.TryPatch(harmony, typeof(Patch_ExceptionLog),
                CeAccess.Method(CeAccess.MessageToolType, "DebugException", typeof(Exception)),
                "MessageTool.DebugException", result, postfix: nameof(LogSwallowed));
        }

        // ===== MessageTool.DebugException(Exception e) =====

        public static void LogSwallowed(Exception __0)
        {
            if (__0 == null || !Prefs.DevMode || !DmsPawnScope.IsMech(CeAccess.EditorPawn()))
            {
                return;
            }
            string text = __0.ToString();
            long now = DateTime.UtcNow.Ticks;
            if (Recent.TryGetValue(text, out long last) && now - last < RepeatInterval.Ticks)
            {
                return;
            }
            if (Recent.Count > 64)
            {
                Recent.Clear();
            }
            Recent[text] = now;
            Log.Error("[DMSCharacterEditorSupport] Character Editor swallowed an exception while a DMS unit was being edited:\n" + text);
        }
    }
}
