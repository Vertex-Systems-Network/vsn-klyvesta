-- F5 persistence hardening acceptance checks.
-- Executed inside the transaction opened by f2_persistence_constraints.sql.

DO $$
BEGIN
    BEGIN
        INSERT INTO identity.security_device (
            id, principal_id, principal_type, trust_state, integrity_state,
            registered_at, last_seen_at, restricted_at, restriction_reason,
            revoked_at, revocation_reason
        ) VALUES (
            '00000000-0000-0000-0000-000000000000',
            '018f0000-0000-7000-8000-000000000211',
            'customer',
            'trusted',
            'unknown',
            now() - interval '2 minutes',
            now() - interval '1 minute',
            NULL,
            NULL,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected zero device identifier to fail';
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
            '018f0000-0000-7000-8000-000000000111',
            '018f0000-0000-7000-8000-000000000211',
            'customer',
            'restricted',
            'unknown',
            now() - interval '2 minutes',
            now() - interval '1 minute',
            now() - interval '30 seconds',
            '   ',
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected blank device restriction reason to fail';
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
            '00000000-0000-0000-0000-000000000000',
            '018f0000-0000-7000-8000-000000000201',
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
        RAISE EXCEPTION 'Expected zero session identifier to fail';
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
            '018f0000-0000-7000-8000-000000000311',
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
            now() - interval '30 seconds',
            '   ',
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected blank session restriction reason to fail';
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
            '018f0000-0000-7000-8000-000000000312',
            '018f0000-0000-7000-8000-000000000201',
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
            now() - interval '15 seconds',
            'not_a_known_reason'
        );
        RAISE EXCEPTION 'Expected unknown session revocation reason to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;
