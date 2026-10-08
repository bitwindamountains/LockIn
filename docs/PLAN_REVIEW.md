# Plan review — 2026-09-27

The specification describes a staged Windows desktop application, independent background service, native bridge, and Manifest V3 extension. The key dependency is safe session expiry: real restrictions must not begin before durable authority and release paths are proven. M0–M2 therefore form a simulation slice before website or app enforcement.

The development machine is Windows 10 Pro 19045 x64, with Chrome 154 and Edge 154. The plan targets Windows 11 first. This checkout can verify portable logic and WPF compilation, but it cannot establish Windows 11 compatibility or real service/Chrome behavior. The installed .NET SDK was absent initially and was installed locally for development. No LockIn service or browser host has been installed.

The simulation intentionally uses a current-user console host and pipe. This gives the desktop a separate session authority while avoiding installation side effects. M3 must replace or adapt this transport with explicit native-host enrollment and authenticated browser-role requests before any real website rule is activated. M5 must address the Windows service identity and process-owner boundary; current-user pipe options are a development-only choice.

The plan's M1 exit gate also calls for monotonic elapsed-time checks, corruption recovery, and a full activation journal. The current slice has UTC checkpoints, backward-clock anomaly release, and an `Activating` recovery state, but those deeper failure cases remain open. They must be completed before real enforcement, especially persistent Windows policy work.

