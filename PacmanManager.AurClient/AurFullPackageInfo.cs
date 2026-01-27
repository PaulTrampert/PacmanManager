namespace PacmanManager.AurClient;

public record AurFullPackageInfo : AurBasicPackageInfo
{
    public IEnumerable<string> Depends { get; init; }
    
    public IEnumerable<string> MakeDepends { get; init; }
    
    public IEnumerable<string> OptDepends { get; init; }
    
    public IEnumerable<string> CheckDepends { get; init; }
    
    public IEnumerable<string> Conflicts { get; init; }
    
    public IEnumerable<string> Provides { get; init; }
    
    public IEnumerable<string> Replaces { get; init; }
    
    public IEnumerable<string> Groups { get; init; }
    
    public IEnumerable<string> License { get; init; }
    
    public IEnumerable<string> Keywords { get; init; }
};