namespace Kavro.Storage;

/// <summary>Kind of work carried by a queue message.</summary>
public enum WorkKind
{
    Orchestration = 0,
    Activity = 1,
    Timer = 2
}