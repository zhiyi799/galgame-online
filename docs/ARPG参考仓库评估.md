# ARPG 参考仓库评估

评估日期：2026-11-25。评估方式：读 README、关键脚本、目录结构、git log、Packages/manifest、ProjectSettings；**未在 Unity 里运行**。三个仓库均为 MIT（已确认），本报告只做研究评估，**未修改主工程，也未修改这三个仓库**。

主工程背景：ASTRAL BLOOM，Unity 6000.0.85f2，BiRP，已验证 ALS 移动；TideSong v2.0 已合入（TeamManager/EchoSystem 为实时战斗层），跑酷的爬墙/跑墙/下落攻击因与 ALS 冲突未合入。

---

## 一、unity-genshin-impact-movement-system（Wafflus）

### 是什么
原神式移动系统的教学复刻，52 个 C# 脚本，1 个场景（MainScene.unity）。状态机 + 浮动胶囊（Floating Capsule）+ 物理移动，ScriptableObject 配角色参数。

实际包含的状态（`Assets/GenshinImpactMovementSystem/Scripts/Characters/Player/StateMachines/Movement/States/`）：
- 地面：Idle / Walk / Run / Sprint / Dash / Stop（轻/中/重）/ Landing（轻/重）/ Roll
- 空中：Jump / Fall / Airborne
- 相机：CameraCursor、CameraZoom（Cinemachine 2.8.4）

### 版本与依赖
- Unity **2020.3.2f1**，BiRP（GraphicsSettings 无自定义管线）。
- 依赖全是 Unity 包：Cinemachine 2.8.4、Input System 1.3.0、ProBuilder 4.5.2、TextMeshPro 3.0.4。无付费插件。
- 自带命名空间，无类名冲突风险。动画来自 Mixamo，地图材质来自 Kenney 原型纹理。

### 红旗
- **最后提交 2022-04-13，已停更约 4 年半**（仅 1 个 release 合并提交）。
- **滑翔和游泳只在 README 里，不在代码里**：README 称复刻了 Moving / Gliding / Swimming，滑翔、游泳两节都打了 `[X]`，但全仓库 grep `glide|swim`（所有文件类型）**零命中**，状态机里也没有 Glide/Swim 状态。README 自己也写了“Released code is only up to date to my last released Tutorial Series video”—— released 的代码落后于 README 的宣称。
- **没有攀爬**：grep `climb|wall|ledge|vault|mantle` 全仓库零命中。README 也从没提过攀爬。

### 可用 vs 参考：参考
代码本身是干净的 BiRP 纯 C#，技术上最容易搬，但**搬了也没用**：它没有爬墙/跑墙/下落攻击，也没有滑翔/游泳；地面移动部分我们已有验证过的 ALS，没必要替换。唯一可借鉴的是状态机拆分方式和 Floating Capsule 做法。

### 与三项开放工作的关系
- **跑酷残留（爬墙/跑墙/下落攻击）：不解决。** 这个仓库没有爬墙实现，也没有墙跑。想补跑酷不能从这里抄，只能参考它的状态机写法自己写。
- **战斗/AbilitySystem：** 无关，本仓库只有移动。
- **声骸/技能配置：** 无关。

---

## 二、GenshinGamePlay（526077247）

### 是什么
参考原神的完整 GamePlay 框架，1941 个 C# 脚本。模块（README 功能概览 + `Assets/Scripts/Code/Game/Component/` 实测）：
- **AbilitySystem**：GAS 风格，Ability / Modifier / Mixin 三层。Mixin 种类很全（`Component/Ability/Mixin/`：DoActionAfterAttack、DoActionBeforeBeAttack、AbsorbByState、AttachToNormalizedTime、DoActionByTick、DoActionOnColliderBox 等）。
- **AI**：知识-决策-控制三层，决策树 + 行为树，BrainModule（感知/目标/威胁/寻路/技能/移动/姿态）。
- **SceneGroup**：机关/关卡系统（舞台-演员-剧本隐喻）。
- **Story 剧情**：Graph 编辑器编排（编辑器在 `Assets/Scripts/Editor/DesignEditor/GraphEditor/`，仓库内自带）。
- **Numeric 数值**：FormulaStringFx 表达式；**Element 元素**：ElementComponent + ElementReactionEvent（元素反应）。
- **Move**：策略模式，ORCA 动态避障、DotRecast / Pathfinding 寻路。
- **Camera**：状态栈相机；**Billboard 血条**；**Camp 阵营**；**UI 框架**；**YooAsset 资源管理**。
- 配置工具链：Excel 导出工具（`Tools/ExcelExport/`，需编译 .sln），Json Graph 编辑器，protobuf/Json 配置加载。

### 版本与依赖
- Unity **2022.3.52f1c1**（国服版后缀），README 要求 Unity 2021.3+。
- **渲染管线是 TaoTie RP（自定义轻量 RP，Forward+/Deferred），不是 BiRP 也不是 URP**：GraphicsSettings 的 `m_CustomRenderPipeline` 指向 `Assets/AssetsPackage/RenderAssets/Tao Tie RP.asset`。Assets 根目录那个 `UniversalRenderPipelineGlobalSettings.asset` 是残留，当前生效的是 TaoTie RP。
- **外部依赖（不在仓库里，需联网拉取）**：`com.taotie.inspector`（GitHub）、`com.taotie.render-pipelines`（GitHub）。YooAsset 已包含在仓库内（`Modules/com.tuyoogame.yooasset/`）。
- TaoTie 框架本体（Entity/Component 生命周期）在仓库内，游戏代码直接用了 `TaoTie` 命名空间（317 个文件引用）。
- 付费插件（README）：SuperScrollView（仓库内**没有**，需另购）、Dynamic Bone（仓库内**有**，`Assets/Scripts/ThirdParty/DynamicBone/`）。
- 体量：AssetsPackage 183M、Scripts 16M、Modules 37M、Tools 9.3M。启动场景 `Assets/AssetsPackage/Scenes/InitScene/Init.unity`。

### 红旗
- **最后提交 2026-11-24，非常活跃**（不是优点也不是缺点，但说明 API 还在变）。
- **整个仓库是一个框架，不是一个库**：热更 DLL（Hotfix + AOT）、YooAsset 资源分包、TaoTie RP、ETTask 异步、DotRecast 寻路全绑在一起。想抽其中一个系统出来，要先把 TaoTie 底座搬过来。
- **渲染管线冲突**：TaoTie RP 与我们 BiRP + AstralToon 完全不兼容，材质/Shader 要全部重做。

### 可用 vs 参考：参考（架构级）
**不能直接用。** 正确用法是读设计：Ability/Modifier/Mixin 三层怎么拆、元素反应怎么做、AI BrainModule 怎么组织、SceneGroup 的机关隐喻。这些是三个仓库里最完整的 ARPG 架构参考，但代码不要往主工程里搬。

### 与三项开放工作的关系
- **跑酷残留：** 未验证到爬墙/跑墙实现（Move 系统是策略模式 + 寻路/避障，README 未提攀爬）。不指望它解决跑酷。
- **战斗/AbilitySystem：最有参考价值。** 它的 Mixin 机制（受击触发、状态触发、按 Tick 触发、按动画归一化时间触发）正是 TideSong 现在缺的东西——TideSong 的技能是硬编码的 `CombatUtil` + 固定数值，没有“状态/触发器/修饰器”这一层。元素反应（ElementReactionEvent）也是我们没有的，可作 Phase 2 元素玩法的设计对照。
- **声骸/技能配置：** 它的配置是 Graph 编辑器 + protobuf/Json + Excel 导出，比 EGamePlay 重。数据驱动的思路可参考，但工具链太重，不适合直接学。

---

## 三、EGamePlay（m969）

### 是什么
基于 Entity-Component 的技能/Buff 战斗框架，3.0 版用 EcsNode 做了 ECS 重构（逻辑与数据分离、业务与视图分离），回合制 demo 已删，只剩 RPG demo（场景 `Assets/GameResources.RpgExample/RpgExample Scene.unity`）。

核心结构（`Assets/App.Model/.../com.model.egameplay/`）：
- **CombatEntity**：CombatEntity / SkillComponent / SkillSystem、SpellComponent / SpellSystem、**BuffComponent / BuffSystem**、AttributeComponent、ActionSystem、BehaviourPoint。
- **Ability**：SkillDefine、AbilityTrigger（11 个文件）、AbilityEffect（18 个文件）、Action/Actions（7 个）。
- **ConfigObject**：AbilityConfig、ExecutionConfig（技能执行体/片段表现）、Other（AbilityManagerObject、AttributeConfigObject）。
- **配置流程**：Excel（`Excel/AbilityConfig.xlsx`、`UnitConfig.xlsx`）→ NPOI 导入 → ScriptableObject 配置 → 运行时挂载到 CombatEntity 施法。

### 版本与依赖
- Unity **2022.3.62f2**（本地工程；README 徽章写 2022.3.53f1）。
- **Odin Inspector：硬性依赖，且不在仓库里。** 65 个文件引用 Sirenix；配置类直接继承 `SerializedScriptableObject`（`AbilityConfigObject.cs:29`、`ExecuteClipData.cs`、`AbilityManagerObject.cs`、`AttributeConfigObject.cs`），`using Sirenix.OdinInspector;` 出现在运行时 Model 代码里。仓库内没有任何 Sirenix DLL。README 移植说明也写明“首先要确保 Odin Inspector 插件已经在工程中”。**没有 Odin，这个仓库编译不过。**
- **DOTween：已包含**（`Assets/Plugins/Demigiant/DOTween/DOTween.dll`，另有 DOTweenPro Examples）。核心代码 11 个文件在用。
- **Animancer：已包含，但只是 Lite 版**（`Assets/Plugins/Animancer/Internal/Animancer.Lite.dll`）；README 写的是 Animancer Pro。动画系统 4 个集成点在用它。
- **NPOI：已包含**（`Assets/Plugins/Editor/npoi/`），Excel 导入可用。
- **EcsNode：已包含**（`App.System/.../com.system.ecsnode` + `App.ThirdParty/EcsNode`）。
- Demo 还捆了免费第三方资源：RPG Character Animation Pack FREE、SuperCharacterController、unity-chan!。

### 红旗
- **最后提交 2026-10-12，近期活跃。** 但 README 明确标了 Work-in-progress。
- **Odin 是最大门槛**：不买 Odin Inspector（Asset Store 付费插件），EGamePlay 的配置系统无法编译；只读设计可以，但代码搬不进主工程。
- Animancer 仓库内是 Lite 不是 Pro，README 与实际不符（小问题）。

### 可用 vs 参考：有条件可用（取决于 Odin）
- **有 Odin：** 可以把 Skill/Buff/Status/AbilityEffect 这一套按 README 的移植清单搬过去（Game.Model、Game.System、Unity.Scripts/EGamePlay、Plugins/Editor/npoi、Excel）。这是三个仓库里唯一“框架级可移植”的。
- **没有 Odin：** 只能参考。最值得抄的是两样东西：① Excel → ScriptableObject 的技能配置管线（NPOI 已包含，模式可以直接学）；② Status/Buff 与 AbilityEffect 的拆分设计。

### 与三项开放工作的关系
- **跑酷残留：** 无关。EGamePlay 是战斗框架，Demo 的角色控制器是第三方 SuperCharacterController，没有爬墙。
- **战斗/AbilitySystem：最直接的补缺。** TideSong 现在缺的正是它有的：Status 状态效果系统、BuffComponent/BuffSystem、技能效果（AbilityEffect）管线、属性系统（AttributeComponent）。我们刚修掉 CombatUtil 的单位 bug，但那只是数值 bug；EGamePlay 给的是“技能-效果-状态”这一整层结构。
- **声骸/技能配置：最合适的参照。** Excel 配表 + ScriptableObject 配置对象，比我们现在 EchoSystem 里硬编码的数值（MainBase、套装加成写死在代码里）更适合长期维护。即使不搬代码，这个“Excel → SO → 运行时挂载”的模式也可以直接照着做（NPOI 已包含在仓库里，Odin 只影响它自带的配置类，不影响我们自己写一套）。

---

## 结论

**排名（按对 ASTRAL BLOOM 当下的有用程度）：EGamePlay > GenshinGamePlay > unity-genshin-impact-movement-system。**

EGamePlay 第一：它正好补我们刚合入的 TideSong 战斗系统最缺的两块——Status/Buff 效果管线和 Excel 驱动的技能/声骸配置，而且 DOTween、Animancer Lite、NPOI、EcsNode 都已包含在仓库里，唯一门槛是 Odin Inspector（付费、未包含，配置类继承 SerializedScriptableObject，缺它编译不过）。即使不买 Odin，它的配置管线模式和效果系统设计也是最值得直接抄的。GenshinGamePlay 第二：Ability/Modifier/Mixin、元素反应、AI BrainModule 是最完整的 ARPG 架构参考，但它是一个绑死 TaoTie RP 自定义管线 + YooAsset 热更的整套框架，与我们 BiRP 栈不兼容，只能读设计、不能搬代码。移动系统第三：已停更 4 年半，README 宣称的滑翔/游泳在代码里不存在，攀爬更从未有过；我们已有验证过的 ALS 移动，它的地面状态机没有增量价值，**三个仓库都解决不了跑酷残留的爬墙/跑墙问题**，那部分只能自己写（或另找专门的攀爬实现）。
