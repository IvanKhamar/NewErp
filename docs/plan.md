# Plan

Open items of this repo that are not in `docs/from-python.md` (features built in Python first, to port here).

## Move to the shared Docker database (planned 2026-11-06, not started)

All three versions should use one MySQL: the Docker one from NewErpPython (`F:\PROJECTS\NewErpPython\NewErpPython`,
`docker compose up -d mysql`, port 3307, database `newerp_dev`). Today this repo uses Windows `MySQL80`, which is behind:
86 tables against 153 in Docker on 2026-11-06.

1. Connection string: change the port and password in `dotnet user-secrets` only, never in `appsettings*.json`.
2. dbhub: create the read-only user `claude_ro` in the Docker server and point `F:\PROJECTS\NewErp\dbhub.toml` at it.
3. Check that the API, `tools/NewErp.DevSeed` and `tools/NewErp.SchemaCheck` work against it, and that the mappings fit
   the Docker tables. The `db/reconstructed.sql` files differ until the groups in `docs/from-python.md` are ported.
4. Update the README and `CLAUDE.md`. Ask the user what happens to `newerp_dev` in `MySQL80` (it holds only invented data).

## Rule change: `dd_*` tables may be changed (2026-11-06)

`dd_` is only the prefix of the old project, so these tables may be changed when a feature needs it. A change goes into
`db/*.sql` of all three repos. No real shop runs on these tables.
