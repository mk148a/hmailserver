// Copyright (c) 2026 hMailServer .NET 10 rewrite contributors.
// http://www.hmailserver.com

#include "stdafx.h"

#include "WebAdminSessionBroker.h"
#include "COMAuthentication.h"
#include "InterfaceApplication.h"

#include "../Common/Application/IniFileSettings.h"
#include "../Common/BO/Domain.h"
#include "../Common/Persistence/PersistentAccount.h"
#include "../Common/Persistence/PersistentDomain.h"

#include <ctime>
#include <openssl/hmac.h>
#include <openssl/rand.h>

#ifdef _DEBUG
#define DEBUG_NEW new(_NORMAL_BLOCK, __FILE__, __LINE__)
#define new DEBUG_NEW
#endif

namespace HM
{
   namespace
   {
      const size_t ProcessKeyLength = 32;
      const size_t TokenLength = 32;

      const GUID ExpectedApplicationInterfaceID =
      { 0x2c1a3ef1, 0x115f, 0x4029, { 0xbb, 0x33, 0xd9, 0xcc, 0xa4, 0xbb, 0x0d, 0xe8 } };

      const GUID ExpectedApplicationClassID =
      { 0xd6567ef8, 0x0a6c, 0x48e7, { 0x92, 0x88, 0xa2, 0x46, 0x31, 0x23, 0xc2, 0xf3 } };

      class PersistentWebAdminSessionPrincipalSource : public IWebAdminSessionPrincipalSource
      {
      public:
         std::shared_ptr<const Account> ReadAccountByID(__int64 accountID) const
         {
            std::shared_ptr<Account> account(new Account);
            if (!PersistentAccount::ReadObject(account, accountID))
               return std::shared_ptr<const Account>();

            return account;
         }

         bool IsDomainActive(__int64 domainID) const
         {
            std::shared_ptr<Domain> domain(new Domain);
            return PersistentDomain::ReadObject(domain, domainID) && domain->GetIsActive();
         }

         bool GetAccountCredentialVersion(__int64 accountID, String &credentialVersion) const
         {
            credentialVersion = "";
            std::shared_ptr<const Account> account = ReadAccountByID(accountID);
            if (!account || account->GetIsAD() || account->GetPassword().IsEmpty())
               return false;

            credentialVersion = account->GetPassword();
            return true;
         }

         bool GetAdministratorCredentialVersion(String &credentialVersion) const
         {
            credentialVersion = IniFileSettings::Instance()->GetAdministratorPassword();
            return !credentialVersion.IsEmpty();
         }
      };

      class TestWebAdminSessionPrincipalSource : public IWebAdminSessionPrincipalSource
      {
      public:
         std::map<__int64, std::shared_ptr<Account> > accounts_;
         std::map<__int64, bool> domain_activity_;
         std::map<__int64, String> account_credential_versions_;
         String administrator_credential_version_;

         std::shared_ptr<const Account> ReadAccountByID(__int64 accountID) const
         {
            std::map<__int64, std::shared_ptr<Account> >::const_iterator found = accounts_.find(accountID);
            if (found == accounts_.end())
               return std::shared_ptr<const Account>();

            return found->second;
         }

         bool IsDomainActive(__int64 domainID) const
         {
            std::map<__int64, bool>::const_iterator found = domain_activity_.find(domainID);
            return found != domain_activity_.end() && found->second;
         }

         bool GetAccountCredentialVersion(__int64 accountID, String &credentialVersion) const
         {
            credentialVersion = "";
            std::map<__int64, String>::const_iterator found = account_credential_versions_.find(accountID);
            if (found == account_credential_versions_.end())
               return false;

            credentialVersion = found->second;
            return !credentialVersion.IsEmpty();
         }

         bool GetAdministratorCredentialVersion(String &credentialVersion) const
         {
            credentialVersion = administrator_credential_version_;
            return !credentialVersion.IsEmpty();
         }
      };

      std::shared_ptr<Account> CreateTestDomainAdministrator()
      {
         std::shared_ptr<Account> account(new Account("domain-admin@example.com", Account::DomainAdmin));
         account->SetID(41);
         account->SetDomainID(7);
         account->SetActive(true);
         return account;
      }
   }

   WebAdminSessionPrincipal::WebAdminSessionPrincipal() :
      account_id_(0),
      domain_id_(0),
      admin_level_(Account::NormalUser)
   {
   }

   WebAdminSessionPrincipal::WebAdminSessionPrincipal(const std::shared_ptr<const Account> &account) :
      account_id_(0),
      domain_id_(0),
      admin_level_(Account::NormalUser)
   {
      if (!account)
         return;

      account_id_ = account->GetID();
      domain_id_ = account->GetDomainID();
      admin_level_ = account->GetAdminLevel();
      address_ = account->GetAddress();
   }

   bool
   WebAdminSessionPrincipal::IsValid() const
   {
      return !address_.IsEmpty();
   }

   bool
   WebAdminSessionPrincipal::Matches(const std::shared_ptr<const Account> &account) const
   {
      return account &&
             account_id_ == account->GetID() &&
             domain_id_ == account->GetDomainID() &&
             admin_level_ == account->GetAdminLevel() &&
             address_.CompareNoCase(account->GetAddress()) == 0;
   }

   WebAdminSessionBroker::WebAdminSessionBroker(const PrincipalRefreshHook &principalRefreshHook,
                                                  const CredentialVersionHook &credentialVersionHook,
                                                  const Clock &clock,
                                                  const Lifetime &lifetime) :
      principal_refresh_hook_(principalRefreshHook),
      credential_version_hook_(credentialVersionHook),
      clock_(clock),
      lifetime_(lifetime),
      process_key_(GenerateProcessKey_()),
      next_sequence_(0)
   {
      if (!principal_refresh_hook_ || !credential_version_hook_ ||
          lifetime_.idle_timeout_seconds_ <= 0 || lifetime_.absolute_timeout_seconds_ <= 0)
      {
         throw 0;
      }
   }

   WebAdminSessionBroker::WebAdminSessionBroker(const PrincipalRefreshHook &principalRefreshHook,
                                                  const CredentialVersionHook &credentialVersionHook,
                                                  const std::vector<unsigned char> &processKey,
                                                  const Clock &clock,
                                                  const Lifetime &lifetime) :
      principal_refresh_hook_(principalRefreshHook),
      credential_version_hook_(credentialVersionHook),
      clock_(clock),
      lifetime_(lifetime),
      process_key_(processKey),
      next_sequence_(0)
   {
      if (!principal_refresh_hook_ || !credential_version_hook_ ||
          process_key_.size() != ProcessKeyLength ||
          lifetime_.idle_timeout_seconds_ <= 0 || lifetime_.absolute_timeout_seconds_ <= 0)
      {
         throw 0;
      }
   }

   bool
   WebAdminSessionBroker::CreateSession(const String &phpSessionID,
                                        const std::shared_ptr<const Account> &authenticatedPrincipal,
                                        String &rawToken)
   {
      rawToken = "";

      try
      {
         WebAdminSessionPrincipal principal(authenticatedPrincipal);
         if (!principal.IsValid() || phpSessionID.IsEmpty())
            return false;

         String credentialVersion;
         if (!credential_version_hook_(principal, credentialVersion))
            return false;

         std::vector<unsigned char> randomToken(TokenLength);
         if (RAND_bytes(&randomToken[0], static_cast<int>(randomToken.size())) != 1)
            return false;

         rawToken = BytesToHex_(&randomToken[0], randomToken.size());
         SessionRecord record;
         record.principal_ = principal;
         record.session_binding_hmac_ = HmacHex_("session-binding", phpSessionID);
         record.credential_version_hmac_ = HmacHex_("credential-version", credentialVersion);
         record.created_at_ = GetCurrentTime_();
         record.last_used_at_ = record.created_at_;

         const String tokenHmac = HmacHex_("token", rawToken);
         {
            boost::lock_guard<boost::mutex> lock(mutex_);
            record.sequence_ = ++next_sequence_;
            sessions_[tokenHmac] = record;
         }

         return true;
      }
      catch (...)
      {
         rawToken = "";
         return false;
      }
   }

   std::shared_ptr<COMAuthentication>
   WebAdminSessionBroker::OpenSession(const String &rawToken, const String &phpSessionID)
   {
      if (rawToken.IsEmpty() || phpSessionID.IsEmpty())
         return std::shared_ptr<COMAuthentication>();

      try
      {
         const String tokenHmac = HmacHex_("token", rawToken);
         const String sessionBindingHmac = HmacHex_("session-binding", phpSessionID);
         SessionRecord record;

         {
            boost::lock_guard<boost::mutex> lock(mutex_);
            std::map<String, SessionRecord>::iterator found = sessions_.find(tokenHmac);
            if (found == sessions_.end())
               return std::shared_ptr<COMAuthentication>();

            const TimePoint now = GetCurrentTime_();
            if (!ConstantTimeEquals_(found->second.session_binding_hmac_, sessionBindingHmac))
               return std::shared_ptr<COMAuthentication>();

            if (IsExpired_(found->second, now))
            {
               sessions_.erase(found);
               return std::shared_ptr<COMAuthentication>();
            }

            record = found->second;
         }

         std::shared_ptr<const Account> currentPrincipal = principal_refresh_hook_(record.principal_);
         String credentialVersion;
         const bool principalAndCredentialAreCurrent =
            record.principal_.Matches(currentPrincipal) &&
            credential_version_hook_(record.principal_, credentialVersion) &&
            ConstantTimeEquals_(record.credential_version_hmac_, HmacHex_("credential-version", credentialVersion));

         {
            boost::lock_guard<boost::mutex> lock(mutex_);
            std::map<String, SessionRecord>::iterator found = sessions_.find(tokenHmac);
            if (found == sessions_.end() || found->second.sequence_ != record.sequence_)
               return std::shared_ptr<COMAuthentication>();

            const TimePoint now = GetCurrentTime_();
            if (!principalAndCredentialAreCurrent || IsExpired_(found->second, now))
            {
               sessions_.erase(found);
               return std::shared_ptr<COMAuthentication>();
            }

            found->second.last_used_at_ = now;
         }

         std::shared_ptr<COMAuthentication> authentication(new COMAuthentication);
         authentication->AttachAuthenticatedPrincipal(currentPrincipal);
         return authentication;
      }
      catch (...)
      {
         RevokeSession(rawToken, phpSessionID);
         return std::shared_ptr<COMAuthentication>();
      }
   }

   void
   WebAdminSessionBroker::RevokeSession(const String &rawToken, const String &phpSessionID)
   {
      if (rawToken.IsEmpty() || phpSessionID.IsEmpty())
         return;

      try
      {
         const String tokenHmac = HmacHex_("token", rawToken);
         const String sessionBindingHmac = HmacHex_("session-binding", phpSessionID);
         boost::lock_guard<boost::mutex> lock(mutex_);
         std::map<String, SessionRecord>::iterator found = sessions_.find(tokenHmac);
         if (found != sessions_.end() && ConstantTimeEquals_(found->second.session_binding_hmac_, sessionBindingHmac))
            sessions_.erase(found);
      }
      catch (...)
      {
      }
   }

   WebAdminSessionBroker::TimePoint
   WebAdminSessionBroker::GetCurrentTime_() const
   {
      if (clock_)
         return clock_();

      const time_t now = ::time(0);
      if (now == static_cast<time_t>(-1))
         throw 0;

      return static_cast<TimePoint>(now);
   }

   bool
   WebAdminSessionBroker::IsExpired_(const SessionRecord &record, TimePoint now) const
   {
      return now < record.created_at_ ||
             now < record.last_used_at_ ||
             now - record.created_at_ >= lifetime_.absolute_timeout_seconds_ ||
             now - record.last_used_at_ >= lifetime_.idle_timeout_seconds_;
   }

   String
   WebAdminSessionBroker::HmacHex_(const String &purpose, const String &value) const
   {
      const String input = purpose + ":" + value;
      unsigned char digest[EVP_MAX_MD_SIZE];
      unsigned int digestLength = 0;

      if (HMAC(EVP_sha256(),
               &process_key_[0],
               static_cast<int>(process_key_.size()),
               reinterpret_cast<const unsigned char *>(input.c_str()),
               static_cast<int>(input.GetLength()),
               digest,
               &digestLength) == 0)
      {
         throw 0;
      }

      return BytesToHex_(digest, digestLength);
   }

   std::vector<unsigned char>
   WebAdminSessionBroker::GenerateProcessKey_()
   {
      std::vector<unsigned char> processKey(ProcessKeyLength);
      if (RAND_bytes(&processKey[0], static_cast<int>(processKey.size())) != 1)
         throw 0;

      return processKey;
   }

   String
   WebAdminSessionBroker::BytesToHex_(const unsigned char *bytes, size_t byteCount)
   {
      String value;
      for (size_t i = 0; i < byteCount; i++)
      {
         String byte;
         byte.Format(_T("%02x"), bytes[i]);
         value += byte;
      }

      return value;
   }

   bool
   WebAdminSessionBroker::ConstantTimeEquals_(const String &left, const String &right)
   {
      if (left.GetLength() != right.GetLength())
         return false;

      unsigned char difference = 0;
      for (int i = 0; i < left.GetLength(); i++)
         difference |= static_cast<unsigned char>(left[i]) ^ static_cast<unsigned char>(right[i]);

      return difference == 0;
   }

   bool
   LegacyWebAdminCredentialAdmission::CreateSession(WebAdminSessionBroker &broker,
                                                     const String &phpSessionID,
                                                     const String &username,
                                                     const String &password,
                                                     String &rawToken)
   {
      rawToken = "";

      try
      {
         COMAuthentication authentication;
         return CreateSession(
            broker,
            phpSessionID,
            username,
            password,
            [&authentication](const String &suppliedUsername, const String &suppliedPassword)
            {
               return authentication.Authenticate(suppliedUsername, suppliedPassword);
            },
            rawToken);
      }
      catch (...)
      {
         rawToken = "";
         return false;
      }
   }

   bool
   LegacyWebAdminCredentialAdmission::CreateSession(WebAdminSessionBroker &broker,
                                                     const String &phpSessionID,
                                                     const String &username,
                                                     const String &password,
                                                     const AuthenticationHook &authenticationHook,
                                                     String &rawToken)
   {
      rawToken = "";

      try
      {
         std::shared_ptr<const Account> principal = authenticationHook(username, password);
         if (!principal || !broker.CreateSession(phpSessionID, principal, rawToken))
         {
            rawToken = "";
            return false;
         }

         return true;
      }
      catch (...)
      {
         rawToken = "";
         return false;
      }
   }

   HRESULT
   LegacyWebAdminApplicationFactory::Create(const std::shared_ptr<COMAuthentication> &authentication,
                                            IInterfaceApplication **application)
   {
      if (!application)
         return E_POINTER;

      *application = 0;
      if (!authentication || !authentication->GetIsAuthenticated())
         return E_ACCESSDENIED;

      CComObject<InterfaceApplication> *rawApplication = 0;
      HRESULT result = CComObject<InterfaceApplication>::CreateInstance(&rawApplication);
      if (FAILED(result) || !rawApplication)
         return FAILED(result) ? result : E_OUTOFMEMORY;

      rawApplication->AddRef();
      rawApplication->AttachAuthentication_(authentication);
      result = rawApplication->QueryInterface(IID_IInterfaceApplication, reinterpret_cast<void **>(application));
      rawApplication->Release();
      return result;
   }

   HRESULT
   LegacyWebAdminSessionRequest::CreateApplication(WebAdminSessionBroker &broker,
                                                   const String &rawToken,
                                                   const String &phpSessionID,
                                                   IInterfaceApplication **application)
   {
      if (!application)
         return E_POINTER;

      *application = 0;
      std::shared_ptr<COMAuthentication> authentication = broker.OpenSession(rawToken, phpSessionID);
      if (!authentication || !authentication->GetIsAuthenticated())
         return E_ACCESSDENIED;

      return LegacyWebAdminApplicationFactory::Create(authentication, application);
   }

   std::shared_ptr<WebAdminSessionBroker>
   LegacyWebAdminSessionBrokerFactory::Create(const WebAdminSessionBroker::Clock &clock,
                                              const WebAdminSessionBroker::Lifetime &lifetime)
   {
      return Create(std::shared_ptr<IWebAdminSessionPrincipalSource>(new PersistentWebAdminSessionPrincipalSource),
                    clock,
                    lifetime);
   }

   std::shared_ptr<WebAdminSessionBroker>
   LegacyWebAdminSessionBrokerFactory::Create(const std::shared_ptr<IWebAdminSessionPrincipalSource> &source,
                                              const WebAdminSessionBroker::Clock &clock,
                                              const WebAdminSessionBroker::Lifetime &lifetime)
   {
      if (!source)
         throw 0;

      return std::shared_ptr<WebAdminSessionBroker>(new WebAdminSessionBroker(
         [source](const WebAdminSessionPrincipal &principal) { return RefreshPrincipal_(source, principal); },
         [source](const WebAdminSessionPrincipal &principal, String &credentialVersion) { return GetCredentialVersion_(source, principal, credentialVersion); },
         clock,
         lifetime));
   }

   std::shared_ptr<WebAdminSessionBroker>
   LegacyWebAdminSessionBrokerFactory::Create(const std::shared_ptr<IWebAdminSessionPrincipalSource> &source,
                                              const std::vector<unsigned char> &processKey,
                                              const WebAdminSessionBroker::Clock &clock,
                                              const WebAdminSessionBroker::Lifetime &lifetime)
   {
      if (!source)
         throw 0;

      return std::shared_ptr<WebAdminSessionBroker>(new WebAdminSessionBroker(
         [source](const WebAdminSessionPrincipal &principal) { return RefreshPrincipal_(source, principal); },
         [source](const WebAdminSessionPrincipal &principal, String &credentialVersion) { return GetCredentialVersion_(source, principal, credentialVersion); },
         processKey,
         clock,
         lifetime));
   }

   bool
   LegacyWebAdminSessionBrokerFactory::IsAdministrator_(const WebAdminSessionPrincipal &principal)
   {
      return principal.GetAccountID() == 0 &&
             principal.GetDomainID() == 0 &&
             principal.GetAdminLevel() == Account::ServerAdmin &&
             principal.GetAddress().CompareNoCase(_T("Administrator")) == 0;
   }

   std::shared_ptr<const Account>
   LegacyWebAdminSessionBrokerFactory::RefreshPrincipal_(
      const std::shared_ptr<IWebAdminSessionPrincipalSource> &source,
      const WebAdminSessionPrincipal &principal)
   {
      if (IsAdministrator_(principal))
      {
         String credentialVersion;
         if (!source->GetAdministratorCredentialVersion(credentialVersion))
            return std::shared_ptr<const Account>();

         return std::shared_ptr<const Account>(new Account("Administrator", Account::ServerAdmin));
      }

      std::shared_ptr<const Account> account = source->ReadAccountByID(principal.GetAccountID());
      if (!account || !account->GetActive() || !source->IsDomainActive(account->GetDomainID()))
         return std::shared_ptr<const Account>();

      return account;
   }

   bool
   LegacyWebAdminSessionBrokerFactory::GetCredentialVersion_(
      const std::shared_ptr<IWebAdminSessionPrincipalSource> &source,
      const WebAdminSessionPrincipal &principal,
      String &credentialVersion)
   {
      credentialVersion = "";

      if (IsAdministrator_(principal))
         return source->GetAdministratorCredentialVersion(credentialVersion);

      std::shared_ptr<const Account> account = RefreshPrincipal_(source, principal);
      if (!principal.Matches(account) || account->GetIsAD())
         return false;

      return source->GetAccountCredentialVersion(principal.GetAccountID(), credentialVersion) &&
             !credentialVersion.IsEmpty();
   }

   LegacyWebAdminSessionService::LegacyWebAdminSessionService() :
      broker_(LegacyWebAdminSessionBrokerFactory::Create())
   {
   }

   LegacyWebAdminSessionService::LegacyWebAdminSessionService(
      const std::shared_ptr<WebAdminSessionBroker> &broker) :
      broker_(broker)
   {
   }

   bool
   LegacyWebAdminSessionService::CreateSession(const String &phpSessionID,
                                               const String &username,
                                               const String &password,
                                               String &rawToken)
   {
      rawToken = "";
      if (!broker_)
         return false;

      return LegacyWebAdminCredentialAdmission::CreateSession(
         *broker_, phpSessionID, username, password, rawToken);
   }

   bool
   LegacyWebAdminSessionService::CreateSession(
      const String &phpSessionID,
      const String &username,
      const String &password,
      const LegacyWebAdminCredentialAdmission::AuthenticationHook &authenticationHook,
      String &rawToken)
   {
      rawToken = "";
      if (!broker_)
         return false;

      return LegacyWebAdminCredentialAdmission::CreateSession(
         *broker_, phpSessionID, username, password, authenticationHook, rawToken);
   }

   HRESULT
   LegacyWebAdminSessionService::CreateApplication(const String &rawToken,
                                                   const String &phpSessionID,
                                                   IInterfaceApplication **application)
   {
      if (!application)
         return E_POINTER;

      *application = 0;
      if (!broker_)
         return E_ACCESSDENIED;

      return LegacyWebAdminSessionRequest::CreateApplication(
         *broker_, rawToken, phpSessionID, application);
   }

   void
   WebAdminSessionBrokerTester::Test()
   {
      TestCredentialAdmission_();
      TestLifecycleAndBinding_();
      TestIdleAndAbsoluteExpiry_();
      TestRevocation_();
      TestCredentialVersionDenial_();
      TestPrincipalRefreshDenial_();
      TestProcessRestartInvalidation_();
      TestAuthoritativeAccountAndDomainDenial_();
      TestAuthoritativeRoleMismatchDenial_();
      TestAuthoritativeCredentialVersionDenial_();
      TestApplicationFactory_();
      TestSessionRequestComposition_();
      TestSessionServiceOwnership_();
      TestSessionServiceNullBroker_();
      TestInstalledApplicationContract_();
   }

   std::shared_ptr<Account>
   WebAdminSessionBrokerTester::CreateAdministrator_()
   {
      std::shared_ptr<Account> account(new Account("Administrator", Account::ServerAdmin));
      account->SetID(0);
      account->SetDomainID(0);
      return account;
   }

   std::vector<unsigned char>
   WebAdminSessionBrokerTester::CreateProcessKey_(unsigned char value)
   {
      return std::vector<unsigned char>(ProcessKeyLength, value);
   }

   void
   WebAdminSessionBrokerTester::TestCredentialAdmission_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<Account> currentPrincipal = CreateAdministrator_();
      bool allowCredentialVersion = true;
      WebAdminSessionBroker broker(
         [&currentPrincipal](const WebAdminSessionPrincipal &) { return std::shared_ptr<const Account>(currentPrincipal); },
         [&allowCredentialVersion](const WebAdminSessionPrincipal &, String &version)
         {
            version = allowCredentialVersion ? "administrator-verifier" : "";
            return allowCredentialVersion;
         },
         CreateProcessKey_(11),
         [&now]() { return now; });

      int authenticationCalls = 0;
      String token = "stale-token";
      if (!LegacyWebAdminCredentialAdmission::CreateSession(
             broker,
             "php-session-admission",
             "Administrator",
             "supplied-password",
             [&authenticationCalls, &currentPrincipal](const String &username, const String &password)
             {
                ++authenticationCalls;
                if (username.Compare(_T("Administrator")) != 0 || password.Compare(_T("supplied-password")) != 0)
                   throw 0;

                return std::shared_ptr<const Account>(currentPrincipal);
             },
             token) ||
          authenticationCalls != 1 || token.GetLength() != 64)
      {
         throw 0;
      }

      std::shared_ptr<COMAuthentication> authentication = broker.OpenSession(token, "php-session-admission");
      if (!authentication || !authentication->GetIsServerAdmin() || authentication->GetAccountID() != 0)
         throw 0;

      authenticationCalls = 0;
      token = "stale-token";
      if (LegacyWebAdminCredentialAdmission::CreateSession(
             broker,
             "php-session-null",
             "Administrator",
             "rejected-password",
             [&authenticationCalls](const String &, const String &)
             {
                ++authenticationCalls;
                return std::shared_ptr<const Account>();
             },
             token) ||
          authenticationCalls != 1 || !token.IsEmpty())
      {
         throw 0;
      }

      authenticationCalls = 0;
      token = "stale-token";
      if (LegacyWebAdminCredentialAdmission::CreateSession(
             broker,
             "php-session-throw",
             "Administrator",
             "throwing-password",
             [&authenticationCalls](const String &, const String &) -> std::shared_ptr<const Account>
             {
                ++authenticationCalls;
                throw 0;
             },
             token) ||
          authenticationCalls != 1 || !token.IsEmpty())
      {
         throw 0;
      }

      authenticationCalls = 0;
      token = "stale-token";
      if (LegacyWebAdminCredentialAdmission::CreateSession(
             broker,
             "",
             "Administrator",
             "supplied-password",
             [&authenticationCalls, &currentPrincipal](const String &, const String &)
             {
                ++authenticationCalls;
                return std::shared_ptr<const Account>(currentPrincipal);
             },
             token) ||
          authenticationCalls != 1 || !token.IsEmpty())
      {
         throw 0;
      }

      allowCredentialVersion = false;
      authenticationCalls = 0;
      token = "stale-token";
      if (LegacyWebAdminCredentialAdmission::CreateSession(
             broker,
             "php-session-credential-denied",
             "Administrator",
             "supplied-password",
             [&authenticationCalls, &currentPrincipal](const String &, const String &)
             {
                ++authenticationCalls;
                return std::shared_ptr<const Account>(currentPrincipal);
             },
             token) ||
          authenticationCalls != 1 || !token.IsEmpty())
      {
         throw 0;
      }
   }

   void
   WebAdminSessionBrokerTester::TestLifecycleAndBinding_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<Account> currentPrincipal = CreateAdministrator_();
      String credentialVersion = "administrator-verifier";
      WebAdminSessionBroker broker(
         [&currentPrincipal](const WebAdminSessionPrincipal &) { return std::shared_ptr<const Account>(currentPrincipal); },
         [&credentialVersion](const WebAdminSessionPrincipal &, String &version) { version = credentialVersion; return true; },
         CreateProcessKey_(1),
         [&now]() { return now; });

      String token;
      if (!broker.CreateSession("php-session-a", currentPrincipal, token) || token.GetLength() != 64)
         throw 0;

      if (broker.OpenSession(token, "other-php-session"))
         throw 0;

      std::shared_ptr<COMAuthentication> authentication = broker.OpenSession(token, "php-session-a");
      if (!authentication || !authentication->GetIsServerAdmin() || authentication->GetAccountID() != 0)
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestIdleAndAbsoluteExpiry_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<Account> currentPrincipal = CreateAdministrator_();
      String credentialVersion = "administrator-verifier";
      WebAdminSessionBroker broker(
         [&currentPrincipal](const WebAdminSessionPrincipal &) { return std::shared_ptr<const Account>(currentPrincipal); },
         [&credentialVersion](const WebAdminSessionPrincipal &, String &version) { version = credentialVersion; return true; },
         CreateProcessKey_(2),
         [&now]() { return now; },
         WebAdminSessionBroker::Lifetime(10, 30));

      String idleToken;
      if (!broker.CreateSession("php-session-idle", currentPrincipal, idleToken))
         throw 0;

      now += 10;
      if (broker.OpenSession(idleToken, "php-session-idle"))
         throw 0;

      now = 2000;
      String absoluteToken;
      if (!broker.CreateSession("php-session-absolute", currentPrincipal, absoluteToken))
         throw 0;

      now += 9;
      if (!broker.OpenSession(absoluteToken, "php-session-absolute"))
         throw 0;

      now = 2030;
      if (broker.OpenSession(absoluteToken, "php-session-absolute"))
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestRevocation_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<Account> currentPrincipal = CreateAdministrator_();
      String credentialVersion = "administrator-verifier";
      WebAdminSessionBroker broker(
         [&currentPrincipal](const WebAdminSessionPrincipal &) { return std::shared_ptr<const Account>(currentPrincipal); },
         [&credentialVersion](const WebAdminSessionPrincipal &, String &version) { version = credentialVersion; return true; },
         CreateProcessKey_(3),
         [&now]() { return now; });

      String token;
      if (!broker.CreateSession("php-session-revoke", currentPrincipal, token))
         throw 0;

      broker.RevokeSession(token, "wrong-php-session");
      if (!broker.OpenSession(token, "php-session-revoke"))
         throw 0;

      broker.RevokeSession(token, "php-session-revoke");
      if (broker.OpenSession(token, "php-session-revoke"))
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestCredentialVersionDenial_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<Account> currentPrincipal = CreateAdministrator_();
      String credentialVersion = "old-administrator-verifier";
      WebAdminSessionBroker broker(
         [&currentPrincipal](const WebAdminSessionPrincipal &) { return std::shared_ptr<const Account>(currentPrincipal); },
         [&credentialVersion](const WebAdminSessionPrincipal &, String &version) { version = credentialVersion; return true; },
         CreateProcessKey_(4),
         [&now]() { return now; });

      String token;
      if (!broker.CreateSession("php-session-credential", currentPrincipal, token))
         throw 0;

      credentialVersion = "new-administrator-verifier";
      if (broker.OpenSession(token, "php-session-credential"))
         throw 0;

      credentialVersion = "old-administrator-verifier";
      if (broker.OpenSession(token, "php-session-credential"))
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestPrincipalRefreshDenial_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<Account> currentPrincipal = CreateAdministrator_();
      String credentialVersion = "administrator-verifier";
      WebAdminSessionBroker broker(
         [&currentPrincipal](const WebAdminSessionPrincipal &) { return std::shared_ptr<const Account>(currentPrincipal); },
         [&credentialVersion](const WebAdminSessionPrincipal &, String &version) { version = credentialVersion; return true; },
         CreateProcessKey_(5),
         [&now]() { return now; });

      String token;
      if (!broker.CreateSession("php-session-principal", currentPrincipal, token))
         throw 0;

      currentPrincipal.reset();
      if (broker.OpenSession(token, "php-session-principal"))
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestProcessRestartInvalidation_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<Account> currentPrincipal = CreateAdministrator_();
      String credentialVersion = "administrator-verifier";
      WebAdminSessionBroker firstProcess(
         [&currentPrincipal](const WebAdminSessionPrincipal &) { return std::shared_ptr<const Account>(currentPrincipal); },
         [&credentialVersion](const WebAdminSessionPrincipal &, String &version) { version = credentialVersion; return true; },
         CreateProcessKey_(6),
         [&now]() { return now; });

      String token;
      if (!firstProcess.CreateSession("php-session-restart", currentPrincipal, token))
         throw 0;

      WebAdminSessionBroker restartedProcess(
         [&currentPrincipal](const WebAdminSessionPrincipal &) { return std::shared_ptr<const Account>(currentPrincipal); },
         [&credentialVersion](const WebAdminSessionPrincipal &, String &version) { version = credentialVersion; return true; },
         CreateProcessKey_(7),
         [&now]() { return now; });

      if (restartedProcess.OpenSession(token, "php-session-restart"))
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestAuthoritativeAccountAndDomainDenial_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<TestWebAdminSessionPrincipalSource> source(new TestWebAdminSessionPrincipalSource);
      std::shared_ptr<Account> account = CreateTestDomainAdministrator();
      source->accounts_[account->GetID()] = account;
      source->domain_activity_[account->GetDomainID()] = true;
      source->account_credential_versions_[account->GetID()] = "account-verifier";

      std::shared_ptr<WebAdminSessionBroker> broker = LegacyWebAdminSessionBrokerFactory::Create(
         source,
         CreateProcessKey_(8),
         [&now]() { return now; });

      String inactiveToken;
      if (!broker->CreateSession("php-session-inactive", account, inactiveToken))
         throw 0;

      account->SetActive(false);
      if (broker->OpenSession(inactiveToken, "php-session-inactive"))
         throw 0;

      account->SetActive(true);
      String deletedToken;
      if (!broker->CreateSession("php-session-deleted", account, deletedToken))
         throw 0;

      source->accounts_.erase(account->GetID());
      if (broker->OpenSession(deletedToken, "php-session-deleted"))
         throw 0;

      source->accounts_[account->GetID()] = account;
      String inactiveDomainToken;
      if (!broker->CreateSession("php-session-domain", account, inactiveDomainToken))
         throw 0;

      source->domain_activity_[account->GetDomainID()] = false;
      if (broker->OpenSession(inactiveDomainToken, "php-session-domain"))
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestAuthoritativeRoleMismatchDenial_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<TestWebAdminSessionPrincipalSource> source(new TestWebAdminSessionPrincipalSource);
      std::shared_ptr<Account> account = CreateTestDomainAdministrator();
      source->accounts_[account->GetID()] = account;
      source->domain_activity_[account->GetDomainID()] = true;
      source->account_credential_versions_[account->GetID()] = "account-verifier";

      std::shared_ptr<WebAdminSessionBroker> broker = LegacyWebAdminSessionBrokerFactory::Create(
         source,
         CreateProcessKey_(9),
         [&now]() { return now; });

      String roleToken;
      if (!broker->CreateSession("php-session-role", account, roleToken))
         throw 0;

      account->SetAdminLevel(Account::NormalUser);
      if (broker->OpenSession(roleToken, "php-session-role"))
         throw 0;

      account->SetAdminLevel(Account::DomainAdmin);
      String domainToken;
      if (!broker->CreateSession("php-session-domain-change", account, domainToken))
         throw 0;

      account->SetDomainID(8);
      source->domain_activity_[8] = true;
      if (broker->OpenSession(domainToken, "php-session-domain-change"))
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestAuthoritativeCredentialVersionDenial_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<TestWebAdminSessionPrincipalSource> source(new TestWebAdminSessionPrincipalSource);
      std::shared_ptr<Account> account = CreateTestDomainAdministrator();
      source->accounts_[account->GetID()] = account;
      source->domain_activity_[account->GetDomainID()] = true;
      source->account_credential_versions_[account->GetID()] = "account-verifier-one";
      source->administrator_credential_version_ = "administrator-verifier-one";

      std::shared_ptr<WebAdminSessionBroker> broker = LegacyWebAdminSessionBrokerFactory::Create(
         source,
         CreateProcessKey_(10),
         [&now]() { return now; });

      String accountToken;
      if (!broker->CreateSession("php-session-account-verifier", account, accountToken))
         throw 0;

      source->account_credential_versions_[account->GetID()] = "account-verifier-two";
      if (broker->OpenSession(accountToken, "php-session-account-verifier"))
         throw 0;

      std::shared_ptr<Account> administrator = CreateAdministrator_();
      String administratorToken;
      if (!broker->CreateSession("php-session-administrator-verifier", administrator, administratorToken))
         throw 0;

      source->administrator_credential_version_ = "administrator-verifier-two";
      if (broker->OpenSession(administratorToken, "php-session-administrator-verifier"))
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestApplicationFactory_()
   {
      IInterfaceApplication *application = 0;
      if (LegacyWebAdminApplicationFactory::Create(std::shared_ptr<COMAuthentication>(), &application) != E_ACCESSDENIED || application)
         throw 0;

      std::shared_ptr<COMAuthentication> anonymousAuthentication(new COMAuthentication);
      if (LegacyWebAdminApplicationFactory::Create(anonymousAuthentication, &application) != E_ACCESSDENIED || application)
         throw 0;

      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<Account> currentPrincipal = CreateAdministrator_();
      String credentialVersion = "administrator-verifier";
      WebAdminSessionBroker broker(
         [&currentPrincipal](const WebAdminSessionPrincipal &) { return std::shared_ptr<const Account>(currentPrincipal); },
         [&credentialVersion](const WebAdminSessionPrincipal &, String &version) { version = credentialVersion; return true; },
         CreateProcessKey_(8),
         [&now]() { return now; });

      String token;
      if (!broker.CreateSession("php-session-application", currentPrincipal, token))
         throw 0;

      std::shared_ptr<COMAuthentication> authentication = broker.OpenSession(token, "php-session-application");
      if (!authentication || FAILED(LegacyWebAdminApplicationFactory::Create(authentication, &application)) || !application)
         throw 0;

      eServerState state = hStateUnknown;
      const HRESULT result = application->get_ServerState(&state);
      application->Release();

      if (FAILED(result))
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestSessionRequestComposition_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<Account> currentPrincipal = CreateAdministrator_();
      String credentialVersion = "administrator-verifier";
      WebAdminSessionBroker broker(
         [&currentPrincipal](const WebAdminSessionPrincipal &) { return std::shared_ptr<const Account>(currentPrincipal); },
         [&credentialVersion](const WebAdminSessionPrincipal &, String &version) { version = credentialVersion; return true; },
         CreateProcessKey_(12),
         [&now]() { return now; },
         WebAdminSessionBroker::Lifetime(10, 30));

      String token;
      if (!broker.CreateSession("php-session-request", currentPrincipal, token))
         throw 0;

      if (LegacyWebAdminSessionRequest::CreateApplication(
             broker, token, "php-session-request", 0) != E_POINTER)
      {
         throw 0;
      }

      IInterfaceApplication *application = reinterpret_cast<IInterfaceApplication *>(1);
      if (LegacyWebAdminSessionRequest::CreateApplication(
             broker, "", "php-session-request", &application) != E_ACCESSDENIED || application)
      {
         throw 0;
      }

      application = reinterpret_cast<IInterfaceApplication *>(1);
      if (LegacyWebAdminSessionRequest::CreateApplication(
             broker, "not-issued-token", "php-session-request", &application) != E_ACCESSDENIED || application)
      {
         throw 0;
      }

      application = reinterpret_cast<IInterfaceApplication *>(1);
      if (LegacyWebAdminSessionRequest::CreateApplication(
             broker, token, "wrong-php-session", &application) != E_ACCESSDENIED || application)
      {
         throw 0;
      }

      if (FAILED(LegacyWebAdminSessionRequest::CreateApplication(
             broker, token, "php-session-request", &application)) || !application)
      {
         throw 0;
      }

      eServerState state = hStateUnknown;
      const HRESULT protectedAccessResult = application->get_ServerState(&state);
      application->Release();
      application = 0;
      if (FAILED(protectedAccessResult))
         throw 0;

      String expiredToken;
      if (!broker.CreateSession("php-session-expired", currentPrincipal, expiredToken))
         throw 0;

      now += 10;
      application = reinterpret_cast<IInterfaceApplication *>(1);
      if (LegacyWebAdminSessionRequest::CreateApplication(
             broker, expiredToken, "php-session-expired", &application) != E_ACCESSDENIED || application)
      {
         throw 0;
      }

      String revokedToken;
      if (!broker.CreateSession("php-session-revoked", currentPrincipal, revokedToken))
         throw 0;

      broker.RevokeSession(revokedToken, "php-session-revoked");
      application = reinterpret_cast<IInterfaceApplication *>(1);
      if (LegacyWebAdminSessionRequest::CreateApplication(
             broker, revokedToken, "php-session-revoked", &application) != E_ACCESSDENIED || application)
      {
         throw 0;
      }
   }

   void
   WebAdminSessionBrokerTester::TestSessionServiceOwnership_()
   {
      WebAdminSessionBroker::TimePoint now = 1000;
      std::shared_ptr<Account> administrator = CreateAdministrator_();
      std::shared_ptr<TestWebAdminSessionPrincipalSource> source(new TestWebAdminSessionPrincipalSource);
      source->administrator_credential_version_ = "administrator-verifier";

      LegacyWebAdminSessionService owner(
         LegacyWebAdminSessionBrokerFactory::Create(source, CreateProcessKey_(13), [&now]() { return now; }));
      LegacyWebAdminSessionService restartedOwner(
         LegacyWebAdminSessionBrokerFactory::Create(source, CreateProcessKey_(14), [&now]() { return now; }));

      String token;
      if (!owner.CreateSession(
             "php-session-owner",
             "Administrator",
             "supplied-password",
             [&administrator](const String &username, const String &password)
             {
                if (username.Compare(_T("Administrator")) != 0 || password.Compare(_T("supplied-password")) != 0)
                   return std::shared_ptr<const Account>();

                return std::shared_ptr<const Account>(administrator);
             },
             token))
      {
         throw 0;
      }

      IInterfaceApplication *application = reinterpret_cast<IInterfaceApplication *>(1);
      if (restartedOwner.CreateApplication(token, "php-session-owner", &application) != E_ACCESSDENIED || application)
         throw 0;

      if (FAILED(owner.CreateApplication(token, "php-session-owner", &application)) || !application)
         throw 0;

      eServerState state = hStateUnknown;
      const HRESULT protectedAccessResult = application->get_ServerState(&state);
      application->Release();
      if (FAILED(protectedAccessResult))
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestSessionServiceNullBroker_()
   {
      std::shared_ptr<WebAdminSessionBroker> nullBroker;
      LegacyWebAdminSessionService owner(nullBroker);

      int authenticationCalls = 0;
      String token = "stale-token";
      if (owner.CreateSession(
             "php-session-null-owner",
             "Administrator",
             "supplied-password",
             [&authenticationCalls](const String &, const String &)
             {
                ++authenticationCalls;
                return std::shared_ptr<const Account>();
             },
             token) ||
          authenticationCalls != 0 || !token.IsEmpty())
      {
         throw 0;
      }

      if (owner.CreateApplication("token", "php-session-null-owner", 0) != E_POINTER)
         throw 0;

      IInterfaceApplication *application = reinterpret_cast<IInterfaceApplication *>(1);
      if (owner.CreateApplication("token", "php-session-null-owner", &application) != E_ACCESSDENIED || application)
         throw 0;
   }

   void
   WebAdminSessionBrokerTester::TestInstalledApplicationContract_()
   {
      if (!InlineIsEqualGUID(IID_IInterfaceApplication, ExpectedApplicationInterfaceID) ||
          !InlineIsEqualGUID(CLSID_Application, ExpectedApplicationClassID))
      {
         throw 0;
      }

      typedef HRESULT (STDMETHODCALLTYPE IInterfaceApplication::*AuthenticateSignature)(BSTR, BSTR, IInterfaceAccount **);
      AuthenticateSignature authenticate = &IInterfaceApplication::Authenticate;
      if (!authenticate)
         throw 0;

      CComObject<InterfaceApplication> *rawApplication = 0;
      if (FAILED(CComObject<InterfaceApplication>::CreateInstance(&rawApplication)) || !rawApplication)
         throw 0;

      IInterfaceApplication *application = 0;
      if (FAILED(rawApplication->QueryInterface(IID_IInterfaceApplication, reinterpret_cast<void **>(&application))) || !application)
         throw 0;

      LPOLESTR memberName = const_cast<LPOLESTR>(L"Authenticate");
      DISPID dispatchID = DISPID_UNKNOWN;
      const HRESULT result = application->GetIDsOfNames(IID_NULL, &memberName, 1, LOCALE_INVARIANT, &dispatchID);
      eServerState state = hStateUnknown;
      const HRESULT directActivationResult = application->get_ServerState(&state);
      application->Release();

      if (FAILED(result) || dispatchID != 17 || SUCCEEDED(directActivationResult))
         throw 0;
   }
}
