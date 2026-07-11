namespace Kavro.Storage;

/// <summary>Atomic batch of changes produced by processing one work item.</summary>
public sealed record WorkItemCommit(
    long AckedMessageId,
    string InstanceId,
    long ExpectedInstanceVersion,
    WorkflowInstanceUpdate? InstanceUpdate,
    IReadOnlyList<HistoryEvent> NewHistoryEvents,
    IReadOnlyList<QueueMessage> OutgoingMessages);