# Identity Persistence Foundation v1

This slice adds the persistence record contract for the approved Admin/User authentication boundary.

Included records:

- user credentials and lifecycle status;
- user roles;
- opaque server-side sessions;
- one-time recovery tokens;
- append-only identity audit events.

Security boundary:

- passwords are represented only as password hashes;
- sessions and recovery tokens are represented only as hashes;
- no provider secret or raw token is stored;
- live trading and PyPSX authorization are not enabled by this slice.

The records are intentionally internal to Infrastructure. Runtime routes remain fail-closed until the database model is registered in `KlyvestaDbContext`, a migration is applied, and integration tests verify session rotation, revocation, expiry, recovery-token one-time use, and admin/user authorization.

This document does not claim production login is complete.