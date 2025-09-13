# Master Plan

## Objectives
1) Ship merchant-ready **MVP** that covers ~80% of Shopify features by orchestrating **Stripe (cards, billing/Connect)**, **TrueLayer (A2A)**, **PayPal**, **Shippo** (shipping), **Avalara** (tax) — with minimal regulatory footprint.
2) Provide **FX quote & remittance** tied to payments under **SPI**, focusing on GBP↔INR/EUR/USD.
3) Maintain **intrusive testing** with coverage ≥99% lines, mutation score ≥95%, and CI gates.
4) Prepare a **clean regulatory pack** (SPI application and agent alternatives) and partner onboarding.
5) Be **exit/dilution-ready in 6 months** with metrics, LOIs, and modular architecture.

## Scope (in)
- Headless checkout UI kit, payment orchestration, FX quote/payouts, marketplace payouts via partner platform products, analytics.
- Compliance artefacts and runbooks; wind-down; outsourcing control.

## Scope (out)
- Acting as a card acquirer, PIS/AIS in own name, safeguarding e-money ourselves, core tax/shipping engines (use partners).

## Success Metrics
- TTFD (time to first demo): ≤ 10 working days.
- Merchant beta: 3–5 merchants transacting via partners with FX quotes enabled.
- Approval rate ≥ 85% on card flows (PSP dependent), A2A failure < 3%.
- FX spread margin (blended): 25–75 bps as corridor configurable.
- Test discipline: coverage ≥99%, mutation ≥95%, k6 95p latency < 200ms @100 RPS.
