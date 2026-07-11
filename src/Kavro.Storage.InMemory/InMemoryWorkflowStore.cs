namespace Kavro.Storage.InMemory;

public sealed class InMemoryWorkflowStore(TimeProvider time) : IWorkflowStore
{
    private readonly object syncLock = new();
    private readonly List<MessageRow> memoryMessages = new();
    private readonly Dictionary<string, WorkflowInstanceRecord>  instances = new();
    private readonly List<HistoryEvent> events = new();
    private long nextMessageId;
    
    public Task CreateInstanceAsync(WorkflowInstanceRecord instance, QueueMessage startMessage,
        CancellationToken ct = default)
    {
        lock (syncLock)
        {
            if(instances.ContainsKey(instance.InstanceId))
                throw new InvalidOperationException($"Instance {instance.InstanceId} already exists");
            
            instances.Add(instance.InstanceId, instance);
            memoryMessages.Add(new MessageRow(++nextMessageId, startMessage));
        }
        return Task.CompletedTask;
    }

    public Task EnqueueAsync(QueueMessage message, CancellationToken ct = default)
    {
        lock (syncLock)
        {
            memoryMessages.Add(new MessageRow(++nextMessageId, message));
        }
        return Task.CompletedTask;   
    }

    public Task<IReadOnlyList<LockedWorkItem>> LockNextAsync(string workerId, TimeSpan leaseDuration, int maxItems,
        CancellationToken ct = default)
    {
        DateTimeOffset now = time.GetUtcNow();

        lock (syncLock)
        {
            var locked = new List<LockedWorkItem>(capacity: maxItems);
            
            var ready = memoryMessages
                .Where(r => r.Message.VisibleAt <= now
                            && (r.LockedBy is null || r.LeaseExpiresAt < now))
                .OrderBy(r => r.Message.VisibleAt)
                .ThenBy(r => r.Id)
                .Take(maxItems);
            
            foreach (var row in ready.ToList())
            {
                row.DeliveryCount++;
                row.LockedBy = workerId;
                row.LeaseExpiresAt = now + leaseDuration;

                locked.Add(new LockedWorkItem(
                    row.Id,
                    row.Message.InstanceId,
                    row.Message.Kind,
                    row.Message.Payload,
                    Attempt: row.DeliveryCount,
                    LeaseExpiresAt: row.LeaseExpiresAt.Value));
            }

            return Task.FromResult<IReadOnlyList<LockedWorkItem>>(locked);
        }
    }

    public Task<bool> RenewLeaseAsync(long messageId, string workerId, TimeSpan extension,
        CancellationToken ct = default)
    {
        DateTimeOffset now = time.GetUtcNow();
        
        lock (syncLock)
        {
            MessageRow? row = memoryMessages.Find(x => x.Id == messageId);
            if (row is null)
                return Task.FromResult(false);
            
            if(row.LockedBy != workerId || row.LeaseExpiresAt < now)
                return Task.FromResult(false);
            else
            {
                row.LeaseExpiresAt = now + extension;
                return Task.FromResult(true);
            }
        }
    }
    public Task CommitAsync(WorkItemCommit commit, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<WorkflowInstanceRecord?> GetInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        WorkflowInstanceRecord? instance;
        lock (syncLock)
        {
            instances.TryGetValue(instanceId, out  instance);
        }
        return Task.FromResult(instance);
    }

    public Task<IReadOnlyList<HistoryEvent>> GetHistoryAsync(string instanceId, CancellationToken ct = default)
    {
        IReadOnlyList<HistoryEvent> historyEvents;
        lock (syncLock)
        {
            historyEvents = events
                .Where(x => x.InstanceId == instanceId)
                .OrderBy(x => x.SequenceNumber)
                .ToList();
        }
        return Task.FromResult(historyEvents);
    }
}