# Installer

The per-user Inno Setup script (`QuickEmoji.iss`) lives here from Stage 7. It installs without admin rights and removes the HKCU Run value on uninstall (SPEC §7, §10). `build-release.ps1` in the repository root compiles it.
