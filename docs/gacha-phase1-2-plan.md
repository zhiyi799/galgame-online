# Gacha Phase 1–2 实施计划（只读分析，不含代码）

> 分析时间：2026-11-16。注意：`GachaScreen.cs` 在分析期间正被另一 agent 重写，
> 文件从 218 行 → 426 行 → 683 行不断增长；文末读到的版本仍未写完
> （`Start()` 调用的 `LoadBanners()` / `DoPull()` / `ShowDetails()` /
> `ShowHistory()` / `ShowBannerInfo()` 等方法在磁盘上尚不存在，文件尾部缺
> 少闭合大括号）。本计划以**已读到的 API/DTO/服务端规则**为准，
> GachaScreen 的接线一律放到最后一步。

## 1. 现有数据模型（客户端已有）

- **DTO（`unity/Assets/Scripts/Api/Dtos.cs`）**
  - `BannerDto`：`id / name / featuredFiveStar / costPerPull / dailyLimit`。
    注意：**没有** `fiveStarPity / fourStarPity`、没有卡池列表、没有概率、没有起止时间。
  - `BannerInfo`（`/api/game/banner/{id}` 与抽卡响应共用）：
    `bannerId / totalCount / fiveRemain / fourRemain / dailyLeft`。
    `fiveRemain` = 距 5★ 硬保底还剩几抽，`fourRemain` 同理；**没有**
    `sinceFive / sinceFour / guaranteed / featuredSince`。
  - `PullOutcome`：`itemId / rarity / isFeatured / kind`（`kind` 为
    `"character"` 或 `"item"`）。
  - `PullResponse`：`ok / error / pulls[] / banner`（抽卡后的新 `BannerInfo`）。
    **没有钱包余额**——抽卡后必须另调 `GetWallet` 刷新货币显示。
  - `WalletDto`：`premium / free / tickets`。新号初始 `0 / 1000 / 0`
    （`0003_functions.sql` 的 `ensure_wallet`）。
- **API（`Api/ApiClient.cs`）**：`GetBanners / GetBannerDetail / Pull(bannerId, count) /
  GetWallet / GetInventory / GetMail / …`，均为协程 + `ApiResult<T>` 回调；
  401 → `GameManager.HandleUnauthorized()` 回登录页；15 s 超时。
- **会话/本地存储（`Core/GameManager.cs`）**：`DontDestroyOnLoad` 单例；
  只存 PlayerPrefs `gm_token`、`gm_username`（注释自认“NOT secure storage”）；
  `Settings.cs` 另存 `api_base_url`；`CloudSaveScreen` 存 `save_slot_N` /
  `save_slot_N_time`。**没有任何抽卡历史存储。**
- **UI（`UI/UiFactory.cs`）**：`ScreenBase(canvas, title, onBack)`（全屏背景 +
  顶栏 + `← 返回`）、`SceneCanvas()`（必须用它，`FindObjectOfType<Canvas>()`
  会误取到常驻的 ToastCanvas）、`Panel / RoundedPanel / Label / Button /
  InputField / ScrollView / ArtImage / AddBackground / AddSilverBorder /
  Glass() / GlassWhite() / Stretch / Place / ClearChildren`，以及
  `GradientSprite / RadialGlowSprite / MoonDiscSprite / RoundedSprite`、
   ultramarine 配色常量（`DeepUltramarine` 等）、`CjkFont`。
  新界面全部复用这些 helper，不另起 UI 体系。
- **导航**：`GameManager.GoTo(scene)` = `SceneManager.LoadScene`（整场景替换，
  无页面栈、无 overlay 路由）；返回按钮一律 `GoTo(SceneMainMenu)`。
  `MainMenuScreen` 的抽卡入口先 `RequireOnline()` 再 `GoTo(SceneGacha)`。
- **场景**：`Assets/Scenes/` 共 7 个（Auth / MainMenu / Gacha / Inventory /
  Mail / CloudSave / Story）。`Assets/Editor/BuildScript.cs` 的 `Scenes`
  数组**写死**这 7 个；新场景需同时改：`.unity` 文件（Camera + 名为
  `Canvas` 的 Canvas + `ScreenBootstrap`）、`BuildScript.Scenes`、
  `GameManager` 的 `SceneX` 常量、`Core/SmokeTestRunner.cs` 的 `Sequence`。
- **Smoke test（`Core/SmokeTestRunner.cs`）**：`-smoke` 走全场景（离线模式，
  不真的抽卡）；`-smoke -gacha-shot` 只截 Gacha 屏到
  `/tmp/gacha-screen-v2.png`；`-token <jwt>` 可在线截图（token 不打日志）。

## 2. 真实保底 / UP 规则（以服务端代码为准）

来源：`cloud/workers/src/gacha.ts`、同目录 `routes/game.ts`、
`cloud/supabase/migrations/0003_functions.sql`（`do_gacha_pull`）、
`0004_seed.sql`、`0001_schema.sql`（`gacha_state`）、
`server/src/GalgameOnline.Shared/GachaEngine.cs`（C# 原型，TS 版与之一致）。

| 规则 | 代码所写 | 来源 |
|---|---|---|
| 5★ 基础概率 | **0.8%**（`BASE_FIVE_STAR_RATE = 0.008`） | `gacha.ts:14` |
| 5★ 软保底 | 从第 70 抽起线性提升：`sinceFive=70` 时约 5.8%，`80` 时约 55%，`89` 时 100%（`t=(sinceFive-70+1)/20`） | `gacha.ts` `fiveStarRate()` |
| 5★ 硬保底 | **90 抽**（两卡池 `five_star_pity = 90`） | `0004_seed.sql` |
| 4★ 基础概率 | **5%**（`BASE_FOUR_STAR_RATE = 0.05`） | `gacha.ts:16` |
| 4★ 硬保底 | **10 抽**（两卡池 `four_star_pity = 10`） | `0004_seed.sql` |
| 50/50 | 5★ 时 50% 出 UP（`RATE_UP_SHARE = 0.5`）；歪了则 `guaranteed = true`，**下个 5★ 必定 UP** | `gacha.ts` `pullOne()` |
| 120 抽 UP 保底 | `FEATURED_GUARANTEE_PULLS = 120`：距上个 UP 5★ 满 120 抽则该抽必定 5★ 且必定 UP | `gacha.ts:17` |
| 保底归属 | **按（账号，卡池）分别计算**：`gacha_state` 主键 `(user_id, banner_id)` | `0001_schema.sql` |
| 限定 / 常驻是否共享保底 | **不共享**：不同 `banner_id` 就是不同行。当前只有 2 个限定池，无常驻池 | 同上 + `0004_seed.sql` |
| 角色 / 武器池是否共享保底 | **不适用：根本没有武器池**。5★/4★ 全是 `kind="character"`，3★ 全是 `kind="item"` | `gacha.ts`、`0004_seed.sql` |
| 重复抽到 | **无任何重复处理**：`do_gacha_pull` 只把 `inventory.count +1`；无星尘/碎片/返还货币，无首抽标记 | `0003_functions.sql` |
| 抽卡费用 | **160 / 抽，1600 / 十连**（`cost_per_pull = 160`）；先扣 `premium` 再扣 `free`；**抽卡券 `tickets` 从不参与扣费** | `0004_seed.sql`、`gacha.ts` `splitCost()`、`routes/game.ts` |
| 每日上限 | 两卡池 `daily_limit = 0` = **不限**；每日计数按 UTC 日期重置 | `0004_seed.sql`、`gacha.ts` |
| 抽数限制 | 服务端只接受 `count = 1 或 10`，其余报错 | `routes/game.ts` `gachaPull()` |

**与常见假设不符的关键点（UI 文案必须按此写）：**

1. **120 保底几乎永远触发不了、但并非死代码**：无 5★ 的干旱最多 89 抽
   （90 抽硬保底），所以 120 只有在“90 抽硬保底歪了 → 再干旱 29 抽”这条
   路径上才会在第 120 抽强制出 UP 5★。它的作用是把 **UP 之间的最长间隔
   锁死在 120 抽**（而不是 50/50 + 大保底单独算出的 180 抽）。
   UI 应写“120 抽内必出 UP”，不要写成“每 120 抽必出 5★”。
2. **“十连必出★4以上”是真的**：4★ 硬保底 10 抽，且 5★ 也会重置 4★
   计数，所以任意连续 10 抽至少一个 4★/5★。
3. **概率提升栏只有第一个是真的**：服务端只有 `featuredFiveStar`
   一个 UP 位（卡池 1 是 101，卡池 2 是 105）；`GachaScreen` 的
   `rateUpIds = [101,102,103]` 中 102/103 只是标准 5★ 池成员（歪时
   三者均分），**没有任何概率提升**。详情页不得把 102/103 写成 UP。
4. **卡池 1、2 的 4★/3★ 池并不相同**：卡池 1 的 4★ 是 201–204、3★ 是
   301–303；卡池 2 的 4★ 是 201/202/203/205、3★ 是 301/302/304。
   详情页必须按卡池分别列出（数据在 `0004_seed.sql`，客户端没有接口）。
5. **`dailyLeft = 0` 不代表“今日已用完”**：`bannerInfo()` 对
   `dailyLimit = 0`（不限）的卡池固定返回 `dailyLeft = 0`（代码注释自认
   的 quirk）。UI 遇到 `dailyLimit == 0` 应显示“不限”，不要显示“剩余 0 次”。

### 未配置的规则（UI 必须显示“未配置”，不得编造）

- 卡池起止时间 / 剩余天数：`banners` 表**没有起止时间字段**；当前
  `GachaScreen` 的“长期开放”是客户端写死的展示文案，不是服务端数据。
- 4★ 是否有 UP / 4★ UP 概率：无此字段，无此机制。
- 重复获得补偿（星尘/碎片/货币返还）：无。
- 首抽 / NEW 标记：服务端不返回，见 §3。
- 抽卡券抵扣：`tickets` 字段存在但抽卡不用它；无“1 券 = 1 抽”规则。
- 保底继承 / 卡池结束后的保底转移：无此概念（保底按 `banner_id` 永久保存）。
- 概率公示表（每物品单独概率）：服务端只有均匀抽取（`pick()` 在池内均分）。

## 3. 各新界面需要什么

### BannerDetailsScreen（卡池详情，Gacha 页内 overlay）
- 显示：卡池名、UP 5★（`BannerDto.featuredFiveStar`，配 `ItemNames.Get` +
  `ArtForItem` 立绘）、标准 5★ 池 / 4★ 池 / 3★ 池（**客户端写死，抄
  `0004_seed.sql`，分卡池两份**——接口不返回池子）、5★ 0.8% / 4★ 5% /
  软保底 70 / 硬保底 90/10 / 50/50 / 120 UP 保底（**客户端写死常量，
  与 `gacha.ts` 一致**）、当前保底进度（`BannerInfo.fiveRemain /
  fourRemain` 换算：已抽 `sinceFive = 90 - fiveRemain`，前提是客户端
  已知硬保底是 90）、总抽数 `totalCount`、每日上限（`dailyLimit == 0`
  显示“不限”）。
- 起止时间、4★ UP、重复补偿三项显示“未配置”。
- 复用 `UiFactory.ScrollView` + `ShowOverlay` 式 overlay（见 §6 方案选择）。

### PullHistoryScreen（抽卡记录，Gacha 页内 overlay）
- 记录形状（§4）：时间、卡池、物品、星级、是否 UP。
- 筛选：按卡池（当前卡池 / 全部）、按星级（全部 / 5★ / 4★ / 3★）、
  只看 UP（`isFeatured`）。分页：本地列表倒序，每页 20 条，“加载更多”
  按钮（数据量小，无需服务端分页——**本来也没有服务端接口**）。
- 空状态：“暂无抽卡记录（记录仅保存在本机）”。

### PullResultScreen（抽卡结果，Gacha 页内 overlay）
- 单抽：1 张大卡（立绘 + 星级 + 名字 + UP 角标）；十连：10 张卡片网格，
  按星级排序展示（5★ 置顶），可逐张点开放大（可选）。
- **首抽 vs 重复：服务端不告诉客户端**。`PullOutcome` 只有
  `itemId / rarity / isFeatured / kind`，无 `isNew / isDuplicate`。
  判定办法（推荐）：进入 Gacha 页时调一次 `GetInventory`，记下已拥有
  的 `itemId` 集合；抽卡前该 id 不在集合中 → 本次显示 NEW；抽完后把
  新 id 并入集合。备选（较弱）：查本地抽卡历史是否出现过该 id——历史
  被清空/换设备就失效。**不要**用 `inventory.count` 判断（抽完才返回，
  且无法区分“本次之前就有 1 个”和“本次刚抽到第 1 个”之外的歧义——
  实际上抽前快照才是唯一可靠口径）。
- 5★ 结果要醒目区分 `isFeatured`（UP 角标 + 金色）与歪（标准池 5★，
  此时 UI 可顺带提示“下个 5★ 必定 UP”，因为 `guaranteed` 已在服务端置真——
  但注意客户端无法从接口直接读到 `guaranteed`，只能从“本次出了非 UP 5★”
  这一事实推断）。

### 抽卡动画
- **必须**：收到服务端响应**之后**才播放（结果由服务端决定，客户端
  绝不本地先摇）；播放期间禁用两个抽卡按钮；可跳过（点一下直接出结果）；
  动画结束（或跳过）后刷新钱包（`GetWallet`）与保底条（响应里的
  `BannerInfo`）；历史记录在**收到响应的同一时刻**写入（§4），不要等动画播完。
- **必须不做**：本地随机决定稀有度/物品；动画期间可再次点击抽卡；
  用假“保底将至”特效暗示未确认的结果；十连动画逐张揭晓时改变顺序
  （按服务端返回顺序展示）。

## 4. 抽卡历史本地存储（推荐方案）

- **推荐 JSON 文件，不用 PlayerPrefs**：记录会随抽数线性增长，
  PlayerPrefs 不适合放变长列表，且与现有 `gm_token` 等短键混在一起
  难以管理。文件路径：
  `Application.persistentDataPath + "/pull_history_<username>.json"`
  （按账号分文件；`username` 取 `GameManager.Username`，做文件名安全过滤）。
- 记录 JSON 形状：
  ```json
  { "version": 1, "account": "<username>", "records": [
    { "seq": 1, "time": "2026-11-16T08:30:00Z", "bannerId": 1,
      "bannerName": "星夜邂逅", "itemId": 101, "rarity": 5,
      "isFeatured": true, "kind": "character", "pullNo": 1 }
  ] }
  ```
  `seq` 为本文件内自增序号；`pullNo` 为当次十连/单抽内的第几抽（1–10）；
  `time` 为收到响应时的 UTC 时间；`bannerName` 冗余存入（卡池改名后历史仍可读）。
- 写入时机：`Pull` 回调成功后、展示结果前，同步写文件（`File.WriteAllText`），
  写完再播动画；失败则打日志 + Toast（不阻塞结果展示，但要在计划外注明：
  写入失败 = 该次记录丢失，无法从服务端找回）。
- 跨重启：文件随应用数据持久化；上限保留最近 500 条（超了从旧往新删）。
  登出（`GameManager.Logout`）**不删**历史文件（按账号分文件，换号不串）。
- 明确边界：**抽卡历史是纯本地功能**——服务端没有 `pull_history` 表、
  没有查询接口，换设备 / 清除应用数据即丢失；UI 空状态与详情页脚注
  都要写明“记录仅保存在本机”。

## 5. 演示 / 本地 vs 服务端

| 内容 | 归属 |
|---|---|
| 抽卡结果、货币扣减、保底计数、50/50 与大保底状态 | **服务端**（Worker + `do_gacha_pull`，行锁 + 乐观并发重试 3 次） |
| 钱包余额、背包、卡池列表 | **服务端**（`GetWallet / GetInventory / GetBanners`） |
| 抽卡历史 | **本地-only**（§4；无服务端接口） |
| 卡池 3、4（春日絮语 / 星辰之约）、剩余时间“长期开放”、概率提升栏的 102/103、角色名“绫华”、名言文案 | **客户端概念数据**（`GachaScreen.Slots` 写死；后端只有卡池 1、2，且无起止时间） |
| 物品中文名、星级图标、立绘映射 | 客户端（`ItemNames.cs`、`ArtForItem`；服务端只认数字 id） |
| 离线模式 | 无抽卡（`RequireOnline()` 拦截）；历史界面离线可看（读本地文件） |

## 6. 实施顺序与文件

> 原则：**新界面全部先做成 Gacha 场景内的 overlay 面板**（复用
> `GachaScreen` 已有的 `BuildOverlay / ShowOverlay` + `ScrollView` 模式，
> 以及底部已接线的“详情 / 历史记录 / 卡池说明”三个文本按钮），**不新
> 建场景**——这样不用改 `BuildScript.Scenes`、不用加 `.unity` 文件、
> 不用动 `SmokeTestRunner.Sequence`。若之后决定独立成场景，再按 §1
> 的四处清单补加。

1. **数据层（新文件，不动 GachaScreen）**
   - `Assets/Scripts/Api/PullHistoryStore.cs`：记录类型 + 读写/追加/筛选/上限 500。
   - `Assets/Scripts/Api/GachaRules.cs`（新文件）：保底常量（0.8% / 5% / 70 /
     90 / 10 / 50% / 120）与两卡池的池子表（抄 `0004_seed.sql`），供详情页使用。
2. **BannerDetailsScreen（新文件 `Assets/Scripts/Screens/BannerDetailsScreen.cs`）**
   - 纯展示 overlay：构造方法接收 `BannerDto + BannerInfo`；内容来源见 §3。
3. **PullHistoryScreen（新文件 `Assets/Scripts/Screens/PullHistoryScreen.cs`）**
   - 读 `PullHistoryStore`，筛选 + 分页 + 空状态。
4. **PullResultScreen + 动画（新文件 `Assets/Scripts/Screens/PullResultScreen.cs`）**
   - 接收 `PullOutcome[]` + 本次 NEW 判定集合；单抽/十连两种布局；跳过按钮；
     动画只做揭示效果，不做随机。
5. **GachaScreen 接线（最后做，等重写 agent 完成）**
   - 补 `DoPull(count)`：`RequireOnline()` → `_busy` 置真并禁用两按钮 →
     `Api.Pull` → 成功时**先写历史文件**、再开 `PullResultScreen`、
     再 `RefreshWallet()` + 用响应 `banner` 更新保底条 → `_busy` 复位。
     失败（余额不足/429/网络）→ Toast + 复位，不写历史。
   - 补 `LoadBanners()` / `GetBannerDetail` 回填、`ShowDetails()` /
     `ShowHistory()` / `ShowBannerInfo()` 三个入口分别打开上述 overlay。
   - 进入页时调一次 `GetInventory` 建“已拥有”集合（供 NEW 判定）。
6. **验证**：`CompileCheck` → Windows/Linux 构建 → `-smoke` 全场景 →
   `-smoke -gacha-shot -token <jwt>` 在线截图（抽卡按钮与三个入口可见、
   无异常）。离线 smoke 不覆盖抽卡（无服务端）。

## 7. 发现的缺口与风险

1. **`GachaScreen.cs` 分析时未完成**：`Start()` 引用的
   `LoadBanners / RefreshWallet / DoPull / ShowDetails / ShowHistory /
   ShowBannerInfo / UpdatePityDisplay` 等在磁盘版本中不存在，文件尾
   缺闭合括号——**当前文件编译不过**。接线步骤必须等重写 agent 落定后
   再以最终版为准；本计划不假设其中任何方法的签名。
2. **双击 / 重复请求防护目前无法确认**：字段 `_busy` 已声明，但使用它
   的 `DoPull` 尚未写入。服务端**没有幂等键**：两次请求 = 两次扣费、
   两次抽卡（`do_gacha_pull` 的行锁只防并发 double-spend，不防用户
   连点两次）。`DoPull` 必须做到：请求期间 `_busy` + 两个按钮
   `interactable = false`，回调里复位；失败也要复位。
3. **客户端拿不到保底全貌**：`BannerDto` 无保底上限（90/10 需客户端
   写死常量）；`BannerInfo` 无 `guaranteed / featuredSince /
   sinceFive`——“下个 5★ 必定 UP”和“距 120 保底还剩几抽”只能靠
   客户端从抽卡结果推断（见 §3），无法直接展示。若要准确展示，需
   服务端在 `bannerInfo()` 里加这三个字段（Worker + Unity DTO 同步改）。
4. **详情页数据无接口**：卡池构成、概率、保底值全靠客户端常量
   （抄 `0004_seed.sql` 与 `gacha.ts`）；服务端改 seed 后客户端
   必须手动同步，否则详情页与实际抽卡不一致。
5. **抽卡后钱包不同步**：`PullResponse` 不含钱包，忘记调 `GetWallet`
   会导致货币显示停留在抽卡前（已知旧版曾有此问题模式）。
6. **历史是单机数据**：换设备/清数据即丢；且“NEW 判定”依赖的
   拥有集合来自 `GetInventory` 快照，若用户在别处抽卡（另一设备），
   本机快照会误判 NEW——可接受，但要在代码注释里写明。
7. **概念卡池与真实卡池的落差**：界面 4 个卡池位只有前 2 个能抽；
   后 2 个是纯展示（`backendId = 0`，不可点抽卡）。详情/历史入口
   必须以当前选中卡池为准，`comingSoon` 卡池不打开详情。
8. **抽卡券是摆设**：钱包 UI 显示 ★ 券，但抽卡不扣券、也无“用券抽”
   的入口；若之后要支持，需服务端先加 `tickets` 扣费分支（目前
   `splitCost` 只处理 premium/free）。
