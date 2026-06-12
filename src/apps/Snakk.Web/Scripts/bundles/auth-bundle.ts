/**
 * Auth bundle — scripts only useful to authenticated users.
 * Loaded by the layout inside the isAuth block, BEFORE save-cache/follow-cache
 * (which subclass CacheManager). Built by esbuild.core.mjs as an IIFE.
 *
 * auth-navbar: logout / notifications / DM badge — all rendered only for auth users.
 * cache-manager: base class consumed solely by save-cache and follow-cache (auth features).
 */

import '../components/auth-navbar';
import '../services/cache-manager';
