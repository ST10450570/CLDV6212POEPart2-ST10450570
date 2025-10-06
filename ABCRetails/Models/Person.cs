using System.Text.Json.Serialization;

namespace ABCRetails.Models
{
    public class Person
    {

        // These attributes map the JSON property (e.g., "name")
        // to our CF property (e.g., "Name") [
        public string? Name { get; set; }

        [JsonPropertyName("email")]

        public string? Email { get; set; }

        [JsonPropertyName("timestamp")]

        public DateTimeOffset? Timestamp { get; set; }
    }
}
