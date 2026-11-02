Uptime MSIX package for Microsoft Partner Center

The identity values below were read from Partner Center > Product management > Product identity and are embedded in the generated manifest:

  Package/Identity/Name: ILDENTEPROIBITO.PCUptime
  Package/Identity/Publisher: CN=12963AAB-4B76-4EDC-AF0B-48E5531EDA21
  Package/Properties/PublisherDisplayName: IL DENTE PROIBITO

Generated package:

  Uptime-1.0.0.0-x64.msix

If the product already has a submitted package, set a higher version before generating another package:

  .\build-msix.ps1 -IdentityName "ILDENTEPROIBITO.PCUptime" -Publisher "CN=12963AAB-4B76-4EDC-AF0B-48E5531EDA21" -PublisherDisplayName "IL DENTE PROIBITO" -Version "<higher-version>"

If this app already has a package in the Store, choose a version greater than the currently submitted package version. The generated package is x64 and self-contained. The Store signs the MSIX after certification; no local publisher certificate is needed for Store submission.

Sources: artifacts\Uptime-Release. The pack script omits PDBs from the installation payload and includes the required Store and tile logo PNGs.
