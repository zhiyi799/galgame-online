using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterDatabase",
    menuName = "ASTRAL BLOOM/Characters/Character Database"
)]
public class GachaCharacterDatabase : ScriptableObject
{
    public List<GachaCharacter> characters =
        new List<GachaCharacter>();

    public GachaCharacter GetCharacter(string id)
    {
        return characters.Find(x => x.id == id);
    }

    public List<GachaCharacter> GetAllCharacters()
    {
        return characters;
    }

    public List<GachaCharacter> GetByRarity(Rarity rarity)
    {
        return characters.FindAll(x => x.rarity == rarity);
    }
}
