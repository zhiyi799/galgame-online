// CloudSaveScreen — slots 1-3, upload/download, empty-slot handling.
// The local save is the VN progress JSON kept in PlayerPrefs ("save_slot_N");
// plus a timestamp. Upload sends it to the Worker; download writes it back
// locally and the Story scene resumes from it. Offline -> toast guard.
using UnityEngine;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public class CloudSaveScreen : MonoBehaviour
    {
        public const string LocalSaveKeyPrefix = "save_slot_";

        private TextMeshProUGUI _statusLabel;

        public static string GetLocalSave(int slot) =>
            PlayerPrefs.GetString(LocalSaveKeyPrefix + slot, "");

        public static void SetLocalSave(int slot, string dataJson)
        {
            PlayerPrefs.SetString(LocalSaveKeyPrefix + slot, dataJson);
            PlayerPrefs.SetString(LocalSaveKeyPrefix + slot + "_time",
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            PlayerPrefs.Save();
        }

        private void Start()
        {
            var canvas = UiFactory.SceneCanvas();
            var (root, _) = UiFactory.ScreenBase(canvas.transform, "云存档",
                () => GameManager.Instance.GoTo(GameManager.SceneMainMenu));

            _statusLabel = UiFactory.Label(root.transform, "Status",
                "本地存档是剧情进度（第几句对话）。上传后可在别的设备下载继续。", 18,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            _statusLabel.rectTransform.anchorMin = new Vector2(0, 1);
            _statusLabel.rectTransform.anchorMax = new Vector2(0, 1);
            UiFactory.Place(_statusLabel.rectTransform, 20, -76, 1000, 30);

            float y = -130;
            for (int slot = 1; slot <= 3; slot++)
            {
                int s = slot;
                var card = UiFactory.Panel(root.transform, "Slot" + s,
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 110),
                    UiFactory.GlassWhite(0.06f));
                var cRt = card.GetComponent<RectTransform>();
                cRt.pivot = new Vector2(0.5f, 1);
                cRt.anchoredPosition = new Vector2(0, y);
                y -= 122;

                var title = UiFactory.Label(card.transform, "Title", "存档槽 " + s, 22,
                    TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
                UiFactory.Place(title.rectTransform, 16, -10, 300, 32);

                string local = GetLocalSave(s);
                string localTime = PlayerPrefs.GetString(LocalSaveKeyPrefix + s + "_time", "");
                var info = UiFactory.Label(card.transform, "Info",
                    string.IsNullOrEmpty(local) ? "本地：空" : "本地：" + localTime + "　" + Summarize(local),
                    16, TextAlignmentOptions.Left, UiFactory.SilverWhite);
                UiFactory.Place(info.rectTransform, 16, -48, 700, 26);

                var upBtn = UiFactory.Button(card.transform, "Upload", "☁️ 上传", () => Upload(s),
                    new Vector2(160, 44));
                var uRt = upBtn.GetComponent<RectTransform>();
                uRt.anchorMin = new Vector2(1, 0.5f); uRt.anchorMax = new Vector2(1, 0.5f);
                uRt.pivot = new Vector2(1, 0.5f);
                uRt.anchoredPosition = new Vector2(-196, 0);

                var downBtn = UiFactory.Button(card.transform, "Download", "☁️ 下载", () => Download(s),
                    new Vector2(160, 44), UiFactory.DeepUltramarine);
                var dRt = downBtn.GetComponent<RectTransform>();
                dRt.anchorMin = new Vector2(1, 0.5f); dRt.anchorMax = new Vector2(1, 0.5f);
                dRt.pivot = new Vector2(1, 0.5f);
                dRt.anchoredPosition = new Vector2(-16, 0);
            }
        }

        private string Summarize(string dataJson)
        {
            try
            {
                var s = JsonUtility.FromJson<StoryScreen.VnSave>(dataJson);
                return "剧情第 " + (s.line + 1) + " 句";
            }
            catch { return "（无法解析）"; }
        }

        private void Upload(int slot)
        {
            var gm = GameManager.Instance;
            if (!gm.RequireOnline()) return;
            string local = GetLocalSave(slot);
            if (string.IsNullOrEmpty(local))
            {
                Toast.Show("本地槽 " + slot + " 是空的，先去剧情里玩一会再上传。");
                return;
            }
            Toast.Show("上传中…");
            StartCoroutine(gm.Api.UploadSave(slot, local, r =>
            {
                if (!r.Success)
                {
                    if (r.Failure == ApiFailureKind.Unauthorized) gm.HandleUnauthorized();
                    else Toast.Show(r.ErrorMessage);
                    return;
                }
                Toast.Show(r.Data.ok ? "已上传到云端槽 " + slot + "。" : (r.Data.error ?? "上传失败。"));
            }));
        }

        private void Download(int slot)
        {
            var gm = GameManager.Instance;
            if (!gm.RequireOnline()) return;
            Toast.Show("下载中…");
            StartCoroutine(gm.Api.DownloadSave(slot, r =>
            {
                if (!r.Success)
                {
                    if (r.Failure == ApiFailureKind.Unauthorized) gm.HandleUnauthorized();
                    else Toast.Show(r.ErrorMessage);
                    return;
                }
                if (!r.Data.ok || string.IsNullOrEmpty(r.Data.dataJson))
                {
                    Toast.Show("云端槽 " + slot + " 没有存档。");
                    return;
                }
                SetLocalSave(slot, r.Data.dataJson);
                Toast.Show("已下载到本地槽 " + slot + "：" + Summarize(r.Data.dataJson) + "。去剧情继续吧。");
            }));
        }
    }
}
