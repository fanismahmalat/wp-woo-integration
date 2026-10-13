<?php
/**
 * Plugin Name: Woo Connect PoC - local callback (DEV ONLY)
 * Description: Lets WooCommerce's wc-auth POST the generated keys to the .NET API on https://localhost:7100.
 *
 * WooCommerce sends the keys with wp_safe_remote_post(), which rejects loopback hosts and ports other
 * than 80/443/8080, and verifies TLS against WordPress' bundled CA list (not the OS keychain), so the
 * ASP.NET Core dev certificate isn't trusted. In production the callback is a public HTTPS URL and
 * none of this is needed.
 *
 * Copy into wp-content/mu-plugins/ of the local site.
 */

if ( 'local' !== wp_get_environment_type() ) {
	return;
}

const WOO_POC_CALLBACK_ORIGIN = 'https://localhost:7100/';

add_filter(
	'http_request_args',
	function ( $args, $url ) {
		if ( str_starts_with( $url, WOO_POC_CALLBACK_ORIGIN ) ) {
			$args['reject_unsafe_urls'] = false; // Skip wp_http_validate_url() loopback/port checks.
			$args['sslverify']          = false; // Dev cert isn't in wp-includes/certificates/ca-bundle.crt.
		}
		return $args;
	},
	10,
	2
);
