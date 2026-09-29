# ADR-0001: Target .NET 10 LTS instead of .NET 8

- Status: **Accepted** (2026-09-29, @simon)
- Date: 2026-09-29

## Context

The planning note specifies .NET 8. .NET 8 (LTS) reaches end of support on **10 November 2026**, about six weeks after this project starts. .NET 10 is the current LTS release (Nov 2025), supported until **November 2028**.

## Decision

Build PDS on **.NET 10 / C# 14 / EF Core 10**.

## Consequences

- The system starts on a supported runtime with roughly two years of runway, and there's no migration straight after launch.
- Developers need the .NET 10 SDK. The dev machine currently has 8.0.404 and 9.0.101 installed.
- The Lambda functions use the .NET 10 managed runtime. The implementation plan checks this is available in the target region. If it isn't, it falls back to a container-image Lambda.
- Reverting to .NET 8 would only mean changing `TargetFramework` and package versions. No design decision depends on .NET 10 features.
