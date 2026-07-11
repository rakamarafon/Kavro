namespace Kavro.Storage;

/// <summary>A message handed to a worker under a lease.</summary>
public sealed record LockedWorkItem(
    long MessageId,
    string InstanceId,
    WorkKind Kind,
    Payload Payload,
    int Attempt,
    DateTimeOffset LeaseExpiresAt);