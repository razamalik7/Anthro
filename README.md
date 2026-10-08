# Anthro

Working title. A society/evolution sim where discoveries come from real
preconditions instead of a tech tree.

## Layout

- `Anthro.Sim/` - the simulation. netstandard2.1, C# 9, **no Unity references**.
  Later dropped into Unity unchanged; Unity only renders its events.
- `Anthro.Runner/` - console app that runs the sim headless and writes logs.
- `Anthro.Tests/` - sanity tests.
- `docs/PLANNING.md` - design decisions. Nothing gets built until it's decided there.

## Commands

    dotnet build
    dotnet run --project Anthro.Runner
    dotnet test
