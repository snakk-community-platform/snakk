// Diverse user-journey scenario. Each VU picks a journey type per
// iteration with realistic weighting (mostly readers, some searchers,
// fewer writers). Use this as the standard "looks like real traffic"
// load profile.
//
// Run:
//   docker compose --profile loadtest run --rm k6 run \
//     -e VUS=50 -e DURATION=10m /scripts/scenarios/journey.js

import { pickAndRunJourney } from '../lib/journeys.js';
import { tolerantThresholds } from '../lib/config.js';

const VUS      = parseInt(__ENV.VUS      || '50');
const DURATION = __ENV.DURATION || '10m';

export const options = {
    stages: [
        { duration: '30s',  target: VUS },
        { duration: DURATION, target: VUS },
        { duration: '30s',  target: 0 },
    ],
    thresholds: {
        ...tolerantThresholds,
        'http_req_duration{name:discussion-detail}': ['p(95)<2000'],
        'http_req_duration{name:search-discussions}':['p(95)<1000'],
        'http_req_duration{name:create-discussion}': ['p(95)<3500'],
        'http_req_duration{name:home}':              ['p(95)<1500'],
    },
};

export default function () {
    pickAndRunJourney();
}
