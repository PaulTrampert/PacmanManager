namespace PacmanManager.AurClient;

public interface IAurClient
{
    Task<AurResponse<AurBasicPackageInfo>> Search(string term, AurSearchBy searchBy);

    Task<AurResponse<AurFullPackageInfo>> Info(IEnumerable<string> packageNames);
}