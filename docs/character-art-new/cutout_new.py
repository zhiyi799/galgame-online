"""Cut out the 11 newly generated ASTRAL BLOOM character images to transparent PNGs.

OOM fix (learned 2026-11-16): create the onnxruntime session with the CPU
memory arena disabled + single thread, and pass it to rembg via sess_opts.
Run ONLY when no Unity build is running (6GB shared memory).
"""
import os
import shutil
import sys

import onnxruntime as ort
from PIL import Image
from rembg import new_session, remove

SRC_DIR = os.path.expanduser("~/workspace/galgame-online/docs/character-art-new")

JOBS = [
    ("media-generation-protagonist-male-raw-0-458add75-b146-40fa-b5e7-2cab6fa5c65b.webp", "protagonist_male"),
    ("media-generation-protagonist-female-raw-0-acc81a8f-002a-4399-b0e5-05f894f48eb5.webp", "protagonist_female"),
    ("media-generation-char-102-raw-0-5338c217-b0d9-4ce5-b102-eb3a5e4c5e61.webp", "char_102"),
    ("media-generation-char-103-raw-0-c214cf3e-1fa2-4718-8e79-0c6a611ccf4f.webp", "char_103"),
    ("media-generation-char-104-raw-0-9fbe0298-93a8-4ec3-b471-e208d72718dd.webp", "char_104"),
    ("media-generation-char-105-raw-0-b89b4b09-9345-4255-9363-e269cd5509f8.webp", "char_105"),
    ("media-generation-char-201-raw-0-a5a74598-614e-4830-87e9-ff0cac836d6e.webp", "char_201"),
    ("media-generation-char-202-raw-0-8b203475-6411-443a-a1ad-aba503efd13b.webp", "char_202"),
    ("media-generation-char-203-raw-0-1f3c6b6d-511f-4dbc-aa2e-89ec4c404373.webp", "char_203"),
    ("media-generation-char-204-raw-0-d8bf958a-1734-4c22-8179-ff4183231ffe.webp", "char_204"),
    ("media-generation-char-205-raw-0-3f810029-c44d-422c-be62-d36ab76c5c6d.webp", "char_205"),
]

# OOM fix: no CPU memory arena, single thread.
opts = ort.SessionOptions()
opts.enable_cpu_mem_arena = False
opts.intra_op_num_threads = 1
opts.inter_op_num_threads = 1

session = new_session("isnet-anime", sess_opts=opts)

for src_name, stem in JOBS:
    src = os.path.join(SRC_DIR, src_name)
    raw_out = os.path.join(SRC_DIR, stem + "_raw.webp")
    cut_out = os.path.join(SRC_DIR, stem + ".png")
    if os.path.exists(cut_out):
        print(f"skip {stem} (cutout exists)", flush=True)
        continue
    if not os.path.exists(src):
        print(f"MISSING {src}", flush=True)
        continue
    # clean-named copy of the raw image
    if not os.path.exists(raw_out):
        shutil.copy2(src, raw_out)
    print(f"processing {stem} ...", flush=True)
    with open(src, "rb") as f:
        data = f.read()
    result = remove(data, session=session)
    from io import BytesIO
    im = Image.open(BytesIO(result)).convert("RGBA")
    bbox = im.getbbox()
    if bbox:
        im = im.crop(bbox)
    im.save(cut_out, "PNG", optimize=True)
    print(f"  saved {cut_out} {im.size}", flush=True)

print("ALL DONE", flush=True)
