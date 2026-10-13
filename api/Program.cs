using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using WooConnect.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<WooOptions>(builder.Configuration.GetSection(WooOptions.Section));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<ConnectionStore>();
builder.Services.AddHttpClient<WooClient>()
    .ConfigurePrimaryHttpMessageHandler(sp => WooClient.CreateHandler(sp.GetRequiredService<IOptions<WooOptions>>().Value.StudioCaPath));

var app = builder.Build();

// 1. Start the flow: create a pending connection and hand the browser the store's authorize URL.
app.MapPost("/api/connect", (ConnectionStore store, IOptions<WooOptions> options) =>
{
    var woo = options.Value;
    var connection = store.Create();

    var authorizeUrl = QueryHelpers.AddQueryString($"{woo.StoreUrl.TrimEnd('/')}/wc-auth/v1/authorize", new Dictionary<string, string?>
    {
        ["app_name"] = woo.AppName,
        ["scope"] = woo.Scope,
        ["user_id"] = connection.Id, // our own id (acts as OAuth "state"), echoed back to callback_url and return_url
        ["return_url"] = woo.ReturnUrl,
        ["callback_url"] = woo.CallbackUrl
    });

    return Results.Ok(new { connection.Id, authorizeUrl });
});

// 2. WooCommerce POSTs the generated keys here before redirecting the browser to return_url.
//    Anything other than 200 makes WooCommerce delete the key and show the merchant an error.
app.MapPost("/api/woo/callback", (WooCallbackPayload payload, ConnectionStore store, ILogger<Program> logger) =>
{
    var accepted = store.TryConnect(payload);

    logger.LogInformation("WooCommerce callback key_id={KeyId} user_id={UserId} permissions={Permissions} accepted={Accepted}",
        payload.KeyId, payload.ConnectionId, payload.KeyPermissions, accepted);

    return accepted ? Results.Ok() : Results.BadRequest();
});

// 3. The React return page reads the connection the callback completed.
app.MapGet("/api/connections/{id}", (string id, ConnectionStore store) =>
    store.Find(id) is { } connection ? Results.Ok(connection) : Results.NotFound());

// 4. Prove the keys work. This is a separate call on purpose: calling the store from inside
//    the callback would block while WordPress is still waiting for our 200.
app.MapGet("/api/connections/{id}/products", async (string id, ConnectionStore store, WooClient woo, CancellationToken ct) =>
{
    if (store.Find(id) is not { Status: ConnectionStatus.Connected } connection) return Results.NotFound();

    try
    {
        return Results.Ok(await woo.GetProductsAsync(connection, ct));
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

// 5. Same as above, but variable products also carry their variations.
app.MapGet("/api/connections/{id}/products-with-variants", async (string id, ConnectionStore store, WooClient woo, CancellationToken ct) =>
{
    if (store.Find(id) is not { Status: ConnectionStatus.Connected } connection) return Results.NotFound();

    try
    {
        return Results.Ok(await woo.GetProductsWithVariantsAsync(connection, ct));
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();
