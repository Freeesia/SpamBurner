using System.Reflection;
using ConsoleAppFramework;
using Mastonet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var app = ConsoleApp.CreateBuilder(args)
    .ConfigureServices((c, s) => s.Configure<ConsoleOptions>(c.Configuration).AddHttpClient())
    .Build();
app.AddRootCommand(Run);

using (app.Logger.BeginScope("startup"))
{
    app.Logger.LogInformation($"App: {app.Environment.ApplicationName}");
    app.Logger.LogInformation($"Env: {app.Environment.EnvironmentName}");
    var assembly = Assembly.GetExecutingAssembly();
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString();
    app.Logger.LogInformation($"Ver: {version}");
}

await app.RunAsync();

static async Task Run(ILogger<Program> logger, IOptions<ConsoleOptions> options, IHttpClientFactory factory)
{
    var (mastodonUrl, mastodonToken) = options.Value;
    var client = new MastodonClient(mastodonUrl, mastodonToken, factory.CreateClient());
    var hoge = await client.GetAdminAccounts(new() { Limit = 50 }, AdminAccountOrigin.Remote, AdminAccountStatus.Active);
    foreach (var account in hoge.Where(a => a.Account?.FollowersCount == 0))
    {
        logger.LogInformation($"{account.Id} {account.Email} {account.Ip} {string.Join(", ", account.Ips.Select(i => i.Ip))}");
    }
}

class ConsoleOptions
{
    public required string MastodonUrl { get; init; }
    public required string MastodonToken { get; init; }

    public void Deconstruct(out string mastodonUrl, out string mastodonToken)
    {
        mastodonUrl = MastodonUrl;
        mastodonToken = MastodonToken;
    }
}