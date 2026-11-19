using System.Collections.Generic;
using UnityEngine;

namespace AstralBloom.Gacha
{
    [CreateAssetMenu(
        fileName = "AB_GachaBanner",
        menuName = "ASTRAL BLOOM/Gacha/Banner Config")]
    public class GachaBannerConfig : ScriptableObject
    {
        [Header("卡池信息")]
        public string bannerId = "limited_001";
        public string bannerTitle = "星轨回响";
        public string featuredName = "限定角色";
        [TextArea]
        public string subtitle = "在群星交汇之处，回应你的召唤。";

        public Sprite bannerArtwork;
        public Sprite featuredPortrait;

        [Header("角色池")]
        public List<GachaCharacter> fiveStarPool = new();
        public List<GachaCharacter> fourStarPool = new();
        public List<GachaCharacter> threeStarPool = new();

        [Header("UP角色")]
        public List<GachaCharacter> rateUpFiveStars = new();

        [Header("概率")]
        [Range(0, 100)]
        public float fiveStarBaseRate = 0.6f;

        [Range(0, 100)]
        public float fourStarRate = 5.1f;

        [Range(0, 100)]
        public float rateUpChance = 50f;

        [Header("保底")]
        public int fiveStarHardPity = 80;
        public int softPityStart = 66;

        [Range(0, 100)]
        public float softPityIncreasePerPull = 6f;

        public bool enableRateUpGuarantee = true;

        [Header("消耗")]
        public int singlePullCost = 160;
        public int tenPullCost = 1600;

        public int resonancePerPull = 1;
    }
}
