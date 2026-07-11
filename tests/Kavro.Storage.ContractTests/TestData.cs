namespace Kavro.Storage.ContractTests;

public static class TestData
{
    public static QueueMessage Msg(
        string instanceId = "wf-1",
        WorkKind kind = WorkKind.Orchestration,
        DateTimeOffset? visibleAt = null,
        string? dedupKey = null,
        string payload = "{}")
        => new(instanceId, kind, Payload.FromUtf8(payload),
            visibleAt ?? DateTimeOffset.MinValue, dedupKey);

    public static WorkflowInstanceRecord Instance(
        string instanceId = "wf-1",
        long version = 1,
        WorkflowStatus status = WorkflowStatus.Pending)
        => new(instanceId, Name: "TestWorkflow", DefinitionVersion: "1.0",
            version, status, Input: null, Result: null,
            CreatedAt: DateTimeOffset.UnixEpoch, UpdatedAt: DateTimeOffset.UnixEpoch);
}