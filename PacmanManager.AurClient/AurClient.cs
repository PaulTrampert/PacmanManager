using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Encodings.Web;

namespace PacmanManager.AurClient;

public class AurClient(HttpClient client) : IAurClient
{
    private async Task<T> GetAsync<T>(string url)
        where T : AurResponse
    {
        var result = await client.GetFromJsonAsync<AurResponse>(url);
        if (result == null)
        {
            throw new Exception($"Could not find response {url}");
        }
        if (result.Type == AurReturnType.Error)
        {
            throw new AurServerException((AurErrorResponse)result);
        }
        
        return (T)result;
    }
    
    private static string GetSearchByValue(AurSearchBy searchBy) => searchBy switch
    {
        AurSearchBy.Name => "name",
        AurSearchBy.NameDesc => "name-desc",
        AurSearchBy.Maintainer => "maintainer",
        AurSearchBy.Depends => "depends",
        AurSearchBy.MakeDepends => "makedepends",
        AurSearchBy.OptDepends => "optdepends",
        AurSearchBy.CheckDepends => "checkdepends",
        _ => throw new ArgumentOutOfRangeException(nameof(searchBy), searchBy, "Invalid search by value")
    };
    
    public Task<AurResponse<AurBasicPackageInfo>> Search(string term, AurSearchBy searchBy)
    {
        return GetAsync<AurResponse<AurBasicPackageInfo>>($"/rpc/v5/search/{UrlEncoder.Default.Encode(term)}?by={UrlEncoder.Default.Encode(GetSearchByValue(searchBy))}");
    }
    
    public Task<AurResponse<AurFullPackageInfo>> Info(IEnumerable<string> packageNames)
    {
        var packageNamesArray = packageNames
            .Where(packageName => !string.IsNullOrWhiteSpace(packageName))
            .Distinct()
            .ToArray();
        if (!packageNamesArray.Any())
        {
            throw new ArgumentException("No package names provided", nameof(packageNames));
        }
        var namesQuery = string.Join("&", packageNamesArray.Select(n => $"arg[]={UrlEncoder.Default.Encode(n)}"));
        return GetAsync<AurResponse<AurFullPackageInfo>>($"/rpc/v5/info?{namesQuery}");
    }
}