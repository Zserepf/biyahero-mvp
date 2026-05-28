using System.Linq.Expressions;
using BiyaHero.Api.Domain;
using BiyaHero.Api.Repositories;
using BiyaHero.Api.Services;
using Microsoft.Extensions.Logging;

namespace BiyaHero.Api.Tests.Features.Auth;

/// <summary>
/// Unit tests for AuditLogService.
/// Validates: Requirements 5.10, 8.5
/// - Audit entries are persisted to the repository
/// - Audit entries are mirrored to ILogger (CloudWatch in production)
/// - All required fields (actor, action, target, timestamp) are recorded
/// </summary>
public class AuditLogServiceTests
{
    private readonly FakeAuditLogRepository _repository = new();
    private readonly FakeLogger _logger = new();
    private readonly AuditLogService _service;

    public AuditLogServiceTests()
    {
        _service = new AuditLogService(_repository, _logger);
    }

    [Fact]
    public async Task LogAsync_PersistsEntryToRepository()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        // Act
        await _service.LogAsync(actorId, "user.suspended", "user", targetId);

        // Assert
        Assert.Single(_repository.Entries);
        var entry = _repository.Entries[0];
        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal("user.suspended", entry.Action);
        Assert.Equal("user", entry.TargetType);
        Assert.Equal(targetId, entry.TargetId);
    }

    [Fact]
    public async Task LogAsync_MirrorsToLogger()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        // Act
        await _service.LogAsync(actorId, "user.promoted", "user", targetId);

        // Assert — logger was called
        Assert.Single(_logger.LogEntries);
        Assert.Contains("AUDIT", _logger.LogEntries[0]);
        Assert.Contains(actorId.ToString(), _logger.LogEntries[0]);
        Assert.Contains("user.promoted", _logger.LogEntries[0]);
    }

    [Fact]
    public async Task LogAsync_WithMetadata_IncludesMetadataInEntry()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var metadata = "{\"previousRole\":\"Commuter\",\"newRole\":\"Moderator\"}";

        // Act
        await _service.LogAsync(actorId, "user.promoted", "user", Guid.NewGuid(), metadata);

        // Assert
        Assert.Single(_repository.Entries);
        Assert.Equal(metadata, _repository.Entries[0].Metadata);
    }

    [Fact]
    public async Task LogAsync_WithoutTargetId_PersistsNullTargetId()
    {
        // Arrange
        var actorId = Guid.NewGuid();

        // Act
        await _service.LogAsync(actorId, "auth.login", "session", targetId: null);

        // Assert
        Assert.Single(_repository.Entries);
        Assert.Null(_repository.Entries[0].TargetId);
    }

    [Fact]
    public async Task LogAsync_WithExplicitTimestamp_UsesProvidedTimestamp()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var specificTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        await _service.LogAsync(actorId, "user.suspended", "user", Guid.NewGuid(), specificTime);

        // Assert
        Assert.Single(_repository.Entries);
        Assert.Equal(specificTime, _repository.Entries[0].OccurredAt);
    }

    [Fact]
    public async Task LogAsync_WithoutExplicitTimestamp_UsesCurrentUtcTime()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        await _service.LogAsync(actorId, "user.suspended", "user", Guid.NewGuid());

        // Assert
        var after = DateTime.UtcNow;
        Assert.Single(_repository.Entries);
        Assert.InRange(_repository.Entries[0].OccurredAt, before, after);
    }

    [Fact]
    public async Task LogAsync_SuperAdminSuspendAction_RecordsCorrectFields()
    {
        // Arrange — simulates Req 5.10: Super Admin suspend action
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        // Act
        await _service.LogAsync(adminId, "user.suspended", "user", targetUserId);

        // Assert — all required fields per Req 5.10
        var entry = _repository.Entries[0];
        Assert.Equal(adminId, entry.ActorId);           // Super Admin user ID
        Assert.Equal("user.suspended", entry.Action);   // action performed
        Assert.Equal("user", entry.TargetType);         // target resource type
        Assert.Equal(targetUserId, entry.TargetId);     // target resource identifier
        Assert.True(entry.OccurredAt > DateTime.MinValue); // UTC timestamp
    }

    // ─── Fakes ────────────────────────────────────────────────────────────

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public List<AuditLogEntry> Entries { get; } = new();

        public Task<AuditLogEntry> CreateAsync(AuditLogEntry entity)
        {
            Entries.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<AuditLogEntry?> FindByIdAsync(Guid id)
            => Task.FromResult(Entries.FirstOrDefault(e => e.Id == id));

        public Task<IReadOnlyList<AuditLogEntry>> FindAllAsync()
            => Task.FromResult<IReadOnlyList<AuditLogEntry>>(Entries.AsReadOnly());

        public Task<IReadOnlyList<AuditLogEntry>> WhereAsync(Expression<Func<AuditLogEntry, bool>> predicate)
        {
            var compiled = predicate.Compile();
            return Task.FromResult<IReadOnlyList<AuditLogEntry>>(Entries.Where(compiled).ToList().AsReadOnly());
        }

        public Task<AuditLogEntry> SaveAsync(AuditLogEntry entity) => Task.FromResult(entity);
        public Task<AuditLogEntry> UpdateAsync(AuditLogEntry entity) => Task.FromResult(entity);
        public Task DeleteAsync(AuditLogEntry entity) { Entries.Remove(entity); return Task.CompletedTask; }

        public Task<IReadOnlyList<AuditLogEntry>> FindByActorIdAsync(Guid actorId)
            => Task.FromResult<IReadOnlyList<AuditLogEntry>>(
                Entries.Where(e => e.ActorId == actorId).OrderByDescending(e => e.OccurredAt).ToList().AsReadOnly());

        public Task<IReadOnlyList<AuditLogEntry>> FindByTargetAsync(string targetType, Guid targetId)
            => Task.FromResult<IReadOnlyList<AuditLogEntry>>(
                Entries.Where(e => e.TargetType == targetType && e.TargetId == targetId).ToList().AsReadOnly());

        public Task<IReadOnlyList<AuditLogEntry>> FindByTimeRangeAsync(DateTime from, DateTime to)
            => Task.FromResult<IReadOnlyList<AuditLogEntry>>(
                Entries.Where(e => e.OccurredAt >= from && e.OccurredAt <= to).ToList().AsReadOnly());
    }

    /// <summary>
    /// Minimal fake logger that captures log messages for assertion.
    /// </summary>
    private sealed class FakeLogger : ILogger<AuditLogService>
    {
        public List<string> LogEntries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LogEntries.Add(formatter(state, exception));
        }
    }
}
