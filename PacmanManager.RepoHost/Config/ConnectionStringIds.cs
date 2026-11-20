namespace PacmanManager.RepoHost.Config;

/// <summary>
/// The names of the connection strings RepoHost reads from the <c>ConnectionStrings</c>
/// configuration section.
/// </summary>
public static class ConnectionStringIds
{
    /// <summary>
    /// The Postgres database, which holds both the application's tables and Wolverine's.
    /// </summary>
    public const string Database = "pacmanmanager";

    /// <summary>
    /// The RabbitMQ broker, an AMQP URI such as <c>amqp://user:password@rabbitmq:5672/</c>.
    /// </summary>
    public const string RabbitMq = "rabbitmq";
}
