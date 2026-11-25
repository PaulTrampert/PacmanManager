namespace PacmanManager.Worker;

/// <summary>
/// How the Worker reaches the Postgres database it shares with RepoHost, which holds both the
/// application's tables and Wolverine's message storage.
/// </summary>
public static class DatabaseConfig
{
    /// <summary>
    /// The name, under <c>ConnectionStrings</c>, of the Postgres connection string. It is the same
    /// name RepoHost and the migrations read.
    /// </summary>
    public const string ConnectionStringName = "pacmanmanager";

    /// <summary>
    /// The Postgres schema Wolverine's message storage tables live in. It must stay the same as
    /// RepoHost's <c>MessagingServiceCollectionExtensions.WolverineSchema</c>, since both processes
    /// keep their envelopes in the one set of tables.
    /// </summary>
    public const string WolverineSchema = "wolverine";
}
