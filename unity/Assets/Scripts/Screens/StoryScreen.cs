// StoryScreen — minimal VN player: dialogue box, speaker name, text, advance
// on click. Placeholder background colour + placeholder character box (no real
// art yet — the user generates art themselves; art comes later via the CDN
// layout in cloud/README.md). Built-in 5-line sample script proves the flow.
// Progress auto-saves to local slot 1; cloud upload/download use the same slot.
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
public class StoryScreen: MonoBehaviour
{
[Serializable]
public class VnSave
{
public int line;
}

[Serializable]
private class ScriptLine
{
public string speaker;
public string text;
}

// Sample script (placeholder content, 5 lines).
private static readonly ScriptLine[] Script = new ScriptLine[]
{
new ScriptLine { speaker = "凛", text = "星星今晚很亮呢。你看，那颗最亮的，就是我们第一次见面的地方。"},
new ScriptLine { speaker = "主角", text = "……嗯。明明只是普通的夜晚，却觉得哪里都不一样了。"},
new ScriptLine { speaker = "凛", text = "因为从今天起，你就是「星夜邂逅」的见证人了。抽卡的运气，也会变好哦。"},
new ScriptLine { speaker = "主角", text = "那只是概率吧……不过，借你吉言。"},
new ScriptLine { speaker = "凛", text = "去抽一次吧。说不定，今晚的星星会回应你。"},
};

private int _line;
private TextMeshProUGUI _nameLabel;
private TextMeshProUGUI _bodyLabel;
private TextMeshProUGUI _hintLabel;
private GameObject _charBox;
private TextMeshProUGUI _charLabel;

private void Start()
{
var canvas = UiFactory.SceneCanvas();
var root = UiFactory.Panel(canvas.transform, "StoryRoot",
Vector2.zero, Vector2.one, Vector2.zero, new Color(0, 0, 0, 0));

// Placeholder background: solid deep-blue panel (real scene art later).
var bg = UiFactory.Panel(root.transform, "Background",
Vector2.zero, Vector2.one, Vector2.zero, UiFactory.DeepUltramarine);
var bgLabel = UiFactory.Label(bg.transform, "BgNote", "", 16,
TextAlignmentOptions.Center, new Color(0.898f, 0.918f, 0.969f, 0.35f));
bgLabel.rectTransform.anchorMin = new Vector2(0.5f, 1);
bgLabel.rectTransform.anchorMax = new Vector2(0.5f, 1);
UiFactory.Place(bgLabel.rectTransform, -300, -80, 600, 28);

// Placeholder character box (real 立绘 later).
_charBox = UiFactory.Panel(root.transform, "CharacterBox",
new Vector2(0.62f, 0.18f), new Vector2(0.92f, 0.86f), Vector2.zero,
UiFactory.MoonlightBlue);
_charLabel = UiFactory.Label(_charBox.transform, "CharName", "立绘占位", 22,
TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
UiFactory.Stretch(_charLabel.rectTransform);

// Top bar: back + cloud save/load.
var top = UiFactory.Panel(root.transform, "TopBar",
new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 56), UiFactory.Glass(0.35f));
var topRt = top.GetComponent<RectTransform>();
topRt.pivot = new Vector2(0.5f, 1);
topRt.anchoredPosition = Vector2.zero;
var back = UiFactory.Button(top.transform, "Back", "← 返回", () =>
GameManager.Instance.GoTo(GameManager.SceneMainMenu), new Vector2(110, 40),
UiFactory.DeepUltramarine);
var bRt = back.GetComponent<RectTransform>();
bRt.anchorMin = new Vector2(0, 0.5f); bRt.anchorMax = new Vector2(0, 0.5f);
bRt.pivot = new Vector2(0, 0.5f);
bRt.anchoredPosition = new Vector2(12, 0);

var upBtn = UiFactory.Button(top.transform, "CloudUp", "☁️ 上传进度", UploadProgress,
new Vector2(150, 40), UiFactory.DeepUltramarine);
var uRt = upBtn.GetComponent<RectTransform>();
uRt.anchorMin = new Vector2(1, 0.5f); uRt.anchorMax = new Vector2(1, 0.5f);
uRt.pivot = new Vector2(1, 0.5f);
uRt.anchoredPosition = new Vector2(-178, 0);
var downBtn = UiFactory.Button(top.transform, "CloudDown", "☁️ 下载进度", DownloadProgress,
new Vector2(150, 40), UiFactory.DeepUltramarine);
var dRt = downBtn.GetComponent<RectTransform>();
dRt.anchorMin = new Vector2(1, 0.5f); dRt.anchorMax = new Vector2(1, 0.5f);
dRt.pivot = new Vector2(1, 0.5f);
dRt.anchoredPosition = new Vector2(-12, 0);

// Dialogue box (click it to advance).
var dlg = UiFactory.Panel(root.transform, "DialogueBox",
new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 210),
UiFactory.Glass(0.88f));
var dlgBtn = dlg.AddComponent<Button>();
dlgBtn.onClick.AddListener(Advance);
dlgBtn.transition = Button.Transition.None;

_nameLabel = UiFactory.Label(dlg.transform, "Name", "", 22,
TextAlignmentOptions.Left, UiFactory.MoonlightBlue, true);
UiFactory.Place(_nameLabel.rectTransform, 24, -12, 400, 32);
_nameLabel.rectTransform.anchorMin = new Vector2(0, 1);
_nameLabel.rectTransform.anchorMax = new Vector2(0, 1);

_bodyLabel = UiFactory.Label(dlg.transform, "Body", "", 22,
TextAlignmentOptions.TopLeft, UiFactory.MoonWhite);
UiFactory.Place(_bodyLabel.rectTransform, 24, -52, 1200, 120);
_bodyLabel.rectTransform.anchorMin = new Vector2(0, 1);
_bodyLabel.rectTransform.anchorMax = new Vector2(0, 1);

_hintLabel = UiFactory.Label(dlg.transform, "Hint", "▼ 点击继续", 15,
TextAlignmentOptions.Right, new Color(0.898f, 0.918f, 0.969f, 0.55f));
UiFactory.Place(_hintLabel.rectTransform, -140, -176, 120, 24);
_hintLabel.rectTransform.anchorMin = new Vector2(1, 1);
_hintLabel.rectTransform.anchorMax = new Vector2(1, 1);

// Resume from local slot 1.
string local = CloudSaveScreen.GetLocalSave(1);
_line = 0;
if (!string.IsNullOrEmpty(local))
{
try
{
var s = JsonUtility.FromJson<VnSave>(local);
_line = Mathf.Clamp(s.line, 0, Script.Length - 1);
}
catch { _line = 0;}
}
ShowLine();

if (GameManager.SmokeMode)
SmokeTestRunner.Run(this);
}

public void Advance()
{
if (_line < Script.Length - 1)
{
_line++;
ShowLine();
CloudSaveScreen.SetLocalSave(1, JsonUtility.ToJson(new VnSave { line = _line}));
}
else
{
_hintLabel.text = "（示例剧本结束 · 点击返回）";
_bodyLabel.text = "这就是全部的示例内容。真实剧本、立绘、CG、BGM、语音以后再接。";
}
}

private void ShowLine()
{
var l = Script[_line];
// The story reads the UNIFIED protagonist identity （无星者） — the
// speaker name never changes with gender. Only the presentation (the
// character box label) shows which gender was chosen.
bool isProtagonist = l.speaker == "主角";
_nameLabel.text = isProtagonist ? ProtagonistManager.StoryIdentity : l.speaker;
_bodyLabel.text = l.text;
_charLabel.text = isProtagonist
? ProtagonistManager.StoryIdentity
+ (ProtagonistManager.HasSelection ? "（" + ProtagonistManager.SelectedGenderLabel + "）" : "")
+ "\n立绘占位"
: l.speaker + "\n立绘占位";
_hintLabel.text = $"▼ 点击继续（{_line + 1}/{Script.Length}）";
}

private void UploadProgress()
{
var gm = GameManager.Instance;
if (!gm.RequireOnline()) return;
string data = JsonUtility.ToJson(new VnSave { line = _line});
CloudSaveScreen.SetLocalSave(1, data);
Toast.Show("上传中…");
StartCoroutine(gm.Api.UploadSave(1, data, r =>
{
if (!r.Success)
{
if (r.Failure == ApiFailureKind.Unauthorized) gm.HandleUnauthorized();
else Toast.Show(r.ErrorMessage);
return;
}
Toast.Show(r.Data.ok? "进度已上传到云端槽 1。": (r.Data.error?? "上传失败。"));
}));
}

private void DownloadProgress()
{
var gm = GameManager.Instance;
if (!gm.RequireOnline()) return;
Toast.Show("下载中…");
StartCoroutine(gm.Api.DownloadSave(1, r =>
{
if (!r.Success)
{
if (r.Failure == ApiFailureKind.Unauthorized) gm.HandleUnauthorized();
else Toast.Show(r.ErrorMessage);
return;
}
if (!r.Data.ok || string.IsNullOrEmpty(r.Data.dataJson))
{
Toast.Show("云端槽 1 没有存档。");
return;
}
CloudSaveScreen.SetLocalSave(1, r.Data.dataJson);
try
{
var s = JsonUtility.FromJson<VnSave>(r.Data.dataJson);
_line = Mathf.Clamp(s.line, 0, Script.Length - 1);
ShowLine();
Toast.Show("已从云端恢复到第 " + (_line + 1) + " 句。");
}
catch { Toast.Show("云端存档无法解析。");}
}));
}
}
}
