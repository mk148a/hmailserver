# SEC-18 Installed Application Graph Live Evidence

- Capture: 07/18/2026 14:02:58
- Host: `NOUTML-KANDIL`
- Read-only: `True`
- COM activation attempted: `False`
- Production database/data directory accessed: `False/False`
- hMailServer service: `Stopped`, start type `Disabled`
- Graph: `22` paths x 2 views = `44` before and `44` after snapshots
- Read errors: `0` before, `0` after
- Immediate before/after native-byte comparison: `True`
- Complete readback: `True`
- Registry32 installed Application CLSID root present: `False` (expected asymmetry)

The JSON report contains exact native `RegQueryValueEx` value bytes as base64, registry value kinds, key presence, and read-error state. No COM activation, registration, ACL write, service start, database access, or data-directory access was performed.

| View | Key path | Present | Values (name:kind) | Read error |
|---|---|---:|---|---|
| Registry32 | `Software\Classes\AppID\{5EDEC473-39E0-43F6-A234-1947071721C8}` | True | :1, LocalService:1 |  |
| Registry32 | `Software\Classes\AppID\hMailServer.EXE` | True | AppID:1 |  |
| Registry32 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}` | False |  |  |
| Registry32 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\LocalServer32` | False |  |  |
| Registry32 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\ProgID` | False |  |  |
| Registry32 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\Programmable` | False |  |  |
| Registry32 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\TypeLib` | False |  |  |
| Registry32 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\VersionIndependentProgID` | False |  |  |
| Registry32 | `Software\Classes\hMailServer.Application` | True | :1 |  |
| Registry32 | `Software\Classes\hMailServer.Application.1` | True | :1 |  |
| Registry32 | `Software\Classes\hMailServer.Application.1\CLSID` | True | :1 |  |
| Registry32 | `Software\Classes\hMailServer.Application\CLSID` | True | :1 |  |
| Registry32 | `Software\Classes\hMailServer.Application\CurVer` | True | :1 |  |
| Registry32 | `Software\Classes\Interface\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}` | True | :1 |  |
| Registry32 | `Software\Classes\Interface\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}\ProxyStubClsid32` | True | :1 |  |
| Registry32 | `Software\Classes\Interface\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}\TypeLib` | True | :1, Version:1 |  |
| Registry32 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}` | True |  |  |
| Registry32 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0` | True | :1 |  |
| Registry32 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\0` | True |  |  |
| Registry32 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\0\win64` | True | :1 |  |
| Registry32 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\FLAGS` | True | :1 |  |
| Registry32 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\HELPDIR` | True | :1 |  |
| Registry64 | `Software\Classes\AppID\{5EDEC473-39E0-43F6-A234-1947071721C8}` | True | :1, LocalService:1 |  |
| Registry64 | `Software\Classes\AppID\hMailServer.EXE` | True | AppID:1 |  |
| Registry64 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}` | True | :1, AppID:1 |  |
| Registry64 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\LocalServer32` | True | :1 |  |
| Registry64 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\ProgID` | True | :1 |  |
| Registry64 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\Programmable` | True |  |  |
| Registry64 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\TypeLib` | True | :1 |  |
| Registry64 | `Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\VersionIndependentProgID` | True | :1 |  |
| Registry64 | `Software\Classes\hMailServer.Application` | True | :1 |  |
| Registry64 | `Software\Classes\hMailServer.Application.1` | True | :1 |  |
| Registry64 | `Software\Classes\hMailServer.Application.1\CLSID` | True | :1 |  |
| Registry64 | `Software\Classes\hMailServer.Application\CLSID` | True | :1 |  |
| Registry64 | `Software\Classes\hMailServer.Application\CurVer` | True | :1 |  |
| Registry64 | `Software\Classes\Interface\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}` | True | :1 |  |
| Registry64 | `Software\Classes\Interface\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}\ProxyStubClsid32` | True | :1 |  |
| Registry64 | `Software\Classes\Interface\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}\TypeLib` | True | :1, Version:1 |  |
| Registry64 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}` | True |  |  |
| Registry64 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0` | True | :1 |  |
| Registry64 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\0` | True |  |  |
| Registry64 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\0\win64` | True | :1 |  |
| Registry64 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\FLAGS` | True | :1 |  |
| Registry64 | `Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\HELPDIR` | True | :1 |  |

- Repository HEAD at capture: `673c3bafcbafb29d935bc24135d8d3d37436ccb0`
- Collector: in-memory PowerShell `Add-Type` native `RegQueryValueEx` reader; checked-in attestation: `False`
- Canonical expected contents validated: `False`; unknown subkeys enumerated: `False`; key security descriptors captured: `False`
- Independent security review: `RED`; independent reality review: `RED`
- Gate decision: `RED`, do not proceed to isolated COM integration.
- Remaining blockers: canonical expected-value validation, recursive unknown-subkey detection, approved asymmetry assertions for all six Registry32 CLSID-subtree keys, checked-in deterministic collector/attestation, and native-reader integration tests.
