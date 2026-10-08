using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using PetroTrans.Api;
using PetroTrans.Domain.Identity;
using Xunit;

namespace PetroTrans.Api.Tests;

public class HealthAndHostTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsOk_OnLoopback()
    {
        await using var host = await TestHost.StartAsync();
        var response = await host.Client.GetAsync("/api/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ok", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("utc", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Root_ServesFrontendIndex()
    {
        await using var host = await TestHost.StartAsync();
        var response = await host.Client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("بترو ترانس", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_BindsLoopbackOnly()
    {
        await using var host = await TestHost.StartAsync();
        var server = host.App.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses.ToArray()
            ?? [];
        if (addresses.Length == 0)
        {
            addresses = host.App.Urls.ToArray();
        }

        Assert.NotEmpty(addresses);
        foreach (var address in addresses)
        {
            var uri = new Uri(address);
            Assert.Equal("127.0.0.1", uri.Host);
            Assert.Equal(Uri.UriSchemeHttp, uri.Scheme);
        }
    }
}

public class AuthApiTests
{
    [Fact]
    public async Task UnauthorizedApiRequests_AreDenied()
    {
        await using var host = await TestHost.StartAsync();
        var me = await host.AnonymousClient.GetAsync("/api/auth/me");
        var ping = await host.AnonymousClient.GetAsync("/api/secure/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, ping.StatusCode);
    }

    [Fact]
    public async Task Setup_CreatesTwoUsers_AndLoginWorks()
    {
        await using var host = await TestHost.StartAsync();
        var setup = await host.Client.PostAsJsonAsync("/api/auth/setup", SampleSetup());
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

        var created = await setup.Content.ReadFromJsonAsync<AuthUserResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("me", created.UserName);
        Assert.DoesNotContain(PermissionCodes.PricingOverride, created.Permissions);
        Assert.DoesNotContain(PermissionCodes.RolesManage, created.Permissions);

        var secondSetup = await host.AnonymousClient.PostAsJsonAsync("/api/auth/setup", SampleSetup());
        Assert.Equal(HttpStatusCode.BadRequest, secondSetup.StatusCode);

        await host.Client.PostAsync("/api/auth/logout", null);

        var badLogin = await host.Client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "father",
            password = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);

        var fatherLogin = await host.Client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "father",
            password = "father1"
        });
        Assert.Equal(HttpStatusCode.OK, fatherLogin.StatusCode);
        var father = await fatherLogin.Content.ReadFromJsonAsync<AuthUserResponse>(JsonOptions);
        Assert.NotNull(father);
        Assert.Contains(RoleCodes.OwnerManager, father.Roles);
        Assert.Contains(PermissionCodes.PricingOverride, father.Permissions);
        Assert.DoesNotContain(PermissionCodes.RolesManage, father.Permissions);
    }

    [Fact]
    public async Task FatherHasPricingOverride_OperatorDoesNot()
    {
        await using var host = await TestHost.StartAsync();
        var setup = await host.Client.PostAsJsonAsync("/api/auth/setup", SampleSetup());
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

        var operatorOverride = await host.Client.GetAsync("/api/secure/pricing-override");
        Assert.Equal(HttpStatusCode.Forbidden, operatorOverride.StatusCode);

        var operatorPing = await host.Client.GetAsync("/api/secure/ping");
        Assert.Equal(HttpStatusCode.OK, operatorPing.StatusCode);

        await host.Client.PostAsync("/api/auth/logout", null);
        var fatherLogin = await host.Client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "father",
            password = "father1"
        });
        Assert.Equal(HttpStatusCode.OK, fatherLogin.StatusCode);

        var fatherOverride = await host.Client.GetAsync("/api/secure/pricing-override");
        Assert.Equal(HttpStatusCode.OK, fatherOverride.StatusCode);
    }

    [Fact]
    public async Task PasswordsAreHashed_NotStoredPlaintext()
    {
        await using var host = await TestHost.StartAsync();
        var setup = await host.Client.PostAsJsonAsync("/api/auth/setup", SampleSetup());
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            PetroTrans.Infrastructure.Persistence.SqlitePaths.GetConnectionString(host.DatabasePath));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT password_hash FROM users";
        await using var reader = await command.ExecuteReaderAsync();
        var hashes = new List<string>();
        while (await reader.ReadAsync())
        {
            hashes.Add(reader.GetString(0));
        }

        Assert.Equal(2, hashes.Count);
        Assert.All(hashes, hash =>
        {
            Assert.False(string.Equals(hash, "father1", StringComparison.Ordinal));
            Assert.False(string.Equals(hash, "operator1", StringComparison.Ordinal));
            Assert.StartsWith("AQAAAA", hash, StringComparison.Ordinal);
        });
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static object SampleSetup()
    {
        return new
        {
            owner = new
            {
                displayName = "الأب",
                userName = "father",
                password = "father1",
                confirmPassword = "father1"
            },
            @operator = new
            {
                displayName = "أنا",
                userName = "me",
                password = "operator1",
                confirmPassword = "operator1"
            }
        };
    }

    private sealed record AuthUserResponse(
        Guid Id,
        string UserName,
        string DisplayName,
        string[] Roles,
        string[] Permissions);
}

internal sealed class TestHost : IAsyncDisposable
{
    private TestHost(
        WebApplication app,
        HttpClient client,
        HttpClient anonymousClient,
        string databasePath)
    {
        App = app;
        Client = client;
        AnonymousClient = anonymousClient;
        DatabasePath = databasePath;
    }

    public WebApplication App { get; }
    public HttpClient Client { get; }
    public HttpClient AnonymousClient { get; }
    public string DatabasePath { get; }

    public static async Task<TestHost> StartAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"petrotrans-api-{Guid.NewGuid():N}.db");
        var port = GetFreeLoopbackPort();
        var app = PetroTransApiHost.Create([], port, databasePath);
        await PetroTransApiHost.InitializeDatabaseAsync(app);
        await app.StartAsync();

        var cookies = new CookieContainer();
        var handler = new HttpClientHandler { CookieContainer = cookies, UseCookies = true };
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}")
        };
        var anonymous = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}")
        };

        return new TestHost(app, client, anonymous, databasePath);
    }

    public static object SampleSetup()
    {
        return new
        {
            owner = new
            {
                displayName = "الأب",
                userName = "father",
                password = "father1",
                confirmPassword = "father1"
            },
            @operator = new
            {
                displayName = "أنا",
                userName = "me",
                password = "operator1",
                confirmPassword = "operator1"
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        AnonymousClient.Dispose();
        await App.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(DatabasePath))
        {
            File.Delete(DatabasePath);
        }
    }

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
