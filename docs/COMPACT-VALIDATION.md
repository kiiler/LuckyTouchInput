# Compact validation — 2026-10-14

## Prototype evidence

The self-contained prototype was built with .NET SDK 10.0.402 on Windows 11 x64.
Release compilation completed with zero warnings and zero errors.
The existing suite passed 350 checks. The prototype harness passed 165 checks.

That interactive harness used a separate test-owned text box and actual injected mouse clicks.
It verified committed ASCII text, digits, Backspace, one-shot Shift, focus retention,
dragging, grip resizing, collapse/expand, rapid repeated keys, configuration round trips,
a fresh form restoring saved bounds, and unchanged target-window geometry.
The probe disabled its own IME; this is not Chinese composition or candidate acceptance.

The user subsequently tried the installed prototype through a phone and reported that
initial verification passed. No duration, error rate, client matrix or fatigue measurement
was recorded. Treat this as preliminary feedback, not broad compatibility certification.

## Repository integration

The product and tests are now separate projects. The original entry point is retained.
The compact executable no longer includes the test probe or a self-test command.
The compact default suite passed 134 checks without input injection or visible windows.
The original suite passed 350 checks after project separation.
Both Standard and Compact self-contained win-x64 packages built successfully.
Each archive passed the package checks, including bundled licenses, excluded private
state and resolution of local documentation links.

The prototype's 165-check interactive result describes the pre-separation executable.
It does not claim a new interactive run of the repository build. The phone-tested executable
installed on the user's machine was not replaced during documentation and GitHub integration.

## Limits

Not yet established: long-running stability, Chinese candidate workflows, every DPI/monitor
transition, native touch gestures, remote-client combinations, secure/elevated input,
coexistence with every Windows keyboard, or quantitative ergonomic improvement.
The complete upstream disposable-VM acceptance suite was not run on the daily-use desktop.

## Reproduction

Follow [the development guide](COMPACT-DEVELOPMENT.md) for fast checks, opt-in interactive
checks, packaging and the phone acceptance checklist. Preserve the distinction between
control-layout renders, real desktop interaction, and user-reported phone behavior.
