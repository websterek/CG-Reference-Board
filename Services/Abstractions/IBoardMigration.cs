using System.Text.Json;

namespace CGReferenceBoard.Services.Abstractions;

public interface IBoardMigration
{
    int From { get; }
    int To { get; }
    JsonElement Migrate(JsonElement content);
}