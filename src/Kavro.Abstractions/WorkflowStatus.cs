namespace Kavro;

/// <summary>Lifecycle status of a workflow instance.</summary>
public enum WorkflowStatus
{
    Pending = 0,
    Running = 1,
    WaitingForTimer = 2,
    WaitingForEvent = 3,
    Completed = 4,
    Failed = 5,
    Terminated = 6
}