# Installer/service/Data rollback preflight

- Overall result: `ENVIRONMENT-BLOCKED`
- Archive/source preflight: `PASS`
- Actual installer drill: `ENVIRONMENT-BLOCKED`
- hMailServer service present before/after: `False` / `False`
- Disposable registered legacy service/COM baseline: `False`
- Machine mutation performed: `False`
- Production targets used: `False`

The safe preflight validates archive structure, bounded reads, process limits,
service snapshot parsing, installer ordering, uninstaller compensation, and
PowerShell syntax. The actual installer drill remains blocked until a
disposable registered legacy service/COM baseline and isolated SQL/Data clone
are available. No service, registry, COM, DCOM, SQL, or Data-directory
mutation was performed.