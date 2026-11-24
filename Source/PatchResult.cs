using System;
using System.Collections.Generic;

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
}
