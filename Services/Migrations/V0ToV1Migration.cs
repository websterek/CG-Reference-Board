using System.Text.Json;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services.Migrations;

public class V0ToV1Migration : IBoardMigration
{
    public int From => 0;
    public int To => 1;

    public JsonElement Migrate(JsonElement content)
    {
        // For now, return content as-is since we're handling Pencil->Brush elsewhere
        // The migration infrastructure is in place for future migrations
        return content;
    }
}