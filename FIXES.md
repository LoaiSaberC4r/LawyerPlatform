# Corrective Patch

This revision fixes the initial template build blockers:

- Makes `global.json` compatible with installed stable .NET 10 feature bands.
- Updates OpenAPI response content dictionaries for Microsoft.OpenApi 3.x interfaces.
- Removes the unnecessary `System.IdentityModel.Tokens.Jwt` dependency from validated-claims reading.
- Moves `ITransactionalCommand<TWriteMarker>` into the persistence abstraction namespace.
- Renames the cache `Ttl` contract to `TimeToLive`.
- Uses source-generated logging for the sample domain-event handler.
- Pins `SQLitePCLRaw.lib.e_sqlite3` to 2.1.12 to avoid the advisory affecting 2.1.11.

After extracting the corrected archive, delete old `bin`, `obj`, and `.vs` directories before restoring.
