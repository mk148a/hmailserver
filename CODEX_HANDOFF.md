# CODEX_HANDOFF.md

Bu dosya yeni bir Codex thread'inin hMailServer .NET 10 rewrite calismasina kaldigi yerden devam edebilmesi icin hazirlandi.

## Projenin Amaci

hMailServer icin Windows uyumlu, side-by-side .NET 10 tabanli yeni server cekirdegi gelistiriliyor. Hedef; legacy C++/ATL hMailServer davranisi, mevcut SQL Server verisi, data directory duzeni, COM/API sozlesmeleri, Administrator uyumlulugu ve VBScript/JScript event davranislari korunarak modern protokol, arama, teslimat, spam/virus ve operasyon altyapisina gecmek.

Legacy C++ server production referansi olmaya devam ediyor. .NET 10 agaci production parity saglanana kadar kontrollu test/uyumluluk hattidir.

## Mevcut Tamamlanan Buyuk Isler

- .NET 10 solution skeleton, servis hostu, local build/test wrapper'lari ve on kosul kontrol scriptleri eklendi.
- Phase 0 legacy C++ stabilizasyonlari tamamlandi: ClamAV INSTREAM raw network-order chunk framing, synchronous timeout cancellation, SpamAssassin partial/invalid response korunumu, MSBuild 17 discovery.
- SQL Server Full-Text Search icin additive migration, search document/queue tablolari, backfill processor, IMAP SEARCH/SORT planner ve SQL-backed metadata arama katmani eklendi.
- IMAP tarafinda LOGIN/AUTHENTICATE PLAIN, SELECT/EXAMINE, nested/public folder, ACL, QUOTA, SEARCH/SORT, FETCH, STORE, COPY/MOVE, APPEND, EXPUNGE, IDLE ve recent flag lifecycle icin buyuk parity dilimleri tamamlandi.
- SMTP tarafinda listener/session skeleton, STARTTLS, AUTH PLAIN/LOGIN, MAIL/RCPT/DATA staging, local/route recipient validation, durable queue persistence, global/account rule islemleri, delivery queue lease/load/dispatch, local delivery, remote SMTP sender, retry/backoff, bounce ve delivery status gozlemlenebilirligi eklendi.
- POP3 tarafinda USER/PASS, CAPA, STAT/LIST/UIDL/RETR/TOP/DELE/RSET/NOOP/QUIT, mailbox lock, implicit TLS ve SQL/data-directory mailbox store eklendi.
- External POP3 fetch icin SQL lease/UID store, POP3 network session, CAPA/STLS probing, UIDL/RETR/DELE/QUIT akis, recipient resolution, yeni/bilinen UIDL ve duplicate sequence baskilama, persisted known-UID duplicate toleransi, legacy `X-hMailServer-ExternalAccount` basligi, spam/AV entegrasyonu ve `OnExternalAccountDownload` script hook'u eklendi; fetch-account script facade'i `NextDownloadTime`/`IsLocked` alanlarini da tasiyor.
- Modern security slice'lari eklendi: ClamAV, SpamAssassin, spam policy, attachment blocking, DNSBL, reverse DNS/PTR, sender-domain MX, greylisting, SURBL, failed-logon auto-ban ve davranis degistirmeyen/disabled-by-default SPF evaluator + SMTP policy temeli.
- Legacy script/event parity buyuk olcude ilerledi: `OnClientConnect`, `OnClientValidatePassword`, `OnClientLogon`, `OnHELO`, `OnRecipientUnknown`, `OnSMTPData`, `OnAcceptMessage`, `OnTooManyInvalidCommands`, delivery eventleri, `OnDeliveryFailed`, `OnError`, rule `ScriptFunction`, mesaj/recipient/attachment facade'leri, client `Authenticated`/`EncryptedConnection` alias'lari ve account-rule `Message.Copy(folderId)`.
- Iki guvenlik raporu 21 benzersiz kayitta birlestirildi. `4dd984156` ile bos administrator hash'i fail-closed yapildi; legacy JScript event literal'lari, rule `ScriptFunction` runtime/COM yetki siniri, SMTP `ETRN`, WebAdmin session fixation ve CSRF rastgeleligi sertlestirildi. ClamAV framing duzeltmesinin daha once `d8942bc12` ile kapandigi dogrulandi; yeniden uretilemeyen iki VBScript iddiasi regresyon testleriyle izleniyor.

## Production-Ready Seviyesi

Durum: production-ready degil. Proje ciddi bir parity seviyesine geldi, fakat halen side-by-side rewrite/test hatti olarak ele alinmali.

Ana nedenler:

- COM/Admin yuzeyi ve legacy object model henuz tam degil.
- SPF evaluator, disabled-by-default SMTP policy boundary, explicit SPF-pass greylisting bypass boundary, DKIM parser/canonicalization/body-hash/header-crypto/DNS lookup/message-level verification + disabled-by-default policy boundary, DKIM pass-domain result surface, disabled-by-default DMARC evaluation/SMTP policy boundary, offline local-PSL organizational-domain resolver ve pinned/paketlenmis PSL lifecycle var; DKIM signing/setter/Admin mutation wiring, DMARC enforcement/Admin policy wiring ve daha sonra SPF/greylisting Administrator/COM setting parity eksik.
- Backup engine ve `OnBackupCompleted` / `OnBackupFailed` eventleri beklemede.
- In-place upgrade runner, mandatory backup/rollback akis dokumani ve operasyonel servis install/uninstall paketi tamamlanmadi.
- Buyuk olcekli performance/soak kabul testleri henuz production gate olarak tamamlanmadi.

## Kalan Kritik Backlog

- Full legacy script object model parity.
- Backup engine tasarimi ve backup completed/failed eventlerinin gercek engine uzerinden baglanmasi.
- Active Directory auth, master user ve daha derin account facade collections/methods.
- DKIM signing/setter/Admin mutation wiring ve DMARC enforcement/Admin policy wiring; daha sonra SPF/greylisting Administrator/COM setting parity.
- COM/API compatibility: mevcut GUID/ProgID/DISPID/type library sozlesmelerinin tam korunmasi ve Administrator-visible nesnelerin tamamlanmasi.
- Migration/operations: in-place upgrade runner, mandatory backup checks, rollback-from-backup dokumani, orphan cleanup, health/metrics/logging, Windows Service install/uninstall.
- SQL Server FTS integration testleri ve production acceptance: 100k mailbox SEARCH/SORT, 1k IMAP connection, SMTP queue latency, memory/handle leak soak.
- External fetch edge-case parity.
- Acik P1 guvenlik maddeleri: WebAdmin'in kalan mutation/delete POST/token gecisi, AV/SpamAssassin test endpoint destination egress politikasi, plaintext PHP session parolasi, COM mutation ownership denetimi, external-fetch egress/SSRF politikasi ve custom antivirus komutunun structured executable/arguments modeline gecisi. AV/SpamAssassin AJAX test action'lari POST-only hale getirildi; bu sadece URL token/GET tetikleme kismini kapatir.

## Current Next Slice

Backlog'daki siradaki ana dilim: legacy Administrator parity'yi ilerletmek. Siradaki bounded SMTP guardrail slice read-only `Settings.MaxMessageSize`, `MaxSMTPRecipientsInBatch`, `DisconnectInvalidClients` ve `MaxNumberOfInvalidCommands` getter'larini existing `maxmessagesize`/`maxsmtprecipientsinbatch`/`disconnectinvalidclients`/`maximumincorrectcommands` `hm_settings.settinginteger` satirlarindan acmali; kurulu vtable/DISPID ve `VARIANT_BOOL` marshaling'i korumali ve setter, live SMTP session/listener policy degisikligi veya daha genis Settings/Admin davranisi eklememeli.

Son tamamlanan kucuk dilimler:

- Authenticated `Application -> Settings` snapshot/store'u legacy retry getter'larini read-only acacak sekilde genisletildi: `SMTPNoOfTries` `smtpnoofretries`, `SMTPMinutesBetweenTry` ise `smtpminutesbetweenretries` satirindan geliyor; obsolete `smtpnooftries` decoy satiri SQL store'dan acikca dislandi. Setter'lar `E_NOTIMPL`, delivery retry scheduling degisikligi kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 25/25, full Net10 testleri 720/720 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u legacy protocol-enabled getter'larini read-only acacak sekilde genisletildi: `ServiceSMTP`, `ServicePOP3` ve `ServiceIMAP` mevcut `protocolsmtp`/`protocolpop3`/`protocolimap` `hm_settings.settinginteger` satirlarindan geliyor. `VARIANT_BOOL` getter/setter metadata'si contract testinde kilitlendi; setter'lar `E_NOTIMPL`, live listener enable/disable ve service state kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 25/25, full Net10 testleri 720/720 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u legacy integer limit getter'larini read-only acacak sekilde genisletildi: `MaxSMTPConnections`, `MaxPOP3Connections`, `MaxIMAPConnections` ve `MaxDeliveryThreads` mevcut legacy `hm_settings.settinginteger` satirlarindan geliyor. Setter'lar `E_NOTIMPL`; live listener/delivery-worker reconfiguration ve service state kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 24/24, full Net10 testleri 719/719 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` yolu legacy `HostName`, `WelcomeSMTP`, `WelcomePOP3` ve `WelcomeIMAP` getter'larini existing `hm_settings.settingstring` satirlarindan read-only acacak sekilde genisletildi. Installed vtable/DISPID ve direct-activation `E_ACCESSDENIED` siniri korundu; setter'lar `E_NOTIMPL`, listener reconfiguration, service state, secret settings ve genis Settings mutation kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 24/24, full Net10 testleri 719/719 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Domains` koleksiyonu legacy `Domains.Names` getter'ini read-only acacak sekilde genisletildi; loaded domain snapshot'larindan `id\tname\tactive\r\n` formatini uretiyor. `Refresh`, collection mutation ve database reload kapsam disi kaldi. Dar domain contract/integration filtresi 6/6, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Account` adapter'i non-secret Active Directory scalar getter'larini read-only acacak sekilde genisletildi: `IsAD`, `ADDomain`, ve `ADUsername` mevcut `hm_accounts.accountisad`/`accountaddomain`/`accountadusername` kolonlarindan geliyor. Setter, AD auth, password/security-sensitive alanlar ve account mutation kapsam disi kaldi. Dar account contract/store/integration filtresi 15/15, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Account` adapter'i legacy `Account.LastLogonTime` getter'ini mevcut `hm_accounts.accountlastlogontime` degerinden read-only acacak sekilde genisletildi. Login-time update, authentication davranisi ve account mutation kapsam disi kaldi. Dar account contract/store/integration filtresi 15/15, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Account` adapter'i legacy `Account.QuotaUsed` getter'ini secili account'un `hm_messages.messagesize` byte toplami ve `accountmaxsize` MB limitinden legacy integer yuzde/truncation davranisiyla read-only acacak sekilde genisletildi. Quota enforcement, account mutation ve filesystem/mailbox scan davranisi kapsam disi kaldi. Dar account contract/store/integration filtresi 15/15, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Account` adapter'i legacy `Account.Size` getter'ini secili account'un `hm_messages.messagesize` byte toplamindan 3 basamakli MB float degeri olarak read-only acacak sekilde genisletildi. Quota enforcement, account mutation ve filesystem/mailbox scan davranisi kapsam disi kaldi. Dar account contract/store/integration filtresi 15/15, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i legacy MSSQL `Domain.Size` getter'ini read-only aggregate olarak acacak sekilde genisletildi; SQL shape `hm_messages.messagesize` toplamindan MB'a truncate ediyor ve legacy `messageaccountid IN (SELECT accountdomainid ...)` davranisi dar store/integration testleriyle sabitlendi. Quota enforcement, account mutation ve filesystem/mailbox scan davranisi kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i legacy `Domain.AllocatedSize` getter'ini secili domain'in `hm_accounts.accountmaxsize` toplamindan gelen read-only aggregate olarak acacak sekilde genisletildi. `Domain.Size`, quota enforcement, account mutation ve filesystem/mailbox scan davranisi kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i mevcut `hm_domains.domainaddomain` degerini read-only `Domain.ADDomainName` getter'i olarak acacak sekilde genisletildi. Setter, AD synchronization ve authentication davranisi kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i mevcut `hm_domains.domainantispamoptions` greylisting flag'ini read-only `Domain.AntiSpamEnableGreylisting` getter'i olarak acacak sekilde genisletildi. Setter, SMTP policy davranisi ve runtime greylisting degisikligi kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i mevcut `hm_domains` signature ayarlarini read-only COM getter'lariyla acacak sekilde genisletildi: `SignatureEnabled`, `SignatureMethod`, `SignaturePlainText`, `SignatureHTML`, `AddSignaturesToReplies` ve `AddSignaturesToLocalMail` mevcut legacy kolonlardan geliyor. Setter'lar, message mutation, SMTP signature uygulama davranisi ve migration kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i mevcut `hm_domains` DKIM ayarlarini read-only COM getter'lariyla acacak sekilde genisletildi: `DKIMSignEnabled`, selector, private-key file path, header/body canonicalization, signing algorithm ve alias-signing flag'i legacy `domainantispamoptions` bitleri ile `domaindkimselector`/`domaindkimprivatekeyfile` kolonlarindan geliyor. Setter'lar, signing, private-key file icerigi okuma ve SMTP policy davranisi kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Resmi Public Suffix List snapshot'i `2026-06-24_06-18-09_UTC` / `18ecca5d54471f21918798da451dd8d03a18f3c7` commit'ine ve `8208f0c918c6cb3ab77b484635fc8683c94cbfff818be81950908e881a5f8be2` SHA-256 degerine pinlendi. Snapshot + deterministic metadata Service build/publish ciktilarina kopyalaniyor; offline build gate header/hash/byte length'i dogruluyor ve maintainer-only refresh komutu expected commit + hash olmadan calismiyor. Runtime/SMTP download eklenmedi. Dar DMARC/PSL filtresi 32/32, prereq temiz, Net10 build 0 uyari/0 hata, publish hash smoke testi basarili ve full Net10 testleri 708/708 gecti.
- DMARC organizational-domain/public-suffix boundary eklendi: `IDmarcOrganizationalDomainResolver` arkasinda Nager.PublicSuffix 3.8.0 ile local PSL dosyasi lazy/thread-safe tek sefer yukleniyor; `HMAILSERVER_DMARC_PUBLIC_SUFFIX_LIST`/`AntiSpam:Dmarc:PublicSuffixListPath` veya executable yanindaki `public_suffix_list.dat` kullaniliyor. Valid liste parent-record fallback, `sp=` secimi ve relaxed sibling alignment'i aciyor; wildcard/exception kurallari testli, missing/invalid/unreadable liste exact-domain DMARC'a fail-open kaliyor ve SMTP path'inde online download yok. Dar DMARC filtresi 30/30, prereq temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 706/706 gecti.
- Disabled-by-default SMTP DMARC policy/input boundary eklendi: `HMAILSERVER_DMARC_ENABLED=false` varsayilani SMTP davranisini degistirmiyor; explicit acikken RFC5322.From domain'i cikariliyor, SPF sonucu ve DKIM pass signing-domain listesi DMARC evaluator'a veri olarak tasiniyor, malformed input/DNS/runtime hatalari fail-open kaliyor ve yalniz `HMAILSERVER_DMARC_MARK_FAILURES_AS_SPAM=true` ile policy failure mevcut spam-flag yoluna map edilebiliyor. Direct SMTP reject/quarantine, organizational-domain/public-suffix discovery, signing ve Administrator/COM setting plumbing baglanmadi. Dar `SmtpDmarcPolicyTests` + `SmtpDkimPolicyTests` + receiver filtresi 38/38, prereq kontrolu temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 698/698 gecti.
- DMARC evaluation-only foundation eklendi: injected TXT resolver arkasinda DMARC record/result modeli, deterministic `p`/`sp`/`aspf`/`adkim`/`pct` parser'i, exact-domain + optional organizational-domain fallback lookup'u, strict/relaxed SPF ve DKIM alignment kontrolleri, subdomain policy secimi, temp DNS failure ile malformed/duplicate record sonuc map'leri kapatildi. SPF/DKIM sonuclari yalniz veri olarak tuketiliyor; SMTP reject/quarantine, spam scoring, signing ve Administrator/COM setting plumbing baglanmadi. Dar `DmarcEvaluationTests` 12/12, prereq kontrolu temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 688/688 gecti.
- Disabled-by-default DKIM SMTP policy boundary eklendi: `HMAILSERVER_DKIM_ENABLED=false` varsayilani SMTP davranisini degistirmiyor; explicit acikken message-level verifier sonucunu tuketiyor, legacy spam-test subset'ine uygun sekilde yalniz `PermFail` icin configured failure score ile spam flag/status uretip `Pass` sonucunu pass sinyali olarak tasiyor, `Neutral`/`TempFail` fail-open kaliyor ve dogrudan SMTP reject eklenmiyor. Signing, DMARC ve Administrator/COM setting plumbing baglanmadi. Dar `SmtpDkimPolicyTests` 5/5 ve receiver DKIM filtresi 2/2 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 676/676 gecti.
- WebAdmin AV/SpamAssassin AJAX test action'lari GET + URL token yerine `application/x-www-form-urlencoded` POST body kullanacak sekilde daraltildi; `background_ajax_virustest.php` ve `background_ajax_spamassassintest.php` POST-only oldu ve mevcut server-admin/CSRF kontrolleri korunuyor. Bu SEC-14/15/16 icin kismi hardening; egress/private-network allowlist politikasi ve diger legacy mutation linkleri P1 olarak kaliyor.
- DKIM message-level verifier cekirdegi eklendi: raw/header-body mesaj girdilerinden `DKIM-Signature` field'lari cikariliyor, legacy gibi ilk 5 imza degerlendiriliyor, parse edilemeyen imzalar `Neutral` olarak atlanip devam ediliyor, herhangi bir imza body-hash + header-signature + DNS key zincirinden `Pass` alirsa hemen `Pass` donuluyor, aksi halde legacy dongudeki son non-pass sonuc korunuyor. SMTP reject, policy score, signing ve Administrator ayarlari baglanmadi. Dar DKIM filtresi 37/37, prereq kontrolu temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 669/669 gecti.
- DKIM DNS/public-key lookup cekirdegi eklendi: `{selector}._domainkey.{domain}` TXT lookup'u `IDkimTxtResolver` boundary arkasindan yapiliyor; key record `v=DKIM1`, non-empty/revoked `p=`, optional `h=`, `g=`, ve `t=s` sinirlariyla legacy result modeline map ediliyor. `SystemDkimTxtResolver` mevcut system DNS TXT resolver'ini yeniden kullaniyor ve async `DkimSignatureVerifier.VerifyAsync` DNS'ten gelen key'i body-hash + header-signature verifier'a besliyor. SMTP reject, policy score, signing ve Administrator ayarlari baglanmadi. Dar DKIM filtresi 31/31, prereq kontrolu temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 663/663 gecti.
- DKIM header crypto verifier eklendi: signed header'lar ve `b=` blanked `DKIM-Signature` canonicalize edilip injected SubjectPublicKeyInfo public key ile `rsa-sha1`/`rsa-sha256` RSA/PKCS#1 imzasi dogrulaniyor. Full evaluation yalniz body hash ve header signature birlikte basarili olursa `Pass` modeli donuyor; signed-header/body/public-key/signature hatalari `PermFail`. Live DNS selector lookup, SMTP reject, policy score, signing ve Administrator ayarlari baglanmadi. Dar DKIM filtresi 23/23, prereq kontrolu temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 655/655 gecti.
- DKIM body-hash verifier eklendi: parsed signature uzerinden canonicalized body icin `bh=` karsilastirmasi, opsiyonel `l=` body length siniri, SHA1/SHA256 secimi, body-hash match icin `Neutral` ve mismatch/uzunluk asimi icin `PermFail` sonuc modeli test edildi. SMTP reject, policy score, signing, DNS selector lookup, public-key/header crypto ve Administrator ayarlari baglanmadi. Dar DKIM filtresi 18/18, full Net10 testleri 650/650 ve Net10 build 0 uyari/0 hata ile gecti.
- DKIM evaluation-only temeli eklendi: legacy `Neutral`/`Pass`/`TempFail`/`PermFail` sonuc modeli, deterministic `DKIM-Signature` tag parser'i, required-field validation, default/simple/relaxed canonicalization secimi, signed-header list parsing, `b=` signature-value blanking ve simple/relaxed body/header canonicalization testleri eklendi. SMTP reject, policy score, signing, DNS selector lookup, public-key/header crypto ve Administrator ayarlari baglanmadi. Dar DKIM filtresi 13/13, full Net10 testleri 645/645 ve Net10 build 0 uyari/0 hata ile gecti.
- SPF pass -> greylisting bypass parity eklendi: `HMAILSERVER_GREYLISTING_BYPASS_ON_SPF_PASS=false` varsayilani normal greylisting davranisini koruyor; yalniz explicit acikken SPF `Pass` greylisting lookup'unu bypass ediyor. `Fail`/`None`/`Neutral`/`SoftFail`/`TempError`/`PermError` bypass veya reject/tempfail uretmiyor. Dar greylisting/SPF/receiver filtresi 34/34, full Net10 testleri 632/632 ve Net10 build 0 uyari/0 hata ile gecti.
- SPF system-DNS + disabled SMTP policy boundary eklendi: OS DNS server'lari uzerinden TXT/A/AAAA/MX/PTR cozen `SystemSpfDnsResolver`, `ISmtpSpfPolicy` boundary'si, `HMAILSERVER_SPF_ENABLED=false` varsayilani, authenticated ve `EnableSpamScan=false` skip yollari, `Fail` -> legacy spam flag/status mapping'i ve `Pass` result preservation tamamlandi. Reject/tempfail davranisi eklenmedi. Dar SPF/receiver filtresi 57/57, full Net10 testleri 629/629 ve Net10 build 0 uyari/0 hata ile gecti.
- SPF evaluation-only temeli eklendi: bounded resolver abstraction, deterministik `v=spf1` parser'i, RFC 7208 sonuc modeli, macro expansion, `include`/`redirect` ve `all`/`a`/`mx`/`ptr`/`ip4`/`ip6`/`exists` mekanizmalari, global DNS-term/void-lookup/recursion/MX/PTR limitleri ve timeout/temporary-error yollari dar testlerle kapatildi. SMTP policy/reject/tempfail davranisi bilincli olarak baglanmadi. Dar SPF filtresi 25/25, full Net10 testleri 614/614 ve Net10 build 0 uyari/0 hata ile gecti.
- Legacy `Links` COM kontrati tam vtable/identity sirasiyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Application -> Links`, mevcut read-only SQL administration store/adapter hatlarini yeniden kullanarak `Domain`, `Account`, `Alias` ve `DistributionList` DBID lookup'u aciyor; bilinmeyen ID `DISP_E_BADINDEX`, direct activation `E_ACCESSDENIED` kaliyor ve yeni SQL/mutation eklenmiyor.
- Legacy `Utilities` COM kontrati tam vtable/identity sirasiyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. `Application -> Utilities` ile direct activation saf helper'larda legacy gibi auth istemeden `MD5`, salted `SHA256`, `GenerateGUID`, email/domain/IP validator, `IsStrongPassword` ve `CriteriaMatch` davranisini aciyor. DNS, Blowfish, local-host resolution, dependency/import/mass-mail/test-suite/message-ID/maintenance operasyonlari `E_NOTIMPL`; yan etkili uyeler once legacy server-admin sinirini uyguluyor.
- 27 Haziran derlenmis guvenlik envanteri mevcut SEC-01..SEC-21 tablosuyla birlestirildi; yeni benzersiz kayit cikmadi. Tek kritik SEC-01, 28 Haziran'da rapordaki `x" & ... & "` payload sekliyle yeniden dogrulandi: VBScript quote doubling payload'i ifade olarak calistirmiyor, handler'a veri olarak iletiyor. WSH tabanli .NET security/ClamAV/admin-auth dar filtresi 10/10 gecti; production kodunda legacy davranisi bozacak gereksiz bir degisiklik yapilmadi.
- Legacy `Application` core scalar davranisi runtime/configuration boundary arkasindan eklendi: `Version` legacy gibi auth istemeden donuyor, `ServerState` ve `InitializationFile` server-admin auth istiyor, `VersionArchitecture` legacy `x86`/`x64` formatina cekildi. `Start`, `Stop`, `Connect`, `Reinitialize` ve `SubmitEMail` yan etkili operasyonlari bilincli olarak `E_NOTIMPL` kaliyor.
- Legacy `Status` COM kontrati tam vtable/identity sirasiyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Application -> Status` delivery queue metnini `hm_messages`/`hm_messagerecipients` uzerinden, `StartTime`, processed/spam/virus sayaçlari, `SessionCount` ve `ThreadID` degerlerini runtime snapshot boundary uzerinden read-only aciyor. SMTP/POP3/IMAP session count lease'leri ile delivery completed, spam-detected ve virus-detected counter hook'lari baglandi; direct activation `E_ACCESSDENIED` kaliyor.
- Legacy `ServerMessages` ve `ServerMessage` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> ServerMessages` count/index/name/id lookup'u mevcut `hm_servermessages` SQL verisinden `smname` sirasiyla geliyor ve `ID`, `Name`, `Text` scalar'larini read-only aciyor. Delivery template execution, `Refresh`, `Save`, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `Directories` COM kontrati tam vtable/identity sirasiyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> Directories` configured/default legacy `hMailServer.ini` degerlerinden `ProgramDirectory`, `DatabaseDirectory`, `DataDirectory`, `LogDirectory`, `TempDirectory`, `EventDirectory` ve `DBScriptDirectory` scalar'larini legacy normalization ile read-only aciyor. Directory mutation/persistence ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `Database` COM kontrati ve `eDBtype` enum degerleri tam vtable/identity sirasiyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. `Application -> Database` required/current DB version, requires-upgrade, database-exists, is-connected ve INI-backed type/server/name scalar'larini read-only aciyor; legacy per-member auth korunuyor. SQL execution, transaction, database create/default-selection, script execution, message filename utility, prerequisite ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- SEC-19 legacy IMAP `RENAME` ACL siniri kapatildi: public-folder hiyerarsi degisikligi kaynakta Delete iznine ek olarak hedefteki en ust mevcut parent uzerinde Create izni istiyor. Mevcut regresyon senaryosu Create olmadan red, izin verildikten sonra basariyi kanitlayacak sekilde daraltildi; RegressionTests assembly ve degisen C++ translation unit derlendi.
- Security hardening dilimi: bos administrator hash'i legacy ve .NET 10'da fail-closed; constructor-time anonymous COM auth kaldirildi; legacy JScript password/delivery/UID literal escaping'i duzeltildi; `ScriptFunction` isim/yetki siniri kapatildi; SMTP `ETRN` auth zorunlu oldu; custom antivirus `%FILE%` message-file argumani quote/escape edildi; WebAdmin login session ID/CSRF token rotation ve cryptographic CSRF token generation eklendi. Dar .NET security testleri 15/15, full Net10 testleri 549/549, opt-in LocalDB 6/6 gecti; legacy RegressionTests assembly build'i ve degisen C++ dosyalarinin selected-file compile'i basarili oldu.
- Legacy `GroupMembers` ve `GroupMember` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Group -> Members` count/index/id lookup'u mevcut `hm_group_members` SQL verisinden `memberid` sirasiyla ve group-ID filtresiyle geliyor; `ID`, `GroupID`, `AccountID` scalar'larini read-only aciyor. Account child facade, ACL runtime davranisi, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `Groups` ve `Group` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> Groups` count/index/name/id lookup'u mevcut `hm_groups` SQL verisinden `groupname` sirasiyla geliyor ve `ID`/`Name` scalar'larini read-only aciyor. Members alt koleksiyonu, ACL davranis entegrasyonu, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `SSLCertificates` ve `SSLCertificate` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> SSLCertificates` count/index/id lookup'u mevcut `hm_sslcertificates` SQL verisinden `sslcertificatename` sirasiyla geliyor ve `ID`, `Name`, `CertificateFile`, `PrivateKeyFile` scalar'larini read-only aciyor. Sertifika yukleme/dogrulama, TCP/IP port reconfiguration, `Clear`, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `SecurityRanges` ve `SecurityRange` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> SecurityRanges` count/index/name/id lookup'u mevcut `hm_securityranges` SQL verisinden `rangeexpires`, `rangepriorityid desc`, `rangename` sirasiyla geliyor. DB-backed adapter legacy iki kolonlu IP depolamasini COM'da `LowerIP`/`UpperIP` string'lerine ceviriyor ve read-only IP range, priority, expiry ve option bit scalar'larini aciyor. IP policy enforcement, auto-ban runtime davranisi, `SetDefault`, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `TCPIPPorts` ve `TCPIPPort` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> TCPIPPorts` count/index/id lookup'u mevcut `hm_tcpipports` SQL verisinden `portaddress1`, `portaddress2`, `portnumber` sirasiyla geliyor. DB-backed adapter legacy iki kolonlu IP depolamasini COM'da `Address` string'ine ceviriyor ve `ID`, `Protocol`, `PortNumber`, `Address`, `UseSSL`, `SSLCertificateID`, `ConnectionSecurity` scalar'larini read-only aciyor. Listener yeniden konfigurasyonu, `SetDefault`, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `IncomingRelays` ve `IncomingRelay` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> IncomingRelays` count/index/name/id lookup'u mevcut `hm_incoming_relays` SQL verisinden `relayname` sirasiyla geliyor. DB-backed adapter legacy iki kolonlu IP depolamasini COM'da `LowerIP`/`UpperIP` string'lerine ceviriyor; SMTP trust davranisi ve mutation'lar `E_NOTIMPL`, direct activation `E_ACCESSDENIED` kaliyor.
- Authenticated `Application -> Rules`, mevcut testli `Rules`/`Rule` adapter ve SQL store hattini `ruleaccountid = 0` global-rule verisi icin yeniden kullaniyor. Yalniz server-admin Application yolu koleksiyona erisebiliyor; global/account rule ayrimi ve gercek SQL yolu test edildi. Criteria/actions, execution ve mutation sinirlari degismeden `E_NOTIMPL` kaliyor.
- Legacy `Routes` ve `Route` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> Routes` count/index/domain-name/id lookup'u mevcut `hm_routes` SQL verisinden legacy domain-name sirasiyla geliyor. DB-backed route adapter parola kolonunu okumadan ID, domain/description/target, retry, all-addresses, relayer auth kullanimi ve username'i, sender/recipient-local bayraklari ile connection-security/UseSSL scalar'larini read-only aciyor; obsolete `TreatSecurityAsLocalDomain` recipient-local alias'ini koruyor. Direct activation `E_ACCESSDENIED`; route-address alt koleksiyonu, parola setter'i ve mutation'lar `E_NOTIMPL` kaliyor.
- Authenticated `Settings -> PublicFolders`, mevcut testli `IMAPFolders`/`IMAPFolder` adapter ve SQL store hattini `folderaccountid = 0` public-root verisi icin yeniden kullaniyor. Yalniz server-admin Settings yolu koleksiyona erisebiliyor; account/public kok ayrimi ve gercek SQL yolu test edildi. Messages, permissions, nested subfolders ve mutation sinirlari degismeden `E_NOTIMPL` kaliyor.
- Legacy `IMAPFolders` ve `IMAPFolder` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Account -> IMAPFolders` yalniz top-level (`folderparentid = -1`) count/index/name/id lookup'u mevcut `hm_imapfolders` SQL verisinden `folderid` sirasiyla getiriyor. DB-backed folder adapter `ID`, `ParentID`, legacy modified UTF-7 decode edilmis `Name`, `Subscribed`, `CurrentUID`, ve `CreationTime` scalar'larini read-only aciyor; direct `IMAPFolders`/`IMAPFolder` aktivasyonu `E_ACCESSDENIED`, messages, permissions, nested subfolders ve mutation'lar `E_NOTIMPL` kaliyor. Dar contract/store/manifest testleri ve opt-in izole SQL integration kapsami guncellendi.
- Legacy `Rules` ve `Rule` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Account -> Rules` count/index/id lookup mevcut `hm_rules` SQL verisinden geliyor. DB-backed rule adapter `ID`, `AccountID`, `Name`, `Active`, ve `UseAND` scalar'larini read-only aciyor; direct `Rules`/`Rule` aktivasyonu `E_ACCESSDENIED`, criteria/actions, execution ve mutation'lar `E_NOTIMPL` kaliyor. Dar contract/store/manifest testleri ve opt-in izole SQL integration kapsami guncellendi.
- Legacy `FetchAccounts` ve `FetchAccount` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Account -> FetchAccounts` count/index/id lookup mevcut `hm_fetchaccounts` SQL verisinden geliyor. DB-backed fetch-account adapter non-secret scalar'lari read-only aciyor (`ID`, `AccountID`, `Name`, server/port/type/user, minutes/days, enabled, MIME processing flags/headers, connection security/UseSSL, antispam/antivirus/route flags, next download time, lock state); password access, `DownloadNow`, `Save`, `Delete`, setters ve direct activation `E_NOTIMPL`/`E_ACCESSDENIED` sinirlarini koruyor. Dar contract/store/manifest ve opt-in izole SQL integration kapsami legacy enum degerleri, direct-TLS-only `UseSSL` alias'i, read-only mutation sinirlari, non-secret SQL projection sirasi, account-scoped DBID izolasyonu ve password omission icin sikilastirildi; focused filtre 11/11 gecti.
- Authenticated SQL-backed `Account` adapter'i secili delivery/detail scalar'lari read-only acacak sekilde genisletildi: vacation/autoreply (`VacationMessageIsOn`, `VacationMessage`, `VacationSubject`, expiry/spam-abort), forwarding (`ForwardEnabled`, `ForwardAddress`, `ForwardKeepOriginal`, spam-abort) ve signature (`SignatureEnabled`, `SignaturePlainText`, `SignatureHTML`) alanlari mevcut `hm_accounts` SQL verisinden geliyor. Behavior execution, password/security-sensitive alanlar, child collection'lar ve scalar mutation'lari `E_NOTIMPL` kaliyor. Dar contract/store testleri ve opt-in izole SQL integration kapsami guncellendi.
- Authenticated SQL-backed `Account` adapter'i secili core detail scalar'lari read-only acacak sekilde genisletildi: `MaxSize`, `PersonFirstName`, ve `PersonLastName` mevcut `hm_accounts` SQL verisinden geliyor. Password/security-sensitive alanlar, behavior-heavy alanlar, child collection'lar ve scalar mutation'lari `E_NOTIMPL` kaliyor. Dar contract/store testleri ve opt-in izole SQL integration kapsami guncellendi.
- Authenticated SQL-backed `Domain` adapter'i secili core detail scalar'lari read-only acacak sekilde genisletildi: `Postmaster`, `MaxMessageSize`, `PlusAddressingEnabled`, `PlusAddressingCharacter`, `MaxSize`, `MaxNumberOfAccounts`, `MaxNumberOfAliases`, `MaxNumberOfDistributionLists`, bunlarin enabled bitleri ve `MaxAccountSize` mevcut `hm_domains` SQL verisinden geliyor. Scalar mutation'lari ve computed/behavior-heavy alanlar (`Size` vb.) `E_NOTIMPL` kaliyor. Dar contract/store testleri ve opt-in izole SQL integration kapsami guncellendi.
- Legacy `DomainAliases` ve `DomainAlias` COM kontratlari tam vtable/identity siralariyla eklendi; authenticated `Domain -> DomainAliases` count/index/id lookup mevcut `hm_domain_aliases` SQL verisinden geliyor. DB-backed domain-alias adapter `ID`, `DomainID`, ve `AliasName` scalar'larini read-only aciyor; direct `DomainAliases`/`DomainAlias` aktivasyonu `E_ACCESSDENIED`, domain-alias mutation'lari `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Legacy `DistributionListRecipients` ve `DistributionListRecipient` COM kontratlari tam vtable/identity siralariyla eklendi; authenticated `DistributionList -> Recipients` count/index/id lookup mevcut `hm_distributionlistsrecipients` SQL verisinden geliyor. DB-backed recipient adapter `ID` ve `RecipientAddress` scalar'larini read-only aciyor; direct `DistributionListRecipients`/`DistributionListRecipient` aktivasyonu `E_ACCESSDENIED`, recipient mutation'lari `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Legacy `DistributionLists` ve `DistributionList` COM kontratlari tam vtable/identity siralariyla eklendi; authenticated `Domain -> DistributionLists` count/index/address/id lookup mevcut `hm_distributionlists` SQL verisinden geliyor. DB-backed distribution-list adapter `ID`, `Address`, `Active`, `RequireSMTPAuth`, `RequireSenderAddress`, ve `Mode` scalar'larini read-only aciyor; direct `DistributionLists`/`DistributionList` aktivasyonu `E_ACCESSDENIED`, list mutation'lari `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Legacy `Aliases` ve `Alias` COM kontratlari tam vtable/identity siralariyla eklendi; authenticated `Domain -> Aliases` count/index/name/id lookup mevcut `hm_aliases` SQL verisinden geliyor. DB-backed alias adapter yalniz `ID`, `DomainID`, `Name`, `Value`, `Active` scalar'larini read-only aciyor; direct `Aliases`/`Alias` aktivasyonu `E_ACCESSDENIED`, alias mutation'lari `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Legacy `Accounts` collection COM kontrati ve hosted `Accounts`/`Account` class identity'leri eklendi; authenticated `Domain -> Accounts` count/index/address/id lookup mevcut `hm_accounts` SQL verisinden geliyor. DB-backed account adapter yalniz `ID`, `DomainID`, `Address`, `Active`, `AdminLevel` scalar'larini read-only aciyor; direct `Accounts`/`Account` aktivasyonu `E_ACCESSDENIED`, account mutation'lari ve derin child collection'lar `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Legacy `Domains` ve `Domain` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration `Application`, `Settings`, `Domains`, `Domain`, `MessageIndexing` siniflarini kapsiyor. Authenticated `Application -> Domains` count/index/name/id lookup mevcut `hm_domains` SQL verisinden geliyor; direct `Domains`/`Domain` aktivasyonu `E_ACCESSDENIED`, mutations ve nested collections `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Explicit opt-in SQL Server integration testi GUID isimli izole database olusturup siliyor ve real store uzerinden authenticated `Application -> Settings -> MessageIndexing` status, Enabled, queue, Clear ve Index akislarini dogruluyor; verilen connection string'deki database'e dokunmuyor. LocalDB canli kosusu basarili.
- Service build/publish authoritative legacy IDL'den `hMailServer.tlb` uretiyor. Saf manifest ve guarded install/uninstall wiring'i hosted COM siniflari icin AppID, CLSID, LocalServer32, versioned/version-independent ProgID, CurVer ve 64-bit TypeLib kayitlarini tamamliyor; normal build/test registry veya SCM'e dokunmuyor ve mevcut legacy service replacement acik opt-in gerektiriyor.
- Legacy installed 5.7 type library ile karsilastirilarak dual `Settings` IID'si, tam 142-accessor/method vtable sirasi, CLSID/versioned ProgID/default-interface metadata'si ve `MessageIndexing` DISPID 89 korundu. Authenticated yol `Application -> Settings -> MessageIndexing` olarak gercek runtime'a ulasiyor; diger Settings uyeleri yetki kontrolunden sonra acik `E_NOTIMPL`, direct Settings aktivasyonu `E_ACCESSDENIED` kaliyor.
- Service configured `InitializationFile`/`HMAILSERVER_INITIALIZATION_FILE` yolundan veya executable-directory `hMailServer.ini` varsayilanindan `[Security] AdministratorPassword` hash'ini yukluyor. Case-insensitive `Administrator` ve legacy MD5/salted-SHA256 dogrulamasi korunuyor; guvensiz empty-hash/empty-password anonymous administration davranisi legacy ve .NET 10'da fail-closed olarak sertlestirildi.
- Windows service dedicated MTA uzerindeki process-local host'a gercek `Application`, `Settings` ve `MessageIndexing` CLSID factory'lerini kaydediyor. Registry'siz process testleri authentication, authenticated child adapter, direct child access denial ve revoke davranislarini dogruluyor; guarded registry/type-library/service-install wiring'i de tamamlandi.
- Legacy installed 5.7 type library ile karsilastirilarak dual `Application` ve `Account` IID'leri, tam 20/61-member vtable sirasi, CLSID/versioned ProgID/default-interface metadata'si, server-admin account sonucu ve detached-account access-denied siniri korundu.
- SQL Server message-indexing administration store'u legacy delivered/indexed count'lari, persisted `MessageIndexing` ayarini, FTS/queue status'unu ve queue-driven `Clear`/`Index`/`Rebuild` islemlerini sagliyor. Service bu store-backed runtime'i process host'a configure ediyor; direct COM activation legacy gibi `E_ACCESSDENIED` kaliyor ve backfill processor `Enabled=false` iken lease almiyor.
- COM-visible `MessageIndexing` class'i legacy CLSID ve versioned `hMailServer.MessageIndexing.1` ProgID'sini koruyor, legacy interface'i default tutuyor, additive `IInterfaceMessageIndexing2` yuzeyini uyguluyor ve butun v1/v2 cagrilarini process host'un sagladigi zorunlu runtime'a delege ediyor. Version-independent ProgID/CurVer alias'i ve authenticated `Application -> Settings` factory yolu da tamamlandi.
- .NET COM assembly'si legacy dual `IInterfaceMessageIndexing` IID'sini, DISPID 1-5 uye seklini ve `Enabled` icin `VARIANT_BOOL` marshaling'ini koruyor; portable kontrat ve Windows COM-host hedefleri birlikte build ediliyor, legacy ve additive `IInterfaceMessageIndexing2` yuzeyleri reflection testleriyle kilitlendi.
- VBScript/JScript `OnClientValidatePassword` account facade'i legacy `Password` scalar'ini SQL'den okunan stored degerle tasiyor; attempted plaintext ayri `password` argumani olarak kaliyor.
- VBScript `OnClientValidatePassword` runner'i `Result.Parameter` alanini uninitialized `Empty` birakmak yerine legacy COM scalar parity'siyle acik numeric `0` olarak seed ediyor.
- JScript `OnClientValidatePassword` runner'i legacy `Result` constructor parity'siyle yazilabilir `Parameter = 0` alani tasiyor; VBScript ve genel event runner'lariyla scalar facade sekli esitlendi.
- Global VBScript/JScript `EventLog.Write(value)` facade'i rule script, `OnError`, ve password-validation script yollarinda legacy event log bicimiyle tamamlandi.
- External POP3 fetch hosted worker startup'ta stale `hm_fetchaccounts.falocked` satirlarini resetleyecek sekilde legacy `PersistentFetchAccount::UnlockAll()` davranisina yaklastirildi.
- External POP3 fetch ayni POP3 listing icindeki duplicate yeni UIDL degerlerini tek indirme/kuyruklama ile sinirlayacak sekilde kapatildi.
- External POP3 fetch ayni POP3 listing icindeki duplicate bilinen UIDL degerlerini tek script/retention cleanup ile sinirlayacak sekilde kapatildi.
- `OnExternalAccountDownload` fetch-account facade'i SQL lease'ten gelen `fanexttry`/`falocked` degerlerini legacy `NextDownloadTime` ve `IsLocked` script alanlari olarak yayacak sekilde genisletildi.
- `HMAILSERVER_CLIENT` facade'i legacy COM isimleri olan `Authenticated` ve `EncryptedConnection` alias'larini VBScript/JScript event handler'larinda destekleyecek sekilde genisletildi.
- External POP3 fetch duplicate persisted `hm_fetchaccounts_uids.uidvalue` satirlarini batch lookup olustururken tolere edecek sekilde kapatildi.
- External POP3 fetch UIDL satirlarini legacy `std::map` gibi artan sequence sirasinda isliyor; duplicate sequence icin son UID'yi tutup ayni remote slotu tek indirme/kuyruklama ile sinirliyor.
- External POP3 fetch yeni indirilen mesajlara script ve receiver islemlerinden once legacy `X-hMailServer-ExternalAccount: <account name>` basligini ekliyor.
- External POP3 fetch `OnExternalAccountDownload` icin negatif `Result.Parameter` degerlerini koruyor ve yeni/bilinen UID yollarinda legacy gibi immediate remote-delete uyguluyor.
- External POP3 fetch pozitif known-UID retention yasini takvim gunune kirmadan tam timestamp farkiyla hesapliyor; esit sinir tutuluyor, asildiginda remote mesaj siliniyor.
- External POP3 fetch normal sonlanan bos `RETR` payload'ini legacy account header eklendikten sonra script/receiver ve UID retention akisinda islemeye devam ediyor.
- External POP3 fetch configured MIME recipient header adlarinda legacy gibi yalniz ilk eslesen alani kullaniyor; duplicate alanlar ek recipient uretmiyor, tum `Received` alanlari ayrica taranmaya devam ediyor.
- External POP3 fetch `Received ... for` recipient degerlerini legacy 254 karakter/email regex'iyle dogruluyor; malformed adresler route acik olsa da account fallback'i asamiyor.
- External POP3 fetch `Received` recipient token'ini legacy `std::rfind` gibi case-sensitive ariyor; uppercase `FOR ` eslesmeyip account fallback recipient'ine donuyor.
- External POP3 fetch ilk parse edilen `From` mailbox'ini legacy 254 karakter/email validator'undan geciriyor; gecersiz veya limit ustu degerleri bos envelope sender'a dusuruyor.
- External POP3 fetch cozulmus recipient'lari legacy `RecipientParser::AddRecipient_` gibi yalniz case-insensitive final adrese gore tekillestiriyor; ayni mailbox'a giden farkli alias'larda ilkini tutuyor.
- External POP3 fetch configured MIME recipient listesi topluca parse edilemezse legacy quote/escape-aware comma compound'larina dusuyor; bozuk adres yanindaki gecerli adresi koruyor.
- External POP3 fetch whitespace-only fakat non-empty MIME recipient-header ayarinda legacy `StdString::IsEmpty()` gibi recipient islemine girip `Received ... for` taramasini koruyor.
- External POP3 fetch STARTTLS akisi legacy CAPA/STLS davranisina yaklastirildi: optional STARTTLS sadece STLS advertise edilmezse plaintext'e duser, required STARTTLS credentials gondermeden fail eder ve advertise edilip reddedilen STLS iki modda da credentials oncesi fail eder.
- External POP3 fetch CAPA reddi davranisi legacy ile sabitlendi: optional STARTTLS plaintext'e devam ederken required STARTTLS `USER`/`PASS` oncesi fail eder.
- External POP3 fetch reddedilen server greeting'inde plain ve STARTTLS modlarinda hicbir istemci komutu veya credential gondermeden fail edecek sekilde legacy ile sabitlendi.
- External POP3 fetch reddedilen `USER` komutunda plain ve optional-STARTTLS plaintext fallback yollarinda `PASS` gondermeden fail edecek sekilde legacy ile sabitlendi.
- External POP3 fetch reddedilen `PASS` komutunda plain ve optional-STARTTLS plaintext fallback yollarinda `UIDL` veya sonraki bir komut gondermeden fail edecek sekilde legacy ile sabitlendi.
- External POP3 fetch reddedilen `UIDL` komutunda plain ve optional-STARTTLS plaintext fallback yollarinda `RETR`/`DELE` gondermeden legacy `QUIT` cleanup yapacak sekilde sabitlendi.
- External POP3 fetch `UIDL +OK` sonrasi terminator gelmeden socket kapanirsa fatal kalacak, account failed-release edilecek ve receiver/UID/`RETR`/`DELE` yan etkisi uretilmeyecek sekilde testle sabitlendi.
- External POP3 fetch bos `UIDL` listing'de `RETR`/`DELE` gondermeden hesabi tamamlayacak ve remote server'da artik gorunmeyen known UID satirlarini silecek sekilde testle sabitlendi.
- External POP3 fetch malformed `UIDL` listing satirlarini atlayip ayni response icindeki gecerli satirlari islemeye devam edecek sekilde TCP parser testiyle sabitlendi.
- External POP3 fetch reddedilen `RETR` komutunda yalniz legacy `QUIT` cleanup yapacak, failed account lease'i release edecek ve receiver/UID/remote-delete yan etkisi uretmeyecek sekilde sabitlendi.
- External POP3 fetch `RETR +OK` sonrasi message body terminator gelmeden socket kapanirsa fatal kalacak, account failed-release edilecek ve receiver/UID/remote-delete yan etkisi uretilmeyecek sekilde testle sabitlendi.
- External POP3 fetch `DELE -ERR` yanitini legacy best-effort cleanup olarak kabul edip UID cleanup ve `QUIT` akisina devam edecek; socket/I/O/cancellation hatalarini fatal tutacak sekilde duzeltildi.
- External POP3 fetch `DELE` response gelmeden socket/I/O koparsa fatal kalacak, known UID korunacak ve account lease failed-release edilecek sekilde testle sabitlendi.
- External POP3 fetch session disposal sirasinda `QUIT -ERR` veya QUIT response oncesi disconnect exception sizdirmeyecek sekilde legacy best-effort cleanup testiyle sabitlendi.
- External POP3 fetch yeni mesaj byte'larinin onune hesap adini tasiyan legacy `X-hMailServer-ExternalAccount` basligini ekliyor; script girdisi ve receiver'a giden sonuc testle sabitlendi.
- External POP3 fetch negatif script retention parametrelerini sifira sikistirmiyor; hem yeni mesaj hem bilinen UID cleanup akisinda tum negatif degerler remote silme karari veriyor.
- External POP3 fetch known-UID yas hesabinda legacy `DateTimeSpan.GetNumberOfDays()` gibi kesirli elapsed gun kullaniyor; 47 saatlik UID 1 gun politikasinda silinirken tam 24 saatlik UID tutuluyor.
- External POP3 fetch sifir-byte `RETR` sonucunu erken hata yapmiyor; `X-hMailServer-ExternalAccount` basligi header-only mesaj olusturuyor ve normal queue/UID akisi testle sabitlendi.
- External POP3 fetch duplicate configured recipient header'larinda `MimeHeader::GetRawFieldValue` parity'siyle ilk degeri kullaniyor; validator ve receiver'a ikinci duplicate adres sizmiyor.
- External POP3 fetch `bad@@example.test` gibi bozuk `Received for` adreslerini legacy `StringParser::IsValidEmailAddress` sozlesmesiyle reddedip account recipient fallback'ine donuyor.
- External POP3 fetch uppercase `FOR ` belirtecini recipient token'i saymiyor; lowercase `for ` davranisi korunurken route recipient yerine account fallback secimi testle sabitlendi.
- External POP3 fetch 255 karakterlik `From` mailbox'ini envelope sender olarak sizdirmiyor; legacy 254 karakter siniri sonrasi bos sender ile receiver akisina devam ettigi testle sabitlendi.
- External POP3 fetch iki farkli MIME alias'i ayni `user@example.test` hesabina cozuldugunde receiver'a tek recipient veriyor ve ilk alias'in `OriginalAddress` degerini koruyor.
- External POP3 fetch `bad@@example.test` yanindaki `"Valid, Recipient" <valid@example.test>` adresini kaybetmiyor; quoted display-name virgulu compound'u bolmeden validator ve receiver'a gecerli adresi tasiyor.
- External POP3 fetch MIME recipient-header ayari `" "` oldugunda configured token uretmese de lowercase `Received for <alias@example.test>` recipient'ini validator ve receiver'a tasiyor.
- JScript password-validation handler'i `Result.Parameter` alanini ilk okumada numeric `0` goruyor ve alani yazip geri okuyabiliyor; eksik-property nedeniyle `undefined` donusu testle kapatildi.
- VBScript password-validation handler'i `Result.Parameter` alaninda `IsEmpty = False`, deger `0` ve yazilabilirlik sozlesmesini goruyor; class-field `Empty` farki testle kapatildi.
- Password-validation handler'lari `oAccount.Password = "legacy-password-hash"` ile `password = "attempted-secret"` degerlerini iki dilde ayri goruyor; stored/attempted ayrimi testle sabitlendi.
- `HMAILSERVER_MESSAGE.RefreshContent`, script tarafindan message file dogrudan degistirildikten sonra header/body alanlarini yeniden yukleyecek sekilde VBScript/JScript testleriyle sabitlendi.
- `HMAILSERVER_MESSAGE.FileName`/`Filename` facade'i script assignment sonrasi `Load`/`Save`/`Copy` file I/O'sunu orijinal runner backing path'inde tutacak sekilde legacy `Filename` read-only davranisina yaklastirildi.
- `HMAILSERVER_MESSAGE.To`/`CC` direct assignment, legacy COM read-only property sekline yaklastirildi; recipient/header mutasyonlari `AddRecipient`, `ClearRecipients`, `Recipients`, ve `HeaderValue` yollarinda kalacak sekilde testlendi.
- Attachment `FileName`/`Filename` ve `Size` metadata'si legacy COM read-only property sekline yaklastirildi; VBScript direct assignment'i reddederken JScript assignment'inin collection backing metadata'sini degistirmedigi testlendi.
- `HMAILSERVER_MESSAGE.ID`, `UID`, `State`, `DeliveryAttempt` ve `InternalDate` queue metadata'si legacy COM read-only property sekline yaklastirildi; VBScript assignment'i reddediyor, JScript canonical seed'leri `Load`/`Save`/`Copy` sinirlarinda geri yukluyor ve 64-bit message ID korunuyor. Legacy C++'taki gibi `State` ile message flags ayrildi; delivery eventleri `State = 1` ve queue `messageflags` degerini `Flag(eMessageFlag)` icin ayri seed ediyor.
- `HMAILSERVER_MESSAGE.Size`, legacy integer `bytes / 1024` floor-KiB hesabina cekildi; 1024 byte altindaki mesajlar `0` donuyor, property read-only kaliyor ve VBScript/JScript `Save` sonrasi backing file boyutunu yeniden okuyor.
- Recipient item `Address`, `OriginalAddress` ve `IsLocalUser` metadata'si legacy COM read-only property sekline yaklastirildi; VBScript assignment'i reddediyor, JScript detached snapshot donduruyor ve `AddRecipient`/`ClearRecipients` message-level mutasyonlari korunuyor.
- `Recipients` collection facade'inda legacy disi `Add`, `Clear` ve `ToHeaderValue` isimleri kaldirildi; `Count`/`Item` okumalari ile message-level `AddRecipient`/`ClearRecipients` mutasyonlari VBScript/JScript'te korunuyor.
- `HMAILSERVER_MESSAGE.AddRecipient`, bos display-name dahil recipient'lari legacy C++ bicimindeki quoted MIME adresiyle ve bosluksuz virgul birlestirmesiyle VBScript/JScript'te yaziyor.
- `HMAILSERVER_MESSAGE.ClearRecipients`, envelope recipient collection ile birlikte legacy C++ davranisindaki gibi `To`, `Cc` ve `Bcc` MIME header'larini VBScript/JScript'te temizliyor.
- `HMAILSERVER_MESSAGE.Save`, legacy C++ davranisindaki gibi bos `Date` degerine current local MIME date ekleyerek mesaji VBScript/JScript'te kaydediyor.
- `HMAILSERVER_MESSAGE.Body` ve `HTMLBody`, bos olmayan script atamalarini legacy `MessageData` davranisindaki gibi trailing `CRLF` ile kaydediyor; bos degerler bos kaliyor.
- `Headers` collection facade'inda runner-only `Refresh` ve `Commit` isimleri kaldirildi; legacy `Count`/`Item`/`ItemByName` okumalari ile header `Name`/`Value`/`Delete` mutasyonlari `Save` uzerinden korunuyor.
- `Recipients.Item`, `Headers.Item` ve `Headers.ItemByName`, gecersiz indeks veya eksik isimde `Nothing`/`null` yerine legacy `DISP_E_BADINDEX` sozlesmesine uygun script hatasi yukseltiyor.
- `Attachments` collection facade'inda runner-only `Load` ve `DeleteAt` isimleri kaldirildi; legacy `Count`/`Item`/`Clear`/`Add` ile attachment item `SaveAs`/`Delete` davranislari korunuyor.
- `Attachments.Add`, kaynak dosya yoksa sessizce donmek yerine legacy `Failed to attach file.` hatasini VBScript/JScript'te yukseltiyor.
- `Attachments.Item`, collection disindaki indekste `Nothing`/`null` dondurmek yerine legacy `DISP_E_BADINDEX` sozlesmesine uygun script hatasi yukseltiyor.
- Yakalanmis attachment item nesneleri collection'da daha onceki bir oge silinse de sabit kimligini koruyor; `Delete` VBScript/JScript'te legacy gibi ilk secilen MIME parcasini kaldiriyor.
- `HMAILSERVER_MESSAGE.HasBodyType`, ham header/body substring aramasi yerine legacy temiz MIME content-type davranisina cekildi; root ve iki nested part seviyesi, case-insensitive eslesme ve noktalivirgul iceren quoted boundary degerleri VBScript/JScript testleriyle sabitlendi.

Yeni thread baslamadan once yine `git status --short --branch` ve `git diff` okunmali. Calisma agaci temiz degilse once mevcut WIP'in kime ait oldugu ve hangi slice'a hizmet ettigi anlasilmali.

## Son Git Durumu

Branch:

```text
net10-modernization...origin/net10-modernization
```

Bu dokuman guncellemesinden once tamamlanan kod commit'i:

```text
80c7248be feat(net10): expose settings retry COM getters
```

Son 30 commit icinde one cikan son dilimler:

- `2087a4e1e feat(net10): expose script account password`
- `1e36992fb fix(net10): seed vb password result parameter`
- `d39e90ca4 fix(net10): seed password result parameter`
- `2db4de6cc fix(net10): scan received with blank fetch headers`
- `eb5a497ef fix(net10): recover valid fetched recipients`
- `cf8e965f9 fix(net10): deduplicate fetched alias recipients`
- `12542dfb5 fix(net10): validate external fetch senders`
- `eb239fe5b fix(net10): match received for token casing`
- `8d94cf12a fix(net10): validate fetched received recipients`
- `67471a5a0 fix(net10): use first fetch recipient header`
- `208705faf fix(net10): accept empty external fetch messages`
- `6b481c125 fix(net10): use elapsed fetch retention days`
- `fbb46edbc fix(net10): honor negative fetch retention`
- `4faa60ea9 fix(net10): tag external fetch messages`
- `49ef83587 fix(net10): order external fetch uidl sequences`
- `a1541a1a1 fix(net10): preserve script attachment identity`
- `78a4bfd5e fix(net10): terminate script message bodies`
- `24e703780 fix(net10): reject invalid script collection lookups`
- `3da58a9c6 fix(net10): reject invalid script attachment indexes`
- `25029bb0a fix(net10): fail missing script attachments`
- `49430b3b1 fix(net10): match script recipient header format`
- `697943ed0 fix(net10): add missing message date on save`
- `c8c92d92f fix(net10): clear script message blind recipients`
- `b5db584df fix(net10): match legacy script body type checks`
- `47df94c53 fix(net10): hide attachment collection helpers`
- `3869e31bf fix(net10): hide message header helpers`
- `aeed04e3b fix(net10): hide recipient collection mutators`
- `5a57de685 fix(net10): keep script recipient metadata readonly`
- `7bcb50f9d fix(net10): match legacy script message size`
- `ae404dcf0 fix(net10): separate script message state and flags`
- `59650f826 fix(net10): keep message queue metadata readonly`
- `cd22514b4 fix(net10): keep attachment metadata readonly`
- `9d899c20d fix(net10): keep script message recipient headers readonly`
- `0e93f5606 fix(net10): keep script message filename backing path stable`
- `49691e554 test(net10): cover message RefreshContent script facade`
- `dbc462807 test(net10): cover empty external fetch UIDL listings`
- `0b9c2d914 test(net10): cover malformed external fetch UIDL rows`
- `68cf89432 test(net10): cover truncated external fetch RETR bodies`
- `2c0ee55db test(net10): cover truncated external fetch UIDL listings`
- `c663cefe0 test(net10): cover external fetch QUIT cleanup failures`
- `7e40efe1a test(net10): cover external fetch DELE transport failures`
- `87d50855d fix(net10): tolerate rejected external fetch DELE`
- `c85ce6aa0 test(net10): cover rejected external fetch RETR`
- `d2e89fe68 test(net10): cover rejected external fetch UIDL`
- `691fe2532 test(net10): cover rejected external fetch PASS`
- `3c485df40 test(net10): cover rejected external fetch USER`
- `ab2710f72 test(net10): cover rejected external fetch greeting`
- `9e187a7fb test(net10): cover rejected external fetch CAPA`
- `79e02e4fe test(net10): cover rejected external fetch STLS`
- `0cb9152bb fix(net10): probe external fetch STLS capability`
- `f2048517a fix(net10): skip duplicate external fetch sequence entries`
- `bfd9916ac fix(net10): tolerate duplicate external fetch UID rows`
- `9470c2e53 feat(net10): add legacy client auth script aliases`
- `bb5e8c0df feat(net10): expose fetch account lock script fields`
- `79327bc45 fix(net10): skip duplicate known external fetch UIDs`
- `27df051c2 fix(net10): skip duplicate external fetch UIDs`
- `718108bf6 fix(net10): reset external fetch locks on startup`
- `f65bb2a05 feat(net10): expose script event log facade`
- `254e118da feat(net10): dispatch legacy OnError scripts`
- `03df16257 feat(net10): support scripted message folder copies`
- `9a0fc5f41 feat(net10): run client connect events for IMAP and POP3`
- `c703f48de feat(net10): add SQL greylisting checks`
- `ce5693bc1 feat(net10): add sender domain MX checks`
- `b7462af49 feat(net10): add optional reverse DNS checks`
- `76f6b0d2a feat(net10): support IMAP search sequence sets`
- `4603eb773 perf(net10): stream SQL search result readers`
- `8cb42b48d perf(net10): reduce IMAP search result allocations`

Bu dokumanin onceki surumundeki EventLog dirty-WIP notu artik gecerli degil; ilgili slice testlenip commit/push edildi.

## Build/Test Komutlari

.NET 10 on kosul kontrolu:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\check-net10-prereqs.ps1 -RequireMsBuild
```

.NET 10 build:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\build-net10.ps1 -Configuration Debug
```

.NET 10 test:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\test-net10.ps1 -Configuration Debug
```

Legacy C++ build:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\build.ps1 -Configuration Debug
```

Legacy regression test build/run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\build-tests.ps1 -Configuration Debug
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\run-tests.ps1
```

`build/build-net10.ps1` su projeleri tek tek build eder:

- `HMailServer.Service`
- `HMailServer.Indexing`
- `HMailServer.Delivery`
- `HMailServer.ComInterop`

`build/test-net10.ps1`, `hmailserver/source/Server.Net10/tests/HMailServer.Net10.Tests/HMailServer.Net10.Tests.csproj` uzerinden MSTest calistirir.

`tools/dotnet10/dotnet.exe` varsa scriptler onu kullanir; yoksa PATH'teki `dotnet` kullanilir.

## Son Build/Test Ciktisi

Son temiz dogrulama notlari:

- EventLog facade dilimi icin Net10 build basariliydi ve full Net10 testler 327/327 gecmisti.
- External fetch stale-lock startup reset dilimi icin dar `ExternalFetchProcessorTests` filtresi 9/9 gecti; ardindan Net10 build basarili oldu ve full Net10 testler 328/328 gecti.
- External fetch duplicate UIDL dilimi icin dar `ExternalFetchProcessorTests` filtresi 10/10 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 329/329 gecti.
- External fetch duplicate known UIDL dilimi icin dar `ExternalFetchProcessorTests` filtresi 11/11 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 330/330 gecti.
- Fetch-account script facade `NextDownloadTime`/`IsLocked` dilimi icin dar `WindowsScriptRuleExecutorTests|SqlServerExternalFetchAccountStoreTests` filtresi 34/34 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 330/330 gecti.
- Client script facade alias dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 30/30 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 330/330 gecti.
- External fetch duplicate persisted known-UID row dilimi icin dar `ExternalFetchProcessorTests` filtresi 12/12 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 331/331 gecti.
- External fetch duplicate remote sequence dilimi icin dar `ExternalFetchProcessorTests` filtresi 13/13 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 332/332 gecti.
- External fetch STLS CAPA probing dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 3/3 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 334/334 gecti.
- External fetch rejected-STLS parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 5/5 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 336/336 gecti.
- External fetch rejected-CAPA parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 7/7 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 338/338 gecti.
- External fetch rejected-greeting parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 10/10 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 341/341 gecti.
- External fetch rejected-USER parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 12/12 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 343/343 gecti.
- External fetch rejected-PASS parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 14/14 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 345/345 gecti.
- External fetch rejected-UIDL parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 16/16 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 347/347 gecti.
- External fetch rejected-RETR parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 32/32 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 350/350 gecti.
- External fetch best-effort DELE parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 34/34 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 352/352 gecti.
- External fetch DELE transport-failure parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 37/37 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 355/355 gecti.
- External fetch QUIT cleanup parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 26/26 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 359/359 gecti.
- External fetch truncated-UIDL-listing parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 44/44 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 362/362 gecti.
- External fetch truncated-RETR-body parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 47/47 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 365/365 gecti.
- External fetch malformed-UIDL-row parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 32/32 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 367/367 gecti.
- External fetch empty-UIDL-listing parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 52/52 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 370/370 gecti.
- `HMAILSERVER_MESSAGE.RefreshContent` script facade dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 32/32 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 372/372 gecti.
- `HMAILSERVER_MESSAGE.FileName`/`Filename` backing-path parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 34/34 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 374/374 gecti.
- `HMAILSERVER_MESSAGE.To`/`CC` read-only direct-assignment parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 36/36 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 376/376 gecti.
- Attachment `FileName`/`Filename`/`Size` read-only metadata parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 38/38 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 378/378 gecti.
- Message `ID`/`UID`/`DeliveryAttempt`/`InternalDate` read-only queue metadata parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 40/40 gecti; 64-bit message ID seed'i dogrulandi, prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 380/380 gecti.
- Message `State`/`Flag(eMessageFlag)` ayrimi dilimi icin dar `WindowsScriptRuleExecutorTests|DeliveryQueueProcessorTests` filtresi 50/50 gecti; delivery event `State = 1` ve queue flag seed'leri ayri dogrulandi, prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 380/380 gecti.
- Message `Size` read-only floor-KiB ve `Save` sonrasi yeniden olcum parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 42/42 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 382/382 gecti.
- Recipient item `Address`/`OriginalAddress`/`IsLocalUser` read-only metadata parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 44/44 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 384/384 gecti.
- Recipient collection `Count`/`Item` legacy surface parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 44/44 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 384/384 gecti.
- Message header collection `Count`/`Item`/`ItemByName` legacy surface parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 44/44 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 384/384 gecti.
- Attachment collection `Count`/`Item`/`Clear`/`Add` legacy surface parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 44/44 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 384/384 gecti.
- Message `HasBodyType` MIME part parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 46/46 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 386/386 gecti.
- Message `ClearRecipients` Bcc cleanup parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 46/46 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 386/386 gecti.
- Message `Save` missing-date parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 48/48 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 388/388 gecti.
- Message `AddRecipient` legacy MIME header format parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 48/48 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 388/388 gecti.
- `Attachments.Add` missing-file error parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 48/48 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 388/388 gecti.
- `Attachments.Item` bad-index parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 48/48 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 388/388 gecti.
- Recipient/header collection bad-index parity dilimi icin dort hedefli VBScript/JScript testi 4/4 ve dar `WindowsScriptRuleExecutorTests` filtresi 48/48 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 388/388 gecti.
- Message `Body`/`HTMLBody` trailing-CRLF parity dilimi icin dort hedefli VBScript/JScript testi 4/4 ve dar `WindowsScriptRuleExecutorTests` filtresi 50/50 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 390/390 gecti.
- Attachment item stable-identity parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 50/50 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 390/390 gecti.
- External fetch UIDL ordered-map parity dilimi icin hedefli duplicate/out-of-order testi 1/1 ve dar `ExternalFetchProcessorTests` filtresi 18/18 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 390/390 gecti.
- External fetch legacy account-header parity dilimi icin hedefli script/receiver testi 1/1 ve dar `ExternalFetchProcessorTests` filtresi 18/18 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 390/390 gecti.
- External fetch negatif retention parity dilimi icin iki processor ve bir gercek VBScript hedefli testi 3/3, birlesik `ExternalFetchProcessorTests|WindowsScriptRuleExecutorTests` filtresi 70/70 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 392/392 gecti.
- External fetch elapsed-retention parity dilimi icin 47 saat/sil ve tam 24 saat/tut hedefli testleri 2/2, dar `ExternalFetchProcessorTests` filtresi 22/22 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 394/394 gecti.
- External fetch bos-RETR parity dilimi icin processor ve loopback TCP hedefli testleri 2/2, birlesik `ExternalFetchProcessorTests|TcpExternalFetchSessionFactoryTests` filtresi 58/58 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 396/396 gecti.
- External fetch duplicate configured-recipient-header parity dilimi icin hedefli test 1/1 ve dar `ExternalFetchProcessorTests` filtresi 24/24 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 397/397 gecti.
- External fetch malformed `Received for` recipient parity dilimi icin gecerli/bozuk hedefli testler 2/2 ve dar `ExternalFetchProcessorTests` filtresi 25/25 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 398/398 gecti.
- External fetch `Received for` token-casing parity dilimi icin lowercase/uppercase hedefli testler 2/2 ve dar `ExternalFetchProcessorTests` filtresi 26/26 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 399/399 gecti.
- External fetch sender-validation parity dilimi icin gecerli/255-karakter hedefli testler 2/2 ve dar `ExternalFetchProcessorTests` filtresi 27/27 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 400/400 gecti.
- External fetch alias-recipient dedup parity dilimi icin alias/duplicate-header hedefli testler 2/2 ve dar `ExternalFetchProcessorTests` filtresi 28/28 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 401/401 gecti.
- External fetch malformed-neighbor recipient parity dilimi icin quoted-comma hedefli test 1/1 ve dar `ExternalFetchProcessorTests` filtresi 29/29 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 402/402 gecti.
- External fetch whitespace-header gate parity dilimi icin whitespace/normal hedefli testler 2/2 ve dar `ExternalFetchProcessorTests` filtresi 30/30 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 403/403 gecti.
- JScript password-validation `Result.Parameter` parity dilimi icin default/reject hedefli testler 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 51/51 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 404/404 gecti.
- VBScript password-validation `Result.Parameter` parity dilimi icin VB/JScript default hedefli testler 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 52/52 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 405/405 gecti.
- Password-validation stored-account-password parity dilimi icin VB/JScript stored/attempted hedefleri 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 54/54 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 407/407 gecti.
- Legacy message-indexing COM kontrat dilimi icin once eksik `IInterfaceMessageIndexing` derleme hatasiyla kanitlandi; interface eklendikten sonra dar `MessageIndexingComContractTests` filtresi 2/2 gecti. Prereq kontrolu temizdi, Net10 build portable ve Windows COM-host assembly'lerini 0 uyari/0 hata ile uretti ve full Net10 testler 409/409 gecti.
- Message-indexing COM class/runtime-adapter dilimi icin eksik class/runtime once derleme hatasiyla kanitlandi; legacy CLSID/versioned ProgID/default-interface metadata'si ve v1/v2 delegasyonu icin dar `MessageIndexingComContractTests` filtresi 5/5 gecti. Sonraki authorization duzeltmesi direct parameterless activation'i legacy `E_ACCESSDENIED` davranisina cekti.
- SQL/service message-indexing runtime dilimi icin store-backed adapter, SQL command sekilleri, authorized host factory ve disabled backfill gate testleriyle birlesik dar filtre 14/14 gecti. Prereq kontrolu temizdi, Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu ve full Net10 testler 418/418 gecti.
- Service-process COM local-server host dilimi once eksik host API derleme hatasiyla kanitlandi; registry'siz process activation/revoke testi ve mevcut COM contract testleri 7/7 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 419/419 gecti.
- COM Application/auth-root dilimi once eksik contract derleme hatasiyla kanitlandi; legacy metadata/vtable, credential ve process activation testleri 11/11 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 429/429 gecti.
- Account Rules COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 493/493 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build 0 uyari/0 hata ile basarili oldu.
- Account IMAPFolders COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path kapsami legacy modified UTF-7 ornekleri ve root/account filtreleriyle 10/10, full Net10 testler 500/500, opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings PublicFolders COM dilimi once authorized Settings yolunda `E_NOTIMPL` ile kanitlandi; dar Settings/folder/SQL-path filtresi 13/13, full Net10 testler 501/501 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings Routes COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 507/507 ve opt-in izole LocalDB integration testleri duzeltilen sequential-reader ordinal sirasi sonrasinda 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Application global Rules COM dilimi once `Application.Rules` yolunda `E_NOTIMPL` ile kanitlandi; dar Application/SQL-path filtresi 9/9, full Net10 testler 508/508 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings IncomingRelays COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 514/514 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings TCPIPPorts COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 10/10, full Net10 testler 521/521 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings SecurityRanges COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 527/527 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings SSLCertificates COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 533/533 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings Groups COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 539/539 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Group Members COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/Groups/manifest/SQL-path filtresi 14/14, full Net10 testler 545/545 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Application Utilities COM dilimi once eksik contract/enum/class derleme hatasiyla kanitlandi; dar Utilities/Application/manifest/process-host filtresi 21/21, full Net10 testler 582/582 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Application Links COM dilimi once eksik contract/class/runtime derleme hatasiyla kanitlandi; dar Links/Application/manifest/process-host filtresi 23/23, full Net10 testler 589/589 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- 28 Haziran security revalidation filtresi VBScript/JScript password, delivery/external-UID escaping, administrator authentication ve ClamAV kapsamini birlikte 10/10 gecti.
- DKIM header crypto dilimi icin dar `DkimEvaluationTests` filtresi 23/23 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 655/655 gecti.
- DKIM DNS/public-key lookup dilimi icin dar `DkimEvaluationTests` filtresi 31/31 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 663/663 gecti.
- DKIM message-level verifier dilimi icin dar `DkimEvaluationTests` filtresi 37/37 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 669/669 gecti.
- DKIM disabled SMTP policy boundary dilimi icin dar `SmtpDkimPolicyTests` filtresi 5/5 ve receiver DKIM filtresi 2/2 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 676/676 gecti.

Terminal/log incelemesi:

- Aktif terminalde eski basarisiz build/test ciktisi yoktu.
- `hmailserver/source/Server.Net10/tests/HMailServer.Net10.Tests/TestResults` altinda MSTest deploy klasorleri var, fakat `.trx` sonuc dosyasi bulunmadi.
- `<workspace-root>\build-logs` altinda son gorunen loglar OpenSSL/PostgreSQL dependency build loglari; Net10 test/build failure logu degil.

## Bilinen Riskler

- Dirty WIP kod dosyalari tamamlanmadan commit/push edilmemeli.
- Script host parity hassas: VBScript/JScript quoting, CR/LF sanitization, temp dosya lifecycle, fail-open/fail-closed semantikleri ve legacy `Result.Value` anlamlari kolay bozulabilir.
- Delivery queue degisiklikleri duplicate delivery, mail kaybi veya yanlis bounce uretebilir.
- SQL FTS/search degisiklikleri hot path performansini ve data-directory fallback davranisini etkileyebilir.
- Anti-abuse kontrollerinde DNS/SQL/socket timeout kararlari mail kabulunu durdurabilir; dokumante edilen fail-open/fail-closed politikaya bagli kal.
- COM/GUID/ProgID/DISPID degisiklikleri Administrator ve dis otomasyonlari kirar.
- Migration DDL'i additive kalmali; eski hMailServer DB'lerinde destructive veya implicit data conversion riski alinmamali.

## Dokunulmamasi Gereken Hassas Alanlar

- Legacy C++ davranisi referans olarak okunmali; uyumluluk amacli degilse gereksiz degistirilmemeli.
- `hmailserver/source/DBScripts/Upgrade5708to6000MSSQL.sql`
- `hmailserver/source/Server.Net10/src/HMailServer.ComInterop`
- Script executor ve facade dosyalari, ozellikle legacy event/object sozlesmeleri.
- IMAP/SMTP/POP3 parser/session hot path'leri.
- Delivery lease/retry/bounce/status persistence kodlari.
- SQL-backed mailbox/search/indexing store'lari.

## Siradaki Onerilen 3 Milestone

1. Legacy script object model parity tamamlama.
   - EventLog.Write tamamlandi; bundan sonra eksik global objeler, account/domain/application facade metodlari ve script collection davranislari legacy testlerle kapatilmali.

2. COM/Admin ve migration operasyonlarini production gate'e tasima.
   - GUID/ProgID/DISPID sozlesmeleri icin compatibility testleri.
   - In-place upgrade runner, backup zorunlulugu, rollback-from-backup akisi, service install/uninstall ve operator dokumani.

3. Security + performance acceptance.
   - DKIM signing/setter/Admin mutation wiring ve DMARC enforcement/Admin policy wiring.
   - SQL Server FTS integration ve 100k mailbox SEARCH/SORT p95 hedefi.
   - 1k concurrent IMAP, SMTP queue latency, delivery throughput ve uzun soak memory/handle testleri.

## Yeni Thread Icin Baslangic Talimati

1. Repo kokune gec: `<repo-root>`.
2. `README.md`, `hmailserver/source/Server.Net10/README.md`, `hmailserver/source/Server.Net10/REWRITE_BACKLOG.md`, `AGENTS.md` ve bu dosyayi oku.
3. `git status --short --branch` ve `git diff` calistir; mevcut WIP kod degisikliklerini sahiplenmeden once anla.
4. Net10 on kosullari dogrula:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\check-net10-prereqs.ps1 -RequireMsBuild
```

5. Current Next Slice olarak authenticated read-only SMTP guardrail getter'larini ele al: `Settings.MaxMessageSize`, `MaxSMTPRecipientsInBatch`, `DisconnectInvalidClients`, `MaxNumberOfInvalidCommands`; existing `maxmessagesize`/`maxsmtprecipientsinbatch`/`disconnectinvalidclients`/`maximumincorrectcommands` `hm_settings.settinginteger` satirlarini kullan, mevcut vtable/DISPID ve `VARIANT_BOOL` marshaling'i koru, setter/live SMTP session-listener policy kapsamlarini acma.
6. Kucuk kod/test commit'i yap, sonra README/backlog/handoff dokumanlarini ayri committe guncelle; en gec her 10 committe bir push yap ve bu iki commitlik landing sonunda push ederek branch'i temiz birak.
