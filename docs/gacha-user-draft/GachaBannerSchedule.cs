using System;
using UnityEngine;

[Serializable]
public class GachaBannerScheduleEntry
{
    [Header("卡池")]
    public GachaBannerConfig banner;

    [Header("开放时间")]
    public string startTime;
    public string endTime;

    [Header("活动状态")]
    public bool enabled = true;

    public DateTime StartDate
    {
        get
        {
            return DateTime.Parse(
                startTime,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind
            );
        }
    }

    public DateTime EndDate
    {
        get
        {
            return DateTime.Parse(
                endTime,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind
            );
        }
    }

    public bool IsActive(DateTime now)
    {
        if (!enabled || banner == null || !banner.isEnabled)
            return false;

        return now >= StartDate && now < EndDate;
    }
}

[CreateAssetMenu(
    fileName = "GachaBannerSchedule",
    menuName = "ASTRAL BLOOM/Gacha/Banner Schedule"
)]
public class GachaBannerSchedule : ScriptableObject
{
    public System.Collections.Generic.List<GachaBannerScheduleEntry> entries =
        new System.Collections.Generic.List<GachaBannerScheduleEntry>();
}
