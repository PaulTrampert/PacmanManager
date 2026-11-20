using Microsoft.Extensions.Hosting;
using PacmanManager.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.ConfigureWorker();

await builder.Build().RunAsync();
