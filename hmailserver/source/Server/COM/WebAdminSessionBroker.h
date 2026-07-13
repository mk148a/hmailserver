// Copyright (c) 2026 hMailServer .NET 10 rewrite contributors.
// http://www.hmailserver.com

#pragma once

#include "../Common/BO/Account.h"

struct IInterfaceApplication;

namespace HM
{
   class COMAuthentication;

   // This is a native dependency seam, not a COM interface. Production reads
   // use the legacy persistence/configuration sources; tests provide snapshots.
   class IWebAdminSessionPrincipalSource
   {
   public:
      virtual ~IWebAdminSessionPrincipalSource() {}

      virtual std::shared_ptr<const Account> ReadAccountByID(__int64 accountID) const = 0;
      virtual bool IsDomainActive(__int64 domainID) const = 0;
      virtual bool GetAccountCredentialVersion(__int64 accountID, String &credentialVersion) const = 0;
      virtual bool GetAdministratorCredentialVersion(String &credentialVersion) const = 0;
   };

   class WebAdminSessionPrincipal
   {
   public:
      WebAdminSessionPrincipal();
      explicit WebAdminSessionPrincipal(const std::shared_ptr<const Account> &account);

      bool IsValid() const;
      bool Matches(const std::shared_ptr<const Account> &account) const;

      __int64 GetAccountID() const { return account_id_; }
      __int64 GetDomainID() const { return domain_id_; }
      Account::AdminLevel GetAdminLevel() const { return admin_level_; }
      const String &GetAddress() const { return address_; }

   private:
      __int64 account_id_;
      __int64 domain_id_;
      Account::AdminLevel admin_level_;
      String address_;
   };

   // This is an internal, process-local token store. It is deliberately not a
   // COM class and has no registry, type-library, or persistence surface.
   class WebAdminSessionBroker
   {
   public:
      typedef __int64 TimePoint;
      typedef std::function<TimePoint()> Clock;
      typedef std::function<std::shared_ptr<const Account>(const WebAdminSessionPrincipal &)> PrincipalRefreshHook;
      typedef std::function<bool(const WebAdminSessionPrincipal &, String &)> CredentialVersionHook;

      class Lifetime
      {
      public:
         Lifetime(TimePoint idleTimeoutSeconds = 20 * 60, TimePoint absoluteTimeoutSeconds = 8 * 60 * 60) :
            idle_timeout_seconds_(idleTimeoutSeconds),
            absolute_timeout_seconds_(absoluteTimeoutSeconds)
         {
         }

         TimePoint idle_timeout_seconds_;
         TimePoint absolute_timeout_seconds_;
      };

      WebAdminSessionBroker(const PrincipalRefreshHook &principalRefreshHook,
                            const CredentialVersionHook &credentialVersionHook,
                            const Clock &clock = Clock(),
                            const Lifetime &lifetime = Lifetime());

      WebAdminSessionBroker(const PrincipalRefreshHook &principalRefreshHook,
                            const CredentialVersionHook &credentialVersionHook,
                            const std::vector<unsigned char> &processKey,
                            const Clock &clock = Clock(),
                            const Lifetime &lifetime = Lifetime());

      bool CreateSession(const String &phpSessionID,
                         const std::shared_ptr<const Account> &authenticatedPrincipal,
                         String &rawToken);

      std::shared_ptr<COMAuthentication> OpenSession(const String &rawToken, const String &phpSessionID);
      void RevokeSession(const String &rawToken, const String &phpSessionID);

   private:
      struct SessionRecord
      {
         SessionRecord() :
            created_at_(0),
            last_used_at_(0),
            sequence_(0)
         {
         }

         WebAdminSessionPrincipal principal_;
         String session_binding_hmac_;
         String credential_version_hmac_;
         TimePoint created_at_;
         TimePoint last_used_at_;
         unsigned __int64 sequence_;
      };

      TimePoint GetCurrentTime_() const;
      bool IsExpired_(const SessionRecord &record, TimePoint now) const;
      String HmacHex_(const String &purpose, const String &value) const;

      static std::vector<unsigned char> GenerateProcessKey_();
      static String BytesToHex_(const unsigned char *bytes, size_t byteCount);
      static bool ConstantTimeEquals_(const String &left, const String &right);

      PrincipalRefreshHook principal_refresh_hook_;
      CredentialVersionHook credential_version_hook_;
      Clock clock_;
      Lifetime lifetime_;
      std::vector<unsigned char> process_key_;
      std::map<String, SessionRecord> sessions_;
      unsigned __int64 next_sequence_;
      boost::mutex mutex_;
   };

   // Authenticates legacy WebAdmin credentials before admitting the resulting
   // principal to the native broker. This is not a COM or request-facing API.
   class LegacyWebAdminCredentialAdmission
   {
   public:
      typedef std::function<std::shared_ptr<const Account>(const String &, const String &)> AuthenticationHook;

      static bool CreateSession(WebAdminSessionBroker &broker,
                                const String &phpSessionID,
                                const String &username,
                                const String &password,
                                String &rawToken);

      static bool CreateSession(WebAdminSessionBroker &broker,
                                const String &phpSessionID,
                                const String &username,
                                const String &password,
                                const AuthenticationHook &authenticationHook,
                                String &rawToken);
   };

   // Creates the installed Application class through a native-only seam. It
   // adds no COM class, interface member, or registration surface.
   class LegacyWebAdminApplicationFactory
   {
   public:
      static HRESULT Create(const std::shared_ptr<COMAuthentication> &authentication,
                            IInterfaceApplication **application);
   };

   // Resolves an existing native broker session and publishes only an
   // authenticated Application. It adds no COM or registration surface.
   class LegacyWebAdminSessionRequest
   {
   public:
      static HRESULT CreateApplication(WebAdminSessionBroker &broker,
                                       const String &rawToken,
                                       const String &phpSessionID,
                                       IInterfaceApplication **application);
   };

   // Composes only internal legacy sources with the native broker. It has no
   // COM registration or request-facing surface.
   class LegacyWebAdminSessionBrokerFactory
   {
   public:
      static std::shared_ptr<WebAdminSessionBroker> Create(
         const WebAdminSessionBroker::Clock &clock = WebAdminSessionBroker::Clock(),
         const WebAdminSessionBroker::Lifetime &lifetime = WebAdminSessionBroker::Lifetime());

      static std::shared_ptr<WebAdminSessionBroker> Create(
         const std::shared_ptr<IWebAdminSessionPrincipalSource> &source,
         const WebAdminSessionBroker::Clock &clock = WebAdminSessionBroker::Clock(),
         const WebAdminSessionBroker::Lifetime &lifetime = WebAdminSessionBroker::Lifetime());

      static std::shared_ptr<WebAdminSessionBroker> Create(
         const std::shared_ptr<IWebAdminSessionPrincipalSource> &source,
         const std::vector<unsigned char> &processKey,
         const WebAdminSessionBroker::Clock &clock = WebAdminSessionBroker::Clock(),
         const WebAdminSessionBroker::Lifetime &lifetime = WebAdminSessionBroker::Lifetime());

   private:
      static bool IsAdministrator_(const WebAdminSessionPrincipal &principal);
      static std::shared_ptr<const Account> RefreshPrincipal_(
         const std::shared_ptr<IWebAdminSessionPrincipalSource> &source,
         const WebAdminSessionPrincipal &principal);
      static bool GetCredentialVersion_(
         const std::shared_ptr<IWebAdminSessionPrincipalSource> &source,
         const WebAdminSessionPrincipal &principal,
         String &credentialVersion);
   };

   class WebAdminSessionBrokerTester
   {
   public:
      void Test();

   private:
      static std::shared_ptr<Account> CreateAdministrator_();
      static std::vector<unsigned char> CreateProcessKey_(unsigned char value);

      void TestCredentialAdmission_();
      void TestLifecycleAndBinding_();
      void TestIdleAndAbsoluteExpiry_();
      void TestRevocation_();
      void TestCredentialVersionDenial_();
      void TestPrincipalRefreshDenial_();
      void TestProcessRestartInvalidation_();
      void TestAuthoritativeAccountAndDomainDenial_();
      void TestAuthoritativeRoleMismatchDenial_();
      void TestAuthoritativeCredentialVersionDenial_();
      void TestApplicationFactory_();
      void TestSessionRequestComposition_();
      void TestInstalledApplicationContract_();
   };
}
