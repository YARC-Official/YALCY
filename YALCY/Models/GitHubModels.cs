using System.Text.Json.Serialization;

namespace YALCY.Models
{
    public class GitHubSearchResult
    {
        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }
        
        [JsonPropertyName("incomplete_results")]
        public bool IncompleteResults { get; set; }
        
        [JsonPropertyName("items")]
        public GitHubUserInfo[]? Items { get; set; }
    }

    public class GitHubUserInfo
    {
        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }
        
        [JsonPropertyName("login")]
        public string? Login { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("node_id")]
        public string? NodeId { get; set; }
    }
}
