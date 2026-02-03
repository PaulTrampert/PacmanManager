using LibAlpmSharp;
using LibAlpmSharp.Config;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.CliTools;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Models.Repository;

namespace PacmanManager.RepoHost.Controllers;

[ApiController]
[Route("admin/[controller]")]
public class RepositoryController(ICliToolRunner cliRunner, ILibAlpm libAlpm, PacmanConfig pacmanConfig)
    : Controller
{
    [HttpGet]
    public IEnumerable<RepositoryInfo> Get()
    {
        var dbs = libAlpm.GetSyncDatabases();
        return dbs.Select(db => new RepositoryInfo
        {
            Name = db.Name,
            Url = $"http{(Request.IsHttps ? "s" : "")}://{Request.Host}/repository/{db.Name}"
        });
    }

    [HttpPut("{name}")]
    public async Task<RepositoryInfo> Put([FromRoute] string name)
    {
        var result = await cliRunner.RunToolAsync(new RepoAdd(name, pacmanConfig.DBPath));
        if (result != 0)
        {
            Response.StatusCode = 500;
            return null!;
        }

        var confDirectory =
            Directory.CreateDirectory(
                $"{Environment.GetEnvironmentVariable("DATA_DIR") ?? "/data"}/pacman-local.conf.d");
        await using var fileStream = System.IO.File.Create(Path.Combine(confDirectory.FullName, $"{name}.conf"));

        var cfgWriter = new StreamWriter(fileStream);

        await cfgWriter.WriteAsync($"[{name}]\n" +
                                   $"SigLevel = Optional TrustAll\n");
        
        cfgWriter.Close();
        
        return new RepositoryInfo
        {
            Name = name,
            Url = $"http{(Request.IsHttps ? "s" : "")}://{Request.Host}/repository/{name}"
        };
    }
}