# Identity withdrawal security checkpoint — 2026-09-14

Canonical lane: `parallel/identity-authorization`
Submission PR: #102
Accepted staging baseline: `aafb5afef7a8bc2f6843f82a9fc62ed71fd2d088`

Implemented API-independent controls:
- verified owned beneficiary requirement;
- beneficiary cooling-off enforcement;
- server-authoritative session inventory;
- session, device and sign-out-all revocation;
- expired-session filtering;
- maker-checker break-glass approval;
- no self-approval;
- trusted-device and phishing-resistant step-up for break-glass approval;
- 19-case `Klyvesta.WithdrawalSecurityVerifier`.

Hard boundary: no production IdP, bank/beneficiary provider, pyPSX credential, withdrawal rail, production PII or real-money movement is introduced. Provider/runtime wiring remains external deployment work.
