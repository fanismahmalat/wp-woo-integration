using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WooConnect.Api;

public enum ConnectionStatus { Pending, Connected }

public sealed class Connection
{
    public required string Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Pending;
    public long? KeyId { get; set; }
    public string? ConsumerKey { get; set; }
    public string? ConsumerSecret { get; set; }
    public string? Permissions { get; set; }
}

// Body WooCommerce POSTs to callback_url after the merchant approves.
public sealed record WooCallbackPayload(
    [property: JsonPropertyName("key_id")] long KeyId,
    [property: JsonPropertyName("user_id")] JsonElement UserId,
    [property: JsonPropertyName("consumer_key")] string? ConsumerKey,
    [property: JsonPropertyName("consumer_secret")] string? ConsumerSecret,
    [property: JsonPropertyName("key_permissions")] string? KeyPermissions)
{
    // WooCommerce echoes back the user_id we sent (a string), but its docs show a number - accept both.
    public string? ConnectionId => UserId.ValueKind switch
    {
        JsonValueKind.String => UserId.GetString(),
        JsonValueKind.Number => UserId.GetRawText(),
        _ => null
    };
}

// In-memory only: connections and keys are lost on restart, which is fine for the spike.
public sealed class ConnectionStore
{
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Connection> _connections = new();

    public Connection Create()
    {
        var connection = new Connection { Id = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow };
        _connections[connection.Id] = connection;
        return connection;
    }

    public Connection? Find(string id) => _connections.GetValueOrDefault(id);

    // The callback endpoint is public, so only accept keys for a connection we started, recently, and only once.
    public bool TryConnect(WooCallbackPayload payload)
    {
        if (payload.ConnectionId is not { } id || Find(id) is not { } connection) return false;
        if (string.IsNullOrEmpty(payload.ConsumerKey) || string.IsNullOrEmpty(payload.ConsumerSecret)) return false;

        lock (connection)
        {
            if (connection.Status != ConnectionStatus.Pending || DateTimeOffset.UtcNow - connection.CreatedAt > PendingTtl)
                return false;

            connection.KeyId = payload.KeyId;
            connection.ConsumerKey = payload.ConsumerKey;
            connection.ConsumerSecret = payload.ConsumerSecret;
            connection.Permissions = payload.KeyPermissions;
            connection.Status = ConnectionStatus.Connected;
            return true;
        }
    }
}
