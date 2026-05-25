// Custom k6 metrics for application-level success/failure tracking.
//
// k6's built-in `http_req_failed` counts transport-level failures: any
// status >= 400 OR network error including the post-body TCP RST that the
// snakk gateway emits today. When that gateway bug is firing, the dashboard
// shows 100% "failure" even though every page rendered correctly.
//
// `app_ok` is a Rate keyed on the `name` tag — it records whether the
// response was *application-level* OK (status 200-399, OR status 0 with a
// non-empty body containing the expected sentinel). This is what dashboards
// should show as the real success signal during load tests.

import { Rate, Counter } from 'k6/metrics';
import { check } from 'k6';
import { ok } from './config.js';

// Pass/fail rate at the application layer, tagged by endpoint name.
// Compute pass% in Grafana as:
//   sum by (name) (rate(k6_app_ok_occurred_total[$__rate_interval]))
//     / sum by (name) (rate(k6_app_ok_total[$__rate_interval]))
export const appOk = new Rate('app_ok');

// Counter of unique application-level failures (status mismatch, content
// missing) — useful for alert-style "did any iteration fail?" panels.
export const appFailures = new Counter('app_failures');

// Single helper that runs k6's check() AND records into appOk / appFailures.
// Use this in scenarios instead of bare `check(r, { 'home ok': r => ok(...) })`.
//
//   import { checkOk } from '../lib/metrics.js';
//   checkOk(getHomePage(), 'home', '<!DOCTYPE');
export function checkOk(response, name, sentinel = null) {
    const success = ok(response, sentinel);
    check(response, { [`${name} ok`]: () => success }, { name });
    appOk.add(success, { name });
    if (!success) {
        appFailures.add(1, { name, status: String(response.status) });
    }
    return success;
}
