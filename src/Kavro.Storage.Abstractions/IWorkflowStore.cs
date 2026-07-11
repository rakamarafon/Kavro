namespace Kavro.Storage;

public interface IWorkflowStore
{
    /// <summary>Atomically creates the instance and enqueues its start message.</summary>
    Task CreateInstanceAsync(
        WorkflowInstanceRecord instance,
        QueueMessage startMessage,
        CancellationToken ct = default);

    /// <summary>Enqueues a message. Duplicate DedupKey is silently ignored.</summary>
    Task EnqueueAsync(QueueMessage message, CancellationToken ct = default);

    /// <summary>
    /// Atomically locks up to <paramref name="maxItems"/> ready messages
    /// (VisibleAt in the past, no active lease) for the given worker.
    /// </summary>
    Task<IReadOnlyList<LockedWorkItem>> LockNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        int maxItems,
        CancellationToken ct = default);

    /// <summary>Extends a lease. Returns false if the lease is expired or owned by another worker.</summary>
    Task<bool> RenewLeaseAsync(
        long messageId,
        string workerId,
        TimeSpan extension,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically applies the whole commit: acks the message, appends history,
    /// updates the instance (with optimistic concurrency check) and enqueues outgoing messages.
    /// Throws <see cref="WorkflowConcurrencyException"/> and applies NOTHING on version conflict.
    /// </summary>
    Task CommitAsync(WorkItemCommit commit, CancellationToken ct = default);

    Task<WorkflowInstanceRecord?> GetInstanceAsync(string instanceId, CancellationToken ct = default);

    Task<IReadOnlyList<HistoryEvent>> GetHistoryAsync(string instanceId, CancellationToken ct = default);
}