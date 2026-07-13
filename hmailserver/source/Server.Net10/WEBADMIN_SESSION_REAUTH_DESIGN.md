# WebAdmin Session Reauthentication Design

## Status

This record is current through the SEC-18 legacy broker-bridge identity and caller-access audit. The foundation changes only internal legacy C++ classes; it does not change PHP WebAdmin, register a broker COM class, alter an installed legacy COM interface, or change the .NET 10 runtime.

## Implemented Foundation

- `Server/COM/WebAdminSessionBroker` is a native, service-local C++ class, not a COM class. It generates a 32-byte token, stores only process-key HMACs of the token, PHP-session binding, and credential version, and never persists a token or password verifier.
- The record expires at 20 minutes idle or 8 hours absolute, is lost on process restart, fails closed on principal/credential refresh failure, and requires the PHP-session binding for both open and revoke. A wrong binding does not revoke a valid session.
- `LegacyWebAdminSessionBrokerFactory` composes injected current-principal and credential-version hooks with fresh `PersistentAccount::ReadObject` and active `PersistentDomain::ReadObject` lookups for regular accounts, plus `IniFileSettings::GetAdministratorPassword` for the legacy `Administrator` principal. External AD accounts fail closed because this bounded source has no current persisted credential verifier for them. `COMAuthentication::AttachAuthenticatedPrincipal` can create an already-authenticated internal principal without extending `IInterfaceApplication`.
- `LegacyWebAdminApplicationFactory::Create` accepts only a non-null authenticated `COMAuthentication`, creates a fresh existing `InterfaceApplication`, and installs that private shared authentication before publishing its `IInterfaceApplication` reference. It adds no COM member, identity, registration, or public authentication setter; `InterfaceApplication::Authenticate` remains unchanged.
- `LegacyWebAdminCredentialAdmission::CreateSession` constructs a fresh local `COMAuthentication` for every production call, delegates username/password validation to its unchanged legacy `Authenticate` method, and passes only a successful principal to the existing broker token path. The injection overload exists only for native tests; it clears the raw token and fails closed on null or exception outcomes. No password field is added to the admission helper, broker, or session record.
- `LegacyWebAdminSessionRequest::CreateApplication` takes an existing broker, raw token, and PHP-session ID, then calls `OpenSession` and publishes only its authenticated result through `LegacyWebAdminApplicationFactory`. It clears the output pointer before every denial and adds no COM, registration, persistence, or request-facing surface.
- `LegacyWebAdminSessionService` retains exactly one broker created through `LegacyWebAdminSessionBrokerFactory` per owning native service instance and exposes only wrappers over credential admission and authenticated request composition. Its injected-broker constructor is a native test seam; a null broker clears raw tokens and denies application creation. It is not yet wired into service startup, COM registration, or PHP.
- `WebAdminSessionBrokerTester`, called by the existing legacy `ClassTester`, covers lifecycle, binding mismatch, idle/absolute expiry, revocation, process restart, account disable/delete, domain/admin-level mismatch, administrator/account credential-version mismatch, null/anonymous factory denial, broker-authenticated Application creation, credential-admission accept/null/exception/empty-session/current-verifier denial, session-request valid/missing/wrong-session/expired/revoked outcomes, same-owner admission/request success, separate-owner restart-model denial, null-owner denial, direct-activation denial, and the installed Application IID/CLSID/`Authenticate` signature/DISPID 17.

Neither native factory, session-request helper, nor service owner can be reached by PHP or any direct COM client.

Credential admission intentionally follows the legacy COM authentication path, including its account/domain cache behavior, alias/default-domain handling, script override, and SSPI behavior. The broker's later credential-version check uses fresh persistence/configuration reads. An out-of-band account update can therefore remain admission-visible until the legacy cache expires or is invalidated, but an already-issued token is still denied after the fresh verifier changes.

## Current Evidence

| Concern | Current behavior | Source |
| --- | --- | --- |
| Login | PHP authenticates the submitted password, regenerates the PHP session ID, and serializes the username, password, and principal fields in `$_SESSION`. | `background_login.php::Login` |
| Per-request authorization | Each request creates a new `hMailServer.Application`, then passes `session_username` and plaintext `session_password` to `Application.Authenticate`. | `initialize.php` |
| Password change | Editing the current account replaces `session_password` before `Account.Save`. | `background_account_save.php` |
| Logout | PHP destroys its session but has no service-side WebAdmin session record to revoke. | `logout.php` |
| Legacy COM state | Every `InterfaceApplication` constructs a fresh `COMAuthentication`; `Authenticate` resets and replaces that object's current account. | `InterfaceApplication::InterfaceApplication`, `COMAuthentication::Authenticate` |
| Installed contract | `IInterfaceApplication.Authenticate` is legacy DISPID 17 on the installed dual interface. Appending a member would change the vtable/type-library contract. | `hMailServer.idl` |
| .NET 10 state | The hosted `Application` also keeps authentication in a per-instance field and its installed dual interface keeps `Authenticate` at DISPID 17. | `Application.cs`, `ApplicationComClass.cs` |

The PHP `session_loggedin` field is only a UI gate. It cannot authorize a new COM `Application` object, and a PHP-only removal of `session_password` would make every authenticated request fail.

## Decision

Use an additive, credentialless `WebAdminSessionBroker` hosted by the mail-server process. PHP persists only an opaque bearer token in its session. The broker persists no password, and creates a fresh authenticated existing `IInterfaceApplication` for each PHP request.

Do not use these alternatives:

- A PHP-only logged-in flag: it cannot authenticate a newly created COM application object.
- A PHP-session encryption key or reversible application key: an attacker who reads the PHP session store and its web configuration can still recover a reusable password.
- A new member on `IInterfaceApplication`: it would change an installed IID/vtable/DISPID contract.
- A public broker guarded only by a bearer token: it would broaden direct activation instead of preserving the existing access boundary.

## Proposed Broker Protocol

The implemented foundation is a native class with no COM identity. A future dedicated internal COM bridge may use new identifiers only after the identity and registration review; it must not alter any existing class, ProgID, IID, DISPIDs, default interface, or type library member.

1. `CreateSession(username, password, phpSessionId)` is called only by the login handler. The broker validates the submitted password using the authoritative server authentication path, generates a 32-byte token using the service random source, and returns the token plus authoritative principal metadata.
2. PHP stores only `session_reauth_token`, `session_loggedin`, the CSRF token, and non-secret UI metadata. It never writes `session_password`.
3. `OpenApplication(token, phpSessionId)` runs at the beginning of every authenticated request. It verifies the token, session binding, expiry, current principal status, and credential version. It returns a fresh already-authenticated existing `IInterfaceApplication`; PHP must not call legacy `Application.Authenticate` again.
4. `Revoke(token, phpSessionId)` is called before logout destroys the PHP session. Missing, expired, revoked, or invalid tokens fail closed to the login page and do not yield an authenticated application.
5. `RotateAfterOwnPasswordChange(token, phpSessionId, newPassword)` is called only after the current user's `Account.Save` succeeds. It validates the newly persisted password, invalidates the old token, and returns a replacement token. If rotation fails, PHP clears its session and forces a new login.

The broker keeps the following only in service process memory:

| Field | Storage rule |
| --- | --- |
| Token | Store only `HMAC-SHA-256(processKey, rawToken)`; return the raw token once to PHP. |
| PHP-session binding | Store only an HMAC of the PHP session ID using the same process key. |
| Principal | Account ID, domain ID, address, and current administrator level. Refresh from the authoritative account/configuration source on every open. |
| Credential version | HMAC of the current persisted password verifier (or administrator password verifier) using the process key. This invalidates all tokens when a password changes outside the current session. |
| Lifetime | Default 20-minute idle timeout and 8-hour absolute timeout. Process restart invalidates every broker session. |

All token, binding, and credential-version comparisons must be constant time. The process key is generated at service startup and is never serialized. Token values must not appear in URLs, logs, exceptions, diagnostics, or COM error text.

## Password and Principal Changes

`OpenApplication` refreshes the current principal before returning an application object. Inactive/deleted accounts, changed domain/account administrator roles, and changed credential versions revoke the token and deny access.

For the current user's successful password change, the handler calls `RotateAfterOwnPasswordChange` with the submitted password already present only in the POST body. The broker validates it against the new persisted verifier before issuing a replacement token. This preserves a successful current-user password-change flow without storing the new password in PHP session serialization. A failure or interrupted request leaves the previous token unusable because its credential version no longer matches.

Changing another account's password does not rotate the current administrator's token. Broker sessions for the changed account fail their next `OpenApplication` credential-version check.

## Access Boundary and COM Compatibility

`WebAdminSessionBroker` must be an additive internal class, registered separately from the legacy COM manifest and restricted by COM launch/access security to the configured WebAdmin IIS application-pool identity. The broker must also verify the caller token under COM impersonation; a bearer token alone is insufficient authorization for direct activation.

The broker creates an existing `InterfaceApplication`/`Application` instance through an internal factory and injects an internal authenticated principal. It must not append to `IInterfaceApplication`, add a member to its default interface, or make any existing child class directly activatable. The broker class itself needs a new interface/CLSID/ProgID only after a dedicated identity and registration review.

## Broker Bridge Identity and Caller-Access Audit

### Evidence

- Every PHP request creates `new COM("hMailServer.Application")` in `WebAdmin/initialize.php`; the existing error path identifies DCOM permissions but supplies no caller identity. The same file authenticates each new Application from `session_username` and `session_password`.
- The legacy installer copies `source/WebAdmin` to `PHPWebAdmin` but contains no IIS site, application-pool, worker identity, AppID ACL, or DCOM ACL configuration. The versioned source therefore cannot identify the trusted PHP caller for a deployment.
- The existing `Application` ProgID/CLSID and its `LocalServer32` registration all use the installed AppID `{5EDEC473-39E0-43F6-A234-1947071721C8}`. The service writes only `LocalService = hMailServer` for that AppID. It does not write `LaunchPermission` or `AccessPermission`.
- `ChMailServerModule::RegisterObjects` calls `CoInitializeSecurity` once for the entire local COM server with `RPC_C_AUTHN_LEVEL_CONNECT` and `RPC_C_IMP_LEVEL_IMPERSONATE`; no current server path calls `CoImpersonateClient`, `CoQueryClientBlanket`, `OpenThreadToken`, or verifies a caller SID.
- The .NET 10 `LegacyComRegistrationManifest` intentionally mirrors the same installed AppID for every current hosted class, while `ComLocalServerHost` registers class factories without caller-identity policy. It cannot host a WebAdmin bridge as another existing Application-class registration.

### Required Additive Contract

Do not add a member, coclass, IID, DISPID, or registration value to the installed `hMailServer.Application` contract or its existing type library. A future bridge must instead use all-new identifiers:

- A new broker type library, separate from `{DB241B59-A1B1-4C59-98FC-8D101A2995F2}`, with a newly allocated and collision-checked library GUID.
- A new dual, nonextensible default interface named `IInterfaceWebAdminSessionBroker`, with a newly allocated IID and a frozen vtable/DISPID list containing only `CreateSession`, `OpenApplication`, `Revoke`, and `RotateAfterOwnPasswordChange`.
- A new `WebAdminSessionBroker` coclass and CLSID plus versioned and version-independent ProgIDs `hMailServer.WebAdminSessionBroker.1` and `hMailServer.WebAdminSessionBroker`. The future implementation must allocate fresh GUIDs once, record them in its separate type library and registration tests, and never reuse an installed Application, child-class, or type-library identifier.
- A separate broker AppID mapped to the same local service only after security validation. It must not inherit the broad installed Application AppID or its registration manifest.

The bridge may return only a fresh existing `IInterfaceApplication` after successful broker validation. It must never expose `WebAdminSessionBroker`, `COMAuthentication`, a raw principal, a process key, or a token lookup capability.

### Mandatory Caller-Access Gate

The raw bearer token is not sufficient authorization for direct COM activation. Before any bridge code, registration, or PHP change is allowed:

1. A non-production deployment inventory must resolve the exact IIS application-pool worker SID used by PHP WebAdmin and prove that the pool is not shared with untrusted applications.
2. The new AppID must use explicit local `LaunchPermission` and `AccessPermission` descriptors limited to that configured SID and the minimum service identities required for activation; it must not rely on the machine default DCOM permissions.
3. Every broker method must impersonate the COM caller, obtain its effective token, compare its SID to the configured worker SID, and always revert impersonation. Missing, anonymous, remote, or mismatched caller identity returns `E_ACCESSDENIED` without token or identity details.
4. A non-production integration test must prove authorized PHP-worker activation and method access, unauthorized local-user denial before and during invocation, bearer-token reuse from an unauthorized process denial, and denial after the configured worker SID changes.
5. Installer/update preflight must resolve the configured SID, write and read back the broker-only AppID ACLs, confirm the existing Application AppID/type-library bytes are unchanged, and fail closed without modifying PHP session behavior if any check fails. Rollback removes only the new broker registration and destroys broker sessions; it never restores a password from a token.

This is a security gate, not a production implementation approval. The current source has no trusted worker SID or broker-only DCOM ACL evidence, so registering the bridge now would broaden direct activation without a proven caller boundary.

Legacy implementation scope:

- Done: add a service-local broker store and an internal `COMAuthentication` principal-attach path.
- Done: compose authoritative account/server-administrator refresh and credential-version hooks with the native broker. Regular external AD accounts fail closed until a separately reviewed current-verifier source exists.
- Done: add an internal `InterfaceApplication` creation path; preserve `InterfaceApplication::Authenticate` exactly.
- Done: add native credential admission through the unchanged legacy `COMAuthentication::Authenticate` path without retaining the supplied password.
- Done: compose an existing broker token/session binding with the internal Application factory through a native-only request helper.
- Done: add a native service-local owner that holds one broker and exposes only those existing admission/request helpers; it is not yet hosted by the service.
- Done: audit the bridge identity and caller-access boundary. Existing PHP/IIS deployment source does not provide a worker SID or broker-only DCOM ACL, so broker COM registration remains intentionally blocked.
- Remaining: change only `initialize.php`, `background_login.php`, `background_account_save.php`, and `logout.php` to use the broker.
- Remaining: capture and validate a non-production IIS worker SID plus broker-only AppID launch/access descriptors before a separately reviewed broker registration.

.NET 10 implementation scope:

- Add the equivalent internal broker/store and service-host registration beside the existing `Application` registration.
- Keep `IInterfaceApplication`, `Application` CLSID/ProgID/default interface, and direct activation behavior unchanged.
- Add a principal authentication provider that covers server, domain, and account administrators before exposing the broker to WebAdmin. The current .NET provider is server-administrator-only, so the broker must not silently reduce legacy WebAdmin access.

## Test Strategy

The foundation covers the native token lifecycle, authoritative principal verification, broker-authenticated Application creation, and installed Application contract; the following PHP, caller-identity, and integration checks remain required before PHP source changes:

1. Login stores no `session_password`; a sentinel password is absent from serialized PHP session data, URLs, logs, and error text.
2. A valid token opens a fresh authenticated application; missing, malformed, wrong-session, expired, idle-expired, revoked, and post-restart tokens are denied.
3. Token and session-binding digests are the only broker token material retained; comparisons are constant time.
4. Logout revokes the broker record before PHP session destruction.
5. Session-ID and CSRF-token rotation on successful login remain intact.
6. Current-user password save returns a rotated token only after the new password verifies; an interrupted rotation forces re-login. Other sessions for that account are revoked by credential-version mismatch.
7. Account disable/delete, administrator/domain/account-role changes, and out-of-band administrator password changes deny the next request.
8. A future broker activation requires both broker-only AppID launch/access descriptors and method-level impersonated caller-SID verification. Unauthorized local clients and bearer-token reuse from another process must be denied; existing direct child activation must remain `E_ACCESSDENIED` and installed Application vtable/type-library bytes must remain unchanged.
9. PHP/WebAdmin acceptance covers login, normal request, logout, current-user password change, domain-admin and server-admin scopes, CSRF failure, and service restart.
10. .NET 10 tests cover the broker's access boundary, token lifecycle, principal refresh, and unchanged `Application.Authenticate` contract before hosted registration is enabled.

## Rollout and Recovery

This design requires no SQL migration and does not touch mail data, `hm_messages`, `hm_message_metadata`, or the data directory.

Deployment must install the service/broker and WebAdmin changes as one versioned unit. Preflight must resolve the IIS worker SID, verify broker-only AppID launch/access descriptors, prove impersonated caller-SID verification, verify the broker registration, secure random source, and ability to create a broker-authenticated application. Broker initialization failure must fail closed to the login page.

At cutover, any legacy PHP session containing `session_password` is destroyed. Do not migrate, encrypt in place, or copy the plaintext password. All users log in again. Service restart intentionally invalidates broker tokens and requires reauthentication.

Rollback also destroys WebAdmin sessions. It must never translate an opaque token back into a password. Reverting to an older WebAdmin package reintroduces SEC-18 and is therefore a documented break-glass operation requiring session purge, access-log review, and an immediate upgrade back to the broker-enabled package.

## Next Implementation Slice

Perform one bounded SEC-18 read-only non-production deployment inventory: capture the actual PHP WebAdmin IIS application-pool worker SID, the effective existing hMailServer AppID security descriptors, and whether an authorized local COM call exposes an impersonable caller token. Record evidence against the broker-only AppID preflight requirements. Do not edit C++/C#/PHP source, registry, IIS, service configuration, or production data; do not register a class, alter `IInterfaceApplication`, persist tokens or passwords, or change SMTP/IMAP/POP3 behavior.
