using UnityEngine;

[CreateAssetMenu(
    fileName = "NewCharacterCostume",
    menuName = "ASTRAL BLOOM/Characters/Costume"
)]
public class GachaCharacterCostume : ScriptableObject
{
    [Header("服装信息")]
    public string costumeId;
    public string characterId;
    public string costumeName;

    [TextArea(2, 5)]
    public string description;

    [Header("立绘")]
    public Sprite artwork;

    [Header("默认状态")]
    public bool unlockedByDefault;
}
