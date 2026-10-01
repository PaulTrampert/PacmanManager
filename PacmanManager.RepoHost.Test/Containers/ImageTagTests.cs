namespace PacmanManager.RepoHost.Test.Containers;

[TestFixture]
public class ImageTagTests
{
    [Test]
    public void FromBranch_ReplacesSlashes()
    {
        Assert.That(ImageTag.FromBranch("feature/165-fix-parallel-tests", "/src"),
            Is.EqualTo("feature-165-fix-parallel-tests"));
    }

    [Test]
    public void FromBranch_KeepsCharactersATagAllows()
    {
        Assert.That(ImageTag.FromBranch("Release_1.2-rc", "/src"), Is.EqualTo("Release_1.2-rc"));
    }

    [Test]
    public void FromBranch_StripsLeadingCharactersATagCannotStartWith()
    {
        Assert.That(ImageTag.FromBranch(".-/main", "/src"), Is.EqualTo("main"));
    }

    [Test]
    public void FromBranch_TruncatesToTheLongestTagDockerAccepts()
    {
        Assert.That(ImageTag.FromBranch(new string('a', 200), "/src"),
            Has.Length.EqualTo(ImageTag.MaxLength));
    }

    [Test]
    public void FromBranch_WithoutABranch_UsesThePath()
    {
        Assert.That(ImageTag.FromBranch(null, "/src/one"), Does.Match("^worktree-[0-9a-f]{12}$"));
    }

    [Test]
    public void FromBranch_WithoutABranch_DiffersBetweenWorkingTrees()
    {
        Assert.That(ImageTag.FromBranch(null, "/src/one"), Is.Not.EqualTo(ImageTag.FromBranch(null, "/src/two")));
    }

    [Test]
    public void FromBranch_WithoutABranch_IsStableForOneWorkingTree()
    {
        Assert.That(ImageTag.FromBranch(null, "/src/one"), Is.EqualTo(ImageTag.FromBranch(null, "/src/one")));
    }

    [Test]
    public void FromBranch_WhenNothingOfTheBranchSurvives_UsesThePath()
    {
        Assert.That(ImageTag.FromBranch("..", "/src/one"), Is.EqualTo(ImageTag.FromBranch(null, "/src/one")));
    }

    [Test]
    public void ForWorkingTree_IsAValidTag()
    {
        Assert.That(ImageTag.ForWorkingTree(TestUtils.DirUtils.FindSolutionDirectory()),
            Does.Match("^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$"));
    }
}
