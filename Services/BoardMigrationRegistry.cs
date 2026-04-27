using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class BoardMigrationRegistry : IBoardMigrationRegistry
{
    private readonly List<IBoardMigration> _migrations = new();
    
    public int CurrentVersion => 1;

    public void Register(IBoardMigration migration)
    {
        _migrations.Add(migration);
    }

    public JsonElement MigrateToLatest(JsonElement content, int fromVersion)
    {
        var sorted = _migrations
            .Where(m => m.From >= fromVersion && m.From < CurrentVersion)
            .OrderBy(m => m.From)
            .ToList();
        
        var result = content;
        foreach (var migration in sorted)
        {
            result = migration.Migrate(result);
        }
        
        return result;
    }
}