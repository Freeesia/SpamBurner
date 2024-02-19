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
    string? maxId = null;
    HashSet<string> ignoreDomains = ["misskey.io", "pawoo.net", "mstdn.jp"];
    var domains = new Dictionary<string, int>();
    var count = 0;
    while (count < 5)
    {
        var accounts = await client.GetAdminAccounts(new() { MaxId = maxId, Limit = 10000 }, AdminAccountOrigin.Remote, AdminAccountStatus.Active);
        maxId = accounts.Last().Id;
        var targets = accounts.Where(a => a.Account?.FollowersCount <= 1 && a.Account?.FollowingCount == 0)
            .Where(a => a.Account?.StatusesCount > 0)
            .Where(a => a.Account?.AvatarUrl.EndsWith("missing.png") ?? false)
            .Where(a => a.UserName.Length == 10)
            .Where(a => !ignoreDomains.Contains(a.Domain!))
            .ToArray();
        if (targets.Length == 0)
        {
            count++;
            continue;
        }
        count = 0;
        foreach (var account in targets)
        {
            if (!domains.TryAdd(account.Domain!, 1))
            {
                domains[account.Domain!]++;
            }
            await client.PerformAccount(account.Id, AdminActionType.Suspend);
            await client.DeleteAccount(account.Id);
            logger.LogInformation($"{account.Id} {account.Account?.AccountName}");
        }
        // return;
    }
    foreach (var (domain, v) in domains.OrderByDescending(kv => kv.Value).Where(kv => kv.Value > 1))
    {
        var users = await client.GetAdminAccounts(new(), AdminAccountOrigin.Remote, AdminAccountStatus.Active, byDomain: domain);
        if (users.Count == 0)
        {
            await client.AdminBlockDomain(domain, AdminBlockDomainAction.Suspend, privateComment: "スパム鯖");
            logger.LogInformation($"block {domain}: spam {v}");
        }
        else
        {
            logger.LogInformation($"skip {domain}: spam {v}, users {users.Count}");
        }
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