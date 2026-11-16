# GalgameOnline — 联网 galgame 后端

C# (.NET 10) 三服务架构，clean-room 实现。架构借鉴 Lunaria 的分层思路
（SdkServer / GameServer / QueryGateway），代码全部自己写，不含 Lunaria 的
AGPL 代码。客户端用 GalgameMaker Studio 引擎改造（见 `client/`）。

## 服务

| 服务 | 端口 | 职责 |
| --- | --- | --- |
| SdkServer | 5000 | 注册 / 登录 / 会话 token（Argon2id 哈希，1 小时有效）/ 限流（每 IP 每分钟 10 次） |
| GameServer | 5100 | 抽卡 / 货币 / 库存 / 邮件 / 云存档 |
| QueryGateway | 5200 | 游戏服实例注册 / 心跳 / 按负载分配 |

## 抽卡机制

- 5 星基础 0.8%，70 抽起软保底，90 抽硬保底；4 星 5%，10 抽保底
- 50% 概率 UP，歪了下次必出 UP；120 抽必出 UP
- 单抽 160 货币，十连 1600；新号送 1000 免费货币

## 卡池（写死在 GameServer `Services/Banners.cs`）

- 1 号池「星夜邂逅」：UP 101，5 星 [102,103,104]，4 星 [201,202,203,204]，3 星 [301,302,303]
- 2 号池「月下初见」：UP 105，5 星 [102,103,104]，4 星 [201,202,203,205]，3 星 [301,302,304]

物品 ID 由客户端映射到角色 / 卡牌，服务端只认数字。

## 运行

```bash
export PATH="$HOME/.dotnet:$PATH"
cd server
dotnet build
./run-all.sh
```

`run-all.sh` 起三个服务。SdkServer 同时监听 TCP 5000 和 Unix socket
`/tmp/sdk.sock`；GameServer 通过 Unix socket 找 SdkServer 验 token
（沙箱里服务之间的 loopback TCP 被禁，真实主机上走默认
`http://localhost:5000` 即可，把 `GameServer__SdkServerUrl` 改回默认值）。

## API 速查

```
POST /api/sdk/signup   {username, password}
POST /api/sdk/login    {username, password}  -> {token, expiresAt, gameServerUrl}
GET  /api/sdk/session  Authorization: Bearer <token>
GET  /api/sdk/status

GET  /api/game/banners
GET  /api/game/banner/{id}
POST /api/game/gacha/pull   {bannerId, count: 1|10}
GET  /api/game/wallet
GET  /api/game/inventory
GET  /api/game/mail
POST /api/game/mail/{id}/claim
POST /api/game/mail/send    {title, body, attachments}
POST /api/game/save         {slot: 1-3, dataJson}
GET  /api/game/save/{slot}

POST /api/gateway/instances/register  {instanceId, url, port, maxPlayers}
POST /api/gateway/instances/heartbeat {instanceId, players}
POST /api/gateway/allocate
GET  /api/gateway/instances
GET  /api/gateway/status
```

除 `status` 外，GameServer 的接口都要 `Authorization: Bearer <token>`。

## 目录

```
server/
  GalgameOnline.slnx
  src/
    GalgameOnline.Shared/      模型 + 抽卡引擎 + DTO
    GalgameOnline.SdkServer/   账号服务
    GalgameOnline.GameServer/  游戏服务
    GalgameOnline.QueryGateway/ 网关
client/
  galgame-maker/   GalgameMaker Studio（待接入网络层）
ref/
  lunaria/     架构参考（只读，不进构建）
  ruxi-skill/  内容生成参考（只读）
```

## 已验证（2026-11-16）

全解 `dotnet build` 0 错误。三个服务同时跑通：注册 → 登录 → 钱包
（新号 1000）→ 单抽（3 星，保底 90→89）→ 货币不足十连被拒 →
邮件补 2000 → 领邮件 → 十连（9×3 星 + 1×4 星，保底 89→79，
钱包 2840→1240）→ 库存聚合正确 → 云存档上传/下载一致 →
网关注册/分配返回游戏服地址。
