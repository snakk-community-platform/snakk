// "Front page of HN" growth test — 500 concurrent VUs sustained for
// 10 min. Used to validate that the platform survives a 5-10x surge
// over normal traffic. Expect some latency degradation but no errors.
//
// Acceptance criteria (loose — first run is exploratory):
//   • App-level success rate: ≥ 97 %
//   • Postgres connections do NOT exceed max (default 100; expect 70-90)
//   • Valkey memory < 90 % of maxmemory
//   • Snakk.Api container CPU < 80 % per panel
//   • No HTTP 429s in Caddy access log
//
// Run:
//   docker compose --profile loadtest run --rm k6 run \
//     /scripts/scenarios/growth-500.js
//
// Watch during run:
//   • Snakk Traces  → p95 latency by service
//   • Snakk Infra   → container CPU/RAM, Postgres slow-query rate
//   • Snakk Profiles → flame graph for whichever service hits 70 %+ CPU

import { pickAndRunJourney } from '../lib/journeys.js';

export const options = {
    stages: [
        { duration: '2m',  target: 500 },
        { duration: '10m', target: 500 },
        { duration: '2m',  target: 0 },
    ],
    thresholds: {
        checks: ['rate>0.97'],
        // Wider budgets — this scenario exists to find the limit, not enforce it
        'http_req_duration{name:home}':              ['p(95)<3000'],
        'http_req_duration{name:discussion-detail}': ['p(95)<5000'],
        'http_req_duration{name:search-discussions}':['p(95)<2000'],
        http_req_failed: ['rate<0.03'],
    },
};

export default function () {
    pickAndRunJourney();
}
