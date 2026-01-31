using System.Text.Json.Serialization;

namespace PacmanManager.AurClient;

public record AurBasicPackageInfo
{
    public long Id { get; init; }
    
    public required string Name { get; init; }
    
    public long PackageBaseId { get; init; }
    
    public required string PackageBase { get; init; }
    
    public required string Version { get; init; }
    
    public string? Description { get; init; }
    
    public string? Url { get; init; }
    
    public long NumVotes { get; init; }
    
    public decimal Popularity { get; init; }
    
    [JsonConverter(typeof(NullableDateTimeOffsetConverter))]
    public DateTimeOffset? OutOfDate { get; init; }
    
    public string? Maintainer { get; init; }
    
    [JsonConverter(typeof(DateTimeOffsetConverter))]
    public DateTimeOffset FirstSubmitted { get; init; }
    
    [JsonConverter(typeof(DateTimeOffsetConverter))]
    public DateTimeOffset LastModified { get; init; }
    
    public string? UrlPath { get; init; }
};