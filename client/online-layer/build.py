#!/usr/bin/env python3
"""Splice the GalgameOnline layer into a copy of GalgameMaker Studio.

Reads the pristine upstream file, applies all edits, writes
galgame-online-studio.html. Idempotent: always starts from the pristine
upstream copy, so re-running is safe.
"""
import sys

SRC = "/home/hatch/workspace/galgame-online/client/galgame-maker/GalgameMaker_Studio (5).html"
DST = "/home/hatch/workspace/galgame-online/client/galgame-online-studio.html"
LAYER = "/home/hatch/workspace/galgame-online/client/online-layer"

def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()

def once(s, old, new, what):
    n = s.count(old)
    if n != 1:
        print(f"FAIL: anchor for [{what}] found {n} times (expected 1)")
        sys.exit(1)
    return s.replace(old, new)

def main():
    s = read(SRC)
    css = read(f"{LAYER}/game-css.txt")
    html = read(f"{LAYER}/game-html.txt")
    js = read(f"{LAYER}/game-js.txt")

    # ---- 1. Studio export view: online config card before Progress ----
    s = once(s, """        <!-- Progress -->""",
"""        <!-- Online -->
        <div class="export-card" style="margin-top:16px;">
          <div class="exp-icon">🌐</div>
          <div class="exp-title">联网服务（GalgameOnline）</div>
          <div class="exp-desc">导出时把这两个地址写进游戏。玩家打开游戏先登录账号，才能抽卡、用云存档；不登录也可以点“离线游玩”。留空则用默认值。</div>
          <div style="display:flex;flex-direction:column;gap:8px;margin-top:10px;">
            <label style="font-size:11px;color:var(--text-dim);">SdkServer 地址
              <input id="onlineSdkUrl" type="text" value="http://127.0.0.1:5000" style="width:100%;margin-top:4px;padding:8px 10px;background:rgba(255,255,255,0.04);border:1px solid var(--border);border-radius:6px;color:var(--text-primary);font-size:12px;">
            </label>
            <label style="font-size:11px;color:var(--text-dim);">GameServer 地址
              <input id="onlineGameUrl" type="text" value="http://127.0.0.1:5100" style="width:100%;margin-top:4px;padding:8px 10px;background:rgba(255,255,255,0.04);border:1px solid var(--border);border-radius:6px;color:var(--text-primary);font-size:12px;">
            </label>
          </div>
        </div>

        <!-- Progress -->""", "export view online card")

    # ---- 2. generateAndDownloadGame: read inputs, pass online config ----
    s = once(s, """function generateAndDownloadGame() {
  const script=document.getElementById('scriptTa').value;
  const gameHTML=buildGameHTML(script);""",
"""function generateAndDownloadGame() {
  const script=document.getElementById('scriptTa').value;
  const online={
    sdkUrl:(document.getElementById('onlineSdkUrl')?.value||'').trim()||'http://127.0.0.1:5000',
    gameUrl:(document.getElementById('onlineGameUrl')?.value||'').trim()||'http://127.0.0.1:5100'
  };
  const gameHTML=buildGameHTML(script, online);""", "generateAndDownloadGame")

    # ---- 3. buildGameHTML signature + default config ----
    s = once(s, """function buildGameHTML(script) {""",
"""function buildGameHTML(script, onlineCfg) {
  onlineCfg = onlineCfg || { sdkUrl:'http://127.0.0.1:5000', gameUrl:'http://127.0.0.1:5100' };""",
             "buildGameHTML signature")

    # ---- 4. ONLINE config inside exported game template ----
    s = once(s, "const ASSETS = ${embeddedAssets};",
             "const ASSETS = ${embeddedAssets};\nconst ONLINE = ${JSON.stringify(onlineCfg)};",
             "ONLINE const in template")

    # ---- 5. Exported CSS ----
    s = once(s, "#skip-flash{", css + "#skip-flash{", "exported css")

    # ---- 6. Gacha menu button (before settings button) ----
    s = once(s, """    <div class="mbtn" onclick="openOverlay('settings')" title="设置 Esc">⚙️</div>""",
"""    <div class="mbtn" onclick="openGacha()" title="抽卡">🎰</div>
    <div class="mbtn" onclick="openOverlay('settings')" title="设置 Esc">⚙️</div>""",
             "gacha menu button")

    # ---- 7. Login + gacha overlays before SAVE/LOAD ----
    s = once(s, "  <!-- SAVE/LOAD -->", html + "  <!-- SAVE/LOAD -->", "login+gacha overlays")

    # ---- 8. Online JS after SAVES line ----
    s = once(s, "const SAVES   = JSON.parse(localStorage.getItem('gmstudio_saves') || '{}');",
             "const SAVES   = JSON.parse(localStorage.getItem('gmstudio_saves') || '{}');\n" + js,
             "online js")

    # ---- 9. openOverlay: render gacha on open ----
    s = once(s, """    if (name === 'tree')     renderTree();
    if (name === 'log')      renderLog();
    if (name === 'saveload') buildSaveGrid();""",
"""    if (name === 'tree')     renderTree();
    if (name === 'log')      renderLog();
    if (name === 'saveload') buildSaveGrid();
    if (name === 'gacha')    renderGacha();""", "openOverlay gacha branch")

    # ---- 10. Cloud buttons in save slots 1-3 ----
    s = once(s, """          '<button class="slot-act-btn" style="color:#f87171aa;" onclick="delSave('+i+')">删除</button>'+
        '</div>';""",
"""          '<button class="slot-act-btn" style="color:#f87171aa;" onclick="delSave('+i+')">删除</button>'+
          (i <= 3 ? '<button class="slot-act-btn" onclick="cloudUpload('+i+')">☁️上传</button><button class="slot-act-btn" onclick="cloudDownload('+i+')">☁️下载</button>' : '')+
        '</div>';""", "cloud buttons in save grid")

    # ---- 11. Escape must not dismiss the login gate before boot ----
    s = once(s, """  if (document.querySelector('.overlay.show')) {
    if (e.key === 'Escape') document.querySelectorAll('.overlay.show').forEach(o => o.classList.remove('show'));
    return;
  }""",
"""  if (document.querySelector('.overlay.show')) {
    if (e.key === 'Escape') {
      const loginOpen = document.getElementById('ov-login') && document.getElementById('ov-login').classList.contains('show');
      if (loginOpen && !bootDone) return;
      document.querySelectorAll('.overlay.show').forEach(o => o.classList.remove('show'));
    }
    return;
  }""", "escape guard for login")

    # ---- 12. Boot through the login gate instead of starting directly ----
    s = once(s, """// ---- START ----
processNext();""",
"""// ---- START (online gate) ----
olBoot();""", "boot gate")

    with open(DST, "w", encoding="utf-8") as f:
        f.write(s)
    print(f"OK: wrote {DST} ({len(s)} bytes)")

if __name__ == "__main__":
    main()
