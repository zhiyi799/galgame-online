// Placeholder item names, copied verbatim from the HTML client's ITEM_NAMES
// table (client/online-layer/game-js.txt). The server only knows numeric
// item IDs; the client maps them to display names here.
// TODO: rename these to the real character / item names when art and story
// content exist. The user generates art themselves; do not invent real names.
using System.Collections.Generic;

namespace GalgameOnline.Api
{
    public static class ItemNames
    {
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            { 101, "星夜·凛" }, { 102, "星夜·雪" }, { 103, "星夜·花" }, { 104, "星夜·月" }, { 105, "月下·凛" },
            { 201, "四星·凛" }, { 202, "四星·雪" }, { 203, "四星·花" }, { 204, "四星·祭" }, { 205, "四星·夜" },
            { 301, "三星·信物" }, { 302, "三星·花瓣" }, { 303, "三星·纸鹤" }, { 304, "三星·月光" },
        };

        public static string Get(int itemId)
        {
            return Names.TryGetValue(itemId, out var n) ? n : "物品#" + itemId;
        }

        public static string Stars(int rarity)
        {
            switch (rarity)
            {
                case 5: return "★★★★★";
                case 4: return "★★★★";
                case 3: return "★★★";
                default: return "★";
            }
        }

        public static string CurrencyName(string kind)
        {
            switch (kind)
            {
                case "premium": return "高级货币";
                case "free": return "免费货币";
                case "ticket": return "抽卡券";
                default: return kind;
            }
        }

        /// <summary>Human-readable attachment text, e.g. "星夜·凛 x1" or "免费货币 x500".</summary>
        public static string AttachmentText(MailAttachmentDto a)
        {
            if (a == null) return "";
            string name = (a.kind == "premium" || a.kind == "free" || a.kind == "ticket")
                ? CurrencyName(a.kind)
                : Get(a.itemId);
            return name + " x" + a.count;
        }
    }
}
