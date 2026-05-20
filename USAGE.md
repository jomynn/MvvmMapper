# MvvmMapper — Usage Guide

Practical examples for running `MvvmMapper.Cli.exe` (or `mvvm-map`) from the console.

---

## 1. Running the Tool

### A. As a dotnet global tool (recommended)

Install once:
```cmd
dotnet tool install -g MvvmMapper --add-source .\nupkg
```

Then run from anywhere:
```cmd
mvvm-map scan C:\Projects\MyWpfApp
```

### B. Direct executable (no install required)

After `dotnet build`, the exe is in the build output:
```cmd
src\MvvmMapper.Cli\bin\Debug\net8.0\MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp
```

Or with a Release build:
```cmd
dotnet build -c Release
src\MvvmMapper.Cli\bin\Release\net8.0\MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp
```

### C. Via dotnet run (source, no build step needed)

```cmd
dotnet run --project src\MvvmMapper.Cli -- scan C:\Projects\MyWpfApp
```

> The `--` separates `dotnet run` options from the tool's own arguments.

### D. Via dotnet dll

```cmd
dotnet src\MvvmMapper.Cli\bin\Debug\net8.0\MvvmMapper.Cli.dll scan C:\Projects\MyWpfApp
```

---

## 2. Scan Command Syntax

```
MvvmMapper.Cli.exe scan <path> [options]
```

| Argument / Option  | Default              | Description                                              |
|--------------------|----------------------|----------------------------------------------------------|
| `<path>`           | *(required)*         | Path to a `.sln`, `.csproj`, or folder                  |
| `--output`         | `./mvvm-map-output`  | Directory where output files are written                 |
| `--format`         | `all`                | `html`, `json`, `mermaid`, or `all`                     |
| `--confidence`     | `low`                | Minimum edge confidence: `high`, `medium`, or `low`     |
| `--config`         | `./mvvm-map.json`    | Path to a custom config file                             |
| `--watch`          | `false`              | Re-scan on every `.cs` / `.xaml` file change            |
| `--verbose`        | `false`              | Enable debug-level logging                               |

---

## 3. Common Examples

### Scan a solution folder

```cmd
MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp
```

Output is written to `.\mvvm-map-output\` by default:
```
mvvm-map-output\
  report.html          ← open this in a browser
  graph.json
  mermaid-by-view.md
  mermaid-by-vm.md
  mermaid-by-endpoint.md
  mermaid.min.js
```

---

### Scan a specific .sln file

```cmd
MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp\MyWpfApp.sln --output C:\Reports\MyWpfApp
```

---

### Scan a single .csproj

```cmd
MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp\src\MyApp\MyApp.csproj --output C:\Reports\MyApp
```

---

### HTML report only

```cmd
MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp --output C:\Reports --format html
```

---

### JSON only (for CI pipelines or scripting)

```cmd
MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp --output C:\Reports --format json
```

---

### High-confidence edges only (ignore naming-convention guesses)

```cmd
MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp --output C:\Reports --confidence high
```

---

### Multiple formats at once

```cmd
MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp --output C:\Reports --format html|json
```

---

### Watch mode — re-scan on every file save

```cmd
MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp --output C:\Reports --watch
```

Press `Ctrl+C` to stop.

---

### Verbose logging (useful for debugging resolver results)

```cmd
MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp --output C:\Reports --verbose
```

---

### Use a custom config file

```cmd
MvvmMapper.Cli.exe scan C:\Projects\MyWpfApp --config C:\Config\myapp-mvvm.json --output C:\Reports
```

---

### Write a default config to the current directory

```cmd
MvvmMapper.Cli.exe init
```

Creates `mvvm-map.json` in the working directory. Edit it to match your project's conventions.

---

## 4. Version & Help

```cmd
MvvmMapper.Cli.exe --version
MvvmMapper.Cli.exe --help
MvvmMapper.Cli.exe scan --help
```

---

## 5. Reading the Console Output

After each scan the tool prints a summary:

```
Scan complete. Output written to: C:\Reports
  Nodes : 18
  Edges : 24
  Shared VMs      : 1
    AuthViewModel (fan-in=3)
  Report: C:\Reports\report.html
  JSON  : C:\Reports\graph.json
  MD    : C:\Reports\mermaid-by-view.md
```

- **Nodes** — total Views, ViewModels, Methods, Services, and Endpoints found
- **Edges** — total relationships (BindsTo, Contains, Invokes, Hits, Calls)
- **Shared VMs** — ViewModels bound by 2 or more Views (design smell indicator)

---

## 6. Interpreting the HTML Report

Open `report.html` in any browser — no internet connection required.

| Tab | Shows |
|-----|-------|
| **By View** | Each View → its ViewModel → confidence level → endpoints reachable |
| **By ViewModel** | Each ViewModel → bound Views → fan-in count → shared flag |
| **By Endpoint** | Each HTTP endpoint → verb + route → Views that can reach it |
| **Diagrams** | Mermaid flowcharts rendered offline (one per View) |

Use the **search box** to filter rows by any text, and the **confidence dropdown** to show only High, High+Medium, or all edges.

---

## 7. Typical CI Usage

```yaml
# GitHub Actions example
- name: Scan WPF solution
  run: |
    dotnet tool install -g MvvmMapper --add-source ./nupkg
    mvvm-map scan ./src --output ./mvvm-report --format json --confidence medium

- name: Upload report
  uses: actions/upload-artifact@v4
  with:
    name: mvvm-report
    path: ./mvvm-report/
```

---

## 8. Sample Projects

Two ready-to-scan samples are included in the repo:

```cmd
# Naming convention pattern (5 Views, 5 ViewModels)
MvvmMapper.Cli.exe scan samples\simple-mvvm --output C:\Reports\simple

# Shared ViewModel pattern (3 Views → 1 AuthViewModel, fan-in=3)
MvvmMapper.Cli.exe scan samples\shared-vm --output C:\Reports\shared
```

Expected output for `shared-vm`:
```
Nodes : 13  |  Edges : 12
Shared VMs : 1
  AuthViewModel (fan-in=3)
```
