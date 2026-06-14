namespace Snakk.Application.DTOs.Admin;

public enum AdminDiscussionMutationStatus
{
    Success,
    NotFound,
    AlreadyInState,
    ActorNotFound
}

public class AdminDiscussionMutationResult
{
    public AdminDiscussionMutationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }

    public static AdminDiscussionMutationResult Ok() =>
        new() { Status = AdminDiscussionMutationStatus.Success };

    public static AdminDiscussionMutationResult NotFound() =>
        new() { Status = AdminDiscussionMutationStatus.NotFound };

    public static AdminDiscussionMutationResult AlreadyInState(string message) =>
        new() { Status = AdminDiscussionMutationStatus.AlreadyInState, ErrorMessage = message };
}
