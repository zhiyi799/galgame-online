// DTOs matching the Cloudflare Worker responses (cloud/workers/src/types.ts).
// Field names are camelCase on the wire; JsonUtility maps them 1:1.
using System;

namespace GalgameOnline.Api
{
    [Serializable]
    public class ErrorBody
    {
        public string error;
    }

    [Serializable]
    public class SignupResponse
    {
        public bool ok;
        public string accountId;
        public string error;
    }

    [Serializable]
    public class LoginResponse
    {
        public bool ok;
        public string token;
        public string expiresAt;
        public string gameServerUrl;
        public string error;
    }

    [Serializable]
    public class SessionResponse
    {
        public bool ok;
        public string accountId;
    }

    [Serializable]
    public class BannerDto
    {
        public int id;
        public string name;
        public int featuredFiveStar;
        public int costPerPull;
        public int dailyLimit;
    }

    [Serializable]
    public class BannerListResponse
    {
        public BannerDto[] banners;
    }

    [Serializable]
    public class BannerInfo
    {
        public int bannerId;
        public int totalCount;
        public int fiveRemain;
        public int fourRemain;
        public int dailyLeft;
    }

    [Serializable]
    public class PullOutcome
    {
        public int itemId;
        public int rarity;
        public bool isFeatured;
        public string kind;
    }

    [Serializable]
    public class PullResponse
    {
        public bool ok;
        public string error;
        public PullOutcome[] pulls;
        public BannerInfo banner;
    }

    [Serializable]
    public class WalletDto
    {
        public int premium;
        public int free;
        public int tickets;
    }

    [Serializable]
    public class InventoryItemDto
    {
        public string accountId;
        public int itemId;
        public string kind;
        public int count;
        public int level;
    }

    [Serializable]
    public class InventoryResponse
    {
        public InventoryItemDto[] items;
    }

    [Serializable]
    public class MailAttachmentDto
    {
        public int itemId;
        public string kind;
        public int count;
    }

    [Serializable]
    public class MailItemDto
    {
        public string id;
        public string accountId;
        public string title;
        public string body;
        public MailAttachmentDto[] attachments;
        public bool read;
        public bool claimed;
        public string createdAt;
        public string expiresAt;
    }

    [Serializable]
    public class MailListResponse
    {
        public MailItemDto[] mail;
    }

    [Serializable]
    public class ClaimResponse
    {
        public bool ok;
        public string error;
        public MailAttachmentDto[] granted;
    }

    [Serializable]
    public class SaveResponse
    {
        public bool ok;
        public string error;
        public string dataJson;
        public string updatedAt;
    }

    // --- Request bodies ---

    [Serializable]
    public class AuthRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class PullRequest
    {
        public int bannerId;
        public int count;
    }

    [Serializable]
    public class SaveUploadRequest
    {
        public int slot;
        public string dataJson;
    }
}
