// Shared API types. Shapes mirror the C# prototype's DTOs (GalgameOnline.Shared)
// so the Unity client and the existing HTML client share one contract.
// Field names are camelCase on the wire; Postgres rows are snake_case and are
// mapped in src/supabase.ts.

export interface Banner {
  id: number;
  name: string;
  featuredFiveStar: number;
  standardFiveStar: number[];
  fourStar: number[];
  threeStar: number[];
  /** Hard pity for 5-star (0 = none). Typical 90. */
  fiveStarPity: number;
  /** Hard pity for 4-star (0 = none). Typical 10. */
  fourStarPity: number;
  /** Cost per single pull, in premium currency. */
  costPerPull: number;
  /** Daily pull cap (0 = unlimited). */
  dailyLimit: number;
}

export interface GachaState {
  total: number;
  sinceFive: number;
  sinceFour: number;
  featuredSince: number;
  guaranteed: boolean;
  dailyCount: number;
  /** ISO timestamp of the UTC day the daily counter belongs to. */
  dailyAnchor: string | null;
}

export interface PullOutcome {
  itemId: number;
  rarity: number;
  isFeatured: boolean;
  kind: string;
}

export interface GachaBannerInfo {
  bannerId: number;
  totalCount: number;
  fiveRemain: number;
  fourRemain: number;
  dailyLeft: number;
}

export interface Wallet {
  premium: number;
  free: number;
  tickets: number;
}

export interface InventoryItem {
  accountId: string;
  itemId: number;
  kind: string;
  count: number;
  level: number;
}

export interface MailAttachment {
  itemId: number;
  kind: string;
  count: number;
}

export interface MailItem {
  id: string;
  accountId: string;
  title: string;
  body: string;
  attachments: MailAttachment[];
  read: boolean;
  claimed: boolean;
  createdAt: string;
  expiresAt: string | null;
}

// --- Request / response envelopes (same as C#) ---

export interface SignupRequest {
  username: string;
  password: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface GachaPullRequest {
  bannerId: number;
  count: number;
}

export interface SaveUploadRequest {
  slot: number;
  dataJson: string;
}

export interface SendMailRequest {
  title: string;
  body: string;
  attachments?: MailAttachment[];
}

export type JsonObject = Record<string, unknown>;
