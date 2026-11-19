using System;
using UnityEngine;
using UnityEngine.UI;

public class GachaBannerRotation : MonoBehaviour
{
    [Header("配置")]
    public GachaBannerSchedule schedule;
    public GachaManager manager;

    [Header("界面")]
    public Text statusText;
    public Text countdownText;
    public Text bannerNameText;

    [Header("刷新间隔")]
    public float refreshInterval = 1f;

    private float timer;
    private GachaBannerConfig activeBanner;

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;

        if (timer < refreshInterval)
            return;

        timer = 0f;
        RefreshCountdown();
    }

    public void Refresh()
    {
        if (schedule == null || manager == null)
            return;

        DateTime now = DateTime.UtcNow;

        GachaBannerScheduleEntry activeEntry = null;
        GachaBannerScheduleEntry nextEntry = null;

        foreach (GachaBannerScheduleEntry entry in schedule.entries)
        {
            if (entry == null || !entry.enabled || entry.banner == null)
                continue;

            DateTime start;
            DateTime end;

            if (!TryGetDates(entry, out start, out end))
                continue;

            if (!entry.banner.isEnabled)
                continue;

            if (now >= start && now < end)
            {
                activeEntry = entry;
                break;
            }

            if (start > now &&
                (nextEntry == null || start < GetStart(nextEntry)))
            {
                nextEntry = entry;
            }
        }

        if (activeEntry != null)
        {
            SetActiveBanner(activeEntry.banner);
            SetStatus("活动进行中");
            SetCountdown(activeEntry.EndDate - now);
        }
        else
        {
            activeBanner = null;
            manager.banner = null;

            SetStatus("当前没有开放的活动卡池");

            if (nextEntry != null)
                SetCountdown(nextEntry.StartDate - now);
            else
                SetCountdown(TimeSpan.Zero);
        }
    }

    private bool TryGetDates(
        GachaBannerScheduleEntry entry,
        out DateTime start,
        out DateTime end)
    {
        start = DateTime.MinValue;
        end = DateTime.MinValue;

        try
        {
            start = entry.StartDate.ToUniversalTime();
            end = entry.EndDate.ToUniversalTime();

            return end > start;
        }
        catch (Exception)
        {
            Debug.LogWarning(
                "卡池时间配置错误：" + entry.banner.bannerDisplayName
            );

            return false;
        }
    }

    private DateTime GetStart(GachaBannerScheduleEntry entry)
    {
        return entry.StartDate.ToUniversalTime();
    }

    private void SetActiveBanner(GachaBannerConfig banner)
    {
        if (activeBanner == banner)
            return;

        activeBanner = banner;
        manager.banner = banner;

        if (bannerNameText != null)
            bannerNameText.text = banner.bannerDisplayName;

        Debug.Log("当前卡池：" + banner.bannerDisplayName);
    }

    private void RefreshCountdown()
    {
        if (schedule == null)
            return;

        DateTime now = DateTime.UtcNow;

        foreach (GachaBannerScheduleEntry entry in schedule.entries)
        {
            if (entry == null || entry.banner != activeBanner)
                continue;

            DateTime end;

            try
            {
                end = entry.EndDate.ToUniversalTime();
            }
            catch
            {
                return;
            }

            SetCountdown(end - now);
            return;
        }
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
            statusText.text = value;
    }

    private void SetCountdown(TimeSpan time)
    {
        if (countdownText == null)
            return;

        if (time <= TimeSpan.Zero)
        {
            countdownText.text = "00:00:00";
            return;
        }

        countdownText.text =
            string.Format(
                "{0:D2}天 {1:D2}:{2:D2}:{3:D2}",
                time.Days,
                time.Hours,
                time.Minutes,
                time.Seconds
            );
    }
}
