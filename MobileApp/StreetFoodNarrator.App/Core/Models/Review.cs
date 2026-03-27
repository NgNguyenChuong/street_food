using SQLite;

namespace StreetFoodNarrator.App.Core.Models;

/// <summary>
/// Review model for POI rating and comments
/// </summary>
[Table("Reviews")]
public class Review
{
    [PrimaryKey, AutoIncrement]
    public int LocalId { get; set; }

    /// <summary>
    /// ID on the server (ObjectId string)
    /// </summary>
    public string? RemoteId { get; set; }

    [Indexed]
    public int POI_ID { get; set; }

    public string UserName { get; set; } = "Khách";

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string DisplayTime => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    
    [Ignore]
    public string UserAvatar => string.IsNullOrEmpty(UserName) ? "user_placeholder.png" : $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(UserName)}&background=random";

    [Ignore]
    public string StarDisplay => new string('★', Rating) + new string('☆', 5 - Rating);
}
