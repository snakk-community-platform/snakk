namespace Snakk.Application.DTOs.Responses;

/// <summary>Generic message response.</summary>
public record MessageResponse(string Message);

/// <summary>Generic success response with optional processed count.</summary>
public record SuccessResponse(bool Success, int? Processed = null);

/// <summary>Generic error response.</summary>
public record ErrorResponse(string Error);
