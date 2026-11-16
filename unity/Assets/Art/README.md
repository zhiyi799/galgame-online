# 美术资源

用户自己生成的角色立绘 / 表情差分 / CG，已做抠图处理（透明底 PNG），可直接进 Unity。

## 处理方式

- 抠图：rembg `isnet-anime` 模型（动漫专用），去灰底 + 去脚下阴影，透明边裁剪。
- 表情差分：从表情表（`5.png`）按格切分，自动排除底部中文标签。
- CG（带场景背景的）不抠图，原样保存。

## 文件

### Characters/ — 全身立绘（透明底）

| 文件 | 原图 | 内容 |
| --- | --- | --- |
| char_01_white_coat.png | 1.png | 白大衣，灰紫短发 |
| char_02_black_coat.png | 2.png | 黑大衣，酒红内衬 |
| char_03_silver_hair.png | 3.png | 深灰大衣，银白紫发 |
| char_04_long_hair.png | upload.png | 黑大衣，银紫长发 |
| char_05_black_suit.png | 1-2.png | 黑西装，黑长靴 |
| char_06_cyber_black.png | 2-2.png | 黑色科技装，青色光条，背包 |
| char_07_swimsuit.png | 3-2.png | 泳装 + 透明罩衫 |
| char_08_casual.png | upload-2.png | 灰开衫，白衬衫，百褶裙 |

### Characters/Expressions/ — 表情差分（透明底）

主角（银紫长发）11 种表情，从 `5.png` 切分：

| 文件 | 表情 |
| --- | --- |
| expr_01_calm.png | 平静 |
| expr_02_gentle_smile.png | 温柔微笑 |
| expr_03_happy.png | 开心 |
| expr_04_shy_blush.png | 害羞脸红 |
| expr_05_surprised.png | 惊讶 |
| expr_06_angry.png | 生气 |
| expr_07_sad.png | 悲伤 |
| expr_08_crying.png | 哭泣 |
| expr_09_playful_smile.png | 调皮微笑 |
| expr_10_bashful.png | 腼腆 |
| expr_11_closed_eye_smile.png | 闭眼微笑 |

### CG/ — 场景 CG（不抠图）

| 文件 | 原图 | 内容 |
| --- | --- | --- |
| cg_01_ballroom_gown.jpg | 4.jpg | 黑色晚礼服，舞厅水晶灯场景 |

## 原图

原图保留在 `~/workspace/user/media_library/image/`（1.png、2.png、3.png、upload.png、1-2.png、2-2.png、3-2.png、4.jpg、upload-2.png、5.png），不进 Unity 工程。

## 工具

抠图脚本：`~/workspace/.tmp/cutout2.py`（全身）、`~/workspace/.tmp/split_expr2.py`（表情表）。
环境：`~/workspace/.venv-rembg`（rembg + onnxruntime），模型缓存在 `~/.rembg/models/`（isnet-anime、bria-rmbg）。
注意：`/tmp` 只有 512M，pip 安装时要把 `TMPDIR` 和 `PIP_CACHE_DIR` 指到 workspace；第一次跑 rembg 会下载模型（约 1GB），且不要和 Unity 构建同时跑（内存会爆，6GB 上限）。
