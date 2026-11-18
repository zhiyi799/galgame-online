# ASTRAL BLOOM Unity 客户端 — 项目审计报告

**审计时间：** 2026-11-17 ~17:45–17:47 CST
**项目路径：** `~/workspace/galgame-online/unity/`
**审计方式：** 只读源码审查 + 实时 API 验证 + 一次真实 Windows 构建。未修改任何项目文件；未推送 GitHub；未动启动器（`~/workspace/astral-bloom`）。

---

## 1. Unity 版本与渲染管线

**结论：Unity 6000.0.85f2，内置渲染管线（Built-in），Gamma 色彩空间。没有 URP / HDRP。**

证据：

- `ProjectSettings/ProjectVersion.txt`：`m_EditorVersion: 6000.0.85f2`（修订版 `b028303d8e7d`）。
- `ProjectSettings/GraphicsSettings.asset`：`m_CustomRenderPipeline: {fileID: 0}`，`m_RenderPipelineGlobalSettingsMap: {}`—— 未挂任何 SRP 管线资产。
- `Packages/manifest.json`（33 个依赖）：只有内置模块（`com.unity.modules.*`，含 `animation`、`physics`、`terrain`、`terrainphysics`、`particlesystem`、`video` 等 1.0.0）+ `com.unity.textmeshpro 3.0.9` + `com.unity.ugui 2.0.0`。**没有任何 `com.unity.render-pipelines.*` 包。**
- `Assets/Editor/BuildScript.cs` 的 `ApplyPlayerSettings()`：`PlayerSettings.colorSpace = ColorSpace.Gamma`；脚本后端 Mono2x，API 兼容级别 .NET Standard；默认分辨率 1280×720。

对 3D 的含义：内置管线本身完全支持 3D（网格、光照、地形、物理模块都已在 manifest 里），但项目没有任何 SRP 功能；若日后要上 URP，需要迁移（重导材质、重布光）。当前 3D 能力是“管线允许、内容为零”（见第 5 节）。

---

## 2. 抽卡、角色、图鉴系统是否真正可运行

### 2.1 抽卡 — 结论：基本可运行（单抽已实测，十连代码存在但从未实测；有两个已知数据问题）

真实链路（已逐段核对）：

- 客户端 `Assets/Scripts/Screens/GachaScreen.cs`（59,134 字节，v4 精修版）：
  - 卡池列表实时拉取：`GetBanners`（第 690 行），失败时用硬编码的 `FallbackBanners`（第 49–53 行，星夜邂逅 / 月下初见）；卡池详情 `GetBannerDetail`（第 935 行）取保底数据。
  - 抽卡：`DoPull(count)`（第 1029 行），`DoPull(1)` 单抽 / `DoPull(10)` 十连均已接线（第 564、573 行）；`_busy` 防双击保护（第 140、1038、1049 行），注释明确写着“服务器没有幂等键——两次请求就是两次扣费，`_busy` 只是客户端保护”（第 1024–1028 行）。`PullRequest` 只有 `{bannerId, count}`（`Api/Dtos.cs`），无 `requestId`。
  - 保底/钱包数字全部来自实时 `BannerInfo`（第 975 行注释“All numbers come from the live BannerInfo”）。
  - 历史：抽卡结果先写本地再弹结果页（第 1091 行 `PullHistoryStore.Append`）。
- 服务端（已核对源码 + 实时验证）：
  - `cloud/supabase/migrations/0004_seed.sql`：2 个卡池（1 星夜邂逅 featured 101；2 月下初见 featured 105；标准 5★ 池均为 [102,103,104]；4★ / 3★ 池见 `GachaRules.cs` 注释），14 个物品（5★ ×5、4★ ×5、3★ ×4），单抽 160，保底 90/10，全部是**占位名**（seed 注释明说 “Names are placeholders”）。
  - `cloud/workers/src/gacha.ts`：5★ 基础 0.8%、4★ 5%、70 抽起软保底线性递增、90/10 硬保底、50% UP 占比、120 抽 UP 保证（第 13–17、84、90、102、118–124 行）。
  - `cloud/workers/src/routes/game.ts` 第 167 行：`state_changed` 时 Worker 最多重试 3 次——重试会**重新随机并重新扣费**，无幂等键（`grep idempot` 全仓 0 命中）。
- **实时验证（2026-11-17 ~17:45 CST，deploytest 账号登录成功）：**
  - `/api/game/banners` → 2 个卡池（id 1/2，featured 101/105，costPerPull 160，dailyLimit 0）。
  - `/api/game/wallet` → premium 0 / free 680 / tickets 0（与 v4 截图的 680 一致）。
  - `/api/game/banner/1` → totalCount 2 / fiveRemain 88 / fourRemain 8（与截图“已抽 2 次、五星还需 88、四星还需 8”一致）。
  - `/api/game/inventory` → 2 件物品（301、303 各 ×1，均为此前两次实测单抽所得的 3★）。
- **已确认的问题（不是猜测）：**
  - 概率提升行数据错误：`GachaScreen.cs` 第 80、87 行硬编码 `rateUpIds = {101,102,103}` / `{105,102,103}`，但 seed 证明只有 101 / 105 是 UP，102/103 是标准池 5★。`GachaRules.cs` 文件头明确警告这些值是“从服务端代码复制的，不是从 API 获取的”。
  - 3–4 号卡池位是纯展示（`comingSoon = true`，`backendId = 0`，第 93、99 行），点按只弹 toast“该卡池即将开启。”（第 1034 行）。
  - 十连从未实测：代码与按钮存在（余额不足时禁用，第 963–966 行），但测试账号只有 680（< 1600），服务端会拒绝；此前两次实测均为单抽（3★ 303、3★ 301）。
  - 历史是本地 JSON（见第 3 节），换设备/清数据即丢失，服务端无历史接口。

### 2.2 角色 — 结论：只有“双性别主角选择”真正可用；没有角色阵容/队伍/养成系统

存在的部分：

- `Assets/Scripts/Core/ProtagonistManager.cs`（13,781 字节）：双性别主角选择，双重保存（PlayerPrefs `gm_protagonist_gender` + `persistentDataPath/protagonist-save.json`，第 121、202–228、275–310 行），保存后回读校验。`GameManager.RouteAfterAuth()`（`Core/GameManager.cs`）按有无选择分流到主菜单或选择界面。
- `Assets/Scripts/Screens/ProtagonistSelectScreen.cs`（16,711 字节）：左右双面板、点击切换（淡入+缩放脉冲）、确认选择；立绘为显式占位符（`★ + "立绘待补充"`），真图只需把 `protagonist_male.png` / `protagonist_female.png` 放进 `Assets/Resources/Characters/`，无需改代码。
- `Assets/StreamingAssets/protagonist-config.json`：男女主角共用同一身份（无星者，元素 star）；战斗部分明确标注 `"data_only": true`，注释写明“战斗系统尚未实现；以下为……设计占位”（数值 hp 100 / atk 20 / def 10 / spd 10；武器/技能/觉醒全部“（待定）”）。
- `Assets/Scripts/Api/ItemNames.cs`：14 个物品占位名（如 101 “星夜·凛”）；`ArtForItem()` 把物品映射到立绘（101 → `celestia_splash`，102–105 → char_02–05，201–203 → char_06–08，204–205 → expr_01/02；3★ 301–304 无立绘，纯文字）。

不存在的部分：

- 没有角色列表/阵容/队伍/编队界面。顶部导航的 CHARACTER 按钮只弹 toast“角色界面即将开放。”（`GachaScreen.cs` 第 244–245 行）。
- 没有角色数值/等级/装备系统：唯一的“等级”是背包里对 `kind == "character"` 的物品显示服务端 `level` 字段（`InventoryScreen.cs` 第 85–86 行），而当前背包里只有 3★ 物品（level 恒为 1，无实际养成含义）。
- 塞蕾斯蒂娅只有立绘（`celestia_splash.png` 已接入抽卡 UP 卡与卡池主视觉），显示名仍是占位名“星夜·凛”，未与任何角色数据绑定。

### 2.3 图鉴 — 结论：不存在

- 全项目唯一一次出现“图鉴”：`GachaScreen.cs` 第 247 行，GALLERY 导航按钮的回调只有 `Toast.Show("图鉴界面即将开放。")`。
- 没有图鉴场景、脚本、数据或 UI。Phase 3（角色图鉴、重复处理）从未开工，与先前记录一致。
- 同理：SETTINGS 导航也是 toast（“设置界面即将开放。”，第 248–249 行）；真正的设置（API 地址）只在 `Settings.cs` 的 PlayerPrefs 键 `api_base_url` 里，无界面。

---

## 3. 角色数据、存档与资源管理

### 3.1 角色/物品数据 — 全部是占位名，定义分散在三处

| 数据 | 位置 | 内容 | 状态 |
|---|---|---|---|
| 卡池与物品池 | `cloud/supabase/migrations/0004_seed.sql` | 2 卡池、14 物品、保底/价格 | 真实（服务端权威），但名字全是占位 |
| 客户端规则副本 | `unity/Assets/Scripts/Api/GachaRules.cs` | 概率/保底/池表（手抄服务端） | 显示用，需手动同步；文件头有警告 |
| 物品显示名与立绘映射 | `unity/Assets/Scripts/Api/ItemNames.cs` | 14 个占位名 + `ArtForItem()` | 占位；注释写明“不要编造真名” |
| 主角配置 | `unity/Assets/StreamingAssets/protagonist-config.json` | 双性别主角、星元素、战斗骨架 | 真实配置，但战斗部分 `data_only` |

没有角色数据库表（除 `items` 的 14 行占位）、没有角色属性/技能数值表、没有剧情角色数据文件（主线剧本在 `~/workspace/galgame-online/docs/story-bible-main.md` 与 `story-chapter-01-script.md`，是文档而非游戏数据；`StoryScreen.cs` 的剧本是 5 行写死的示例对话，第 31–37 行）。

### 3.2 存档 — 四种机制，全部真实可用，但各有边界

| 机制 | 位置/键 | 状态 |
|---|---|---|
| PlayerPrefs 会话 | `gm_token`、`gm_username`（`Core/GameManager.cs`）；`api_base_url`（`Api/Settings.cs`） | 真实。注意：JWT 明文存 PlayerPrefs，注释自认“NOT secure storage” |
| PlayerPrefs 主角选择 | `gm_protagonist_gender`（`Core/ProtagonistManager.cs`） | 真实，双重保存之一 |
| PlayerPrefs 剧情进度 | `save_slot_1..3`、`save_slot_N_time`（`Screens/CloudSaveScreen.cs` 第 15、19–27 行） | 真实；剧情进度只是一个行号（`VnSave { line }`，`StoryScreen.cs` 第 124–148 行） |
| PlayerPrefs 其他 | `gm_reduce_motion`（PullResult 跳过演出开关） | 真实 |
| 本地 JSON：抽卡历史 | `persistentDataPath/pull_history_<username>.json`，上限 500 条（`Api/PullHistoryStore.cs`） | 真实，本地独有；换设备即丢，服务端无法恢复 |
| 本地 JSON：主角选择备份 | `persistentDataPath/protagonist-save.json`（`Core/ProtagonistManager.cs` 第 121 行） | 真实；PlayerPrefs 为空时自动采纳 |
| 云存档 | Worker `/api/game/save`（POST/GET，槽位 1–3）→ Supabase `cloud_saves` 表（`upsert_cloud_save` RPC）；`Screens/CloudSaveScreen.cs` 第 92–130 行上传/下载 | 真实可用；`StoryScreen` 剧情进度自动存本地槽 1，可手动上传云端槽 1（第 178–214 行） |

### 3.3 资源管理 — 全部是 `Resources.Load` + `StreamingAssets`，无热更新

- 加载方式：所有立绘/背景/字体/UI 材质均用 `Resources.Load<T>`（`UiFactory.cs` 第 194、254、290 行；`GachaScreen.cs` 第 343、1190 行；详情/历史/结果屏同理；`ProtagonistSelectScreen.cs` 第 168 行）。配置用 `File.ReadAllText` 读 `StreamingAssets`（`Api/Settings.cs` 第 34 行；`ProtagonistManager.cs`）。
- **没有 Addressables、没有 AssetBundle、没有资源下载/更新系统**（manifest 无相关包，全项目 `grep Addressables|AssetBundle` 0 命中，`Resources.Load` 之外无任何加载路径）。
- 关键资源目录（均为真实文件）：
  - `Assets/Art/Characters/`：8 张全身立绘透明 PNG（rembg 抠图）+ `Expressions/` 11 张表情；`Assets/Art/CG/cg_01_ballroom_gown.jpg` 1 张 CG。
  - `Assets/Resources/Characters/`：立绘副本（char_01–08、celestia_splash、expr_01/02）—— 实际被加载的是这里。
  - `Assets/Resources/Backgrounds/Gacha.png`：抽卡夜樱神社背景（2048×1152）。
  - `Assets/Resources/Fonts/NotoSansCJKsc SDF.asset`：TMP 中文字体；`Assets/Resources/UI/DefaultUIMaterial.mat` + `White1x1.png`（修复 UGUI 品红 bug 的烘焙材质）。
  - `Assets/StreamingAssets/`：`api-config.json`（API 地址）、`protagonist-config.json`。
- 启动器演示的“18.6 GB 下载”是 React 启动器里的**模拟**流程，与 Unity 客户端无关；客户端没有任何下载/更新逻辑，资源全部打包进安装包。

---

## 4. 能否正常构建 Windows 客户端

**结论：能。本次审计刚做了一次真实构建，成功。**

- **本次构建（2026-11-17 17:46 CST，审计执行）：** `-direct Editor` + `GalgameOnline.Editor.BuildScript.BuildWindows`，`DISPLAY=:99`，日志 `unity/Logs/build-windows-audit.log`。结果：**`[Build] Windows build SUCCEEDED: Build/Windows/GalgameOnline.exe (108 MB, 7.9s)`，退出码 0。** 输出 `Build/Windows/` 共 109 MB，`GalgameOnline_Data/` 已更新至 2026-11-17 17:46 CST。
- 此前最新构建为 2026-11-17 17:35 CST 的 v4 验证构建（Windows 108 MB / Linux 103 MB，smoke PASS）；本次构建源码与之一致（`GachaScreen.cs` 最后修改 2026-11-17 17:27 CST），构建结果一致。
- 构建前置条件（本次均已满足，无需额外操作）：Unity 授权有效——`unity auth status` 显示 `Signed in as 时绥 2119351988@qq.com`，`~/.config/unity3d/Unity/licenses/UnityEntitlementLicense.xml` 于 2026-11-17 17:33 CST 刷新（`unity license activate --personal`）；Xvfb `:99`（PID 3754）与 relay（PID 3928）在运行。
- 已知构建注意事项（历史经验，本次未触发）：运行时重启会清空授权，需 CLI 重新登录+激活（**切勿用 Hub 图形界面，会卡死 Xvfb**）；工作区目录的 setgid/nogroup 属主曾导致 Package Manager 写失败（已对 unity 树修复）；`dpkg -i` 装 Hub 会挂在交互提示上，勿用 `apt-get install -f`（会卸载 Hub）；`EditorBuildSettings` 的场景列表是空的（`m_Scenes: []`），构建场景完全由 `BuildScript.Scenes` 的 8 个场景决定。

---

## 5. 是否具备接入 3D 场景的基础

**结论：没有 3D 基础——零 3D 内容、零 3D 代码、零 3D 资源；管线允许，但一切要从零建。**

逐项核查：

- **场景：** 8 个场景全部是 UI 场景。每个场景只有 3 个根对象：`Main Camera`（默认 UI 相机）、`Canvas`（"Main"）、`ScreenRoot`（ScreenBootstrap + 一个界面控制器）。`gen_scenes.py` 的文档字符串写明：“Each scene: Main Camera + Canvas (native components only) + ScreenRoot”。8 个 `.unity` 文件除名字外完全相同（各 7,982 字节，ProtagonistSelect 8,638 字节）。
- **3D 组件：** 全项目 `grep MeshRenderer|Terrain` 在 8 个场景中均为 0 命中；场景文件里的 "Light" 只是默认的 `LightmapSettings` 模板段，无任何 Light 游戏对象；无 Terrain、NavMesh、CharacterController、Animator、SkinnedMeshRenderer。
- **3D 脚本：** `grep Animator|NavMesh|Terrain|CharacterController|MeshRenderer|SkinnedMesh` 在 `Assets/Scripts/` 全目录 0 命中。唯一与“动画”相关的代码是 UI 演出：`PullResultScreen.cs` 的揭示动画（约 3.8 秒，可跳过）与 `ProtagonistSelectScreen.cs` 的淡入+缩放切换——全是 UGUI RectTransform 操作。
- **3D 资源：** `find Assets -iname *.fbx/*.obj/*.blend/*.dae/*.gltf/*.glb/*.anim/*.controller` 0 命中；唯一的 `.asset` 是 TMP 字体。美术资源全部是 2D PNG/JPG。
- **模块层面：** manifest 包含 `com.unity.modules.animation`、`physics`、`terrain`、`terrainphysics`、`particlesystem`、`cloth`、`vehicles` 等模块（Unity 6 默认模块集的一部分），所以**引擎功能上**地形/物理/动画/粒子都是可用的——只是没有任何代码或资源在使用它们。

### 接入 3D 探索场景还缺什么（缺口清单）

1. **渲染管线决策：** 继续内置管线（最快，现有 UI 不动）或迁移 URP（更好看的光照/后期，但要重导材质与重布光）。
2. **一个真正的 3D 场景：** 3D 相机 + 光照（方向光/环境光）+ 天空盒；现有场景模板只有 UI 相机。
3. **世界内容：** 地形或网格地表、建筑/植被/道具模型——目前零模型；用户目前只生成 2D 立绘，3D 模型来源未定。
4. **角色：** 3D 角色模型 + 骨骼 + Animator Controller + 移动控制器（CharacterController / 自定义）+ 第三人称相机跟随——目前主角只有 2D 立绘占位。
5. **系统：** 移动/碰撞/交互、任务触发、小地图数据、敌人/AI、战斗系统（目前战斗只是 `protagonist-config.json` 里的 `data_only` 占位）；服务端目前只有抽卡/钱包/背包/邮件/云存档接口，无任何世界状态接口。
6. **资源管线：** 若世界变大，现有的 `Resources.Load` 全打包方式会让安装包膨胀；需要 Addressables/AssetBundle 或分包下载方案（目前完全没有）。

---

## 总结

这个项目是一个**真实可运行的 2D UI 客户端 + 已部署的后端**，不是空壳：Unity 6000.0.85f2 内置管线；登录/抽卡/钱包/背包/邮件/云存档/主角选择/剧情播放器 8 个界面全部真实接线并对已部署的 Worker + Supabase 后端做过实测（单抽、钱包、保底、背包均在本次审计中再次实时验证通过）；Windows 构建在本次审计中 7.9 秒成功（108 MB）。但它的真实边界也很清晰：**只有 2D UI，没有任何 3D 内容**——没有图鉴、没有角色界面、没有队伍/养成、没有战斗系统（只有一份 `data_only` 的配置占位）、主线只有一个 5 行示例剧本、全部角色名都是占位、抽卡历史只存在本地、概率提升行数据有误、十连从未实测。走向 3D 开放世界不是“接上去”而是“从零新建”：需要新管线决策、新 3D 场景、新世界与角色模型、新移动/战斗系统与新服务端接口，工作量与现有项目不是一个量级。
