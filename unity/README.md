# GalgameOnline — Unity PC 客户端

Unity **6000.0.85f2**，内置渲染管线，UGUI + TextMeshPro。只和 Cloudflare
Workers 后端说话（`../cloud/`），不直连 Supabase、不碰数据库。

## 结构

```
unity/
  Assets/
    Fonts/                 Noto Sans CJK SC（中文渲染用，从系统字体拆出）
    Resources/Fonts/       生成的 TMP 字体资源（NotoSansCJKsc SDF.asset）
    Scenes/                Auth / MainMenu / Gacha / Inventory / Mail / CloudSave / Story
    Scripts/
      Api/                 ApiClient（UnityWebRequest）、DTOs、ItemNames、Settings（API 地址）
      Core/                GameManager（单例）、Toast、ScreenBootstrap、SmokeTestRunner
      UI/                  UiFactory（代码建 UI）
      Screens/             7 个界面控制器
    Editor/BuildScript.cs  字体生成 / Windows 构建 / Linux 构建 / 编译检查
    StreamingAssets/api-config.json   打包进游戏的 API 地址（可改）
  ProjectSettings/         Built-in 管线、Mono 脚本后端、1280x720
```

## 功能

- **登录/注册/离线游玩**：启动时用保存的 token 验 `GET /api/sdk/session`，有效进主菜单，无效回登录。离线跳过登录，在线功能点按提示。
- **主菜单**：钱包（高级/免费/抽卡券）、未读邮件红点、设置（API 地址）。
- **抽卡**：两卡池、UP 五星、保底剩余、单抽/十连、结果列表（星级+物品名）。
- **背包**：角色/道具分组、数量、等级、总数、空状态。
- **邮件**：倒序列表、标题/正文/日期/附件、领取、已领取状态。
- **云存档**：槽 1–3，上传/下载，空槽提示。
- **剧情**：最小 VN 播放器（对话、说话人、点击推进），内置 5 句示例剧本；背景和立绘是占位色块，真实美术以后从 CDN 加载。进度自动存本地槽 1，可云上传/下载。

## API 地址（重要：服务器未部署）

Worker 还没部署。客户端按这个顺序取地址：

1. 游戏内设置（PlayerPrefs `api_base_url`）
2. `StreamingAssets/api-config.json` 里的 `apiBaseUrl`
3. 内置默认 `https://galgame-online-api.<your-subdomain>.workers.dev`（占位）

部署后（`wrangler deploy`）把真实 `*.workers.dev` 地址填进设置，或改
`api-config.json` 重新打包。本地开发用 `http://127.0.0.1:8787`（`wrangler dev`）。

## 构建（Windows PC）

```bash
export DISPLAY=:99   # 沙箱无真显示，先起 Xvfb（见下）
# 生成 TMP 中文字体资源（首次构建前跑一次）
Unity -batchmode -quit -projectPath . \
  -executeMethod GalgameOnline.Editor.BuildScript.GenerateFontAsset
# 构建 Windows x86_64
Unity -batchmode -quit -projectPath . \
  -executeMethod GalgameOnline.Editor.BuildScript.BuildWindows
# 产物：Build/Windows/GalgameOnline.exe
```

编辑器路径（本机）：`~/workspace/tools/unity/Editors/6000.0.85f2-direct/Editor/Unity`。
Windows 构建模块已手动装好（`Editor/Data/PlaybackEngines/WindowsStandaloneSupport`）。
脚本后端 Mono（不需要 Windows IL2CPP 模块）。

## 冒烟测试（Linux 构建跑真机）

```bash
Unity -batchmode -quit -projectPath . \
  -executeMethod GalgameOnline.Editor.BuildScript.BuildLinux
xvfb-run -a ./Build/Linux/GalgameOnline.x86_64 -batchmode -nographics -smoke
```

`-smoke` 让游戏自动走一遍：Auth → 主菜单（离线）→ 抽卡 → 背包 → 邮件 →
云存档 → 剧情（推进 2 句），全程无异常则打印 `[SMOKE] PASS` 并以 0 退出，
有异常则 `[SMOKE] FAIL` 并以 1 退出。Worker 未部署时所有在线调用都会失败，
但必须表现为可读提示而不是崩溃。

## 注意事项

- **许可证**：本沙箱每次运行时重启都会换机器绑定，缓存的 Unity Personal
  许可证随之失效。表现为 `No valid Unity Editor license found`。需用 Hub
  重新登录（OAuth，不用密码/验证码）刷新许可证，见 `~/workspace/hub-browser-capture.sh`。
- **Token 存在 PlayerPrefs**：现在够用，不是安全存储。
- **支付**：用户已明确延期，不做支付 UI/SDK。
- **物品名是占位**：`Assets/Scripts/Api/ItemNames.cs` 里的名字（星夜·凛等）
  照 HTML 客户端抄来，等真实角色/美术定了再改。
- **美术**：用户自己用生图工具做，客户端只有占位色块，不编造、不下载任何美术资源。

## GitHub 推送里的二进制文件（注意）

同步工具只能推 UTF-8 文本文件，以下 23 个二进制文件**不在 GitHub 上**，
只在本地（`unity/Assets/`）：

- `Assets/Fonts/NotoSansCJKsc-Regular.ttf`（16MB，中文字体源文件）
- `Assets/TextMesh Pro/Fonts/LiberationSans.ttf`、`Assets/TextMesh Pro/Sprites/EmojiOne.png`
- `Assets/Art/` 全部 20 个文件（8 张立绘 + 11 张表情差分 + 1 张 CG）

从 GitHub 全新检出后，先把上面这些文件放回对应位置，再跑
`GenerateFontAsset`（需要 `NotoSansCJKsc-Regular.ttf`），否则字体生成会失败。
`Assets/Art/` 的文件目前还没接入场景（占位色块），缺了也不影响编译和运行。
