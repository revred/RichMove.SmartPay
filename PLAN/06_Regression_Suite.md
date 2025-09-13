# Regression Suite (High-Level)

1) **Checkout**: init -> authorize -> capture/refund -> dispute webhook mirror.
2) **A2A**: init -> bank selection -> success/abort -> settlement event.
3) **FX**: quote -> snapshot with order -> payout reconciliation -> rounding and expiry.
4) **Payouts**: PSP default settlements; SPI remittance fallback for cross-currency.
5) **Webhooks**: idempotent handling; signature verify; retries/backoff.
6) **Security**: RLS access, role least privilege; secrets not logged.
7) **Analytics**: GMV, fees, approval rate, currency mix consistent per day.
