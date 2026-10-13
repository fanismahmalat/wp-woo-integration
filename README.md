# Woo Connect PoC

Spike of WooCommerce's [`/wc-auth/v1/authorize`](https://developer.woocommerce.com/docs/apis/rest-api/authentication/) key-exchange flow with a .NET 10 API and a minimal React shell.

## Flow
1. `POST /api/connect` creates a pending connection and returns `{store}/wc-auth/v1/authorize?app_name&scope&user_id&return_url&callback_url` (`user_id` = our connection id, used like OAuth `state`).
2. Merchant logs in to WP and clicks **Approve**.
3. WooCommerce POSTs `{key_id, user_id, consumer_key, consumer_secret, key_permissions}` to `callback_url` (`POST /api/woo/callback`). It must be `https://` and must answer **200**, otherwise WC deletes the key.
4. Browser is redirected to `return_url?success=1|0&user_id=…`.
5. `GET /api/connections/{id}/products` proves the keys work (Basic auth over HTTPS).

## Run
```sh
cd api && dotnet run     # https://localhost:7100 (callback) + http://localhost:5100
cd web && npm run dev    # http://localhost:5174 (proxies /api)
```
Config lives in `api/appsettings.Development.json` (`WooCommerce` section).

## Local WP (WordPress Studio) setup
- WooCommerce installed on `https://my-wordpress-website.wp.local`.
- `wordpress/woo-poc-dev-callback.php` copied into the site's `wp-content/mu-plugins/`. WC sends the callback with `wp_safe_remote_post()`, which blocks localhost / non-80/443/8080 ports and doesn't trust the ASP.NET dev cert. **Dev only** — a real deployment has a public HTTPS callback.
- The API trusts Studio's local CA (`~/.studio/certificates/studio-ca.crt`) for calls to the store.

## Notes
- Don't call the store from inside the callback — WordPress is blocked waiting for our 200.
- `http://` stores require OAuth 1.0a signing instead of Basic auth (not implemented).
- Connections are in-memory; restarting the API forgets them.
