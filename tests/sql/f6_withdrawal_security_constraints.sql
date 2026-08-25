-- Executed from f2_persistence_constraints.sql inside the same transaction.
-- F6: beneficiary versioning, withdrawal lifecycle and transaction-authorization persistence acceptance checks.

-- Authoritative customer device and two sessions used by the F6 transaction-authorization tests.
INSERT INTO identity.security_device (
    id, principal_id, principal_type, trust_state, integrity_state,
    registered_at, last_seen_at, restricted_at, restriction_reason,
    revoked_at, revocation_reason
) VALUES (
    '018f0000-0000-7000-8000-00000000f601',
    '018f0000-0000-7000-8000-00000000f201',
    'customer',
    'trusted',
    'meets_baseline',
    now() - interval '20 minutes',
    now() - interval '30 seconds',
    NULL,
    NULL,
    NULL,
    NULL
);

INSERT INTO identity.security_session (
    id, principal_id, principal_type, device_id,
    authenticated_at, created_at, last_seen_at, idle_timeout_seconds,
    absolute_expires_at, authentication_strength, restricted,
    restricted_at, restriction_reason, revoked_at, revocation_reason
) VALUES
(
    '018f0000-0000-7000-8000-00000000f301',
    '018f0000-0000-7000-8000-00000000f201',
    'customer',
    '018f0000-0000-7000-8000-00000000f601',
    now() - interval '10 minutes',
    now() - interval '10 minutes',
    now() - interval '30 seconds',
    900,
    now() + interval '1 hour',
    'strong_mfa',
    FALSE,
    NULL,
    NULL,
    NULL,
    NULL
),
(
    '018f0000-0000-7000-8000-00000000f302',
    '018f0000-0000-7000-8000-00000000f201',
    'customer',
    '018f0000-0000-7000-8000-00000000f601',
    now() - interval '9 minutes',
    now() - interval '9 minutes',
    now() - interval '20 seconds',
    900,
    now() + interval '1 hour',
    'strong_mfa',
    FALSE,
    NULL,
    NULL,
    NULL,
    NULL
);

-- A database writer cannot fabricate an already-active beneficiary version.
DO $$
BEGIN
    BEGIN
        INSERT INTO funding.withdrawal_beneficiary_version (
            version_id, beneficiary_id, version_number, customer_id, destination_hash,
            state, verification_evidence_reference, created_at, verified_at, available_after,
            blocked_at, block_reason, revoked_at, revocation_reason
        ) VALUES (
            '018f0000-0000-7000-8000-00000000f500',
            '018f0000-0000-7000-8000-00000000f510',
            1,
            '018f0000-0000-7000-8000-00000000f401',
            repeat('F', 64),
            'active',
            'fabricated-evidence',
            now() - interval '20 minutes',
            now() - interval '10 minutes',
            now() - interval '5 minutes',
            NULL,
            NULL,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected beneficiary to require pending-verification initial state';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

INSERT INTO funding.withdrawal_beneficiary_version (
    version_id, beneficiary_id, version_number, customer_id, destination_hash,
    state, verification_evidence_reference, created_at, verified_at, available_after,
    blocked_at, block_reason, revoked_at, revocation_reason
) VALUES (
    '018f0000-0000-7000-8000-00000000f501',
    '018f0000-0000-7000-8000-00000000f511',
    1,
    '018f0000-0000-7000-8000-00000000f401',
    repeat('A', 64),
    'pending_verification',
    NULL,
    now() - interval '20 minutes',
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL
);

UPDATE funding.withdrawal_beneficiary_version
SET state = 'verified_cooling_off',
    verification_evidence_reference = 'bank-verification-f6-1',
    verified_at = now() - interval '10 minutes',
    available_after = now() - interval '5 minutes'
WHERE version_id = '018f0000-0000-7000-8000-00000000f501';

UPDATE funding.withdrawal_beneficiary_version
SET state = 'active'
WHERE version_id = '018f0000-0000-7000-8000-00000000f501';

DO $$
BEGIN
    BEGIN
        UPDATE funding.withdrawal_beneficiary_version
        SET destination_hash = repeat('B', 64)
        WHERE version_id = '018f0000-0000-7000-8000-00000000f501';
        RAISE EXCEPTION 'Expected beneficiary destination mutation to require a new version';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

-- Cross-customer binding fails closed even when a valid beneficiary version identifier is supplied.
DO $$
BEGIN
    BEGIN
        INSERT INTO funding.withdrawal (
            id, customer_id, beneficiary_version_id, amount, currency,
            destination_hash, transaction_data_hash, state, requested_by_principal_id,
            created_at, updated_at, reason_code, approved_by_principal_id, approved_at,
            authorization_id, external_reference, outcome_evidence_reference
        ) VALUES (
            '018f0000-0000-7000-8000-00000000f700',
            '018f0000-0000-7000-8000-00000000f402',
            '018f0000-0000-7000-8000-00000000f501',
            1000.00000000,
            'PKR',
            repeat('A', 64),
            repeat('0', 64),
            'requested',
            '018f0000-0000-7000-8000-00000000f201',
            now(),
            now(),
            NULL,
            NULL,
            NULL,
            NULL,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected cross-customer beneficiary binding to fail';
    EXCEPTION
        WHEN check_violation OR foreign_key_violation THEN NULL;
    END;
END
$$;

-- Withdrawal A: complete happy path and preserve immutable historical evidence.
INSERT INTO funding.withdrawal (
    id, customer_id, beneficiary_version_id, amount, currency,
    destination_hash, transaction_data_hash, state, requested_by_principal_id,
    created_at, updated_at, reason_code, approved_by_principal_id, approved_at,
    authorization_id, external_reference, outcome_evidence_reference
) VALUES (
    '018f0000-0000-7000-8000-00000000f701',
    '018f0000-0000-7000-8000-00000000f401',
    '018f0000-0000-7000-8000-00000000f501',
    1000.00000000,
    'PKR',
    repeat('A', 64),
    repeat('1', 64),
    'requested',
    '018f0000-0000-7000-8000-00000000f201',
    now(),
    now(),
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL
);

DO $$
BEGIN
    BEGIN
        UPDATE funding.withdrawal
        SET amount = 2000.00000000
        WHERE id = '018f0000-0000-7000-8000-00000000f701';
        RAISE EXCEPTION 'Expected significant withdrawal data to be immutable';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        UPDATE funding.withdrawal
        SET state = 'approved', updated_at = now()
        WHERE id = '018f0000-0000-7000-8000-00000000f701';
        RAISE EXCEPTION 'Expected requested withdrawal to be unable to skip security and policy checks';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

UPDATE funding.withdrawal
SET state = 'security_check', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f701';

UPDATE funding.withdrawal
SET state = 'policy_check', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f701';

UPDATE funding.withdrawal
SET state = 'approved', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f701';

INSERT INTO funding.withdrawal_authorization (
    id, withdrawal_id, principal_id, session_id,
    transaction_data_hash, authorized_at, expires_at
) VALUES (
    '018f0000-0000-7000-8000-00000000f801',
    '018f0000-0000-7000-8000-00000000f701',
    '018f0000-0000-7000-8000-00000000f201',
    '018f0000-0000-7000-8000-00000000f301',
    repeat('1', 64),
    now(),
    now() + interval '10 minutes'
);

DO $$
BEGIN
    BEGIN
        UPDATE funding.withdrawal_authorization
        SET expires_at = now() + interval '20 minutes'
        WHERE id = '018f0000-0000-7000-8000-00000000f801';
        RAISE EXCEPTION 'Expected withdrawal authorization snapshot to be append-only';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

UPDATE funding.withdrawal
SET state = 'submission_pending',
    authorization_id = '018f0000-0000-7000-8000-00000000f801',
    updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f701';

UPDATE funding.withdrawal
SET state = 'submitted',
    external_reference = 'provider-withdrawal-f6-1',
    updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f701';

UPDATE funding.withdrawal
SET state = 'processing', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f701';

UPDATE funding.withdrawal
SET state = 'completed',
    outcome_evidence_reference = 'reconciliation-f6-completed-1',
    updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f701';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM funding.withdrawal
        WHERE id = '018f0000-0000-7000-8000-00000000f701'
          AND state = 'completed'
          AND authorization_id = '018f0000-0000-7000-8000-00000000f801'
          AND external_reference = 'provider-withdrawal-f6-1'
          AND outcome_evidence_reference = 'reconciliation-f6-completed-1'
    ) THEN
        RAISE EXCEPTION 'Expected completed withdrawal to preserve authorization and reconciliation evidence';
    END IF;
END
$$;

DO $$
BEGIN
    BEGIN
        DELETE FROM funding.withdrawal
        WHERE id = '018f0000-0000-7000-8000-00000000f701';
        RAISE EXCEPTION 'Expected historical withdrawal deletion to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

-- Withdrawal B: protected approval separation, exact authorization binding and UNKNOWN reconciliation.
INSERT INTO funding.withdrawal (
    id, customer_id, beneficiary_version_id, amount, currency,
    destination_hash, transaction_data_hash, state, requested_by_principal_id,
    created_at, updated_at, reason_code, approved_by_principal_id, approved_at,
    authorization_id, external_reference, outcome_evidence_reference
) VALUES (
    '018f0000-0000-7000-8000-00000000f702',
    '018f0000-0000-7000-8000-00000000f401',
    '018f0000-0000-7000-8000-00000000f501',
    2000.00000000,
    'PKR',
    repeat('A', 64),
    repeat('2', 64),
    'requested',
    '018f0000-0000-7000-8000-00000000f201',
    now(), now(), NULL, NULL, NULL, NULL, NULL, NULL
);

UPDATE funding.withdrawal SET state = 'security_check', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f702';
UPDATE funding.withdrawal SET state = 'policy_check', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f702';
UPDATE funding.withdrawal SET state = 'approval_pending', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f702';

DO $$
BEGIN
    BEGIN
        UPDATE funding.withdrawal
        SET state = 'approved',
            approved_by_principal_id = requested_by_principal_id,
            approved_at = now(),
            updated_at = now()
        WHERE id = '018f0000-0000-7000-8000-00000000f702';
        RAISE EXCEPTION 'Expected maker to be unable to approve own protected withdrawal';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

UPDATE funding.withdrawal
SET state = 'approved',
    approved_by_principal_id = '018f0000-0000-7000-8000-00000000f999',
    approved_at = now(),
    updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f702';

DO $$
BEGIN
    BEGIN
        UPDATE funding.withdrawal
        SET state = 'submission_pending',
            authorization_id = '018f0000-0000-7000-8000-00000000f801',
            updated_at = now()
        WHERE id = '018f0000-0000-7000-8000-00000000f702';
        RAISE EXCEPTION 'Expected authorization from another withdrawal to be unusable';
    EXCEPTION
        WHEN check_violation OR foreign_key_violation THEN NULL;
    END;
END
$$;

INSERT INTO funding.withdrawal_authorization (
    id, withdrawal_id, principal_id, session_id,
    transaction_data_hash, authorized_at, expires_at
) VALUES (
    '018f0000-0000-7000-8000-00000000f802',
    '018f0000-0000-7000-8000-00000000f702',
    '018f0000-0000-7000-8000-00000000f201',
    '018f0000-0000-7000-8000-00000000f301',
    repeat('2', 64),
    now(),
    now() + interval '10 minutes'
);

UPDATE funding.withdrawal
SET state = 'submission_pending',
    authorization_id = '018f0000-0000-7000-8000-00000000f802',
    updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f702';

UPDATE funding.withdrawal
SET state = 'unknown', reason_code = 'provider_timeout', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f702';

DO $$
BEGIN
    BEGIN
        UPDATE funding.withdrawal
        SET state = 'completed', updated_at = now()
        WHERE id = '018f0000-0000-7000-8000-00000000f702';
        RAISE EXCEPTION 'Expected UNKNOWN resolution to require outcome evidence';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

UPDATE funding.withdrawal
SET state = 'completed',
    outcome_evidence_reference = 'reconciliation-f6-unknown-resolved',
    updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f702';

-- Withdrawal C: authorization cannot survive authoritative session revocation.
INSERT INTO funding.withdrawal (
    id, customer_id, beneficiary_version_id, amount, currency,
    destination_hash, transaction_data_hash, state, requested_by_principal_id,
    created_at, updated_at, reason_code, approved_by_principal_id, approved_at,
    authorization_id, external_reference, outcome_evidence_reference
) VALUES (
    '018f0000-0000-7000-8000-00000000f703',
    '018f0000-0000-7000-8000-00000000f401',
    '018f0000-0000-7000-8000-00000000f501',
    3000.00000000,
    'PKR', repeat('A', 64), repeat('3', 64), 'requested',
    '018f0000-0000-7000-8000-00000000f201',
    now(), now(), NULL, NULL, NULL, NULL, NULL, NULL
);
UPDATE funding.withdrawal SET state = 'security_check', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f703';
UPDATE funding.withdrawal SET state = 'policy_check', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f703';
UPDATE funding.withdrawal SET state = 'approved', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f703';

INSERT INTO funding.withdrawal_authorization (
    id, withdrawal_id, principal_id, session_id,
    transaction_data_hash, authorized_at, expires_at
) VALUES (
    '018f0000-0000-7000-8000-00000000f803',
    '018f0000-0000-7000-8000-00000000f703',
    '018f0000-0000-7000-8000-00000000f201',
    '018f0000-0000-7000-8000-00000000f301',
    repeat('3', 64), now(), now() + interval '10 minutes'
);

UPDATE identity.security_session
SET revoked_at = now(), revocation_reason = 'security_incident', restricted = FALSE
WHERE id = '018f0000-0000-7000-8000-00000000f301';

DO $$
BEGIN
    BEGIN
        UPDATE funding.withdrawal
        SET state = 'submission_pending',
            authorization_id = '018f0000-0000-7000-8000-00000000f803',
            updated_at = now()
        WHERE id = '018f0000-0000-7000-8000-00000000f703';
        RAISE EXCEPTION 'Expected revoked session to invalidate submission eligibility';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

-- Withdrawal D: beneficiary is rechecked at the final submission boundary.
INSERT INTO funding.withdrawal (
    id, customer_id, beneficiary_version_id, amount, currency,
    destination_hash, transaction_data_hash, state, requested_by_principal_id,
    created_at, updated_at, reason_code, approved_by_principal_id, approved_at,
    authorization_id, external_reference, outcome_evidence_reference
) VALUES (
    '018f0000-0000-7000-8000-00000000f704',
    '018f0000-0000-7000-8000-00000000f401',
    '018f0000-0000-7000-8000-00000000f501',
    4000.00000000,
    'PKR', repeat('A', 64), repeat('4', 64), 'requested',
    '018f0000-0000-7000-8000-00000000f201',
    now(), now(), NULL, NULL, NULL, NULL, NULL, NULL
);
UPDATE funding.withdrawal SET state = 'security_check', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f704';
UPDATE funding.withdrawal SET state = 'policy_check', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f704';
UPDATE funding.withdrawal SET state = 'approved', updated_at = now()
WHERE id = '018f0000-0000-7000-8000-00000000f704';

INSERT INTO funding.withdrawal_authorization (
    id, withdrawal_id, principal_id, session_id,
    transaction_data_hash, authorized_at, expires_at
) VALUES (
    '018f0000-0000-7000-8000-00000000f804',
    '018f0000-0000-7000-8000-00000000f704',
    '018f0000-0000-7000-8000-00000000f201',
    '018f0000-0000-7000-8000-00000000f302',
    repeat('4', 64), now(), now() + interval '10 minutes'
);

UPDATE funding.withdrawal_beneficiary_version
SET state = 'revoked',
    revoked_at = now(),
    revocation_reason = 'destination_replaced'
WHERE version_id = '018f0000-0000-7000-8000-00000000f501';

DO $$
BEGIN
    BEGIN
        UPDATE funding.withdrawal
        SET state = 'submission_pending',
            authorization_id = '018f0000-0000-7000-8000-00000000f804',
            updated_at = now()
        WHERE id = '018f0000-0000-7000-8000-00000000f704';
        RAISE EXCEPTION 'Expected revoked beneficiary to invalidate submission eligibility';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    IF to_regclass('funding.ix_withdrawal_customer_state_created_at') IS NULL THEN
        RAISE EXCEPTION 'Expected withdrawal customer/state lookup index';
    END IF;

    IF to_regclass('funding.ix_withdrawal_authorization_withdrawal_expiry') IS NULL THEN
        RAISE EXCEPTION 'Expected withdrawal authorization expiry lookup index';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_trigger
        WHERE tgname = 'trg_withdrawal_lifecycle'
          AND NOT tgisinternal
    ) THEN
        RAISE EXCEPTION 'Expected withdrawal lifecycle database trigger';
    END IF;
END
$$;
