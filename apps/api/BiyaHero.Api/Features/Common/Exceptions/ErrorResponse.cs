namespace BiyaHero.Api.Features.Common.Exceptions;

/// <summary>
/// Standard error envelope returned by the ExceptionHandlingMiddleware.
/// Must be a named type so it can be registered with the AOT JSON source generator.
/// </summary>
public sealed record ErrorResponse(ErrorDetail Error);

public sealed record ErrorDetail(string Code, string Message, object? Details);
