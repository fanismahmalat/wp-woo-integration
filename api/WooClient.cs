using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WooConnect.Api;

public sealed record ProductsResult(int Status, string[]? Products, JsonElement? Error);

public sealed record Variant(int Id, string? Sku, string? Price, int? StockQuantity, Dictionary<string, string?> Attributes, int ParentId);
public sealed record ProductWithVariants(int Id, string? Name, string? Type, string? Sku, string? Price, Variant[] Variants);
public sealed record CatalogResult(int Status, ProductWithVariants[]? Products, JsonElement? Error);

public sealed class WooClient(HttpClient http, IOptions<WooOptions> options)
{
    public async Task<ProductsResult> GetProductsAsync(Connection connection, CancellationToken ct)
    {
        var (status, json) = await GetAsync(connection, "products?per_page=50", ct);

        return status == 200
            ? new ProductsResult(status, json.EnumerateArray().Select(p => p.GetProperty("name").GetString() ?? "").ToArray(), null)
            : new ProductsResult(status, null, json);
    }

    // The products list only carries variation ids, so variable products need one extra call each
    // to /products/{id}/variations (fired in parallel).
    public async Task<CatalogResult> GetProductsWithVariantsAsync(Connection connection, CancellationToken ct)
    {
        var (status, products) = await GetAsync(connection, "products?per_page=50", ct);
        if (status != 200) return new CatalogResult(status, null, products);

        var tasks = products.EnumerateArray().Select(async product =>
        {
            var id = product.GetProperty("id").GetInt32();
            var type = product.GetProperty("type").GetString();
            var variants = Array.Empty<Variant>();

            if (type == "variable")
            {
                var (variationsStatus, variations) = await GetAsync(connection, $"products/{id}/variations?per_page=100", ct);
                if (variationsStatus != 200) throw new HttpRequestException($"Variations for product {id} returned {variationsStatus}: {variations}");
                variants = variations.EnumerateArray().Select(ToVariant).ToArray();
            }

            return new ProductWithVariants(id, product.GetProperty("name").GetString(), type,
                product.GetProperty("sku").GetString(), product.GetProperty("price").GetString(), variants);
        });

        return new CatalogResult(status, await Task.WhenAll(tasks), null);
    }

    private static Variant ToVariant(JsonElement v) => new(
        v.GetProperty("id").GetInt32(),
        v.GetProperty("sku").GetString(),
        v.GetProperty("price").GetString(),
        v.GetProperty("stock_quantity").ValueKind == JsonValueKind.Number ? v.GetProperty("stock_quantity").GetInt32() : null,
        v.GetProperty("attributes").EnumerateArray().ToDictionary(
            a => a.GetProperty("name").GetString() ?? "",
            a => a.GetProperty("option").GetString()),
        v.GetProperty("parent_id").GetInt32()
            );

    private async Task<(int Status, JsonElement Json)> GetAsync(Connection connection, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.Value.StoreUrl.TrimEnd('/')}/wp-json/wc/v3/{path}");

        // Over HTTPS WooCommerce accepts the key pair as HTTP Basic credentials.
        // (Plain http:// stores would require OAuth 1.0a signing instead.)
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connection.ConsumerKey}:{connection.ConsumerSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await http.SendAsync(request, ct);
        return ((int)response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>(ct));
    }

    // Dev only: WP Studio serves *.wp.local with certs signed by its own local CA, which the OS doesn't trust.
    // Trust that one CA explicitly rather than turning certificate validation off.
    public static HttpMessageHandler CreateHandler(string? studioCaPath)
    {
        var handler = new SocketsHttpHandler();
        if (string.IsNullOrEmpty(studioCaPath)) return handler;

        var caPath = studioCaPath.StartsWith("~/")
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), studioCaPath[2..])
            : studioCaPath;
        var studioCa = X509CertificateLoader.LoadCertificateFromFile(caPath);

        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            // Only an untrusted chain gets a second look; a host-name mismatch is still a failure.
            if (errors != SslPolicyErrors.RemoteCertificateChainErrors || certificate is null) return false;

            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(studioCa);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(X509CertificateLoader.LoadCertificate(certificate.GetRawCertData()));
        };
        return handler;
    }
}
