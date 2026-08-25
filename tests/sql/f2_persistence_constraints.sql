\set ON_ERROR_STOP on

BEGIN;

INSERT INTO ops.idempotency_record (
    id, scope, key, request_hash, state, operation_id, created_at, completed_at, expires_at
) VALUES (
    '018f0000-0000-7000-8000-000000000001',
    'test-order',
    'idem-1',
    repeat('a', 64),
    'in_progress',
    NULL,
    now(),
    NULL,
    now() + interval '1 hour'
);

DO $$
BEGIN
    BEGIN
        INSERT INTO ops.idempotency_record (
            id, scope, key, request_hash, state, operation_id, created_at, completed_at, expires_at
        ) VALUES (
            '018f0000-0000-7000-8000-000000000002',
            'test-order',
            'idem-1',
            repeat('b', 64),
            'in_progress',
            NULL,
            now(),
            NULL,
            now() + interval '1 hour'
        );
        RAISE EXCEPTION 'Expected duplicate idempotency scope/key to fail';
    EXCEPTION
        WHEN unique_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO ops.idempotency_record (
            id, scope, key, request_hash, state, operation_id, created_at, completed_at, expires_at
        ) VALUES (
            '018f0000-0000-7000-8000-000000000003',
            'test-order',
            'idem-expiry',
            repeat('c', 64),
            'in_progress',
            NULL,
            now(),
            NULL,
            now() - interval '1 minute'
        );
        RAISE EXCEPTION 'Expected invalid idempotency expiry to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO ops.idempotency_record (
            id, scope, key, request_hash, state, operation_id, created_at, completed_at, expires_at
        ) VALUES (
            '018f0000-0000-7000-8000-000000000004',
            'test-order',
            'idem-state',
            repeat('d', 64),
            'unknown',
            NULL,
            now(),
            NULL,
            now() + interval '1 hour'
        );
        RAISE EXCEPTION 'Expected invalid idempotency state to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO ops.idempotency_record (
            id, scope, key, request_hash, state, operation_id, created_at, completed_at, expires_at
        ) VALUES (
            '018f0000-0000-7000-8000-000000000005',
            'test-order',
            'idem-time-inversion',
            repeat('e', 64),
            'completed',
            NULL,
            now(),
            now() - interval '1 minute',
            now() + interval '1 hour'
        );
        RAISE EXCEPTION 'Expected idempotency completion time inversion to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

INSERT INTO ops.inbox_message (
    id, provider, message_id, payload_hash, payload_json, state, received_at, processed_at
) VALUES (
    '018f0000-0000-7000-8000-000000000011',
    'paper-broker',
    'event-1',
    repeat('f', 64),
    '{"event":"fill"}'::jsonb,
    'received',
    now(),
    NULL
);

DO $$
BEGIN
    BEGIN
        INSERT INTO ops.inbox_message (
            id, provider, message_id, payload_hash, payload_json, state, received_at, processed_at
        ) VALUES (
            '018f0000-0000-7000-8000-000000000012',
            'paper-broker',
            'event-1',
            repeat('a', 64),
            '{"event":"duplicate"}'::jsonb,
            'received',
            now(),
            NULL
        );
        RAISE EXCEPTION 'Expected duplicate provider/message_id to fail';
    EXCEPTION
        WHEN unique_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO ops.inbox_message (
            id, provider, message_id, payload_hash, payload_json, state, received_at, processed_at
        ) VALUES (
            '018f0000-0000-7000-8000-000000000013',
            'paper-broker',
            'event-time-inversion',
            repeat('b', 64),
            '{"event":"invalid-time"}'::jsonb,
            'processed',
            now(),
            now() - interval '1 minute'
        );
        RAISE EXCEPTION 'Expected inbox processing time inversion to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

DO $$
BEGIN
    BEGIN
        INSERT INTO notification.outbox (
            id, event_type, payload_json, headers_json, occurred_at, published_at,
            attempt_count, next_attempt_at, last_error_code
        ) VALUES (
            '018f0000-0000-7000-8000-000000000021',
            'test.event',
            '{}'::jsonb,
            NULL,
            now(),
            NULL,
            -1,
            NULL,
            NULL
        );
        RAISE EXCEPTION 'Expected negative outbox attempt count to fail';
    EXCEPTION
        WHEN check_violation THEN NULL;
    END;
END
$$;

\ir f5_security_session_constraints.sql

ROLLBACK;
