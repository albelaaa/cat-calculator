# CAT Exam Calculator

A compact Windows desktop calculator styled after the supplied CAT-style reference. The fixed client area is 342 × 421 pixels. It has a built-in elapsed-time stopwatch, two displays, the requested key layout, memory, arithmetic, percent, square root, reciprocal, sign toggle, clear, and backspace.

## Run

On Windows 10 or 11, double-click `CATCalculator.exe`. It is a single executable with no Python, Node.js, browser, server, or separate application install. It uses the Windows .NET Framework 4.x desktop libraries that are part of standard Windows installations.

## Build

The complete C# source and manifest are included. Open PowerShell in this folder and run:

```powershell
.
build.ps1
```

The script uses the Windows .NET Framework C# compiler and writes `CATCalculator.exe` beside the source. The compiler is normally available with Windows developer tools or Visual Studio Build Tools.

## Controls

- Click the on-screen buttons or enter digits and `+`, `-`, `*`, `/`, `%` from the keyboard.
- `Enter` or `=` evaluates; `Backspace` deletes one digit; `Esc` or `Delete` clears.
- `F9` changes the sign, `R` calculates the square root, and `I` calculates the reciprocal.
- Click the stopwatch to pause or resume it. Right-click the stopwatch to reset it.

Percent follows standard four-function calculator behavior: for example, `200 + 10 % =` gives `220`. Division by zero and square roots of negative values display an error.
