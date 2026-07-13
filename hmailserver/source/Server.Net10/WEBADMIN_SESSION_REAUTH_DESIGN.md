# WebAdmin Session Reauthentication Design

## Status

This record is current through the SEC-18 legacy internal-application factory implementation. The foundation changes only internal legacy C++ classes; it does not change PHP WebAdmin, register a broker COM class, alter an installed legacy COM interface, or change the .NET 10 runtime.

## Implemented Foundation

- `Server/COM/WebAdminSessionBroker` is a native, service-local C++ class, not a COM class. It generates a 32-byte token, stores only process-key HMACs of the token, PHP-session binding, and credential version, and never persists a token or password verifier.
- The record expires at 20 minutes idle or 8 hours absolute, is lost on process restart, fails closed on principal/credential refresh failure, and requires the PHP-session binding for both open and revoke. A wrong binding does not revoke a valid session.
- `LegacyWebAdminSessionBrokerFactory` composes injected current-principal and credential-version hooks with fresh `PersistentAccount::ReadObject` and active `PersistentDomain::ReadObject` lookups for regular accounts, plus `IniFileSettings::GetAdministratorPassword` for the legacy `Administrator` principal. External AD accounts fail closed because this bounded source has no current persisted credential verifier for them. `COMAuthentication::AttachAuthenticatedPrincipal` can create an already-authenticated internal principal without extending `IInterfaceApplication`.
- `LegacyWebAdminApplicationFactory::Create` accepts only a non-null authenticated `COMAuthentication`, creates a fresh existing `InterfaceApplication`, and installs that private shared authentication before publishing its `IInterfaceApplication` reference. It adds no COM member, identity, registration, or public authentication setter; `InterfaceApplication::Authenticate` remains unchanged.
- `WebAdminSessionBrokerTester`, called by the existing legacy `ClassTester`, covers lifecycle, binding mismatch, idle/absolute expiry, revocation, process restart, account disable/delete, domain/admin-level mismatch, administrator/account credential-version mismatch, null/anonymous factory denial, broker-authenticated Application creation, direct-activation denial, and the installed Application IID/CLSID/`Authenticate` signature/DISPID 17.

The native factory cannot be reached by PHP or any direct COM client.

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

Legacy implementation scope:

- Done: add a service-local broker store and an internal `COMAuthentication` principal-attach path.
- Done: compose authoritative account/server-administrator refresh and credential-version hooks with the native broker. Regular external AD accounts fail closed until a separately reviewed current-verifier source exists.
- Done: add an internal `InterfaceApplication` creation path; preserve `InterfaceApplication::Authenticate` exactly.
- Remaining: change only `initialize.php`, `background_login.php`, `background_account_save.php`, and `logout.php` to use the broker.
- Remaining: add a separately reviewed broker registration and DCOM identity restrictions.

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
8. Unauthorized local COM clients cannot activate or use a future registered broker; existing direct child activation must remain `E_ACCESSDENIED` and the installed Application vtable/type-library bytes must remain unchanged. Caller-identity checks are deferred because this foundation intentionally has no registered COM class.
9. PHP/WebAdmin acceptance covers login, normal request, logout, current-user password change, domain-admin and server-admin scopes, CSRF failure, and service restart.
10. .NET 10 tests cover the broker's access boundary, token lifecycle, principal refresh, and unchanged `Application.Authenticate` contract before hosted registration is enabled.

## Rollout and Recovery

This design requires no SQL migration and does not touch mail data, `hm_messages`, `hm_message_metadata`, or the data directory.

Deployment must install the service/broker and WebAdmin changes as one versioned unit. Preflight must verify the broker registration, application-pool identity ACL, secure random source, and ability to create a broker-authenticated application. Broker initialization failure must fail closed to the login page.

At cutover, any legacy PHP session containing `session_password` is destroyed. Do not migrate, encrypt in place, or copy the plaintext password. All users log in again. Service restart intentionally invalidates broker tokens and requires reauthentication.

Rollback also destroys WebAdmin sessions. It must never translate an opaque token back into a password. Reverting to an older WebAdmin package reintroduces SEC-18 and is therefore a documented break-glass operation requiring session purge, access-log review, and an immediate upgrade back to the broker-enabled package.

## Next Implementation Slice

Implement one bounded SEC-18 legacy credential-admission slice: add a native-only broker helper that validates a supplied login username/password through a fresh existing `COMAuthentication::Authenticate` and delegates only a successful principal to the existing token-creation path. Add focused C++ tests for accepted and rejected injected authentication outcomes with no raw password retained. Do not create a broker COM identity or registration, alter `IInterfaceApplication`, change PHP/WebAdmin, add a public COM member, persist tokens or passwords, alter password persistence, or change SMTP/IMAP/POP3 behavior.
