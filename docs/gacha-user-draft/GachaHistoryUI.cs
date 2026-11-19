using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GachaHistoryUI : MonoBehaviour
{
    [Header("数据")]
    public GachaManager manager;

    [Header("界面")]
    public GameObject historyPanel;
    public Transform contentRoot;
    public Button recordPrefab;

    [Header("筛选")]
    public Dropdown rarityDropdown;
    public Dropdown rateUpDropdown;

    [Header("记录数量")]
    public Text recordCountText;

    private readonly List<Button> generatedRecords =
        new List<Button>();

    private void Start()
    {
        SetupDropdowns();

        if (rarityDropdown != null)
            rarityDropdown.onValueChanged.AddListener(
                delegate { Refresh(); }
            );

        if (rateUpDropdown != null)
            rateUpDropdown.onValueChanged.AddListener(
                delegate { Refresh(); }
            );
    }

    private void SetupDropdowns()
    {
        if (rarityDropdown != null)
        {
            rarityDropdown.ClearOptions();

            rarityDropdown.AddOptions(
                new List<string>
                {
                    "全部稀有度",
                    "三星",
                    "四星",
                    "五星"
                }
            );
        }

        if (rateUpDropdown != null)
        {
            rateUpDropdown.ClearOptions();

            rateUpDropdown.AddOptions(
                new List<string>
                {
                    "全部角色",
                    "UP 角色",
                    "非 UP 角色"
                }
            );
        }
    }

    public void Open()
    {
        if (historyPanel != null)
            historyPanel.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        if (historyPanel != null)
            historyPanel.SetActive(false);
    }

    public void Refresh()
    {
        ClearRecords();

        if (manager == null ||
            manager.Save == null ||
            contentRoot == null ||
            recordPrefab == null)
            return;

        int rarityFilter = rarityDropdown != null
            ? rarityDropdown.value
            : 0;

        int upFilter = rateUpDropdown != null
            ? rateUpDropdown.value
            : 0;

        int displayed = 0;

        foreach (GachaPullRecord record in manager.Save.history)
        {
            if (record == null)
                continue;

            if (!MatchesRarity(record, rarityFilter))
                continue;

            if (!MatchesRateUp(record, upFilter))
                continue;

            CreateRecord(record);
            displayed++;
        }

        if (recordCountText != null)
        {
            recordCountText.text =
                "显示记录：" + displayed;
        }
    }

    private bool MatchesRarity(
        GachaPullRecord record,
        int filter)
    {
        if (filter == 0)
            return true;

        return (int)record.rarity == filter + 2;
    }

    private bool MatchesRateUp(
        GachaPullRecord record,
        int filter)
    {
        if (filter == 0)
            return true;

        if (filter == 1)
            return record.isRateUp;

        return !record.isRateUp;
    }

    private void CreateRecord(GachaPullRecord record)
    {
        Button button = Instantiate(
            recordPrefab,
            contentRoot
        );

        generatedRecords.Add(button);

        Text label = button.GetComponentInChildren<Text>();

        if (label != null)
        {
            string stars = new string(
                '★',
                (int)record.rarity
            );

            label.text =
                "#" + record.pullIndex +
                "  " + record.characterName +
                "  " + stars +
                "\n" + record.time +
                (record.isRateUp ? "  [UP]" : "");
        }

        button.interactable = false;
    }

    private void ClearRecords()
    {
        foreach (Button button in generatedRecords)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        generatedRecords.Clear();
    }

    private void OnDestroy()
    {
        if (rarityDropdown != null)
            rarityDropdown.onValueChanged.RemoveAllListeners();

        if (rateUpDropdown != null)
            rateUpDropdown.onValueChanged.RemoveAllListeners();
    }
}
