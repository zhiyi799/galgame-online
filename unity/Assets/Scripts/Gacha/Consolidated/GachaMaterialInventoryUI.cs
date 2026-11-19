using UnityEngine;

public class GachaMaterialInventoryUI : MonoBehaviour
{
    [Header("系统")]
    public GachaMaterialInventory inventory;

    [Header("UI")]
    public Transform content;
    public GachaMaterialItemUI itemPrefab;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (inventory == null ||
            content == null ||
            itemPrefab == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        var materials = inventory.GetAllMaterials();

        foreach (GachaMaterialSave entry in materials)
        {
            if (entry == null || entry.amount <= 0)
                continue;

            GachaMaterialConfig config =
                inventory.GetConfigForEntry(entry);

            if (config == null)
                continue;

            GachaMaterialItemUI item =
                Instantiate(itemPrefab, content);

            item.SetData(config, entry.amount);
        }
    }
}
