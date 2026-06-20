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
- Modern security slice'lari eklendi: ClamAV, SpamAssassin, spam policy, attachment blocking, DNSBL, reverse DNS/PTR, sender-domain MX, greylisting, SURBL ve failed-logon auto-ban.
- Legacy script/event parity buyuk olcude ilerledi: `OnClientConnect`, `OnClientValidatePassword`, `OnClientLogon`, `OnHELO`, `OnRecipientUnknown`, `OnSMTPData`, `OnAcceptMessage`, `OnTooManyInvalidCommands`, delivery eventleri, `OnDeliveryFailed`, `OnError`, rule `ScriptFunction`, mesaj/recipient/attachment facade'leri, client `Authenticated`/`EncryptedConnection` alias'lari ve account-rule `Message.Copy(folderId)`.

## Production-Ready Seviyesi

Durum: production-ready degil. Proje ciddi bir parity seviyesine geldi, fakat halen side-by-side rewrite/test hatti olarak ele alinmali.

Ana nedenler:

- COM/Admin yuzeyi ve legacy object model henuz tam degil.
- SPF/DKIM/DMARC eksik.
- Backup engine ve `OnBackupCompleted` / `OnBackupFailed` eventleri beklemede.
- In-place upgrade runner, mandatory backup/rollback akis dokumani ve operasyonel servis install/uninstall paketi tamamlanmadi.
- Buyuk olcekli performance/soak kabul testleri henuz production gate olarak tamamlanmadi.

## Kalan Kritik Backlog

- Full legacy script object model parity.
- Backup engine tasarimi ve backup completed/failed eventlerinin gercek engine uzerinden baglanmasi.
- Active Directory auth, master user ve daha derin account facade collections/methods.
- SPF, DKIM, DMARC.
- COM/API compatibility: mevcut GUID/ProgID/DISPID/type library sozlesmelerinin tam korunmasi ve Administrator-visible nesnelerin tamamlanmasi.
- Migration/operations: in-place upgrade runner, mandatory backup checks, rollback-from-backup dokumani, orphan cleanup, health/metrics/logging, Windows Service install/uninstall.
- SQL Server FTS integration testleri ve production acceptance: 100k mailbox SEARCH/SORT, 1k IMAP connection, SMTP queue latency, memory/handle leak soak.
- External fetch edge-case parity.

## Current Next Slice

Backlog'daki siradaki ana dilim: legacy script object parity'yi `Message.Copy(folderId)` ve global `EventLog.Write(value)` otesinde tamamlamak, backup eventlerini .NET backup engine gelmeden synthetic callback olarak uretmemek, delivery status/bounce template parity'yi queue worker evrildikce korumak ve external fetch edge-case'lerini kapatmak.

Son tamamlanan kucuk dilimler:

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

Bu dokuman guncellemesi baslamadan once bilinen origin head:

```text
f218e3a6a docs(net10): document received recipient validation
```

Son 30 commit icinde one cikan son dilimler:

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
   - SPF/DKIM/DMARC.
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

5. Current Next Slice olarak legacy script object parity, external fetch edge-case parity veya COM/Admin compatibility tarafindan en kucuk testlenebilir dilimi sec. EventLog.Write tamamlandigi icin ayni alana geri donulacaksa once yeni eksik legacy davranisi kanitlayan test yaz.
6. Kucuk kod/test commit'i yap, sonra README/backlog/handoff dokumanlarini ayri committe guncelle ve tek push ile gonder.
