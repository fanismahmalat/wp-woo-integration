namespace WooConnect.Api;

public sealed class WooOptions
{
    public const string Section = "WooCommerce";

    public string StoreUrl { get; set; } = "";
    public string AppName { get; set; } = "";

    // read | write | read_write
    public string Scope { get; set; } = "read_write";

    // Must be https:// - WooCommerce POSTs the generated keys here (server-to-server).
    public string CallbackUrl { get; set; } = "";

    // Browser lands here after approve/deny with ?success=1|0&user_id={connection id}.
    public string ReturnUrl { get; set; } = "";

    // Dev only: WP Studio's local CA, so we can call the store over HTTPS without disabling validation.
    public string? StudioCaPath { get; set; }
}
