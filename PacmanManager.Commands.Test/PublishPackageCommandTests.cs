using System.Text.Json;

namespace PacmanManager.Commands.Test;

public class PublishPackageCommandTests
{
    private static IEnumerable<JsonSerializerOptions> SerializerOptions()
    {
        yield return JsonSerializerOptions.Default;
        yield return JsonSerializerOptions.Web;
    }

    [TestCaseSource(nameof(SerializerOptions))]
    public void RoundTripsThroughJsonUnchanged(JsonSerializerOptions options)
    {
        var command = new PublishPackageCommand
        {
            RepositoryId = Guid.CreateVersion7(),
            PackageFilePath = "/data/uploads/0193a5b2-7c1e-7d4f-9a3b-2f1e0d9c8b7a.pkg.tar.zst",
        };

        var json = JsonSerializer.Serialize(command, options);
        var roundTripped = JsonSerializer.Deserialize<PublishPackageCommand>(json, options);

        Assert.That(roundTripped, Is.EqualTo(command));
    }
}
