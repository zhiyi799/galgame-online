// MailScreen — newest-first list (title/body/date/attachments), claim button
// per mail, claimed state. The unread count is fed back to the main menu badge.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public class MailScreen : MonoBehaviour
    {
        private RectTransform _content;
        private TextMeshProUGUI _statusLabel;

        private void Start()
        {
            var canvas = UiFactory.SceneCanvas();
            var (root, _) = UiFactory.ScreenBase(canvas.transform, "邮件",
                () => GameManager.Instance.GoTo(GameManager.SceneMainMenu));

            _statusLabel = UiFactory.Label(root.transform, "Status", "加载中…", 18,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            _statusLabel.rectTransform.anchorMin = new Vector2(0, 1);
            _statusLabel.rectTransform.anchorMax = new Vector2(0, 1);
            UiFactory.Place(_statusLabel.rectTransform, 20, -76, 800, 30);

            var (scroll, content) = UiFactory.ScrollView(root.transform, "List",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0));
            var sRt = scroll.GetComponent<RectTransform>();
            sRt.offsetMin = new Vector2(16, 16);
            sRt.offsetMax = new Vector2(-16, -116);
            _content = content;

            LoadMail();
        }

        private void LoadMail()
        {
            UiFactory.ClearChildren(_content);
            _statusLabel.text = "加载中…";
            StartCoroutine(GameManager.Instance.Api.GetMail(r =>
            {
                if (!r.Success)
                {
                    if (r.Failure == ApiFailureKind.Unauthorized) GameManager.Instance.HandleUnauthorized();
                    else { _statusLabel.text = r.ErrorMessage; Toast.Show(r.ErrorMessage); }
                    return;
                }
                var mails = r.Data.mail ?? new MailItemDto[0];
                int unread = 0;
                foreach (var m in mails) if (!m.read) unread++;
                GameManager.Instance.UnreadMailCount = unread;
                _statusLabel.text = mails.Length == 0 ? "没有邮件。" : $"共 {mails.Length} 封 · 未读 {unread} 封";

                foreach (var m in mails)
                {
                    var mail = m;
                    var card = UiFactory.Panel(_content, "Mail",
                        new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 118),
                        UiFactory.GlassWhite(0.06f));
                    var cardRt = card.GetComponent<RectTransform>();
                    cardRt.pivot = new Vector2(0.5f, 1);

                    string title = (mail.read ? "" : "● ") + mail.title;
                    var t = UiFactory.Label(card.transform, "Title", title, 20,
                        TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
                    UiFactory.Place(t.rectTransform, 12, -8, 700, 28);

                    string date = "";
                    if (!string.IsNullOrEmpty(mail.createdAt))
                    {
                        if (System.DateTime.TryParse(mail.createdAt, out var dt))
                            date = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                        else date = mail.createdAt;
                    }
                    var d = UiFactory.Label(card.transform, "Date", date, 15,
                        TextAlignmentOptions.Left, UiFactory.SilverWhite);
                    UiFactory.Place(d.rectTransform, 12, -38, 400, 24);

                    var b = UiFactory.Label(card.transform, "Body", mail.body ?? "", 17,
                        TextAlignmentOptions.Left, UiFactory.SilverWhite);
                    UiFactory.Place(b.rectTransform, 12, -62, 700, 26);

                    string att = "";
                    if (mail.attachments != null && mail.attachments.Length > 0)
                    {
                        var parts = new System.Collections.Generic.List<string>();
                        foreach (var attItem in mail.attachments) parts.Add(ItemNames.AttachmentText(attItem));
                        att = "附件：" + string.Join("、", parts);
                    }
                    var a = UiFactory.Label(card.transform, "Attachments", att, 16,
                        TextAlignmentOptions.Left, UiFactory.MoonlightBlue);
                    UiFactory.Place(a.rectTransform, 12, -88, 700, 24);

                    bool hasAttachments = mail.attachments != null && mail.attachments.Length > 0;
                    var claimBtn = UiFactory.Button(card.transform, "Claim",
                        mail.claimed ? "已领取" : "领取",
                        () => ClaimMail(mail), new Vector2(110, 40),
                        mail.claimed ? UiFactory.DeepUltramarine : null);
                    var cRt = claimBtn.GetComponent<RectTransform>();
                    cRt.anchorMin = new Vector2(1, 0.5f);
                    cRt.anchorMax = new Vector2(1, 0.5f);
                    cRt.pivot = new Vector2(1, 0.5f);
                    cRt.anchoredPosition = new Vector2(-12, 0);
                    claimBtn.interactable = !mail.claimed && hasAttachments;
                    if (!hasAttachments && !mail.claimed)
                        claimBtn.GetComponentInChildren<TextMeshProUGUI>().text = "无附件";
                }
            }));
        }

        private void ClaimMail(MailItemDto mail)
        {
            Toast.Show("领取中…");
            StartCoroutine(GameManager.Instance.Api.ClaimMail(mail.id, r =>
            {
                if (!r.Success)
                {
                    if (r.Failure == ApiFailureKind.Unauthorized) GameManager.Instance.HandleUnauthorized();
                    else Toast.Show(r.ErrorMessage);
                    return;
                }
                if (!r.Data.ok)
                {
                    Toast.Show(r.Data.error ?? "领取失败。");
                    return;
                }
                string got = "";
                if (r.Data.granted != null && r.Data.granted.Length > 0)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    foreach (var g in r.Data.granted) parts.Add(ItemNames.AttachmentText(g));
                    got = "获得：" + string.Join("、", parts);
                }
                Toast.Show(string.IsNullOrEmpty(got) ? "已领取。" : got);
                GameManager.Instance.RefreshWallet();
                LoadMail();
            }));
        }
    }
}
