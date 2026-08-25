# Repository Licensing and Distribution Model

Status: **OWNER-APPROVED OPERATING MODEL FOR THIS REPOSITORY**

Decision date: 2026-08-25

## Decision

Klyvesta uses a **split repository/distribution model**.

This repository, `Vertex-Systems-Network/vsn-klyvesta`, remains the intentionally public, GPL-3.0-licensed repository for generic/open engineering foundations, public architecture, public contracts/examples, due-diligence material, and non-confidential implementation primitives that the owner intentionally chooses to publish under GPL-3.0.

This decision does **not** relicense past code and does not remove the existing GPL-3.0 licence or rights already granted for repository history.

## Proprietary boundary

The following must not be added to this public GPL repository unless the owner makes a new explicit distribution/licensing decision with appropriate legal review:

- proprietary investment-selection or portfolio-strategy logic;
- confidential broker/partner contracts, credentials, private API documentation or commercially restricted integration material;
- customer PII or production customer data;
- production secrets, signing keys, KMS material or privileged infrastructure configuration;
- proprietary customer/business workflows intended to remain closed source;
- confidential risk, surveillance, fraud or abuse-detection rules whose publication would weaken controls;
- private operational incident material.

Closed-source/proprietary production components must be developed in a separately approved **private repository/security boundary** with its own licence, access controls, secrets policy, CI/CD and dependency policy.

Do not copy GPL-covered implementation from this repository into a proprietary codebase in a way that creates unintended licensing obligations without confirming the applicable legal position.

## Dependency licence policy

Every new runtime/build dependency requires maintenance, security, provenance and licence review.

Default policy for this public GPL-3.0 repository:

- prefer well-maintained dependencies with clear SPDX-identifiable licences and known provenance;
- confirm compatibility with GPL-3.0 and the repository's intended distribution before adoption;
- do not introduce AGPL, SSPL, non-commercial, field-of-use-restricted, source-available or custom/proprietary licence terms without explicit owner/legal approval;
- record material licence exceptions and their rationale;
- keep third-party notices/attribution required by dependency licences;
- release pipelines must eventually emit SBOM/provenance evidence.

For future proprietary/private components, dependency compatibility must be evaluated against that component's separately approved proprietary licence and distribution model; GPL compatibility in this public repository must not be assumed to imply proprietary compatibility elsewhere.

## Broker and market-data rights

A public repository licence does not grant Klyvesta rights to redistribute broker documentation, exchange/market data, vendor datasets or third-party research. Those rights remain contract-specific and must be verified under P0 before confidential or redistribution-restricted material is stored or published.

## Change control

Changing this repository from the split/public-GPL model, changing/removing `LICENSE`, relicensing existing contributions, or moving proprietary code into this repository requires a new explicit owner decision and legal review as appropriate.

Repository visibility alone does not change existing licence grants.
