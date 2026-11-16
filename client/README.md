# GalgameOnline 客户端接入

## 文件

- `galgame-maker/` — GalgameMaker Studio 上游原版（只读参考，不要改）。
- `galgame-online-studio.html` — 改好的 Studio：导出游戏时带联网层。
- `online-layer/` — 改动来源：`game-css.txt` / `game-html.txt` / `game-js.txt`
  三个片段 + `build.py`（从上游原版重新生成改好版的拼接脚本，
  所有 12 处改动都有锚点断言，锚点对不上会直接失败）。

## 改了什么

**Studio（编辑器）**
- 导出视图新增「联网服务」卡片：SdkServer / GameServer 地址输入框，
  默认 `http://127.0.0.1:5000` / `http://127.0.0.1:5100`。
- `generateAndDownloadGame()` 读输入框传给 `buildGameHTML(script, online)`；
  预览（`openPreview`）走默认值。

**导出的游戏（`buildGameHTML` 模板内）**
- `const ONLINE = {sdkUrl, gameUrl}` 配置块写进每个导出的游戏。
- 启动改为登录门：`olBoot()` — 有 token 先验 `GET /api/sdk/session`，
  有效直接进游戏；无 token / 过期显示登录框（登录 / 注册 / 离线游玩）。
  Token 存 `localStorage`（`gm_online_token`）。
- 标题栏菜单新增 🎰 抽卡按钮（离线时点按提示先登录）。
- 抽卡面板：两卡池（名 + UP 五星 + 单抽价 + 保底剩余）、钱包
  （高级/免费/抽卡券）、单抽/十连、结果列表（星级 + 物品名）。
  物品名映射表 `ITEM_NAMES`（101–105、201–205、301–304）在游戏 JS
  顶部，改名只改这张表。
- 存档格 SLOT 1–3 增加 ☁️上传 / ☁️下载：上传把本地存档 blob 发到云端，
  下载取回写进本地槽并自动读取；云端无存档时提示。

其余一律不动：剧本引擎、编辑器、本地存档、离线游玩与原来完全一致。

## 验证（2026-11-16）

- Studio 整段 JS `node --check` 通过；`buildGameHTML` 单独抽出
  `node --check` 通过；导出的游戏整段 JS `node --check` 通过。
- 导出实测：`ov-login` / `ov-gacha` / `ONLINE` / 云存档按钮等 18 个
  标记全部存在；`const ONLINE` 正确渲染；启动改为 `olBoot()`。
- Node  harness（DOM stub + 真实后端）：注册→自动登录→钱包 1000→
  卡池渲染→单抽（3 星，钱包 1000→840）→云存档上传/下载往返→
  空云槽提示→无 token 显示登录→离线游玩，12/12 通过。
- curl 复核（新号）：10 连货币不足被拒
  （`insufficient currency: need 1600, have 1000`）、单抽出 4 星、
  保底 90→89、云存档 slot 2 往返一致。

## 故意没做的事

- 邮件中心：后端有接口，游戏里没加入口（需要时再加）。
- 库存查看：抽到的物品只在抽卡结果里显示，没做独立库存页。
- 没有标题画面：原引擎本来就没有标题画面，登录框就是启动门；
  抽卡按钮放在游戏内菜单栏（而不是标题画面）。
- 没碰 Lunaria 的任何代码；抽卡机制是 clean-room 实现。
