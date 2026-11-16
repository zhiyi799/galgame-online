# ASTRAL BLOOM — 星樱物语 · 官方游戏启动器

3A 级二次元幻想游戏启动器（网页演示版）。群青色是整个世界的环境色：群青夜空、神社、樱花、月光与幻想遗迹交织的日式幻想世界。

> **演示说明**：这是一个前端演示启动器。登录为本地演示（邮箱注册/登录 + Steam 为唯一第三方登录，QQ/微信已移除；任意邮箱均可登录，不会验证或上传真实账号信息）；下载为模拟数据（非真实文件传输）；背景音乐默认静音，未配置任何音频文件。桌面客户端可把 `DownloadPanel` 与 `useAuth` 中的 `// DESKTOP:` 预留接口替换为真实安装 / 下载 / 启动调用。

## 技术栈

- React 19 + TypeScript + Vite 8
- Tailwind CSS 4 + Framer Motion + Lucide React
- Canvas 粒子系统（樱花花瓣 + 闪烁星辰）

## 安装与运行

```bash
cd astral-bloom
npm install
npm run dev        # 开发服务器，默认 http://localhost:5173
npm run build      # 类型检查 + 生产构建，产物在 dist/
npm run preview    # 本地预览生产构建
```

需要 Node.js 18+（推荐 Node 20/22/24）。

## 功能

- **登录页**：群青夜空背景（左上满月 / 中央悬浮幻想塔 / 神社鸟居 / 右侧巨樱 / 石板路灯笼）、原创女主角立绘（右侧回眸，透明分层）、品牌标题、Galgame 风格菜单（GAME START / CONTINUE / CONFIGURATION / GALLERY / EXIT，带图标，GAME START 高亮）、玻璃质感登录面板（WELCOME BACK，邮箱账号 + 登录密码、显示/隐藏密码、记住账号、自动登录、忘记密码、立即注册、ENTER THE WORLD 进入游戏，状态 IDLE / VERIFYING / SUCCESS / FAILED；第三方登录仅 Steam，QQ/微信已移除）。
- **启动器主页**（登录后）：版本与更新公告、最新活动、宣传插画与世界介绍、大型 DOWNLOAD GAME / START GAME 按钮（模拟下载：速度、已下载/总量、进度、暂停/继续/取消）、文件完整性校验、服务器区域选择、启动参数设置。
- **设置面板**：GENERAL / DISPLAY / DOWNLOAD / AUDIO / ABOUT，左侧分类导航 + 右侧内容。
- **动效**：樱花飘落、星辰闪烁、云层漂移、月光脉动、鼠标视差、角色呼吸与轮廓光、按钮高光流动；60 FPS 目标，粒子数量有上限，支持 `prefers-reduced-motion`，切后台时暂停粒子循环。
- **音乐**：背景音乐开关与音量控制（设置 → AUDIO），默认静音，未配置音频文件时保持静音。

## 目录结构

```
src/
  components/
    BackgroundScene/   背景场景（主视觉 + 视差 + 氛围光）
    CharacterDisplay/  女主角立绘（呼吸动画、轮廓光）
    SakuraParticles/   Canvas 粒子（樱花 + 星辰）
    Navbar/            顶部导航（Logo、HOME/NEWS/COMMUNITY/SUPPORT、语言、音乐、设置）
    GameMenu/          Galgame 风格菜单
    LoginPanel/        登录面板（演示登录）
    DownloadPanel/     下载 / 启动面板（模拟下载）
    SettingsModal/     设置弹窗
    StatusBar/         底部状态栏
  pages/
    LoginPage/         登录页
    DashboardPage/     启动器主页
  hooks/
    useAuth.ts         演示登录（本地）
    useSettings.ts     设置（localStorage 持久化）
    useParallax.ts     鼠标视差
  assets/
    backgrounds/       主视觉（town-night.webp，原创生成）
    characters/        女主角透明立绘（heroine.png，原创生成 + 抠图）
  styles/
    theme.ts           设计 Token（群青色系、游戏信息、演示标注）
```

## 设计 Token（群青色系）

| 颜色 | Hex | 用途 |
| --- | --- | --- |
| 深群青 | `#142B67` | 主色 / 背景 |
| 经典群青 | `#244DAB` | 主按钮 |
| 星空蓝 | `#4169C9` | 悬停 / 光效 |
| 月光蓝 | `#7897DB` | 高光 / 点缀 |
| 月白 | `#E5EAF7` | 主要文字 |
| 银白 | `#C9D4E8` | 次要文字 / 细线 |
| 樱花粉 | `#DDAFCB` | 少量点缀 |
| 淡金色 | `#D9CBAA` | 少量点缀 |

视觉占比：群青色约 55%，深空蓝/蓝灰约 20%，月白及银白约 15%，樱花粉与淡金色约 10%。
