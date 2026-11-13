/**
 * Teste de stress — CashFlow
 *
 * Requisito não-funcional: o serviço de consolidado diário recebe até 50 req/s
 * com no máximo 5% de perda de requisições.
 *
 * Como rodar:
 *   k6 run tests/stress/cashflow-stress.js
 *
 * Com output resumido:
 *   k6 run --summary-trend-stats="avg,p(95),p(99),max" tests/stress/cashflow-stress.js
 */

import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Rate, Trend } from "k6/metrics";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

// ---------------------------------------------------------------------------
// Configuração
// ---------------------------------------------------------------------------

const BASE_URL = __ENV.BASE_URL || "http://localhost:5000";
const TEST_USER_EMAIL = `stress-${Date.now()}@cashflow.test`;
const TEST_USER_PASSWORD = "Stress#1234";
const TODAY = new Date().toISOString().split("T")[0];

// ---------------------------------------------------------------------------
// Métricas customizadas
// ---------------------------------------------------------------------------

const transactionErrors = new Counter("transaction_errors");
const balanceErrors = new Counter("balance_errors");
const transactionErrorRate = new Rate("transaction_error_rate");
const balanceErrorRate = new Rate("balance_error_rate");
const transactionDuration = new Trend("transaction_duration", true);
const balanceDuration = new Trend("balance_duration", true);

// ---------------------------------------------------------------------------
// Cenários e thresholds
// ---------------------------------------------------------------------------

export const options = {
  scenarios: {
    // Rampa: sobe até 50 VUs em 30s, mantém por 2min, desce em 10s
    ramp_up: {
      executor: "ramping-vus",
      startVUs: 1,
      stages: [
        { duration: "30s", target: 50 }, // aquecimento
        { duration: "2m", target: 50 }, // carga sustentada (pico)
        { duration: "10s", target: 0 }, // desaquecimento
      ],
      gracefulRampDown: "10s",
    },

    // Pico de 50 req/s constante nos 2 minutos centrais (somente transações)
    constant_rate: {
      executor: "constant-arrival-rate",
      rate: 50,
      timeUnit: "1s",
      duration: "2m",
      preAllocatedVUs: 60,
      maxVUs: 100,
      startTime: "30s", // começa junto com o pico do ramp_up
      exec: "createTransaction",
    },
  },

  thresholds: {
    // Requisito principal: máx 5% de perda
    transaction_error_rate: ["rate<0.05"],
    balance_error_rate: ["rate<0.05"],

    // Latência aceitável (p95 < 2s, p99 < 5s)
    transaction_duration: ["p(95)<2000", "p(99)<5000"],
    balance_duration: ["p(95)<2000", "p(99)<5000"],

    // Taxa de erro geral do k6
    http_req_failed: ["rate<0.05"],
  },
};

// ---------------------------------------------------------------------------
// Setup: cria usuário de teste e obtém JWT
// ---------------------------------------------------------------------------

export function setup() {
  // Registra o usuário
  const signUpRes = http.post(
    `${BASE_URL}/api/v1/auth/sign-up`,
    JSON.stringify({ email: TEST_USER_EMAIL, password: TEST_USER_PASSWORD }),
    { headers: { "Content-Type": "application/json" } }
  );

  if (signUpRes.status !== 201 && signUpRes.status !== 200) {
    console.error(`sign-up falhou: ${signUpRes.status} — ${signUpRes.body}`);
  }

  // Faz login e extrai o token
  const signInRes = http.post(
    `${BASE_URL}/api/v1/auth/sign-in`,
    JSON.stringify({ email: TEST_USER_EMAIL, password: TEST_USER_PASSWORD }),
    { headers: { "Content-Type": "application/json" } }
  );

  if (signInRes.status !== 200) {
    console.error(`sign-in falhou: ${signInRes.status} — ${signInRes.body}`);
    return { token: null };
  }

  const body = JSON.parse(signInRes.body);
  console.log(`Setup OK — usuário: ${TEST_USER_EMAIL}`);
  return { token: body.accessToken };
}

// ---------------------------------------------------------------------------
// Função default: mix de criação de lançamentos + consulta de saldo
// ---------------------------------------------------------------------------

export default function (data) {
  if (!data.token) {
    console.error("Token não disponível — abortando VU");
    return;
  }

  // 70% criação de lançamento / 30% consulta de saldo
  if (Math.random() < 0.7) {
    doCreateTransaction(data.token);
  } else {
    doGetBalance(data.token);
  }

  sleep(0.1); // 100ms entre iterações por VU
}

// ---------------------------------------------------------------------------
// Executor dedicado ao cenário de taxa constante
// ---------------------------------------------------------------------------

export function createTransaction(data) {
  if (!data.token) return;
  doCreateTransaction(data.token);
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function doCreateTransaction(token) {
  const type = Math.random() < 0.5 ? "Credit" : "Debit";
  const amount = parseFloat((Math.random() * 1000 + 1).toFixed(2));

  const start = Date.now();
  const res = http.post(
    `${BASE_URL}/api/v1/transactions`,
    JSON.stringify({ type, amount }),
    {
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
        "Idempotency-Key": uuidv4(),
      },
      timeout: "10s",
    }
  );
  transactionDuration.add(Date.now() - start);

  const ok = check(res, {
    "transaction: status 201": (r) => r.status === 201,
  });

  transactionErrorRate.add(!ok);
  if (!ok) {
    transactionErrors.add(1);
    if (res.status !== 409) {
      // 409 = idempotência duplicada (esperado em retries)
      console.warn(`transaction falhou: ${res.status} — ${res.body}`);
    }
  }
}

function doGetBalance(token) {
  const start = Date.now();
  const res = http.get(`${BASE_URL}/api/v1/balances?date=${TODAY}`, {
    headers: { Authorization: `Bearer ${token}` },
    timeout: "10s",
  });
  balanceDuration.add(Date.now() - start);

  const ok = check(res, {
    "balance: status 200": (r) => r.status === 200,
  });

  balanceErrorRate.add(!ok);
  if (!ok) {
    balanceErrors.add(1);
    console.warn(`balance falhou: ${res.status} — ${res.body}`);
  }
}
