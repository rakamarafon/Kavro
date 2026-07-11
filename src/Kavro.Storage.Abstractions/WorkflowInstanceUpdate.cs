namespace Kavro.Storage;

/// <summary>Mutation of instance state applied within a commit.</summary>
public sealed record WorkflowInstanceUpdate(
    WorkflowStatus Status,
    string? Result);