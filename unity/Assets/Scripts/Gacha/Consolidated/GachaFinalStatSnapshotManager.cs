using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最终属性快照刷新管理器。
///
/// 负责：
/// 1. 保存角色的最终属性快照。
/// 2. 标记快照过期。
/// 3. 按需重新计算。
/// 4. 防止重复刷新。
///
/// 不直接修改角色、武器、潜能或天赋数据。
/// </summary>
public class GachaFinalStatSnapshotManager : MonoBehaviour
{
    private sealed class SnapshotEntry
    {
        public GachaFinalStatSnapshot snapshot;

        public bool dirty = true;

        public bool removed;

        public int version;

        // 每次属性变化都会递增。
        public int sourceVersion;
    }

    private readonly Dictionary<string, SnapshotEntry> snapshots =
        new Dictionary<string, SnapshotEntry>();

    private readonly HashSet<string> refreshing =
        new HashSet<string>();

    /// <summary>
    /// 快照成功刷新时触发。
    /// </summary>
    public event Action<string, GachaFinalStatSnapshot> OnSnapshotUpdated;

    /// <summary>
    /// 快照失效时触发。
    /// </summary>
    public event Action<string> OnSnapshotInvalidated;

    /// <summary>
    /// 获取快照版本。
    /// </summary>
    public int GetVersion(string characterId)
    {
        if (!IsValidId(characterId))
            return -1;

        if (!snapshots.TryGetValue(characterId, out var entry))
            return -1;

        return entry.version;
    }

    /// <summary>
    /// 判断快照是否存在且有效。
    /// </summary>
    public bool HasValidSnapshot(string characterId)
    {
        if (!IsValidId(characterId))
            return false;

        return snapshots.TryGetValue(characterId, out var entry)
            && !entry.dirty
            && entry.snapshot != null;
    }

    /// <summary>
    /// 标记一个角色的快照过期。
    /// </summary>
    public void Invalidate(string characterId)
    {
        if (!IsValidId(characterId))
            return;

        if (!snapshots.TryGetValue(characterId, out var entry))
        {
            entry = new SnapshotEntry();
            snapshots.Add(characterId, entry);
        }

        entry.dirty = true;
        entry.sourceVersion++;

        RaiseSnapshotInvalidated(characterId);
    }

    /// <summary>
    /// 标记全部角色快照过期。
    /// 适用于全局属性规则或配置发生变化。
    /// </summary>
    public void InvalidateAll()
    {
        var invalidatedIds =
            new System.Collections.Generic.List<string>();

        foreach (var pair in snapshots)
        {
            pair.Value.dirty = true;
            pair.Value.sourceVersion++;

            invalidatedIds.Add(pair.Key);
        }

        // 遍历结束后再触发事件。
        foreach (string characterId in invalidatedIds)
        {
            RaiseSnapshotInvalidated(characterId);
        }
    }

    /// <summary>
    /// 批量标记指定角色的快照过期。
    /// </summary>
    public void InvalidateBatch(
        System.Collections.Generic.IEnumerable<string> characterIds)
    {
        if (characterIds == null)
            return;

        foreach (string characterId in characterIds)
        {
            Invalidate(characterId);
        }
    }

    /// <summary>
    /// 判断指定角色是否正在刷新。
    /// </summary>
    public bool IsRefreshing(string characterId)
    {
        if (!IsValidId(characterId))
            return false;

        return refreshing.Contains(characterId);
    }

    /// <summary>
    /// 提交已经计算完成的快照。
    /// 只允许提交有效快照。
    /// 仅供 Refresh() 内部调用，外部无法绕过
    /// sourceVersion 校验直接提交。
    /// </summary>
    private bool CommitSnapshot(
        string characterId,
        GachaFinalStatSnapshot snapshot)
    {
        if (!IsValidId(characterId) || snapshot == null)
            return false;

        if (!snapshots.TryGetValue(characterId, out var entry))
        {
            entry = new SnapshotEntry();
            snapshots.Add(characterId, entry);
        }

        entry.snapshot = snapshot;
        entry.dirty = false;
        entry.version++;

        RaiseSnapshotUpdated(characterId, snapshot);

        return true;
    }

    /// <summary>
    /// 获取当前有效快照。
    /// 过期或不存在时返回 null。
    /// </summary>
    public GachaFinalStatSnapshot GetSnapshot(string characterId)
    {
        if (!IsValidId(characterId))
            return null;

        if (!snapshots.TryGetValue(characterId, out var entry))
            return null;

        if (entry.dirty)
            return null;

        return entry.snapshot;
    }

    /// <summary>
    /// 按需刷新。
    ///
    /// collectAndCalculate 必须：
    /// 1. 收集四个属性桶。
    /// 2. 调用 GachaFinalStatProvider。
    /// 3. 返回不可变快照。
    ///
    /// 具体管理器接线留待真实接口确认。
    /// </summary>
    public bool Refresh(
        string characterId,
        System.Func<GachaFinalStatSnapshot> collectAndCalculate)
    {
        if (!IsValidId(characterId) || collectAndCalculate == null)
            return false;

        if (refreshing.Contains(characterId))
            return false;

        if (!snapshots.TryGetValue(characterId, out var entry))
        {
            entry = new SnapshotEntry();
            snapshots.Add(characterId, entry);
        }

        refreshing.Add(characterId);

        // 记录开始计算时的数据版本。
        int capturedSourceVersion = entry.sourceVersion;

        try
        {
            GachaFinalStatSnapshot snapshot =
                collectAndCalculate();

            if (snapshot == null)
                return false;

            // 刷新期间快照被删除，拒绝提交旧快照。
            if (entry.removed ||
                !snapshots.TryGetValue(characterId, out var currentEntry) ||
                !ReferenceEquals(entry, currentEntry))
            {
                return false;
            }

            // 计算期间属性发生变化，拒绝提交旧快照。
            if (entry.sourceVersion != capturedSourceVersion)
            {
                entry.dirty = true;

                Debug.LogWarning(
                    "[GachaFinalStatSnapshotManager] 属性在刷新期间发生变化，" +
                    "已拒绝提交旧快照：" + characterId
                );

                return false;
            }

            return CommitSnapshot(characterId, snapshot);
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[GachaFinalStatSnapshotManager] 刷新失败：" +
                characterId + "\n" + exception
            );

            return false;
        }
        finally
        {
            refreshing.Remove(characterId);
        }
    }

    /// <summary>
    /// 获取快照；如果过期则执行刷新。
    /// </summary>
    public GachaFinalStatSnapshot GetOrRefresh(
        string characterId,
        Func<GachaFinalStatSnapshot> collectAndCalculate)
    {
        if (HasValidSnapshot(characterId))
            return GetSnapshot(characterId);

        bool success = Refresh(
            characterId,
            collectAndCalculate
        );

        if (!success)
            return null;

        return GetSnapshot(characterId);
    }

    /// <summary>
    /// 删除指定角色快照。
    /// 例如角色数据被删除时调用。
    /// </summary>
    public void RemoveSnapshot(string characterId)
    {
        if (!IsValidId(characterId))
            return;

        if (snapshots.TryGetValue(characterId, out var entry))
        {
            entry.removed = true;
            entry.dirty = true;
            entry.sourceVersion++;
        }

        snapshots.Remove(characterId);
    }

    /// <summary>
    /// 清除全部快照。
    /// 不清空 refreshing：正在执行的刷新标记
    /// 由 Refresh() 的 finally 自行移除，
    /// 避免同一角色在旧刷新尚未结束时再次进入刷新。
    /// </summary>
    public void ClearAll()
    {
        snapshots.Clear();
    }

    /// <summary>
    /// 安全派发快照更新事件。
    /// 逐个调用订阅者，单个订阅者异常不影响其他订阅者，
    /// 也不影响快照提交结果。
    /// </summary>
    private void RaiseSnapshotUpdated(
        string characterId,
        GachaFinalStatSnapshot snapshot)
    {
        Action<string, GachaFinalStatSnapshot> handlers =
            OnSnapshotUpdated;

        if (handlers == null)
            return;

        foreach (Action<string, GachaFinalStatSnapshot> handler
            in handlers.GetInvocationList())
        {
            try
            {
                handler?.Invoke(characterId, snapshot);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[GachaFinalStatSnapshotManager] " +
                    "快照更新事件订阅者执行失败：" +
                    characterId + "\n" + exception
                );
            }
        }
    }

    /// <summary>
    /// 安全派发快照失效事件。
    /// 逐个调用订阅者，单个订阅者异常不影响其他订阅者，
    /// 也不中断其他角色的失效通知。
    /// </summary>
    private void RaiseSnapshotInvalidated(string characterId)
    {
        Action<string> handlers = OnSnapshotInvalidated;

        if (handlers == null)
            return;

        foreach (Action<string> handler in handlers.GetInvocationList())
        {
            try
            {
                handler?.Invoke(characterId);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[GachaFinalStatSnapshotManager] " +
                    "快照失效事件订阅者执行失败：" +
                    characterId + "\n" + exception
                );
            }
        }
    }

    private static bool IsValidId(string characterId)
    {
        return !string.IsNullOrWhiteSpace(characterId);
    }
}
