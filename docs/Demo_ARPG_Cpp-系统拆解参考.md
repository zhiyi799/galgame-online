# Demo_ARPG_Cpp — UE5 高级动作 RPG 系统拆解参考

来源：https://github.com/Yunyang29/Demo_ARPG_Cpp.git
拆解方式：源码静态分析（克隆 1142 个文件，C++ 共 103 个 .h/.cpp）。未运行、未编译（UE5 工程，本机无 UE）。
拆解时间：2026-11-25 09:25 CST
用途：ASTRAL BLOOM Phase 2 战斗系统参考。只参考架构与机制设计，不抄代码、不抄美术、不抄课程资产。

## 一、总体定位

UE5 高级动作 RPG 框架，基于 Udemy 课程《Unreal Engine 5 C++: Advanced Action RPG》开发，并有独立实现与扩展。**仅限教学与研究用途，资产归原课程及作者所有——不要复制任何资产或课程内容。**

- 引擎：UE 5.5（EngineAssociation 5.5，兼容 UE 5.3+）
- 语言：C++ + Blueprint
- 核心插件：GameplayAbilities（GAS）、MotionWarping、ModelingToolsEditorMode
- 另有联机复制（Multiplayer Replication）

核心特性：轻／重攻击连击、方向性受击、翻滚、格挡与招架；GAS 技能（消耗、冷却、武器专属技能）；敌人 AI（自定义行为树节点 + EQS）；波次生存模式；通知系统、主／暂停／胜负菜单、加载界面；目标锁定系统；数据驱动战斗属性；异步／同步加载。

## 二、架构：基类 + 玩家／敌人特化

全工程按 Base → Player / Enemy 分层，模块化、可扩展：

- 角色：Character_Base → Character_Player / Character_Enemy
- 战斗组件：CombatComponent_Base → CombatComponent_Player / CombatComponent_Enemy
- 技能：GameplayAbility_Base → GameplayAbility_Player / GameplayAbility_Enemy
- 动画：AnimInstance_Base → AnimInstance_Character → AnimInstance_Player，另有 LinkedAnimLayer_Player（链接动画层，上半身／武器层）
- 控制器：PlayerController_Base / AIController_Base
- 游戏模式：GameMode_Base / GameMode_Survival（波次生成）

## 三、GAS 技能系统（重点参考）

- AbilitySystemComponent_Base：统一的 ASC 基类。
- AttributeSet_Base：CurrentHealth / MaxHealth、CurrentRage / MaxRage、AttackPower、DefensePower、DamageTaken。怒气（Rage）是独立资源条，与生命并列。
- GEExeCalc_DamageTaken：自定义伤害执行计算，捕获 Source 的 AttackPower、Target 的 DefensePower 与 DamageTaken，用标准 GAS ExecCalc 模式算伤害。这是 ASTRAL BLOOM 伤害公式的对照点（我方 D = A×M×B×100/(100+DEF)×C×(1−R)，同样是攻击力对防御力的结构）。
- AbilityTask：AbilityTask_ExecuteTaskOnTick（每帧执行任务）、AbilityTask_WaitSpawnEnemies（等待生成敌人，生存模式用）。
- 玩家技能示例：GameplayAbility_PickUpStones（拾取石头）、GameplayAbility_TargetLock（目标锁定，见下）。
- DataAssets：DataAsset_StartUp_Player / DataAsset_StartUp_Enemy（开局授予的技能与效果，数据驱动）；DataAsset_InputConfig（输入配置）。

## 四、战斗系统

- CombatComponent_Base：OnHitTargetActor（命中目标回调）、OnPullFromTargetActor（拉取／交互回调）、ToggleCurrentEquippedWeaponCollision（开关武器碰撞）、ToggleBodyCollision（开关身体碰撞，带伤害类型参数）。武器碰撞只在挥砍帧开启，是近战命中的标准做法。
- CombatComponent_Player：GetPlayerCurEquippedWeaponDamageAtLevel（按等级取当前武器伤害），武器伤害随等级成长。
- 武器：Weapon_Base / Weapon_Player；投射物：Projectile_Base；拾取物：PickUp_Base / Stone_Base。
- MotionWarping 插件已启用：用于攻击前冲、翻身等根运动修正，是战斗手感的关键。
- 战斗宣称包含：轻／重连击、方向性受击反应、翻滚、格挡、招架。具体连击段数与招架判定窗口在 C++ 层未展开（多半在蓝图与动画蒙太奇里），需以蓝图为准。

## 五、目标锁定系统（GAS 技能实现）

GameplayAbility_TargetLock 是一个完整的 GAS 技能，值得对照我方锁定设计：

- ActivateAbility / EndAbility：标准 GAS 生命周期。
- OnTargetLockTick：每帧重瞄准。
- SwitchTarget：按方向标签（左／右）切换目标。
- TryLockOnTarget、GetNearestTargetFromAvailableActors：最近目标选取。
- GetAvailableActorsAroundTarget：取目标左右两侧的候选目标（用于切换方向判断）。
- DrawTargetLockWidget：绘制锁定框 UI。

## 六、敌人 AI

- 自定义行为树节点只有两个 C++ 类：BTService_OrientToTargetActor（持续朝向目标）、BTTask_RotateToFaceTarget（旋转面向目标）。其余 AI 行为（巡逻、追击、攻击选择）多半在蓝图行为树与 EQS 查询里，C++ 层只补了朝向相关的两个节点。
- 敌人角色：Character_Enemy，配 CombatComponent_Enemy。

## 七、UI 与存档

- C++ 层只有一个 Widget_Base，大部分 UI（通知、主菜单、暂停、胜负、加载）应在蓝图 UMG 里。
- SaveGame 目录存在（存档系统），Types 目录存放共享类型与枚举。

## 八、与 ASTRAL BLOOM 的对照

- GAS 架构是 Phase 2 战斗最直接的对照：AttributeSet、自定义伤害 ExecCalc、AbilityTask、开局 DataAsset 这一套，与我方快照／五路聚合／战斗公式是同一类问题的两种实现。C-01 审计时可对照它的分层（Base → Player/Enemy）与我方 Gacha 系统的分层。
- 目标锁定做成 GAS 技能（带每帧重瞄准与左右切换）是可借鉴的模式；我方切换／退场（C-05 属性化退场技能）设计时可参考它的切换方向判定。
- 招架／格挡／翻滚 + 方向性受击是 C-03 招架补完的对照；MotionWarping 的攻击前冲是战斗手感的参考点。
- 生存波次模式（GameMode_Survival + AbilityTask_WaitSpawnEnemies）可作为 C-06 战斗集成验收的测试模式参考。
- 限制：UE5 工程，代码不能进 Unity 项目；课程资产不得复制；连击与招架的具体判定在蓝图层，C++ 拆解看不到完整细节。

## 九、未验证项

- 未在 UE 编辑器中打开或编译，蓝图行为树、动画蒙太奇、UMG 的具体实现未核对。
- 招架判定窗口、连击段数、格挡减伤数值等未在 C++ 层确认。
- 联机复制的具体实现未展开。
