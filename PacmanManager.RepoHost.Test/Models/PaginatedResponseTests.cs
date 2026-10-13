using System.Text.Json;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test.Models;

/// <summary>
/// Tests for how a page's echoed sort is written and read.
/// </summary>
[TestFixture]
public class PaginatedResponseTests
{
    [Test]
    public void Sort_RoundTripsByName_UnderDefaultWebOptions()
    {
        // HttpClient's JSON helpers use the web defaults, with no string enum converter, which is
        // how every E2E test reads a page; the direction must still be written and read by name.
        var page = new PaginatedResponse<string>
        {
            Results = ["a"],
            Offset = 0,
            Total = 1,
            Sort = new AppliedSort { SortBy = "Created", Direction = SortDirection.Descending }
        };

        var json = JsonSerializer.Serialize(page, JsonSerializerOptions.Web);
        var read = JsonSerializer.Deserialize<PaginatedResponse<string>>(json, JsonSerializerOptions.Web);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"sort\":{\"sortBy\":\"Created\",\"direction\":\"Descending\"}"));
            Assert.That(read!.Sort, Is.EqualTo(page.Sort));
        });
    }
}
