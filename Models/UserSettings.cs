namespace CGReferenceBoard.Models;

/// <summary>Persisted user preference bag (serialized to user_settings.json).</summary>
public sealed class UserSettings
{
    public string AnnotationEffect { get; set; } = "None";
    public string GridBackground { get; set; } = "Dots";
}
