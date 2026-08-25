-- Executed from f2_persistence_constraints.sql inside the same transaction.
-- F5: authoritative session/device persistence acceptance checks.

INSERT INTO identity.security_device (
    id, principal_id, principal_type, trust_state, integrity_state,
    registered_at, last_seen_at, restricted_at, restriction_reason,
    revoked_at, revocation_reason
) VALUES (
    '018f0000-0000-7000-8000-000000000101',
    '018f0000-0000-7000-8000-000000000201',
    'customer',
    'trusted',
    'meets_baseline',
    now() - interval '10 minutes',
    now() - interval '1 minute',
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
) VALUES (
    '018f0000-0000-7000-8000-000000000301',
    '018f0000-0000-7000-8000-000000000201',
    'customer',
    '018f0000-0000-7000-8000-000000000101',
    now() - interval '9 minutes',
    now() - interval '9 minutes',
    now() - interval '1 minute',
    900,
    now() + interval '1 hour',
    'strong_mfa',
    FALSE,
    NULL,
    NULL,
    NULL,
    NULL
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM identity.security_session s
        JOIN identity.security_device d
          ON d.id = s.device_id
         AND d.principal_id = s.principal_id
         AND d.principal_type = s.principal_type
        WHERE s.id = '018f0000-0000-7000-8000-000000000301'
          AND s.authentication_strength = 'strong_mfa'
          AND d.trust_state = 'trusted'
    ) THEN
        RAISE EXCEPTION 'Expected security session/device round-trip to preserve ownership and state';
    END IF;
END
$$;

-- Revoked records may preserve prior restriction evidence for audit.
INSERT INTO identity.security_device (
    id, principal_id, principal_type, trust_state, integrity_state,
    registered_at, last_seen_at, restricted_at, restriction_reason,
    revoked_at, revocation_reason
) VALUES (
    '018f0000-0000-7000-8000-000000000102',
    '018f0000-0000-7000-8000-000000000202',
    'customer',
    'revoked',
    'degraded',
    now() - interval '20 minutes',
    now() - interval '10 minutes',
    now() - interval '8 minutes',
    'recovery_completed',
    now() - interval '5 minutes',
    'device_revoked'
);

INSERT INTO identity.security_session (
    id, principal_id, principal_type, device_id,
    authenticated_at, created_at, last_seen_at, idle_timeout_seconds,
    absolute_expires_at, authentication_strength, restricted,
    restricted_at, restriction_reason, revoked_at, revocation_reason
) VALUES (
    '018f0000-0000-7000-8000-000000000302',
    '018f0000-0000-7000-8000-000000000202',
    'customer',
    '018f0000-0000-7000-8000-000000000102',
    now() - interval '19 minutes',
    now() - interval '19 minutes',
    now() - interval '10 minutes',
    900,
    now() + interval '1 hour',
    'phishing_resistant',
    FALSE,
    now() - interval '8 minutes',
    'recovery_completed',
    now() - interval '5 minutes',
    'recovery_completed'
);

DO $$
BEGIN
    BEGIN
        INSERT INTO identity.security_session (
            id, principal_id, principal_type, device_id,
            authenticated_at, created_at, last_seen_at, idle_timeout_seconds,
            absolute_expires_at, authentication_strength, restricted,
            restricted_at, restriction_reason, revoked_at, revocation_reason
        ) VALUES (
            '018f0000-0000-7000-8000-000000000303',
            '018f0000-0000-7000-8000-000000000299',
            'customer',
            '018f0000-0000-7000-8000-000000000101',
            now() - interval '2 minutes',
            now() - interval '2 minutes',
            now() - interval '1 minute',
            900,
            now() + interval '1 hour',
            'strong_mfa',
            FALSE,
            NULL,
            NULL,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected cross-principal device/session binding to fail';
    EXCEPTION
        WHEN foreign_key_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO identity.security_session (
            id, principal_id, principal_type, device_id,
            authenticated_at, created_at, last_seen_at, idle_timeout_seconds,
            absolute_expires_at, authentication_strength, restricted,
            restricted_at, restriction_reason, revoked_at, revocation_reason
        ) VALUES (
            '018f0000-0000-7000-8000-000000000304',
            '018f0000-0000-7000-8000-000000000201',
            'customer',
            '018f0000-0000-7000-8000-000000009999',
            now() - interval '2 minutes',
            now() - interval '2 minutes',
            now() - interval '1 minute',
            900,
            now() + interval '1 hour',
            'strong_mfa',
            FALSE,
            NULL,
            NULL,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected unknown device reference to fail';
    EXCEPTION
        WHEN foreign_key_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO identity.security_device (
            id, principal_id, principal_type, trust_state, integrity_state,
            registered_at, last_seen_at, restricted_at, restriction_reason,
            revoked_at, revocation_reason
        ) VALUES (
            '018f0000-0000-7000-8000-000000000103',
            '018f0000-0000-7000-8000-000000000203',
            'customer',
            'restricted',
            'unknown',
            now() - interval '5 minutes',
            now() - interval '1 minute',
            NULL,
            NULL,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected restricted device without restriction evidence to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO identity.security_device (
            id, principal_id, principal_type, trust_state, integrity_state,
            registered_at, last_seen_at, restricted_at, restriction_reason,
            revoked_at, revocation_reason
        ) VALUES (
            '018f0000-0000-7000-8000-000000000104',
            '018f0000-0000-7000-8000-000000000204',
            'customer',
            'revoked',
            'unknown',
            now() - interval '5 minutes',
            now() - interval '1 minute',
            NULL,
            NULL,
            now(),
            NULL
        );
        RAISE EXCEPTION 'Expected revoked device without revocation reason to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO identity.security_session (
            id, principal_id, principal_type, device_id,
            authenticated_at, created_at, last_seen_at, idle_timeout_seconds,
            absolute_expires_at, authentication_strength, restricted,
            restricted_at, restriction_reason, revoked_at, revocation_reason
        ) VALUES (
            '018f0000-0000-7000-8000-000000000305',
            '018f0000-0000-7000-8000-000000000201',
            'customer',
            '018f0000-0000-7000-8000-000000000101',
            now() - interval '2 minutes',
            now() - interval '2 minutes',
            now() - interval '1 minute',
            0,
            now() + interval '1 hour',
            'strong_mfa',
            FALSE,
            NULL,
            NULL,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected non-positive idle timeout to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO identity.security_session (
            id, principal_id, principal_type, device_id,
            authenticated_at, created_at, last_seen_at, idle_timeout_seconds,
            absolute_expires_at, authentication_strength, restricted,
            restricted_at, restriction_reason, revoked_at, revocation_reason
        ) VALUES (
            '018f0000-0000-7000-8000-000000000306',
            '018f0000-0000-7000-8000-000000000201',
            'customer',
            '018f0000-0000-7000-8000-000000000101',
            now() - interval '2 minutes',
            now() - interval '2 minutes',
            now() + interval '1 hour',
            900,
            now() + interval '1 hour',
            'strong_mfa',
            FALSE,
            NULL,
            NULL,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected last_seen_at at absolute expiry to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO identity.security_session (
            id, principal_id, principal_type, device_id,
            authenticated_at, created_at, last_seen_at, idle_timeout_seconds,
            absolute_expires_at, authentication_strength, restricted,
            restricted_at, restriction_reason, revoked_at, revocation_reason
        ) VALUES (
            '018f0000-0000-7000-8000-000000000307',
            '018f0000-0000-7000-8000-000000000201',
            'customer',
            '018f0000-0000-7000-8000-000000000101',
            now() - interval '2 minutes',
            now() - interval '2 minutes',
            now() - interval '1 minute',
            900,
            now() + interval '1 hour',
            'unknown',
            FALSE,
            NULL,
            NULL,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected unknown authentication strength to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO identity.security_session (
            id, principal_id, principal_type, device_id,
            authenticated_at, created_at, last_seen_at, idle_timeout_seconds,
            absolute_expires_at, authentication_strength, restricted,
            restricted_at, restriction_reason, revoked_at, revocation_reason
        ) VALUES (
            '018f0000-0000-7000-8000-000000000308',
            '018f0000-0000-7000-8000-000000000201',
            'customer',
            '018f0000-0000-7000-8000-000000000101',
            now() - interval '2 minutes',
            now() - interval '2 minutes',
            now() - interval '1 minute',
            900,
            now() + interval '1 hour',
            'strong_mfa',
            TRUE,
            NULL,
            NULL,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected restricted session without restriction evidence to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO identity.security_session (
            id, principal_id, principal_type, device_id,
            authenticated_at, created_at, last_seen_at, idle_timeout_seconds,
            absolute_expires_at, authentication_strength, restricted,
            restricted_at, restriction_reason, revoked_at, revocation_reason
        ) VALUES (
            '018f0000-0000-7000-8000-000000000309',
            '018f0000-0000-7000-8000-000000000201',
            'customer',
            '018f0000-0000-7000-8000-000000000101',
            now() - interval '2 minutes',
            now() - interval '2 minutes',
            now() - interval '1 minute',
            900,
            now() + interval '1 hour',
            'strong_mfa',
            TRUE,
            now() - interval '3 minutes',
            'security_hold',
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected restriction timestamp before session creation to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    IF to_regclass('identity.ix_security_device_principal_state') IS NULL THEN
        RAISE EXCEPTION 'Expected principal device lookup index';
    END IF;

    IF to_regclass('identity.ix_security_session_principal_lifecycle') IS NULL THEN
        RAISE EXCEPTION 'Expected principal session lifecycle index';
    END IF;

    IF to_regclass('identity.ix_security_session_device_lifecycle') IS NULL THEN
        RAISE EXCEPTION 'Expected device session lifecycle index';
    END IF;
END
$$;
