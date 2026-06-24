// "Normal day" capacity test — 200 concurrent VUs sustained for 15 min.
// Use this as a baseline before perf work + after, to compare p95/p99
// regressions. Realistic-mix workload via the journey library.
//
// Expectations on the current dev box (4 GB RAM, single host):
//   • App-level success rate: ≥ 99 %
//   • p95 discussion-detail: < 2 s
//   • p95 search: < 1 s
//   • No container OOM kills (watch the Snakk Infra dashboard)
//
// Run:
//   docker compose --profile loadtest run --rm k6 run \
//     /scripts/scenarios/capacity-200.js

import { pickAndRunJourney } from '../lib/journeys.js';

export const options = {
    stages: [
        { duration: '1m',  target: 200 }, // ramp
        { duration: '15m', target: 200 }, // sustained
        { duration: '1m',  target: 0 },   // drain
    ],
    thresholds: {
        // Application-level — the real success signal
        checks: ['rate>0.98'],
        // Latency budgets at this concurrency
        'http_req_duration{name:home}':              ['p(95)<1500', 'p(99)<3000'],
        'http_req_duration{name:discussion-detail}': ['p(95)<2000', 'p(99)<4000'],
        'http_req_duration{name:search-discussions}':['p(95)<1000', 'p(99)<2500'],
        'http_req_duration{name:create-discussion}': ['p(95)<3500'],
        // Transport-level — should be 0 with the gateway RST fix in place
        http_req_failed: ['rate<0.01'],
    },
};

export default function () {
    pickAndRunJourney();
}
