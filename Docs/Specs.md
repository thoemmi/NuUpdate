# Specifications

## Requirements
- no certificate
  - no MSI
  - no ClickOnce&trade;
- only dependencies which are absolutely necessary (i.e. no Reactive Extensions)

## Components 

### Installer

- Check if already installed
- Package name and update URL are embedded as Win32 resources (ID 42)
- Package is embedded as Win32 resource (ID 43)
- optionally look for newer packages
- copy Uninstaller to package folder
- create an uninstall entry in registry
- create shortcuts in Start Menu

### Update component 

- check for updates
- delete old version
- download updates
- update shortcuts
- update pinned executables
- update Uninstaller

### Uninstaller

- repair (optionally)
- uninstall
  - ensure that the uninstaller itself will be removed
  


  
## Open Issues

- should the uninstaller be updatable?
  - no: the installer copies it to _%LOCALAPPDATA%\%APPNAME%_
  - yes: the installer copies it to _%LOCALAPPDATA%\%APPNAME%\&lt;version&gt;_
  Also, each package must include the installer