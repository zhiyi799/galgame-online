#!/usr/bin/env python3
"""ASTRAL BLOOM 高程图 -> Unity 高度图管线.

把 AI 生成的 2D 高程分层图（6 个色带）转成 Unity 可导入的 16 位灰度高度图.

诚实前提:
- 高程图是设计参考值,不是实测数据;从色带反推的高度只是近似值.
- 6 个色带是阶梯,必须高斯平滑去阶梯感.
- 图上的文字/道路/等高线/建筑都不是地形:只匹配 6 个色带颜色,
  匹配不上的像素用最近已知点插值补,不把文字和道路当成地形.

输入: docs/*.webp 高程图
输出: <key>_513.raw (16 位小端,行主序,第 0 行 = 南 = 图片底行,已做上下翻转)
      <key>_preview.png (色带着色预览,北在上,供人工核对)
      <key>_meta.json (映射表/统计)

色带定义:每张图 6 个目标 RGB(从图例主色采样)+ 对应高程(米).
高度归一化: h_norm = band_height / region_max; Unity 地形高度 = region_max 米.
采样网格:默认 513x513(可调).
平滑:scipy 高斯滤波,sigma 以网格像素为单位(默认 6,可调).

用法:
  python3 heightmap.py baita [--sigma 6] [--grid 513] [--outdir DIR]
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

# ---------------------------------------------------------------- 配置
# 色带顺序:深蓝(最低) -> 浅蓝 -> 绿 -> 黄 -> 橙 -> 红(最高)
CONFIG = {
    "baita": {
        "file": "astral-bloom-baita-elevation-fixed.webp",
        "bands": [  # (R,G,B, height_m)
            (24, 104, 168, 0),
            (120, 184, 208, 50),
            (105, 175, 95, 120),
            (240, 192, 64, 200),
            (240, 144, 64, 300),
            (224, 96, 64, 420),
        ],
        "max_m": 420,
        "crop": (0.02, 0.10, 0.98, 0.92),
        # A 标注深蓝底框会误判成 0m 河谷,强制插值;图例色块很小可忽略
        "overrides": [],
        "masks": [(0.45, 0.02, 0.62, 0.11), (0.82, 0.02, 0.87, 0.28)],
    },
    "skyvault": {
        "file": "astral-bloom-skyvault-elevation-fixed.webp",
        "bands": [
            (56, 144, 192, 300),   # 深蓝 200-400m,取中值
            (125, 190, 210, 500),  # 浅蓝 400-600m
            (130, 175, 95, 700),   # 绿 600-800m
            (200, 192, 88, 900),   # 黄 800-1000m
            (240, 176, 80, 1100),  # 橙 1000-1200m
            (208, 96, 80, 1300),   # 红 1200-1400m
        ],
        "max_m": 1400,
        "crop": (0.02, 0.10, 0.98, 0.92),
        "masks": [(0.82, 0.0, 1.0, 0.28)],
        "overrides": [(0.505, 0.256, 0.068, 0.085, 4)],
    },
    "xinghuan": {
        "file": "astral-bloom-xinghuan-elevation.webp",
        "bands": [
            (0, 80, 160, 0),       # 深蓝 河谷水面 0m
            (130, 205, 215, 20),   # 浅蓝 低地 20m
            (110, 185, 110, 40),   # 绿 低丘 40m
            (240, 215, 85, 60),    # 黄 中层台地 60m
            (240, 150, 65, 80),    # 橙 高层台地 80m
            (220, 85, 70, 100),    # 红 中央高地 100m
        ],
        "max_m": 100,
        "crop": (0.02, 0.10, 0.98, 0.92),
        "overrides": [],
        "masks": [(0.82, 0.03, 1.0, 0.35)],
    },
    "yongye": {
        "file": "astral-bloom-yongye-elevation.webp",
        "bands": [
            (0, 80, 136, 0),       # 深蓝 湖面/河谷 0m
            (112, 176, 208, 40),   # 浅蓝 低地 40m
            (120, 176, 96, 90),    # 绿 低丘 90m
            (232, 208, 96, 150),   # 黄 中层台地 150m
            (240, 160, 64, 220),   # 橙 高地 220m
            (225, 70, 60, 300),    # 红 最高峰 300m
        ],
        "max_m": 300,
        "crop": (0.02, 0.10, 0.98, 0.92),
        "overrides": [],
        "masks": [(0.83, 0.0, 1.0, 0.31)],
    },
    "wuhai": {
        "file": "astral-bloom-wuhai-border-elevation.webp",
        "bands": [
            (0, 80, 144, 0),       # 深蓝 河谷 0m
            (136, 192, 208, 100),  # 浅蓝 低地 100m
            (136, 192, 128, 200),  # 绿 低丘 200m
            (240, 192, 64, 320),   # 黄 中阶地 320m
            (240, 160, 56, 450),   # 橙 高地 450m
            (224, 64, 64, 600),    # 红 最高峰 600m
        ],
        "max_m": 600,
        "crop": (0.02, 0.10, 0.98, 0.92),
        "overrides": [],
        "masks": [(0.82, 0.0, 1.0, 0.25)],
    },
}

UNKNOWN_DIST = 60  # RGB 欧氏距离超过此值视为非地形(文字/道路/建筑),走插值


def apply_masks(idx, grid, masks):
    """把图例/标注色块框强制设为未知(走插值);masks 为 crop 分数坐标矩形列表."""
    idx = idx.copy()
    for (mx0, my0, mx1, my1) in masks:
        x0, x1 = int(mx0 * grid), int(mx1 * grid)
        y0, y1 = int(my0 * grid), int(my1 * grid)
        idx[y0:y1, x0:x1] = -1
    return idx


def match_bands(img_rgb, targets):
    """把每个像素匹配到最近的色带;返回 band_idx(-1=未知)+ 最小距离."""
    h, w, _ = img_rgb.shape
    px = img_rgb.reshape(-1, 3).astype(np.float32)
    tgt = np.asarray(targets, dtype=np.float32)
    # 分块算距离,省内存
    best_idx = np.empty(px.shape[0], dtype=np.int16)
    best_d = np.empty(px.shape[0], dtype=np.float32)
    step = 200_000
    for i in range(0, px.shape[0], step):
        chunk = px[i:i + step]
        d = np.sqrt(((chunk[:, None, :] - tgt[None, :, :]) ** 2).sum(axis=2))
        best_idx[i:i + step] = d.argmin(axis=1)
        best_d[i:i + step] = d.min(axis=1)
    idx = best_idx.reshape(h, w)
    dist = best_d.reshape(h, w)
    idx = np.where(dist > UNKNOWN_DIST, -1, idx)
    return idx, dist


def fill_unknown(height, known_mask):
    """最近已知点插值补未知像素."""
    if known_mask.all():
        return height
    # scipy 1.11: return_indices=True 返回 (distances, indices),indices 形状 (ndim, H, W)
    _, indices = ndimage.distance_transform_edt(~known_mask, return_indices=True)
    return height[tuple(indices)]


def inpaint_smooth(height, known_mask, sigma=15.0):
    """未知区用重模糊场填充(平滑插值),已知区保持原值."""
    h_blur = ndimage.gaussian_filter(height, sigma=sigma, mode="nearest")
    return np.where(known_mask, height, h_blur)


def apply_overrides(height_m, band_h, grid, overrides):
    """按地图自身标注强制指定区域的高度(椭圆区).overrides: (cx,cy,rx,ry,band_idx)."""
    yy, xx = np.mgrid[0:grid, 0:grid]
    fx = xx / grid
    fy = yy / grid
    for (cx, cy, rx, ry, band) in overrides:
        mask = ((fx - cx) / rx) ** 2 + ((fy - cy) / ry) ** 2 <= 1.0
        height_m[mask] = band_h[band]
    return height_m


def render_preview(height_norm, band_rgb, band_h, max_m):
    """色带着色 + 简单山体阴影预览(北在上)."""
    h, w = height_norm.shape
    # 按高度找最近色带颜色
    heights = np.asarray(band_h, dtype=float) / max_m
    rgb = np.asarray(band_rgb, dtype=float)
    # 每个像素取最近的 band 高度对应的颜色
    flat = height_norm.reshape(-1)
    d = np.abs(flat[:, None] - heights[None, :])
    sel = d.argmin(axis=1)
    img = rgb[sel].reshape(h, w, 3).astype(np.uint8)
    # 山体阴影:用高度梯度做简单明暗
    gy, gx = np.gradient(height_norm)
    shade = np.clip(1.0 - (gx * 1.2 + gy * 0.6) * 8, 0.55, 1.15)
    shaded = np.clip(img.astype(float) * shade[..., None], 0, 255).astype(np.uint8)
    return Image.fromarray(shaded, "RGB")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("key", choices=sorted(CONFIG))
    ap.add_argument("--sigma", type=float, default=3.0)
    ap.add_argument("--grid", type=int, default=513)
    ap.add_argument("--docs", default=os.path.expanduser("~/workspace/galgame-online/docs"))
    ap.add_argument("--outdir", default=os.path.expanduser("~/workspace/galgame-online/tools/heightmap/out"))
    args = ap.parse_args()

    cfg = CONFIG[args.key]
    src = os.path.join(args.docs, cfg["file"])
    im = Image.open(src).convert("RGB")
    W, H = im.size
    x0, y0, x1, y1 = cfg["crop"]
    crop = im.crop((int(W * x0), int(H * y0), int(W * x1), int(H * y1)))
    cw, ch = crop.size
    aspect = cw / ch
    print(f"[{args.key}] source {W}x{H} crop {cw}x{ch} aspect {aspect:.3f}")

    # 重采样到网格
    small = crop.resize((args.grid, args.grid), Image.BILINEAR)
    arr = np.asarray(small)
    targets = [(r, g, b) for r, g, b, _ in cfg["bands"]]
    band_h = [hh for _, _, _, hh in cfg["bands"]]
    idx, dist = match_bands(arr, targets)
    idx = apply_masks(idx, args.grid, cfg.get("masks", []))
    known = idx >= 0
    print(f"[{args.key}] matched {known.mean()*100:.1f}%  unknown {(~known).mean()*100:.1f}%  "
          f"mean_dist {dist[known].mean() if known.any() else 0:.1f}")
    band_counts = {int(i): int((idx == i).sum()) for i in range(6)}
    print(f"[{args.key}] band pixel counts {band_counts}")

    # 色带 -> 高度(米) -> 归一化
    h_m = np.zeros(idx.shape, dtype=np.float32)
    for i, hh in enumerate(band_h):
        h_m[idx == i] = hh
    h_filled = fill_unknown(h_m, known)
    h_inpaint = inpaint_smooth(h_filled, known, sigma=15.0)
    h_inpaint = apply_overrides(h_inpaint, band_h, args.grid, cfg.get("overrides", []))
    h_norm = h_inpaint / cfg["max_m"]

    # 色带匹配图(未知=白色)供人工核对
    pal = np.array(targets + [(255, 255, 255)], dtype=np.uint8)
    band_vis = pal[np.where(idx >= 0, idx, 6)]
    Image.fromarray(band_vis, "RGB").save(os.path.join(args.outdir, f"{args.key}_{args.grid}_bandmap.png"))
    # 平滑去阶梯
    h_smooth = ndimage.gaussian_filter(h_norm, sigma=args.sigma, mode="nearest")
    h_smooth = np.clip(h_smooth, 0, 1)

    os.makedirs(args.outdir, exist_ok=True)
    # RAW:16 位小端,第 0 行 = 南 = 图片底行(上下翻转)
    raw16 = (np.flipud(h_smooth) * 65535).astype("<u2")
    raw_path = os.path.join(args.outdir, f"{args.key}_{args.grid}.raw")
    raw16.tofile(raw_path)
    # 预览图(北在上)
    prev = render_preview(h_smooth, targets, band_h, cfg["max_m"])
    prev_path = os.path.join(args.outdir, f"{args.key}_{args.grid}_preview.png")
    prev.save(prev_path)
    meta = {
        "key": args.key,
        "source": cfg["file"],
        "source_size": [W, H],
        "crop_frac": list(cfg["crop"]),
        "crop_px": [cw, ch],
        "aspect_w_h": round(aspect, 4),
        "grid": args.grid,
        "sigma": args.sigma,
        "bands": [{"rgb": list(t), "height_m": hh} for t, hh in zip(targets, band_h)],
        "max_m": cfg["max_m"],
        "matched_pct": round(float(known.mean() * 100), 2),
        "band_pixel_counts": band_counts,
        "height_min_m": round(float(h_smooth.min() * cfg["max_m"]), 1),
        "height_max_m": round(float(h_smooth.max() * cfg["max_m"]), 1),
        "raw": os.path.basename(raw_path),
        "preview": os.path.basename(prev_path),
        "note": "RAW row 0 = south (flipped vertically from image). Terrain height = max_m; "
                "height value = band_height / max_m. Schematic design values, NOT surveyed data.",
    }
    with open(os.path.join(args.outdir, f"{args.key}_{args.grid}_meta.json"), "w") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    print(f"[{args.key}] wrote {raw_path} ({os.path.getsize(raw_path)} B) + {prev_path}")
    print(f"[{args.key}] height range after smooth: {meta['height_min_m']} - {meta['height_max_m']} m "
          f"(terrain height = {cfg['max_m']} m)")


if __name__ == "__main__":
    main()
