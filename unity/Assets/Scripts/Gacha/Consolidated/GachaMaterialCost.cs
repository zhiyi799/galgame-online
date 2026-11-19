using System;

[Serializable]
public class GachaMaterialCost
{
    public string materialId;
    public int amount = 1;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(materialId)
            && amount > 0;
    }
}
