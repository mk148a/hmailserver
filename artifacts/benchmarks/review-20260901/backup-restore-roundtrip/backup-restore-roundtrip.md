# Net10 backup -> restore -> backup round-trip

- Result: `PASS`
- Tests: `25/25` passed, `0` failed, `0` skipped
- SQL: `localhost`, Integrated Security
- Database pattern: `hmailserver_net10_*` with drop in test `finally`
- Data roots: test-owned temporary roots with delete in `finally`
- Production service/database/Data directory used: `False`

This is isolated restore evidence; it does not prove production backup/restore, installer rollback, or service rollback.