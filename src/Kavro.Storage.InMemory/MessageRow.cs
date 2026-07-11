namespace Kavro.Storage.InMemory;

/// <summary>Mutable queue row, mimics a database table row. Mutated only under the store lock.</summary>
internal sealed class MessageRow(long id, QueueMessage message)
{
    public long Id { get; } = id;
    public QueueMessage Message { get; } = message;
    public int DeliveryCount { get; set; }
    public string? LockedBy { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
}