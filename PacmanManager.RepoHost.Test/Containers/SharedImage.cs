using DotNet.Testcontainers.Images;

namespace PacmanManager.RepoHost.Test.Containers;

/// <summary>
/// A Docker image that is built at most once, however many fixtures ask for it.
/// </summary>
/// <param name="build">Creates the image definition. Called at most once, by the first caller.</param>
/// <remarks>
/// The first call to <see cref="GetAsync"/> builds the image and every later call, concurrent or
/// not, awaits that same build. A failed build is not retried: every later caller sees the same
/// exception, which is the useful behaviour when the Dockerfile itself is broken, since retrying
/// would spend the same minute and a half failing once per fixture.
/// </remarks>
public sealed class SharedImage(Func<IFutureDockerImage> build)
{
    private readonly Lazy<Task<IFutureDockerImage>> _image = new(async () =>
    {
        var image = build();
        await image.CreateAsync();
        return image;
    });

    /// <summary>
    /// Gets the image, building it first if nothing has yet.
    /// </summary>
    /// <returns>The built image.</returns>
    public Task<IFutureDockerImage> GetAsync() => _image.Value;
}
