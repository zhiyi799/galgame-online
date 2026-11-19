using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GachaMaterialItemUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text materialNameText;
    public TMP_Text amountText;

    public void SetData(
        GachaMaterialConfig config,
        int amount)
    {
        if (config == null)
            return;

        if (icon != null)
        {
            icon.sprite = config.icon;
            icon.enabled = config.icon != null;
        }

        if (materialNameText != null)
            materialNameText.text = config.materialName;

        if (amountText != null)
            amountText.text = amount.ToString();
    }
}
