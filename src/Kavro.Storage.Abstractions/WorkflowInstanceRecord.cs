namespace Kavro.Storage;

/// <summary>Persistent record of a workflow instance.</summary>
public sealed record WorkflowInstanceRecord(
    string InstanceId,
    string Name,
    string DefinitionVersion,
    long Version,
    WorkflowStatus Status,
    string? Input,
    string? Result,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);