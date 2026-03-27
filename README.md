# ShiftyGrid

**Grid-based window management driven by keyboard shortcuts and rules**

ShiftyGrid is a keyboard-driven window manager for Windows that divides your monitor into a grid system for precise, reproducible window positioning. Define custom shortcuts and window organization rules in YAML to automate your entire window management workflow.

## Is ShiftyGrid for You?

ShiftyGrid is ideal if you:
- Spend significant time juggling multiple windows and want to organize them faster
- Prefer keyboard shortcuts over mouse interactions
- Want reproducible window layouts that work across different monitor resolutions
- Need to automatically position windows based on application type
- Want fine-grained control over which windows get managed

## Quick Start

1. **Download & Run**: Start the ShiftyGrid server
   ```bash
   ShiftyGrid start
   ```

2. **Configure**: Edit `config.yaml` to define your keyboard shortcuts and window rules

3. **Use**: Press your configured hotkeys to manage windows instantly

**Example**: `Ctrl+Alt+Left` to move the current window to the left half of your screen

---

## Features

- **Grid-Based Window Positioning**: Use intuitive grid coordinates (e.g., 12×12) instead of pixels for resolution-independent layouts
- **Keyboard-Driven**: Define custom shortcuts and modal modes for instant window operations
- **Window Organization Rules**: Automatically position windows based on window title, class, or process name
- **Window Ignore Rules**: Protect system windows and specific applications from being moved
- **Flexible Configuration**: Simple YAML configuration for all shortcuts, modes, and rules
- **Modal Shortcuts**: Group related commands under activation keys (e.g., press `Ctrl+Shift+D` to enter "Move Mode")
- **Window Arrangement**: Snap multiple windows into grid layouts with one hotkey
- **Window Navigation**: Focus or swap adjacent windows using arrow keys

>[!NOTE]
> **ShiftyGrid is not a tiling window manager**. It focuses on **quick manual window positioning** driven by keyboard shortcuts. The primary use case is user-initiated positioning—press a hotkey to move the current window where you want it. Window organization rules are available for automating positioning of specific applications (e.g., always put Firefox on the left), but automatic tiling of all windows is not the design goal.

---

## The Grid

ShiftyGrid divides your monitor into a grid of cells instead of using pixel-based positioning. Each window's position and size are defined by grid coordinates (e.g., `0,0,6,12`). This approach makes layouts reproducible and portable - the same coordinates work consistently across any monitor resolution. By default, ShiftyGrid uses a 12×12 grid.

### Coordinates

Each window position uses `startX,startY,endX,endY` to define the top-left and bottom-right corners. Top left corner of your monitor is the starting point (0,0).

```
     0   1   2   3   4   5   6   7   8   9  10  11  (X-axis / columns)
   +---+---+---+---+---+---+---+---+---+---+---+---+
 0 |                                               |
 1 |                                               |
 2 |                                               |
 3 |                                               |
 4 |                                               |
 5 |                                               |
 6 |                                               |
 7 |                                               |
 8 |                                               |
 9 |                                               |
10 |                                               |
11 |                                               |
   +---+---+---+---+---+---+---+---+---+---+---+---+
(Y-axis / rows)
```

### Grid Size

You can define it globally in config file

```yaml
general:
  grid: 12x12
```

or via `coordiantes` command parameter,

```bash
ShiftyGrid move --coordinates 0,0,12,24 --grid 24x24
```

Command-level grid overrides take priority over the global setting.

---

## Commands

#### `start`
Starts the ShiftyGrid server instance that monitors for keyboard shortcuts and executes window operations.

```bash
ShiftyGrid start [--config <path>] [--logs <path>] [--log-level <level>]
```

#### `move`
Move the foreground window to specified grid coordinates.

```bash
ShiftyGrid move --coordinates <x1,y1,x2,y2> [--grid <NxM>]
```
See [Configuration](#keyboard-shortcuts) for defining shortcuts that use this command.

#### `arrange`
Arrange visible windows on the current monitor in a grid layout, optionally constrained to a specific zone.

```bash
ShiftyGrid arrange [--rows <1-2>] [--cols <1-4>] [--zone <x1,y1,x2,y2>]
```

The `--zone` parameter lets you arrange windows within a specific rectangular region instead of the full screen. This is useful for multi-monitor setups or reserved screen areas. Zone coordinates use the same grid system as window positioning.

#### `swap <direction>`
Swap the foreground window with an adjacent window (`Left`, `Right`, `Up`, or `Down`).

```bash
ShiftyGrid swap <direction>
```

#### `focus <direction>`
Move focus to the adjacent window in the specified direction (like directional Alt+Tab).

```bash
ShiftyGrid focus <direction>
```

#### `resize`
Resize the foreground window by adjusting its borders. Adjacent windows are automatically resized to maintain the layout.

```bash
ShiftyGrid resize --direction <direction> [--outer]
```

**Example**: If you have two windows side-by-side (A|B) and expand window A to the right, window B automatically shrinks to make room. This uses the `proximity_threshold` setting to determine which windows are adjacent and should be resized together (see [Configuration](#configuration-structure)).

#### `promote`
Toggle promotion of the foreground window to specified coordinates. The window "remembers" its previous position and returns to it when unpromoted. Promoting a different window restores the previous promotion.

```bash
ShiftyGrid promote --coordinates <x1,y1,x2,y2> [--grid <NxM>]
```

#### `organize`
Organize windows according to predefined rules (see [Configuration - Window Organization Rules](#window-organization-rules)). Organizes only the foreground window by default.

```bash
ShiftyGrid organize [--all] [--window <hwnd>]
```
- `--all`: Organize all visible windows across all monitors
- `--window <hwnd>`: Organize a specific window by handle

#### `reload`
Reload the configuration file and apply changes without restarting the server.

```bash
ShiftyGrid reload
```

#### `status`
Display the status of the running ShiftyGrid server instance.

```bash
ShiftyGrid status
```

#### `exit`
Stop the running ShiftyGrid server instance.

```bash
ShiftyGrid exit
```

#### `about`
Display version information and project details.

```bash
ShiftyGrid about
```

---

## Configuration

ShiftyGrid is configured through a YAML file (`config.yaml`) that defines:
- General settings: Grid size, logging, gaps between windows
- Keyboard shortcuts: Global shortcuts and modal modes
- Window organization rules: Automatically position windows based on matching criteria
- Ignore rules: Exclude specific windows from management

### Configuration Structure

```yaml
general:
  gap: 10                       # Gap between arranged windows (pixels)
  grid: 12x12                   # Default grid size
  proximity_threshold: 20       # Distance in pixels to detect adjacent windows for resizing
  log_level: info               # Logging level

startup:
  commands:
    - "organize --all"          # Commands to run at startup

keyboard:
  shortcuts:
    - bindings: [ctrl+alt+left]
      command: swap left

  modes:
    - id: move_mode
      name: "Move Mode"
      activation: [ctrl+shift+d]
      shortcuts:
        - bindings: ["1"]
          command: move --coordinates 0,0,6,12

organize:
  rules:
    - match:
        process_name: "firefox.exe"
      command: move --coordinates 0,0,6,12

ignore:
  rules:
    - match:
        process_name: "dwm.exe"
```

### Keyboard Shortcuts

#### Global Shortcuts

Execute commands immediately with a hotkey. Define them like:

```yaml
keyboard:
  shortcuts:
    - bindings: [ctrl+alt+left, ctrl+shift+left]  # Multiple hotkeys for same command
      command: swap left
    - bindings: [ctrl+alt+=, ctrl+alt+plus]
      command: arrange --rows 1 --cols 2
```

Key combinations use format: `modifier+modifier+key` (e.g., `ctrl+alt+left`, `win+shift+1`)

#### Modal Shortcuts

Group related commands under a mode activation key. Once activated, mode shortcuts become available until you press Escape or execute a shortcut that exits the mode:

```yaml
keyboard:
  modes:
    - id: move_mode
      name: "Move Mode"
      activation: [ctrl+shift+d]        # Press to enter mode
      allow_escape: true                # Press Escape to exit
      timeout_ms: 5000                  # Auto-exit after 5 seconds
      shortcuts:
        - bindings: ["1"]
          command: move --coordinates 0,0,6,12
          exit_mode: true               # Exit mode after executing
        - bindings: ["2"]
          command: move --coordinates 6,0,12,12
```

### Window Matching

Window organization rules and ignore rules use **window matching** to identify which windows to affect. Match using:
- `title_pattern`: Match window title
- `class_name`: Match window class name
- `process_name`: Match executable name

Within a single rule, all specified fields must match (AND logic). Multiple rules use OR logic.

#### Pattern Types

**Substring matching** (default, case-insensitive):
```yaml
title_pattern: "Visual Studio"    # Matches any title containing "Visual Studio"
process_name: "devenv.exe"        # Matches the devenv.exe process
```

**Regex matching** (prefix with `regex:`):
```yaml
title_pattern: "regex:Visual Studio.*"
process_name: "regex:chrome|firefox|edge"
```

>[!TIP]
> You can use [WinLister](https://www.nirsoft.net/utils/winlister.html) tool to discover window properties.

### Window Organization Rules

Automatically position windows based on matching criteria. Rules are evaluated in order; the first match wins.

```yaml
organize:
  rules:
    - match:
        process_name: "code.exe"
      command: move --coordinates 0,0,8,12

    - match:
        process_name: "firefox.exe"
        title_pattern: "regex:.*Research.*"
      command: move --coordinates 8,0,12,12

    - match:
        title_pattern: "Settings"
      command: promote --coordinates 1,1,11,11
```

When you run `ShiftyGrid organize --all`, each window is checked against rules in order. The first matching rule's command executes for that window.

### Window Ignore Rules

Prevent specific windows from being moved by any window management operation. Useful for protecting system windows, always-on-top applications, or floating dialogs.

```yaml
ignore:
  rules:
    - match:
        process_name: "dwm.exe"                # Desktop Window Manager
    - match:
        class_name: "Shell_TrayWnd"            # Taskbar
    - match:
        process_name: "obs.exe"                # Streaming software
    - match:
        title_pattern: "regex:.*Overlay.*"    # Application overlays
```

If a window matches ANY ignore rule, it won't be moved by `arrange`, `swap`, `focus`, `resize`, or `organize` commands. Ignore rules take priority over organization rules.
