# Uptime — ricostruzione

Requisiti: .NET 10 SDK, Windows 10 1809+.

```
dotnet test
dotnet run --project src/Uptime.Presentation
```

## Struttura
- `src/Uptime.Core` — logica pura: uptime, salute, analisi sessioni/stabilità, salvataggio atomico, localizzazione completa IT/EN.
- `src/Uptime.Infrastructure` — Win32: uptime, inattività, registro eventi, alimentazione (shutdown.exe), Fast Startup, avvio con Windows.
- `src/Uptime.Presentation` — Avalonia: Monitor, Stabilità, Operazioni.
- `tests/Uptime.Core.Tests` — xUnit.

## Fatto
Monitor, Stabilità (sessioni, incidenti, export JSON/CSV), Operazioni (riavvio pulito, spegnimento completo,
riavvio al BIOS con conferma, timer, Fast Startup, avvio con Windows), Impostazioni (selettore lingua, tre widget e preset),
tray con menu, barra titolo personalizzata, Mini-HUD, badge copiabile, scorciatoia globale Win+Alt+U, record personale locale ed evento 1074.

## Da fare
Attivazione effettiva del promemoria notturno, 12 widget configurabili e cartella Esecuzione automatica/StartupApproved.
Le lingue complete disponibili sono italiano e inglese; altre lingue non compaiono nel selettore finché la traduzione non è completa.
Il Mini-HUD è una finestra sempre in primo piano; il badge viene copiato come testo. I preset sono Completo, Compatto e Focus.

## Da verificare sul PC
Pubblicazione Windows x64 Release self-contained: `artifacts/Uptime-Release`. Restano da verificare sul PC `shutdown /s /full`,
la lettura del registro System senza privilegi e il prompt UAC per Fast Startup.
