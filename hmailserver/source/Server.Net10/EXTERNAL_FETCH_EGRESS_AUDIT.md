# External Fetch Egress Audit

Date: 2026-07-14

This records the external-fetch egress slice for the production parity loop.
It does not change COM contracts, SQL schema, POP3 commands, TLS settings, or
legacy destination behavior.

## Legacy Reference

- `Server/ExternalFetcher/ExternalFetch.cpp::ExternalFetch::Start` creates a
  `POP3ClientConnection` with the configured server name as the TLS hostname,
  calls `DNSResolver::GetIpAddresses(server, addresses, true)`, selects the
  first returned address, and calls `TCPConnection::Connect` with that numeric
  address. An empty result logs and aborts the attempt.
- `Server/Common/TCPIP/DNSResolver.cpp::DNSResolver::GetIpAddresses` and
  `GetIpAddressesRecursive_` query A and, when available, AAAA records in the
  configured IPv4/IPv6 order. When no address is returned, CNAME lookup is
  followed recursively up to the legacy recursion bound. There is no private,
  loopback, link-local, metadata, proxy, or redirect policy.
- `Server/Common/TCPIP/TCPConnection.cpp::TCPConnection::Connect` and
  `StartAsyncConnect_` connect to the supplied numeric endpoint. The only
  destination guard is the legacy self-connection check for a local address on
  one of hMailServer's own listener ports. Other local or private POP3 servers
  remain valid configured destinations.
- `Server/Common/TCPIP/TCPConnection.cpp::TCPConnection::AsyncHandshake` uses
  the global `VerifyRemoteSslCertificate` setting and the configured server name
  for certificate verification/SNI when the connection is TLS protected.
- `Server/ExternalFetcher/ExternalFetchManager.cpp::FetchIsAllowed_` checks
  only that the destination account and domain are active. It does not check
  the remote server address.
- `Server/ExternalFetcher/ExternalFetchTask.cpp::ExternalFetchTask::DoWork`
  calls `PersistentFetchAccount::SetNextTryTime` and then unlocks the account
  after every fetch attempt, including resolution and connection failure.
  `PersistentFetchAccount::SetNextTryTime` uses `faminutes` for the delay.
- `Server/ExternalFetcher/POP3ClientConnection.cpp` has no proxy or redirect
  behavior. `OnConnected`, the STARTTLS capability/state handlers, and
  `OnCouldNotConnect` define the connection and failure flow; timeout handling
  sends `QUIT`.
- Legacy regression coverage in `test/RegressionTests/POP3/Fetching/Basics.cs`
  and `ExternalAccountSslTests.cs` uses local POP3 endpoints, so an unconditional
  private/loopback deny would break an established local-server scenario.

## .NET 10 Comparison

- `src/HMailServer.Protocols/Pop3/TcpExternalFetchSessionFactory.cs::ConnectAsync`
  now resolves `ExternalFetchAccountLease.ServerAddress` once through
  `IExternalFetchAddressResolver`, evaluates the complete answer set through
  `ExternalFetchEndpointPolicy`, and connects to the selected numeric
  endpoint. The original configured hostname remains the TLS
  `TargetHost`.
- The same factory's `UpgradeToTlsAsync` uses the original configured hostname
  as `SslClientAuthenticationOptions.TargetHost`. STARTTLS is negotiated by
  `TcpExternalFetchSession.InitializeAsync` after the greeting and CAPA check.
- `src/HMailServer.Protocols/Pop3/ExternalFetchProcessor.cs::RunBatchAsync`
  catches per-account failures and now completes the lease through the existing
  `CompleteAsync` path. That SQL path schedules `fanexttry` from `faminutes`,
  preserving legacy retry timing; this bounded change landed in `dada12fea`.
- `src/HMailServer.Service/ExternalFetchHostedService.cs` still polls every 30
  seconds by default, and `Program.cs` still registers the raw TCP session
  factory. The new resolver/policy seam does not add proxy or redirect
  behavior.
- `ExternalFetchPop3ClientOptions` adds an audit-only-by-default
  `EnforceEgressPolicy` switch and an explicit `AllowedPrivateCidrs` list.
  `Program.cs` maps these to `ExternalFetch:EgressEnforce` /
  `HMAILSERVER_EXTERNAL_FETCH_EGRESS_ENFORCE` and
  `ExternalFetch:AllowedPrivateCidrs` /
  `HMAILSERVER_EXTERNAL_FETCH_ALLOWED_PRIVATE_CIDRS`. The default preserves
  configured legacy local POP3 compatibility; enforcement is a
  configuration-only rollback boundary.
- Existing focused coverage now includes
  `ExternalFetchEndpointPolicyTests` for public, explicit loopback, private
  CIDR opt-in, mixed, metadata, mapped-IPv6, and special-use answers, plus
  `TcpExternalFetchSessionFactoryTests` for resolve-once numeric endpoint
  pinning and enforcement-before-connect, alongside
  `ExternalFetchProcessorTests` (31), and
  `SqlServerExternalFetchAccountStoreTests` (4). DNS answer changes between
  calls, and live TLS certificate/SNI handshake behavior remain explicit test
  gaps; the factory-level TLS hostname seam and credential-free decision
  observer are covered.
- The .NET COM `FetchAccount` mutators and `DownloadNow` remain `E_NOTIMPL`,
  so this audit does not open a new administrator destination-mutation path.

## Current Slice and Next Boundary

The bounded .NET 10 slice is complete: resolution is injected and performed
once, the first resolver-order numeric endpoint is used, all answers are
evaluated before the connection, IPv4-mapped IPv6 values are normalized,
  metadata/cloud-platform targets are denied, arbitrary hostnames resolving to
  loopback are denied, and explicit `localhost`/loopback literals require an
  explicit matching CIDR entry when enforcement is enabled. Private/ULA
  destinations require the same allow-list. Audit-only mode remains the
  default so existing configured local POP3 deployments are not changed
  without an operator switch.

The .NET failure path preserves the legacy `faminutes` retry schedule. The
external-fetch client now has explicit operation deadlines and the legacy
POP3 control-line budget; remaining egress work is operational live DNS/TLS
integration coverage before enabling enforcement broadly. Credential-free
policy decisions now flow through the service logger without logging
credentials. These are separate from this resolve-once slice.

## Remaining Higher-Priority Blockers

The production-gate security review found blockers that prevent broad egress
enforcement and remain ahead of live policy rollout:

- The registered legacy `InterfaceFetchAccount` class remains directly
  activatable for COM identity compatibility, but commit `62f40dc77` now leaves
  that constructor unattached. Its existing member guards fail closed before
  `put_ServerAddress`, `put_AccountID`, `Save`, or `DownloadNow` can reach
  `hm_fetchaccounts` persistence or external-fetch scheduling. The authorized
  `Account -> FetchAccounts` attachment paths remain unchanged. The remaining
  SEC-20 evidence gaps are live DNS/TLS integration and adaptive timeout
  behavior under high load; the next COM slice must preserve the installed
  CLSID/IID/ProgID/DISPID/vtable shape while keeping every mutator behind an
  authenticated owning collection.
- `source/WebAdmin/background_account_externalaccount_save.php` now requires
  POST before reading scope IDs or resolving the domain/account/fetch-account
  objects, so external-account add/edit/delete/download-now mutations cannot
  execute through GET-shaped requests. The handler also reads action, scope,
  field, and CSRF values from POST only; the existing `index.php` background
  validation remains in place, and the add/edit form carries the token in a
  hidden POST field. The edit path now captures the existing
  `ServerAddress`/`Port`/`Username`/`ConnectionSecurity` tuple before applying
  setters and clears the stored password when that tuple changes and the
  submitted password is blank or omitted. String comparisons are type-safe, explicit
  passwords take precedence, unchanged edits retain the existing password,
  and new-item add behavior is unchanged.
- This retargeting hardening is WebAdmin-only. Legacy
  `InterfaceFetchAccount::get/put_ServerAddress`, `get/put_Port`,
  `get/put_Username`, `get/put_Password`, `get/put_ConnectionSecurity`, and
  `InterfaceFetchAccount::Save` keep the installed COM shape; legacy
  `PersistentFetchAccount::ReadObject` decrypts `fapassword` and
  `PersistentFetchAccount::SaveObject` rewrites it. The .NET 10
  `FetchAccount` setters and `Save()` remain `E_NOTIMPL`, so full COM
  mutation parity is still a separate migration blocker.
- The .NET external POP3 client has no explicit greeting/command/idle timeout
  or maximum line/message budget equivalent to the legacy POP3-client timeout.
  This remains an operational/performance security gap, separate from the
  resolver policy seam.

The resolve-once/policy boundary now covers the first four implementation
points above. It preserves no-proxy/no-redirect behavior and uses the caller's
cancellation token for resolution and numeric connect. Factory-level tests
cover the TLS hostname seam and credential-free decision observer; remaining
live evidence is DNS changes between calls and TLS certificate/SNI handshake
behavior.

Rollout should begin in audit-only mode, then enable enforcement with a
configuration-only rollback. No database migration, COM identity change,
legacy C++ behavior change, or production data operation is required.
