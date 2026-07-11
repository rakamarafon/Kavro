namespace Kavro.Storage;

/// <summary>Thrown when a commit carries a stale instance version.</summary>
public sealed class WorkflowConcurrencyException(string instanceId, long expectedVersion)
    : Exception($"Version conflict on instance '{instanceId}': expected version {expectedVersion}.")
{
    public string InstanceId { get; } = instanceId;
    public long ExpectedVersion { get; } = expectedVersion;
}