# External Fetch Egress Audit

Date: 2026-07-13

This is a read-only parity audit for the production parity loop. It does not
change COM contracts, SQL schema, POP3 commands, TLS settings, service
configuration, or destination access.

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
  passes `ExternalFetchAccountLease.ServerAddress` directly to
  `TcpClient.ConnectAsync`. Runtime DNS resolution and endpoint selection are
  therefore not exposed to a policy seam and no numeric endpoint is pinned.
- The same factory's `UpgradeToTlsAsync` uses the original configured hostname
  as `SslClientAuthenticationOptions.TargetHost`. STARTTLS is negotiated by
  `TcpExternalFetchSession.InitializeAsync` after the greeting and CAPA check.
- `src/HMailServer.Protocols/Pop3/ExternalFetchProcessor.cs::RunBatchAsync`
  catches per-account failures and now completes the lease through the existing
  `CompleteAsync` path. That SQL path schedules `fanexttry` from `faminutes`,
  preserving legacy retry timing; this bounded change landed in `dada12fea`.
- `src/HMailServer.Service/ExternalFetchHostedService.cs` polls every 30 seconds
  by default, and `Program.cs` registers the raw TCP session factory. No
  external-fetch egress policy, address allow-list, private-network classifier,
  proxy, redirect handler, or DNS-resolution injection exists.
- `ExternalFetchPop3ClientOptions` currently contains only buffer sizes and
  `NoDelay`. `ExternalFetchAccountLease` carries the legacy address, port,
  credentials, and security mode but no policy context.
- Existing focused coverage is protocol/processor behavior only:
  `TcpExternalFetchSessionFactoryTests` (20 test methods),
  `ExternalFetchProcessorTests` (31), and
  `SqlServerExternalFetchAccountStoreTests` (4). No test covers mixed DNS
  answers, DNS rebinding, special-use addresses, metadata addresses, endpoint
  pinning, proxy/redirect behavior, or egress denial.
- The .NET COM `FetchAccount` mutators and `DownloadNow` remain `E_NOTIMPL`,
  so this audit does not open a new administrator destination-mutation path.

## Decision and Next Boundary

The current local gate is not ready for a blanket private-network deny. Legacy
supports configured local POP3 servers, and the .NET failure path now preserves
the legacy `faminutes` retry schedule so a future policy denial does not create
a tight retry loop. The completed failure-scheduling slice is therefore kept
separate from destination policy.

## Higher-Priority Blockers

The production-gate security review found blockers that outrank egress-policy
enforcement and must be handled before a new destination policy is opened:

- The registered legacy `InterfaceFetchAccount` class is directly activatable.
  Its constructor creates a live object before an authenticated owning
  `Account -> FetchAccounts` parent is attached. `put_ServerAddress`,
  `put_AccountID`, `Save`, and `DownloadNow` can therefore be reached without
  the parent ownership boundary. See
  `Server/COM/InterfaceFetchAccount.cpp` and
  `Server/COM/InterfaceFetchAccount.rgs`. The next COM slice must preserve the
  installed CLSID/IID/ProgID/DISPID/vtable shape while denying direct activation
  and requiring an authenticated owning collection for every mutator.
- `source/WebAdmin/background_account_externalaccount_save.php` now requires
  POST before reading scope IDs or resolving the domain/account/fetch-account
  objects, so external-account add/edit/delete/download-now mutations cannot
  execute through GET-shaped requests. The handler also reads action, scope,
  field, and CSRF values from POST only; the existing `index.php` background
  validation remains in place, and the add/edit form carries the token in a
  hidden POST field. Credential-authority retargeting remains a separate slice.
- Changing an external-account destination while retaining a blank password
  field can replay the stored password to a new POP3 authority. Destination,
  port, username, and security mode need credential-authority handling in a
  later WebAdmin/COM slice.
- The .NET external POP3 client has no explicit greeting/command/idle timeout
  or maximum line/message budget equivalent to the legacy POP3-client timeout.
  This remains an operational/performance security gap, separate from the
  resolver policy seam.

After the higher-priority COM/WebAdmin blockers are closed, the egress slice
should add an injected address-resolution/policy boundary at
`IExternalFetchSessionFactory`/`TcpExternalFetchSessionFactory`. It should:

1. Resolve once and connect to an approved numeric endpoint while retaining the
   original hostname for TLS SNI/certificate validation.
2. Evaluate every resolved address, including IPv4-mapped IPv6 values, against
   loopback, unspecified, link-local, private, carrier-grade NAT, unique-local,
   and cloud metadata ranges.
3. Permit local/private destinations only through an explicit, auditable
   configuration or CIDR allow-list; keep metadata targets denied.
4. Preserve no-proxy/no-redirect POP3 behavior, cancellation/timeouts, and
   legacy local-server compatibility through an explicit rollout switch.
5. Add tests for public targets, allowed local targets, denied special-use
   targets, mixed answers, DNS changes between calls, and TLS hostname
   preservation. Log the policy decision without logging credentials.

Rollout should begin in audit-only mode, then enable enforcement with a
configuration-only rollback. No database migration, COM identity change,
legacy C++ behavior change, or production data operation is required.
