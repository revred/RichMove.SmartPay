import http from 'k6/http';
import { sleep, check } from 'k6';

export const options = {
  vus: __ENV.VUS ? Number(__ENV.VUS) : 2,
  duration: __ENV.DURATION || '30s'
};

const BASE = __ENV.BASE_URL || 'http://localhost:5001';
const PATH = '/api/fx/quote';
const TENANT = __ENV.TENANT || 'default';

export default function () {
  const amount = Math.floor(Math.random() * 5000) + 50;
  const body = JSON.stringify({
    fromCurrency: 'USD',
    toCurrency: 'GBP',
    amount
  });

  const res = http.post(`${BASE}${PATH}`, body, {
    headers: {
      'Content-Type': 'application/json',
      'X-Tenant': TENANT
    },
    tags: { endpoint: 'fx-quote' }
  });

  check(res, {
    'status is 200/201': r => r.status === 200 || r.status === 201,
    'has JSON body': r => r.headers['Content-Type'] && r.headers['Content-Type'].includes('application/json')
  });

  sleep(0.5);
}