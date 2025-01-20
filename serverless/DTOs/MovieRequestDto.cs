#nullable enable
using System;
using Newtonsoft.Json;

namespace serverless.DTOs;

public class MovieRequestDto
{
    [JsonProperty("id")] 
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("title")]
    public string Title { get; set; } = String.Empty;
    [JsonProperty("genre")]
    public string? Genre { get; set; }
    [JsonProperty("description")]
    public string? Description { get; set; }
    [JsonProperty("imageUrl")]
    public string ImageUrl { get; set; } = String.Empty;
    [JsonProperty("movieUrl")]
    public string MovieUrl { get; set; } = String.Empty;
}