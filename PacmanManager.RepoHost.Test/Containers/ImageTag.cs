using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PacmanManager.RepoHost.Test.Containers;

/// <summary>
/// The tag the test images built from this working tree are given, so that test runs in two
/// worktrees on one machine never build over each other's images.
/// </summary>
/// <remarks>
/// A fixed <c>:latest</c> was shared by every checkout on the machine: a run in one worktree could
/// retag the image another worktree's fixtures were about to start, and test the other branch's
/// code. The tag is the checked-out branch, which git allows in only one worktree at a time. A
/// detached HEAD has no branch -- CI checks out the pull request's merge commit that way -- so it
/// falls back to a hash of the working tree's path, which is just as unique on one machine.
/// </remarks>
public static partial class ImageTag
{
    /// <summary>
    /// The longest tag Docker accepts.
    /// </summary>
    public const int MaxLength = 128;

    /// <summary>
    /// Gets the tag for the images built from the working tree at <paramref name="solutionDirectory"/>.
    /// </summary>
    /// <param name="solutionDirectory">The root of the working tree.</param>
    /// <returns>A valid Docker tag unique to that working tree.</returns>
    public static string ForWorkingTree(string solutionDirectory) =>
        FromBranch(CurrentBranch(solutionDirectory), solutionDirectory);

    /// <summary>
    /// Gets the tag for a working tree, given the branch it has checked out.
    /// </summary>
    /// <param name="branch">The checked-out branch, or <c>null</c> when HEAD is detached.</param>
    /// <param name="solutionDirectory">The root of the working tree.</param>
    /// <returns>The branch made into a valid tag, or a tag derived from the path if there is none.</returns>
    internal static string FromBranch(string? branch, string solutionDirectory)
    {
        var tag = branch is null ? "" : Sanitize(branch);
        return tag.Length > 0 ? tag : $"worktree-{PathHash(solutionDirectory)}";
    }

    /// <summary>
    /// Turns a branch name into a valid Docker tag: <c>[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}</c>.
    /// </summary>
    /// <param name="branch">The branch name.</param>
    /// <returns>The tag, or an empty string when nothing of the branch name survives.</returns>
    internal static string Sanitize(string branch)
    {
        var tag = InvalidTagCharacters().Replace(branch, "-").TrimStart('.', '-');
        return tag.Length > MaxLength ? tag[..MaxLength] : tag;
    }

    private static string PathHash(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path))))[..12];

    private static string? CurrentBranch(string solutionDirectory)
    {
        try
        {
            // symbolic-ref rather than rev-parse --abbrev-ref: on a detached HEAD it exits non-zero
            // instead of printing the literal "HEAD".
            using var git = Process.Start(new ProcessStartInfo("git", ["-C", solutionDirectory, "symbolic-ref", "--short", "-q", "HEAD"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (git is null)
            {
                return null;
            }

            var output = git.StandardOutput.ReadToEnd().Trim();
            git.WaitForExit();
            return git.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch (Win32Exception)
        {
            // git is not installed; the path hash is still unique.
            return null;
        }
    }

    [GeneratedRegex("[^A-Za-z0-9_.-]")]
    private static partial Regex InvalidTagCharacters();
}
