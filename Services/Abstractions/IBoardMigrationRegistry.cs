using System.Collections.Generic;
using System.Text.Json;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services.Abstractions;

public interface IBoardMigrationRegistry
{
    void Register(IBoardMigration migration);
    JsonElement MigrateToLatest(JsonElement content, int fromVersion);
    int CurrentVersion { get; }
}