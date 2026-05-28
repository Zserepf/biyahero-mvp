namespace BiyaHero.Api.Features.I18n;

/// <summary>
/// Request body for POST /v1/i18n/missing-keys.
/// Reports translation keys that are missing for a given locale.
/// The frontend fires this when a translated string is not found in the active bundle.
/// Requirements: 10.6
/// </summary>
public sealed record MissingKeysRequest(List<MissingKeyEntry> Keys);

/// <summary>
/// A single missing translation key entry with locale and page context.
/// </summary>
public sealed record MissingKeyEntry(string Key, string Locale, string? Context);
