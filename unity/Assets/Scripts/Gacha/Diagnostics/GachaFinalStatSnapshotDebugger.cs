using UnityEngine;

/// <summary>
/// 最终战斗属性快照诊断工具。
/// 仅用于 Unity Play Mode 调试。
/// 不修改快照、不触发刷新、不修改角色数据。
/// </summary>
public class GachaFinalStatSnapshotDebugger : MonoBehaviour
{
    [Header("诊断依赖")]
    public GachaFinalStatSnapshotManager snapshotManager;

    [Header("角色 ID")]
    public string characterId;

    [ContextMenu("诊断/检查快照状态")]
    public void CheckSnapshotStatus()
    {
        if (snapshotManager == null)
        {
            Debug.LogError(
                "[SnapshotDebugger] SnapshotManager 未绑定。",
                this
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(characterId))
        {
            Debug.LogError(
                "[SnapshotDebugger] CharacterId 为空。",
                this
            );
            return;
        }

        bool refreshing =
            snapshotManager.IsRefreshing(characterId);

        bool valid =
            snapshotManager.HasValidSnapshot(characterId);

        int version =
            snapshotManager.GetVersion(characterId);

        GachaFinalStatSnapshot snapshot =
            snapshotManager.GetSnapshot(characterId);

        Debug.Log(
            "[SnapshotDebugger] 快照状态\n" +
            $"CharacterId: {characterId}\n" +
            $"Refreshing: {refreshing}\n" +
            $"HasValidSnapshot: {valid}\n" +
            $"Version: {version}\n" +
            $"SnapshotExists: {snapshot != null}",
            this
        );
    }

    [ContextMenu("诊断/读取11项最终属性")]
    public void CheckFinalStats()
    {
        if (snapshotManager == null)
        {
            Debug.LogError(
                "[SnapshotDebugger] SnapshotManager 未绑定。",
                this
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(characterId))
        {
            Debug.LogError(
                "[SnapshotDebugger] CharacterId 为空。",
                this
            );
            return;
        }

        GachaFinalStatSnapshot snapshot =
            snapshotManager.GetSnapshot(characterId);

        if (snapshot == null)
        {
            Debug.LogWarning(
                "[SnapshotDebugger] 快照不存在或已失效，拒绝读取属性。",
                this
            );
            return;
        }

        GachaFinalCombatStats stats =
            snapshot.GetStats();

        if (stats == null || !stats.IsValid())
        {
            Debug.LogError(
                "[SnapshotDebugger] 属性数据为空或无效。",
                this
            );
            return;
        }

        Debug.Log(
            "[SnapshotDebugger] 最终战斗属性\n" +
            $"Attack: {stats.attack:F2}\n" +
            $"MaxHP: {stats.maxHP:F2}\n" +
            $"Defense: {stats.defense:F2}\n" +
            $"Speed: {stats.speed:F2}\n" +
            $"CritRate: {stats.critRate:F4}\n" +
            $"CritDamage: {stats.critDamage:F4}\n" +
            $"EnergyRecharge: {stats.energyRecharge:F4}\n" +
            $"HealingBonus: {stats.healingBonus:F4}\n" +
            $"DamageReduction: {stats.damageReduction:F4}\n" +
            $"SkillDamageBonus: {stats.skillDamageBonus:F4}\n" +
            $"UltimateDamageBonus: {stats.ultimateDamageBonus:F4}",
            this
        );
    }
}
