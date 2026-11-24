# Dead Man's Switch - Character Editor Support

让 [Character Editor](https://steamcommunity.com/sharedfiles/filedetails/?id=1874644848) 能编辑
《The Dead Man's Switch（失能机关）》及其拓展模组的机械体、合成人：在地图上选中它们再打开
编辑器时，编辑的就是选中的那个单位，而不是列表里的别人；编辑器对它们也不再显示经历、特性
这些只对人类有意义的板块，和对待原版机械体一致。

**目标版本：RimWorld 1.7**（基于本机 1.7.5014 rev1195 程序集编译，Harmony 3.0 / net10；
适配 Character Editor v1.7.1）。

## 结构

```
About/About.xml                             依赖 Harmony + 失能机关本体 + Character Editor
1.7/Assemblies/DMSCharacterEditorSupport.dll
Source/                                     C# 源码 + csproj
```

## 设计

### 根因

Character Editor（下称 CE）从"地图当前选中项"确定要编辑的小人时，用的是精确类型比较
`GetType() == typeof(Pawn)`。原版机械体继承 `BasePawn`，类就是 `Pawn`，所以能编辑；
DMS 一家的机械体/合成人为了加入自己的行为，全部换成了 `Pawn` 的子类
（`Fortified.WeaponUsableMech`、`Fortified.HumanlikeMech`、`DMS.HumanlikeMech` 等），
于是被 CE 判成"不是小人"。

CE 的"殖民者"列表只列人形单位，己方阵营的机械体（原版或 DMS）都不在里面，所以编辑己方
机械体只有"地图上选中再打开编辑器"这一条路——恰好对 DMS 是断的。

### 补丁：只在 CE 没认出来时补上

CE 涉及的类型全是 internal，没有可用的公开接口，只能按名字反射定位后手动 Patch。
以下三处都只处理"选中的是 DMS 子类小人"的情况，其余一律交给 CE 自己：

- **`PosTool.FirstPawnFromSelector`**（Postfix）：编辑器左侧小人列表同步地图选中项。
  原方法返回 null 且选中的是 DMS 小人时，返回它。
- **`EditorUI.PreselectPawn`**（Prefix）：打开编辑器时确定初始小人。没有显式指定小人、
  而地图上直接选中的是 DMS 小人时，把它当作显式指定传进去，走 CE 自己的
  `API.Pawn = pawn` 分支。
- **`EditorUI.PreselectPawn`**（Postfix）：选中的是休眠仓一类容器、里面装着 DMS 小人，
  而原方法没认出来时补上。容器分支留给 Postfix，是因为原方法会顺手把 CE 自己的休眠仓/坟墓
  登记到 `UIContainers[0]`（关闭编辑器时据此弹出），改写成显式指定反而会跳过这一步。

CE 未加载时什么都不做。CE 内部改名导致目标找不到时，只在日志里提示，不影响游戏。

### 机械体：不显示经历 / 特性 / 技能

Fortified 框架给带 `HumanlikeMechExtension` 的 DMS 机械体挂了一个 `Pawn_StoryTracker`
（Synthetic 的 Armiman / BigSister / Maiden、Synthroid 等），为的是显示体型、头型、发型和
装备。CE 只看 `pawn.story` 是否为空，于是把这些只对人类有意义的板块一并画了出来，还允许往
机械体里写入背景故事和特性。

DMS 一家都不是人形种族（`intelligence` 是 ToolUser），原版生成器从不给它们经历或特性，
原版机械体在 CE 里也没有这些板块。所以对 DMS 机械体做两件事，其余不动：

- **隐藏五个板块**（Prefix，跳过原方法）：经历、不能做的事、特性、喜爱的颜色、技能。前缀挂在
  `EditorUI.BlockBio` 的 `DrawBackstory` / `DrawIncapableOf` / `DrawTraits` / `DrawFavorite` /
  `DrawSkills` 上。它们画在固定坐标上，跳过后角色页的版面与原版机械体一致。
- **拦住写入**（Prefix，跳过原方法）：隐藏板块挡住了界面上的按钮，但 CE 还有不经界面的
  写入途径，比如右侧的"随机化"按钮、套用预设。CE 里写经历只有 `BackstoryTool.SetBackstory`
  的两个重载，写特性最终都汇到 `TraitTool.AddTrait`；在这三个入口对 DMS 机械体直接跳过，
  无论从哪里操作，经历和特性都写不进去。

技能面板隐藏的理由和前四个不同：CE 画它时不检查 `story` 是否为空，凡是有技能、没有 story 的
单位（所有机械体）一画就空引用，异常被 CE 吞掉后整个面板画不出来——原版机械体在 CE 里
本来就没有技能面板。这里对 DMS 机械体直接跳过，不论有没有 story 都不再报这个错，也都不显示
技能。原版机械体不经过这些补丁，保持 CE 原样（包括它自己吞掉的那个异常）。

体型、发型、头型是 DMS 人形机械体本来就有的外观功能，保持 CE 原有行为。

### 排错：CE 吞掉的异常会写进日志

CE 在五十多处 `catch` 住异常后交给 `MessageTool.DebugException`，而它只在开发者模式下弹一条
屏幕消息，从不写日志。本模组在同样的条件下把异常补写进日志（`[DMSCharacterEditorSupport]
Character Editor swallowed an exception ...`），并且只记录正在编辑失能机关单位时发生的；
同一个异常 5 秒内不重复。

### 放行范围

只放行失能机关一家，其他模组的 Pawn 子类保持 CE 原有行为：

- 类的命名空间以 `Fortified` / `DMS` 开头（本体、Fortified 框架及沿用其命名空间的拓展）；
- 或种族定义由失能机关系列的模组提供，用来覆盖自带程序集、命名空间各异的第三方拓展。
  判据是模组名含 "Dead Man"，或 packageId 含 `deadmanswitch` / `deadmansswitch`、
  以 `dms.` 开头、含 `.dms.`、或为 `aoba.framework`。

### 与其他补丁的关系

「失能机关系列 - 专用兼容补丁」自带的两个 Character Editor 模块处理的是 CE 新建机械体时的
年龄、可部署炮塔物品，和本模组改的不是同一批方法，可以同时启用。

## 编译

```bash
cd Source && dotnet build -c Release
cp bin/Release/DMSCharacterEditorSupport.dll ../1.7/Assemblies/
```

引用均为本机绝对路径（游戏根目录 + 工坊目录），`Private=false` 不复制引用。
Character Editor 与 Fortified 不作为编译引用，只在运行时反射。

## 使用注意

- 只对失能机关一家的单位生效：打开编辑器时认出它们，并隐藏经历 / 特性 / 技能等板块。
  CE 的小人列表本身不变，原版单位和其他模组的单位不经过任何一处补丁，行为与没装本模组时相同。
- 角色页中栏的"能力"板块不在隐藏范围内：游戏给每个机械体（原版和 DMS 的都算）都发能力
  跟踪器，CE 见到就显示，这是它对所有机械体的一贯行为。
- CE 更新后若日志出现 `Character Editor target(s) unavailable`，说明 CE 内部结构变了，
  本模组此时不生效，需要按新版本重新核对目标。
- 建议与「失能机关系列 - 专用兼容补丁」一起启用，它修掉了 CE 里另外两处 DMS 相关问题。

## 验证状态

- [x] About.xml 语法合法；三个依赖的 packageId 与工坊 id 和本机已安装版本一致
- [x] 编译零警告零错误（net10.0，对 1.7.5014 Assembly-CSharp + Harmony 3.0.0.0）
- [x] 离线验证（CE 启用）：用发布目录里的 DLL，对本机真实的 CharacterEditor v1.7.1.0 程序集
      实际打补丁并执行，76 项全部通过——11 个目标全部挂载、被补丁方法的返回值、放行范围、
      隐藏条件对各类小人的判断（原版单位不隐藏）、写经历/写特性对 DMS 机械体被拦下而其他单位照常执行、
      开局页面与已显式指定小人时不插手
- [x] 离线验证（CE 未加载）：19 项全部通过——不抛异常，不补任何方法
- [x] 游戏内加载（认出 DMS 机械体）：首版实测日志出现自检行，整局无异常
- [x] 游戏内实测（11/11 版，2026-11-24）：加载正常；原先 `DrawSkills` 的空引用报错未再出现；
      DMS 机械体的角色页上仍显示一栏——经核对是"能力"板块：CE 对所有机械体（原版的也一样，
      游戏给每个机械体都发能力跟踪器）都会显示它，属 CE 原有行为，与本模组无关；实测者接受现状
- [ ] 未逐项确认：经历 / 不能做的事 / 特性 / 喜爱的颜色 / 技能各板块在 DMS 机械体上的实际消失
      情况，以及"随机化" / 套用预设时经历和特性的写入拦截（离线验证已覆盖，游戏内未逐项试过）

### 游戏内验证步骤

1. 将本目录（或符号链接）放入 `RimWorld/Mods/`：
   `ln -s "$PWD" "/Users/lzzd/Library/Application Support/Steam/steamapps/common/RimWorld/Mods/"`
2. 启用模组（依赖 Harmony、失能机关本体、Character Editor）
3. 检查日志有 `[DMSCharacterEditorSupport] Initialized: patched 11/11 Character Editor target(s).`
   （macOS 上日志在 `~/Library/Application Support/Ludeon Studios/RimWorld by Ludeon Studios/Logs/Player.log`）
4. 进存档，地图上选中一个己方 DMS 机械体 → 打开 Character Editor → 应直接显示该机械体
5. 对照：选中原版机械体、殖民者 → 行为与没装本模组时一致
6. 编辑 DMS 人形机械体（如 Synthetic 的 Armiman）：角色页不应有经历、不能做的事、特性、
   喜爱的颜色、技能，版面与原版机械体一致；发型、头型、体型按钮仍在
7. 对照：编辑殖民者 → 经历、特性、技能照常显示、可以编辑
8. 编辑任意 DMS 机械体（包括没有 story 的，如 Automatroid）：屏幕上不再出现
   `BlockBio.DrawSkills` 的空引用消息
9. 点右侧"随机化"按钮，或给该机械体套用一个带经历/特性的预设：经历和特性保持为空，无红字
10. 在编辑器里逐个切换标签页、改一项再关闭重开 → 无红字，且修改生效
