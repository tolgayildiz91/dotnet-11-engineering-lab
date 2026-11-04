import http from 'k6/http';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

const route = { json: 'status', lookup: 'products', async: 'orders' }[__ENV.WORKLOAD];
if (!route || !__ENV.SUMMARY_PATH) throw new Error('WORKLOAD and SUMMARY_PATH are required');

const started = new Counter('measured_started');
const completed = new Counter('measured_completed');
const failed = new Counter('measured_failed');
const latency = new Trend('measured_latency_ms', true);

http.setResponseCallback(http.expectedStatuses(200));

export const options = {
    scenarios: {
        trial: { executor: 'constant-vus', vus: 32, duration: '23s', gracefulStop: '4s' },
    },
    discardResponseBodies: true,
    maxRedirects: 0,
    summaryTrendStats: ['p(50)', 'p(95)', 'p(99)', 'max'],
    thresholds: { measured_failed: ['count==0'] },
};

export default function () {
    const elapsed = Date.now() - exec.scenario.startTime;
    if (elapsed >= 23000) return;
    const measured = elapsed >= 8000;
    if (measured) started.add(1);
    const response = http.get(`http://127.0.0.1:33044/api/${route}/42`, { timeout: '2s' });
    if (measured) {
        completed.add(1);
        failed.add(Number(response.status !== 200 || response.proto !== 'HTTP/1.1'));
        latency.add(response.timings.duration);
    }
}

export function handleSummary(data) {
    return { [__ENV.SUMMARY_PATH]: JSON.stringify(data, null, 2) };
}
