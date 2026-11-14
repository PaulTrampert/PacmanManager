using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Test.Entities;

/// <summary>
/// <see cref="Job.As{TMessage}"/> reads back the message a job stores for the type its
/// <see cref="Job.MessageType"/> names, and refuses any other.
/// </summary>
[TestFixture]
public class JobTests
{
    private record FirstCommand(Guid RepositoryId, string PackageFilePath);

    private record SecondCommand(Guid RepositoryId, string PackageFilePath);

    [Test]
    public void For_RecordsTheMessageTypeAsPending()
    {
        // Arrange
        var userId = Guid.CreateVersion7();

        // Act
        var job = Job.For(new FirstCommand(Guid.CreateVersion7(), "/data/uploads/a"), userId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(job.MessageType, Is.EqualTo(typeof(FirstCommand).FullName));
            Assert.That(job.State, Is.EqualTo(JobState.Pending));
            Assert.That(job.UserId, Is.EqualTo(userId));
            Assert.That(job.Id.Version, Is.EqualTo(7));
        });
    }

    [Test]
    public void As_TheTypeMessageTypeNames_ReturnsTheStoredMessage()
    {
        // Arrange
        var message = new FirstCommand(Guid.CreateVersion7(), "/data/uploads/a");
        var userId = Guid.CreateVersion7();
        var job = Job.For(message, userId);

        // Act
        var typed = job.As<FirstCommand>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(typed.Id, Is.EqualTo(job.Id));
            Assert.That(typed.UserId, Is.EqualTo(userId));
            Assert.That(typed.Message, Is.EqualTo(message));
        });
    }

    [Test]
    public void As_AJobWithoutAUser_CarriesNoUser()
    {
        // Arrange
        var job = Job.For(new FirstCommand(Guid.CreateVersion7(), "/data/uploads/a"), null);

        // Act
        var typed = job.As<FirstCommand>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(typed.UserId, Is.Null);
            Assert.That(typed.User, Is.Null);
        });
    }

    [Test]
    public void As_AMessageStoredOutsideFor_ReadsItByMessageType()
    {
        // A job loaded from the database was not made by For; only its columns decide what As reads.
        // Arrange
        var repositoryId = Guid.CreateVersion7();
        var job = new Job
        {
            MessageType = Job.MessageTypeOf<FirstCommand>(),
            Message = $$"""{"repositoryId":"{{repositoryId}}","packageFilePath":"/data/uploads/b"}""",
        };

        // Act
        var typed = job.As<FirstCommand>();

        // Assert
        Assert.That(typed.Message, Is.EqualTo(new FirstCommand(repositoryId, "/data/uploads/b")));
    }

    [Test]
    public void As_AnyOtherType_ThrowsJobMessageTypeException()
    {
        // The two commands have the same shape, so only the type check tells them apart.
        // Arrange
        var job = Job.For(new FirstCommand(Guid.CreateVersion7(), "/data/uploads/a"), null);

        // Act
        var thrown = Assert.Throws<JobMessageTypeException>(() => job.As<SecondCommand>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(thrown!.JobId, Is.EqualTo(job.Id));
            Assert.That(thrown.StoredType, Is.EqualTo(typeof(FirstCommand).FullName));
            Assert.That(thrown.RequestedType, Is.EqualTo(typeof(SecondCommand).FullName));
        });
    }

    [Test]
    public void As_AnUnrelatedType_ThrowsJobMessageTypeException()
    {
        // Arrange
        var job = Job.For(new FirstCommand(Guid.CreateVersion7(), "/data/uploads/a"), null);

        // Act & Assert
        Assert.Throws<JobMessageTypeException>(() => job.As<string>());
    }
}
