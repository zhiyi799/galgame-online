# 主角系统 — 双性别预设主角（ASTRAL BLOOM）

> 状态：已实现并验证（2026-11-17）。Unity 6000.0.85f2，Windows + Linux 构建通过，冒烟测试通过，重启持久化通过。
>
> 核心设计理念：「无论以怎样的模样踏上旅途，你都是那个被星辰记住的人。」

## 1. 设计定位

主角采用《鸣潮》漂泊者模式：

- 玩家在游戏开始时选择**男性主角**或**女性主角**。
- 两位主角拥有各自独立的官方立绘、服装、发型和外貌（用户自己生成立绘，见 §6）。
- 不采用捏脸系统。
- 两位主角属于**同一个故事人物**：相同身份（无星者）、相同能力（听见已经消失的星辰声音）、相同核心力量（星律）、相同过去谜团（三百年前的寂星灾变、星樱计划、世界循环）。
- 主线剧情**不因性别选择而改变**；对话只适配称呼（他 / 她），不存在两套互相矛盾的剧情。
- 主角拥有独立人格、情感、判断能力和剧情表现，不是没有性格的玩家替身。

## 2. 文件一览

### 新增文件

| 文件 | 说明 |
| --- | --- |
| `Assets/StreamingAssets/protagonist-config.json` | 主角数据配置（用户给出的 schema 原样 + 每性别的 `art_resource` / `intro` / `trait` + 共享战斗配置，`data_only: true`） |
| `Assets/Scripts/Core/ProtagonistManager.cs` | 静态管理器：配置加载、选择读写、双份持久化、代词、统一身份 |
| `Assets/Scripts/Screens/ProtagonistSelectScreen.cs` | 主角选择界面（标题、双面板、点击切换、确认选择、占位立绘） |
| `Assets/Scenes/ProtagonistSelect.unity` | 选择界面场景（独立 GUID；ScreenBootstrap GUID 保持不变） |
| `Assets/Resources/Characters/celestia_splash.png` | 塞蕾斯蒂娅抽卡立绘（1048×1080，由用户提供的五星角色立绘裁剪，豆包水印已排除，目视确认无水印） |
| `docs/protagonist-system.md` | 本文档 |
| `docs/protagonist-select-male.png` | 选择界面截图（男主角已选） |
| `docs/protagonist-select-female.png` | 选择界面截图（女主角已选） |
| `docs/protagonist-gacha-celestia.png` | 抽卡界面截图（塞蕾斯蒂娅立绘） |

### 修改文件

| 文件 | 改动 |
| --- | --- |
| `Assets/Scripts/Core/GameManager.cs` | 新增 `SceneProtagonistSelect` 常量与 `RouteAfterAuth()`：有选择 → MainMenu，无选择 → ProtagonistSelect |
| `Assets/Scripts/Screens/AuthScreen.cs` | 3 处 `GoTo(MainMenu)` 改为 `RouteAfterAuth()` |
| `Assets/Scripts/Screens/MainMenuScreen.cs` | 主界面显示「主角：无星者（男/女）」；未选择时自动跳回选择界面（冒烟模式跳过） |
| `Assets/Scripts/Screens/StoryScreen.cs` | 主角发言人显示统一身份「无星者」（不随性别改变） |
| `Assets/Scripts/Screens/GachaScreen.cs` | 1 号卡池立绘改为 `celestia_splash`，名牌改为「塞蕾斯蒂娅」，初始中央立绘改为 `celestia_splash` |
| `Assets/Scripts/Api/ItemNames.cs` | `ArtForItem(101)` 返回 `celestia_splash`；显示名「星夜·凛」**故意未改名**，仍是用户待改的占位名 |
| `Assets/Editor/BuildScript.cs` | `ProtagonistSelect.unity` 加入构建场景列表 |
| `Assets/Scripts/Core/SmokeTestRunner.cs` | 场景序列加入 ProtagonistSelect；新增 `-protagonist-shot` / `-protagonist-test <gender>` / `-protagonist-check <gender>` 模式 |
| `Assets/Scripts/Screens/ProtagonistSelectScreen.cs` | （验证期小修）占位星标 `✦` 改为 `★`——`✦` 在 Noto CJK 字体中缺字，渲染为豆腐框；`★` 正常渲染 |

**未改动**：后端物品/卡池/抽卡机制、Cloudflare Worker、启动器（`~/workspace/astral-bloom/`）、抽卡/背包/邮件/云存档页面、画布层级修复（ScreenBase 背景先于 root、UiFactory.SceneCanvas()、RectMask2D）。

## 3. 流程

```
登录（AuthScreen）
  └─ RouteAfterAuth()
       ├─ 已有选择（ProtagonistManager.HasSelection）→ MainMenu
       └─ 无选择 → ProtagonistSelect
            ├─ 点击男/女面板 → 预览切换（淡入 + 缩放脉冲）
            ├─ 确认选择 → ProtagonistManager.SelectGender() → 双份保存 → MainMenu
            └─ 已有保存的选择进入本界面时，自动预选该性别
```

- 主界面显示「主角：无星者（男）」或「主角：无星者（女）」；未选择时显示「主角：无星者（未选择）」并自动跳回选择界面。
- 剧情系统读取统一身份：`ProtagonistManager.StoryIdentity` 恒为「无星者」。
- 对话称呼适配：`SubjectPronoun`（他 / 她）、`PossessivePronoun`（他的 / 她的）。只有代词随性别变化，没有第二条主线。

## 4. 数据配置

`Assets/StreamingAssets/protagonist-config.json`（用户 schema 原样，另加 `art_resource` / `intro` / `trait` 与共享战斗段）：

```json
{
  "protagonist": {
    "selected_gender": null,
    "male": {
      "id": "protagonist_male", "name": "无星者", "gender": "male",
      "title": "无星者", "element": "star", "story_identity": "无星者",
      "art_resource": "protagonist_male",
      "intro": "能够听见已经消失的星辰声音的少年。沉稳、理性，星律在他手中如星空般安静而深远。",
      "trait": "沉稳 · 理性 · 神秘"
    },
    "female": {
      "id": "protagonist_female", "name": "无星者", "gender": "female",
      "title": "无星者", "element": "star", "story_identity": "无星者",
      "art_resource": "protagonist_female",
      "intro": "能够听见已经消失的星辰声音的少女。清冷、坚韧，星光在她身后轻盈流转。",
      "trait": "清冷 · 坚韧 · 灵动"
    }
  },
  "combat": { "data_only": true, "element": "star", "role": "protagonist", "...": "见文件" }
}
```

- `selected_gender` 在 shipped 配置里恒为 `null`（配置是只读的，实际选择存在玩家存档里，不写回配置文件）。
- 配置缺失或损坏时，`ProtagonistManager` 使用同值的内置后备配置，系统仍可工作。

## 5. 持久化（双份保存，重启不丢失）

`ProtagonistManager.SelectGender(gender)` 同时写两处，写完立即回读验证：

1. **PlayerPrefs**：键 `gm_protagonist_gender`，值 `male` / `female`。
2. **本地 JSON 存档**：`Application.persistentDataPath/protagonist-save.json`，内容 `{"selected_gender":"female"}`。

读取顺序：PlayerPrefs 优先；PlayerPrefs 为空时采用存档文件，并把 PlayerPrefs 从文件恢复。选择与登录状态无关——换账号、退出登录都不会丢失选择。

**禁止事项**：不存在两套主线进度；切换性别不会损坏存档；选择只影响表现层（立绘、动画集、代词），不影响剧情进度。

冒烟测试钩子（`SmokeTestRunner`）：

- `-smoke -protagonist-test -protagonist-gender female`：进入选择界面、确认选择、保存、回读，输出 `protagonist-test PASS/FAIL`。
- `-smoke -protagonist-check -protagonist-gender female`：全新进程，清空缓存后从磁盘重读，输出 `protagonist check PASS/FAIL`。
- `-smoke -protagonist-shot -protagonist-gender male|female`：截取选择界面到 `/tmp/protagonist-select-<gender>.png`。

## 6. 立绘占位（重要：等用户自己生图）

**主角立绘目前是明确占位，不是最终美术。** 选择界面两个面板显示 ★ +「立绘待补充」+「男主角」/「女主角」。用户自己生成角色立绘（「人物等我自己生」），**不要**用 AI 生成风格不一致的角色立绘代替。

换入用户立绘的方法（无需改代码）：

1. 把男主角立绘存为 `Assets/Resources/Characters/protagonist_male.png`，女主角存为 `Assets/Resources/Characters/protagonist_female.png`。
2. 重新构建。选择界面自动显示真实立绘（按 `art_resource` 字段加载）。

设计要求（用户brief）：男主角少年感+青年感、沉稳理性神秘，群青/银白/深蓝星律服装，非现代休闲服；女主角清冷坚韧灵动、少女感+神秘感，同色系，不过度堆叠蝴蝶结/花朵/蕾丝；两人视觉语言统一，但不是同一模型改性别。

### 塞蕾斯蒂娅抽卡立绘（已接入）

用户提供的五星角色三张设计稿（立绘 / 三视图 / 头像细节）中，**立绘用作抽卡立绘**（用户指示「抽卡立绘」）：

- 来源：`docs/celestia-5star-illustration.png`（2048×1152，右下角有「豆包 AI 生成」水印）。
- 处理：裁剪为 `Assets/Resources/Characters/celestia_splash.png`（1048×1080），裁剪区域排除了水印，截图目视确认无水印。
- 接入：1 号卡池（月下樱华）中央立绘与 UP 卡片、物品 101 的立绘均指向 `celestia_splash`；名牌为「塞蕾斯蒂娅」。
- 后端物品/卡池/抽卡机制**未改动**；物品 101 的显示名「星夜·凛」仍是占位名，等用户改名。

## 7. 战斗系统适配（数据-only，未实现）

`protagonist-config.json` 的 `combat` 段与 `ProtagonistManager.Combat` 只是**配置骨架**（`data_only: true`），战斗系统本身尚未实现：

- 男女主角共享：星律属性（star）、定位（protagonist）、基础数值（HP 100 / 攻击 20 / 防御 10 / 速度 10，占位数值）、武器（星律武器，待定）、技能（普攻/技能/爆发，待定）、觉醒（星律觉醒，未解锁）。
- 允许按性别做不同的攻击/待机动作与演出：`ProtagonistManager.AnimationSetForGender(gender)` 返回 `protagonist_male_anims` / `protagonist_female_anims`（目前只是命名钩子，无动画资源）。
- 数值与技能名均为设计占位，待数值策划确定。

## 8. 选择界面文案（与用户 brief 一致）

- 标题：ASTRAL BLOOM
- 副标题：星樱物语
- 选择提示：「请选择与你一同踏上星辰旅途的伙伴。」
- 男主角：无星者（沉稳 · 理性 · 神秘）
- 女主角：无星者（清冷 · 坚韧 · 灵动）
- 确认按钮：确认选择（未选时禁用）
- 底部说明：「首次选择后将保存，再次进入游戏时将直接加载已选择的主角。」
- 核心句：「无论以怎样的模样踏上旅途，你都是那个被星辰记住的人。」

视觉：深群青底色 + 群青面板 + 月光白文字，与全项目群青配色一致；选中面板高亮 + 右上角「已选择」标记，未选中面板变暗。

## 9. 复用的现有模块

- `UiFactory`（Label / Button / ArtImage / SceneCanvas / 背景与边框助手）——选择界面全部用现有 UI 工厂搭建。
- `ScreenBase` 背景先于 root 的层级规则、Toast、GameManager 场景路由。
- `BuildScript` 构建流程（全限定名 `GalgameOnline.Editor.BuildScript.*`）。
- `SmokeTestRunner` 的场景遍历与截图模式。
- 抽卡界面现有布局（v3）与 `ItemNames.ArtForItem` 映射表。

## 10. 验证结果（2026-11-17）

环境：Unity 6000.0.85f2（`-direct` 编辑器），Xvfb :99。许可证曾失效（Editor 报 `No valid Unity Editor license found`，CLI 显示 `License: none active` 但 `Signed in: yes`），用 `unity license activate --personal --accept-eula --non-interactive` 重新激活 Personal 许可证后恢复；未重新走浏览器登录，未启动 Hub GUI。

| 检查 | 结果 |
| --- | --- |
| CompileCheck | 0 错误（26 条 CS0618 过时 API 警告，非错误；其余为 ALSA/FMOD 沙箱噪音） |
| Windows 构建 | **成功**，108 MB，10.9s（`Build/Windows/GalgameOnline.exe`） |
| Linux 构建 | **成功**，103 MB，11.2s（`Build/Linux/GalgameOnline.x86_64`） |
| 冒烟测试 `-smoke` | **[SMOKE] PASS**，8 个场景全部加载（Auth / MainMenu / ProtagonistSelect / Gacha / Inventory / Mail / CloudSave / Story），0 异常，剧情推进 2 行正常 |
| 持久化 Run 1 `-protagonist-test female` | **PASS**：`saved=True readback=female`，存档文件 `~/.config/unity3d/GalgameOnline/GalgameOnline/protagonist-save.json` 内容 `{"selected_gender":"female"}` |
| 持久化 Run 2 `-protagonist-check female`（全新进程） | **PASS**：`saved=female expected=female` |
| 选择界面截图（男女） | 已目视确认：标题/副标题/提示/双面板/已选择标记/确认按钮/底部说明/核心句齐全，无重叠、无裁切；★ 占位星标正常渲染 |
| 抽卡界面截图（塞蕾斯蒂娅） | 已目视确认：中央立绘为塞蕾斯蒂娅，豆包水印已去除；UP 卡片、保底（五星还需 88 抽 / 四星还需 8 抽）、已抽 2 次、钱包 680 均为真实数据；十连因余额不足置灰 |

已知事项：

- Linux player 在冒烟 PASS 后不自动退出（沙箱 Xvfb 已知现象），需按 PID `kill -9`；`timeout` 的 SIGTERM 对其无效。退出码 137 是 kill 造成，不代表测试失败。
- 离线 `-gacha-shot`（无 token）只能截到登录页：Gacha 屏未登录会重定向到 Auth。抽卡截图需用真实 token（测试账号 `deploytest` 登录换取），本次已按此方式取得。
- `PullResultScreen.cs:311` 的装饰星标仍用 `✦`/`✧`（Phase 1+2 旧代码，仅在抽卡结果揭示动画的短暂图层显示）；本次只修了主角选择界面的占位星标，未动该处。
- 主角立绘是占位，战斗系统是数据-only（见 §6、§7）。

## 11. 待用户决定 / 后续工作

- 用户自己生成男女主角立绘后，按 §6 放入 `Assets/Resources/Characters/` 即可显示。
- 战斗系统实现时，按 §7 的共享配置 + `AnimationSetForGender` 钩子接入。
- 物品 101 显示名「星夜·凛」等占位名，等用户改名。
- 抽卡十连未做过真机测试（测试账号余额 680 < 1600），与本次无关，仍是待定项。
