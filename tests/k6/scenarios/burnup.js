// Burnup test — ramp VUs in 50-VU steps every 2 min until the SLO
// breaks. Captures the breaking point as a comparable metric across
// PRs (track `vus_max` reached with all thresholds still passing).
//
// Use this on a feature branch to verify perf work moved the breaking
// point UP — if mainline burns out at 250 VUs and your branch at 400,
// that's the win.
//
// Run:
//   docker compose --profile loadtest run --rm k6 run \
//     /scripts/scenarios/burnup.js
//
// k6 will keep running through all stages even if thresholds fail —
// look at the final summary for the highest stage where all thresholds
// were still green. Or use the Snakk k6 dashboard and watch the
// "p95 by endpoint" panel to spot the inflection visually.

import { pickAndRunJourney } from '../lib/journeys.js';

export const options = {
    stages: [
        { duration: '2m', target: 50  }, // baseline
        { duration: '2m', target: 100 },
        { duration: '2m', target: 150 },
        { duration: '2m', target: 200 },
        { duration: '2m', target: 300 },
        { duration: '2m', target: 400 },
        { duration: '2m', target: 500 },
        { duration: '2m', target: 700 },
        { duration: '2m', target: 1000 },
        { duration: '1m', target: 0   }, // drain
    ],
    thresholds: {
        // Thresholds DON'T abort the run (need `abortOnFail: true` for that)
        // — they just mark the final summary as red. We want to see where
        // the system breaks, not stop testing.
        checks: ['rate>0.95'],
        'http_req_duration{name:home}':              ['p(95)<2000'],
        'http_req_duration{name:discussion-detail}': ['p(95)<3000'],
        'http_req_duration{name:search-discussions}':['p(95)<1500'],
    },
};

export default function () {
    pickAndRunJourney();
}
