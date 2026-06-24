// Spike scenario. A baseline level of traffic, then a sudden 4x burst to
// validate that the gateway + Polly resilience + connection-pool sizing
// hold up. Use this to look for tail-latency cliffs and any 503/connection
// reset patterns in the Caddy + gRPC logs.
//
// Run:
//   docker compose --profile loadtest run --rm k6 run \
//     -o experimental-opentelemetry /scripts/scenarios/spike.js

import { sleep } from 'k6';
import { pickRandom, COMMUNITIES, DEFAULT_HUB_SPACES } from '../lib/config.js';
import { checkOk } from '../lib/metrics.js';
import { getHomePage, getCommunityPage, getSpacePage } from '../lib/endpoints.js';

export const options = {
    stages: [
        { duration: '30s', target: 20 },   // baseline
        { duration: '1m',  target: 20 },   // hold
        { duration: '10s', target: 200 },  // BURST
        { duration: '30s', target: 200 },  // hold the burst
        { duration: '10s', target: 20 },   // drop back
        { duration: '30s', target: 20 },   // hold to observe recovery
        { duration: '20s', target: 0 },    // drain
    ],
    thresholds: {
        // Spike tolerates more variance — burst is meant to find limits
        checks: ['rate>0.85'],
        http_req_duration: ['p(95)<3000'],
        'http_req_duration{name:home}': ['p(99)<5000'],
    },
};

export default function () {
    checkOk(getHomePage(), 'home', '<!DOCTYPE');
    const community = pickRandom(COMMUNITIES);
    const { hub, space } = pickRandom(DEFAULT_HUB_SPACES);
    checkOk(getCommunityPage(community), 'community', '<!DOCTYPE');
    checkOk(getSpacePage(hub, space), 'space', '<!DOCTYPE');
    sleep(0.5);
}
