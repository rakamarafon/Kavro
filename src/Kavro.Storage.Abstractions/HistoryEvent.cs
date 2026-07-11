namespace Kavro.Storage;

/// <summary>Append-only history event of a workflow instance.</summary>
public sealed record HistoryEvent(
    string InstanceId,
    long SequenceNumber,
    string EventType,
    Payload Payload,
    DateTimeOffset CreatedAt);