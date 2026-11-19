using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "GachaCharacterDatabase",
    menuName = "ASTRAL BLOOM/Character/Character Database")]
public class GachaCharacterDatabase : ScriptableObject
{
    [SerializeField]
    private List<GachaCharacterDatabaseEntry> characters =
        new List<GachaCharacterDatabaseEntry>();

    private Dictionary<string, GachaCharacterDatabaseEntry> lookup;

    private void OnEnable()
    {
        RebuildLookup();
    }

    public void RebuildLookup()
    {
        lookup = new Dictionary<string, GachaCharacterDatabaseEntry>(
            StringComparer.Ordinal);

        if (characters == null)
            return;

        foreach (GachaCharacterDatabaseEntry entry in characters)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.characterId))
                continue;

            // 重复 ID 不覆盖已有定义，避免静默读取错误角色。
            if (!lookup.ContainsKey(entry.characterId))
                lookup.Add(entry.characterId, entry);
            else
                Debug.LogError(
                    "角色数据库存在重复 ID：" + entry.characterId,
                    this);
        }
    }

    public GachaCharacterDatabaseEntry GetCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        if (lookup == null)
            RebuildLookup();

        return lookup.TryGetValue(characterId, out var entry)
            ? entry
            : null;
    }

    public bool Contains(string characterId)
    {
        return GetCharacter(characterId) != null;
    }

    public IReadOnlyList<GachaCharacterDatabaseEntry> GetAllCharacters()
    {
        return characters;
    }
}

[Serializable]
public class GachaCharacterDatabaseEntry
{
    public string characterId;

    public string characterName;

    public GachaFinalCombatStats baseStats;

    public GachaGrowthConfig growthConfig;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(characterId) &&
               baseStats != null &&
               baseStats.IsValid() &&
               growthConfig != null &&
               growthConfig.IsValid();
    }
}
