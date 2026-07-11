namespace Kavro.Storage;

/// <summary>A message to be enqueued. Payload is opaque to the store.</summary>
public sealed record QueueMessage(
    string InstanceId,
    WorkKind Kind,
    Payload Payload,
    DateTimeOffset VisibleAt,
    string? DedupKey = null);