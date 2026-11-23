using System;
using Verse;

namespace DMSCharacterEditorSupport
{
    /// <summary>
    /// 判定一个小人是否属于"失能机关一家"的 Pawn 子类。
    ///
    /// DMS 本体与其拓展的机械体/合成人全部继承 Pawn（Fortified.WeaponUsableMech、
    /// Fortified.HumanlikeMech、DMS.HumanlikeMech 等），原版机械体则恰好是 Pawn 本身。
    /// 两条规则任中其一即放行：
    ///   1. 类的命名空间是 Fortified / DMS 开头（本体、Fortified 框架及沿用其命名空间的拓展）；
    ///   2. 种族定义由失能机关系列的模组提供（覆盖自带程序集、命名空间各异的第三方拓展）。
    /// 其他模组的 Pawn 子类不在范围内，保持 Character Editor 原有行为。
    /// </summary>
    public static class DmsPawnScope
    {
        public static bool IsSupported(object obj)
        {
            if (!(obj is Pawn pawn))
            {
                return false;
            }
            ModContentPack source = pawn.def?.modContentPack;
            return IsSupportedType(pawn.GetType(), source?.Name, source?.PackageId);
        }

        public static bool IsSupportedType(Type type, string sourceModName, string sourceModPackageId)
        {
            if (type == null || type == typeof(Pawn) || !typeof(Pawn).IsAssignableFrom(type))
            {
                // 精确的 Pawn 交给 Character Editor 自己处理
                return false;
            }

            string ns = type.Namespace;
            if (ns == "Fortified" || (ns != null && ns.StartsWith("Fortified.", StringComparison.Ordinal)))
            {
                return true;
            }
            if (ns != null && ns.StartsWith("DMS", StringComparison.Ordinal))
            {
                return true;
            }
            return IsDmsPack(sourceModName, sourceModPackageId);
        }

        private static bool IsDmsPack(string name, string packageId)
        {
            name = name ?? "";
            // PackageId 由游戏统一转成小写。官方 id 写作 DeadManSwitch（无第二个 s），
            // 部分第三方拓展写作 deadmansswitch / dms.* / *.dms.*
            packageId = (packageId ?? "").ToLowerInvariant();
            return name.IndexOf("Dead Man", StringComparison.OrdinalIgnoreCase) >= 0
                || packageId.IndexOf("deadmanswitch", StringComparison.Ordinal) >= 0
                || packageId.IndexOf("deadmansswitch", StringComparison.Ordinal) >= 0
                || packageId.StartsWith("dms.", StringComparison.Ordinal)
                || packageId.IndexOf(".dms.", StringComparison.Ordinal) >= 0
                || packageId == "aoba.framework";
        }
    }
}
