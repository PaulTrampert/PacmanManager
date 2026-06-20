namespace PacmanManager.TestUtils;

public class DirUtils
{
    /// <summary>
    /// Finds the solution directory by searching upwards from the current directory for a .sln file.
    /// </summary>
    /// <returns>The path to the solution directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the solution directory cannot be found.</exception>
    public static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            // Check if this directory contains a .sln file
            if (directory.GetFiles("*.sln").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not find solution directory. Searched upwards from: " + AppContext.BaseDirectory);
    }
}