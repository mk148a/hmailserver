// Copyright (c) 2026 hMailServer .NET 10 rewrite contributors.
// http://www.hmailserver.com

#include "stdafx.h"

#include "WebAdminSessionBroker.h"
#include "COMAuthentication.h"
#include "InterfaceApplication.h"

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

   void
   WebAdminSessionBrokerTester::Test()
   {
      TestLifecycleAndBinding_();
      TestIdleAndAbsoluteExpiry_();
      TestRevocation_();
      TestCredentialVersionDenial_();
      TestPrincipalRefreshDenial_();
      TestProcessRestartInvalidation_();
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
      application->Release();

      if (FAILED(result) || dispatchID != 17)
         throw 0;
   }
}
