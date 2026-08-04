namespace Risk.Domain.Errors;

/// <summary>
/// Describes why a <c>GameCommand</c> was rejected by the engine.
/// </summary>
public sealed record GameError(GameErrorCode Code, string Message);
