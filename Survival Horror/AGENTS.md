# Project organization rules

- Read `Docs/AssetOrganization.md` before adding or moving assets.
- Put owned runtime content in `Assets/Game`, grouped by feature or asset family. Keep each weapon/enemy's related content together; share only genuinely reused content.
- Put experiments and test scenes in `Assets/Sandbox`. Sandbox is a navigation boundary, not an automatic build exclusion.
- Preserve asset GUIDs: use Unity asset moves and keep `.meta` files. Update builder paths, build scenes, Resources keys, and documentation together.
- Do not move or delete vendor examples without checking dependencies. Document necessary vendor edits.
- Keep sources and provenance in `ArtSource`; shared automation in `Tools`; review outputs in ignored `Exports/Reviews`.
- Do not add numbered backups, temporary validation scripts, captures, or logs to runtime asset folders. Do not delete existing variants solely because their names look obsolete.
- Name new assets by subject and role. Create subfolders only when needed. Avoid global catch-all Materials, Scripts, or Misc folders.
