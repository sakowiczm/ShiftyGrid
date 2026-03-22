# ShiftyGrid

A keyboard-driven window manager for Windows with modal shortcuts and grid-based positioning.

## Features

- **Modal Keyboard Shortcuts**: Define custom keyboard shortcuts and modal modes for different window management tasks
- **Grid-Based Window Positioning**: Position windows on an intuitive 12×12 grid system with precise control
- **Flexible Configuration**: YAML-based configuration file for shortcuts, modes, and rules
- **Window Arrangement**: Quickly snap windows into predefined grid layouts
- **Window Swapping**: Swap window positions with adjacent windows
- **Window Focus Navigation**: Navigate between windows using arrow keys
- **Window Promotion**: Temporarily promote windows to custom coordinates

## The Grid Concept

ShiftyGrid divides your monitor into a **coordinate grid** system for precise window positioning. Instead of using pixels, you define window positions relative to the grid, making layouts portable across different monitor sizes and resolutions.

### How It Works

1. **Define Your Grid**: You choose how to divide your monitor (e.g., into 12×12 grid units, 24×24, etc.)
2. **Position Windows**: Each window is positioned using grid coordinates rather than pixel coordinates
3. **Reproducible Layouts**: The same grid coordinates work consistently regardless of monitor size

### Default Grid: 12×12

By default, ShiftyGrid uses a **12×12 grid**. This means your monitor is divided into 12 columns and 12 rows, giving you 144 addressable positions.

### Understanding Coordinates

Each window position is defined by **four numbers**: `startX,startY,endX,endY`

- **Starting Point**: The grid origin `(0, 0)` is at the **top-left corner** of your monitor
- **X-axis**: Increases from left to right (0 to 11 in a 12×12 grid)
- **Y-axis**: Increases from top to bottom (0 to 11 in a 12×12 grid)
- **startX, startY**: Top-left corner of the window
- **endX, endY**: Bottom-right corner of the window

### Visual Grid Layout

```
     0   1   2   3   4   5   6   7   8   9  10  11  (X-axis)
   +--+--+--+--+--+--+--+--+--+--+--+--+
 0 |                                  |
 1 |                                  |
 2 |                                  |
 3 |                                  |
 4 |                                  |
 5 |                                  |
 6 |                                  |
 7 |                                  |
 8 |                                  |
 9 |                                  |
10 |                                  |
11 |                                  |
   +--+--+--+--+--+--+--+--+--+--+--+--+
(Y-axis)
```

### Common Coordinate Examples

- **Left half**: `0,0,6,12` (columns 0-5, all rows)
- **Right half**: `6,0,12,12` (columns 6-11, all rows)
- **Center with margin**: `2,0,10,12` (centered horizontally, full height)
- **Full screen**: `0,0,12,12` (all columns and rows)
- **Top-left quadrant**: `0,0,6,6` (top-left 1/4 of screen)
- **Bottom-right quadrant**: `6,6,12,12` (bottom-right 1/4 of screen)

### Customizing Grid Size

ShiftyGrid supports grid customization at two levels:

#### Global Grid Definition

Define a default grid size in your `config.yaml` that applies to all commands:

```yaml
general:
  grid: 24x24  # Divide monitor into 24×24 grid instead of 12×12
```

#### Command-Level Grid Override

Most commands support the `--grid` parameter to override the global grid for that specific command:

```bash
ShiftyGrid move --coordinates 0,0,12,24 --grid 24x24    # Use 24×24 grid for this command
ShiftyGrid promote --coordinates 4,0,20,24 --grid 24x24  # Use different grid
```

#### Grid Priority

When both global and command-level grids are defined:
1. **Command-level grid takes priority** over the global grid
2. **Global grid is used** when no command-level override is specified
3. This allows maximum flexibility - use a default grid globally but override it for specific commands that need different precision

**Example:**
```yaml
general:
  grid: 12x12  # Default: 12×12 for most commands
```

Then in your keyboard shortcuts:
```bash
ShiftyGrid move --coordinates 0,0,6,12                    # Uses global 12×12 grid
ShiftyGrid move --coordinates 0,0,12,24 --grid 24x24     # Overrides with 24×24 grid
```

#### Grid Size Recommendations

- **12×12** (default): Good balance of simplicity and precision for most use cases
- **24×24**: Finer control for precise positioning, more coordinates to remember
- **6×6**: Simpler, faster to type, less precision
- **Custom sizes**: Choose based on your monitor resolution and precision needs

## Commands

### Core Commands

#### `start`
Starts the ShiftyGrid server instance that monitors for keyboard shortcuts and executes window operations.

**Usage:**
```bash
ShiftyGrid start [--config <path>] [--logs <path>] [--log-level <level>]
```

**Options:**
- `--config, -c <path>`: Path to configuration file (default: `config.yaml`)
- `--logs, -l <path>`: Directory for log files (default: executable directory)
- `--log-level <level>`: Log level: `none`, `debug`, `info`, `warn`, `error` (default: `info`)

---

#### `move`
Move the foreground window to specified grid coordinates.

**Usage:**
```bash
ShiftyGrid move --coordinates <x1,y1,x2,y2> [--grid <NxM>]
```

**Options:**
- `--coordinates, -c <coordinates>`: Target coordinates (required). Format: `startX,startY,endX,endY`
- `--grid, -g <size>`: Grid size override (optional). Format: `NxM` (e.g., `12x12`). If not specified, uses the global grid from config

**Examples:**
```bash
ShiftyGrid move --coordinates 0,0,6,12              # Move to left half (uses global grid)
ShiftyGrid move --coordinates 6,0,12,12             # Move to right half (uses global grid)
ShiftyGrid move --coordinates 0,0,12,24 --grid 24x24  # Move using 24×24 grid instead
```

---

#### `arrange`
Arrange visible windows on the current monitor in a grid layout. Optionally restrict the arrangement to a specific zone (region) of the monitor.

**Usage:**
```bash
ShiftyGrid arrange [--rows <1-2>] [--cols <1-4>] [--zone <x1,y1,x2,y2>]
```

**Options:**
- `--rows, -r <count>`: Number of rows (1-2, default: 1)
- `--cols, -c <count>`: Number of columns (1-4, default: 2)
- `--zone, -z <coordinates>`: Limit arrangement to a specific zone (optional). Format: `x1,y1,x2,y2`

### Understanding the `--zone` Parameter

The `--zone` parameter constrains window arrangement to a specific rectangular region of your monitor. This is useful when you want to:
- Arrange windows in only part of your screen
- Keep certain areas reserved for other applications
- Create different layouts for different monitor regions
- Avoid moving windows in specific areas

**How it works:**
- Without `--zone`: Windows are arranged across the entire monitor
- With `--zone x1,y1,x2,y2`: Only the region defined by these coordinates is divided into rows and columns

**Important**: The zone coordinates use the same grid system as window positioning. For example, in a 12×12 grid:
- `--zone 0,0,6,12` = left half of monitor
- `--zone 6,0,12,12` = right half of monitor
- `--zone 2,2,10,10` = center area with margins

**Example breakdown** for `arrange --rows 1 --cols 2 --zone 0,0,6,12`:
1. Define the zone: Left half of the screen (columns 0-6)
2. Divide the zone into: 1 row, 2 columns
3. Result: Two windows side-by-side in the left half, each occupying 3 grid units wide

**Examples:**
```bash
# Basic arrangements across full screen
ShiftyGrid arrange --rows 1 --cols 2        # Split screen: 2 windows side-by-side
ShiftyGrid arrange --rows 1 --cols 3        # Three columns: 3 windows across
ShiftyGrid arrange --rows 2 --cols 2        # 2×2 grid: 4 windows in grid

# Arrangements with zones
ShiftyGrid arrange --rows 1 --cols 2 --zone 0,0,6,12       # 2 windows in left half
ShiftyGrid arrange --rows 2 --cols 2 --zone 0,0,12,12      # 4 windows in full screen (same as no zone)
ShiftyGrid arrange --rows 1 --cols 2 --zone 2,2,10,10      # 2 windows in center area
ShiftyGrid arrange --rows 2 --cols 1 --zone 6,0,12,12      # 2 windows stacked in right half
```

### Zone Use Cases

**Case 1: Dual-Monitor Workflow**
```bash
# Arrange only on the left portion (primary area for work)
ShiftyGrid arrange --rows 2 --cols 2 --zone 0,0,8,12

# Keep the right area (columns 8-12) untouched for reference material
```

**Case 2: Sidebar Layout**
```bash
# Reserve left 2 units for a narrow sidebar, arrange rest in 2 columns
ShiftyGrid arrange --rows 1 --cols 2 --zone 2,0,12,12
```

**Case 3: Centered Workspace**
```bash
# Create a centered workspace with equal margins on all sides
ShiftyGrid arrange --rows 2 --cols 2 --zone 1,1,11,11
```

**Case 4: Bottom Panel Area**
```bash
# Avoid the taskbar/panel area (typically bottom 1-2 units)
ShiftyGrid arrange --rows 1 --cols 3 --zone 0,0,12,10
```

---

#### `swap <direction>`
Swap the foreground window with an adjacent window in the specified direction.

**Usage:**
```bash
ShiftyGrid swap <direction>
```

**Arguments:**
- `<direction>`: `Left`, `Right`, `Up`, or `Down`

**Examples:**
```bash
ShiftyGrid swap Left      # Swap with window on the left
ShiftyGrid swap Right     # Swap with window on the right
```

---

#### `focus <direction>`
Move focus to the adjacent window in the specified direction (like Alt+Tab but directional).

**Usage:**
```bash
ShiftyGrid focus <direction>
```

**Arguments:**
- `<direction>`: `Left`, `Right`, `Up`, or `Down`

**Examples:**
```bash
ShiftyGrid focus Left     # Focus window on the left
ShiftyGrid focus Up       # Focus window above
```

---

#### `resize`
Resize the foreground window by adjusting its borders.

**Usage:**
```bash
ShiftyGrid resize --direction <direction> [--outer]
```

**Options:**
- `--direction <direction>`: Direction to resize: `Left`, `Right`, `Up`, `Down` (required)
- `--outer`: Move the outer border instead of the inner border (optional)

**Examples:**
```bash
ShiftyGrid resize --direction Right          # Expand right edge
ShiftyGrid resize --direction Left --outer   # Contract from left edge
```

---

#### `promote`
Toggle promotion of the foreground window to specified coordinates. Useful for temporarily moving a window while remembering its original position. Promoting a new window automatically restores the previously promoted window.

**Usage:**
```bash
ShiftyGrid promote --coordinates <x1,y1,x2,y2> [--grid <NxM>]
```

**Options:**
- `--coordinates, -c <coordinates>`: Promotion coordinates (required). Format: `startX,startY,endX,endY`
- `--grid, -g <size>`: Grid size override (optional). Format: `NxM`. If not specified, uses the global grid from config

**Examples:**
```bash
ShiftyGrid promote --coordinates 1,0,11,12              # Toggle promotion to center (uses global grid)
ShiftyGrid promote --coordinates 0,0,12,12             # Toggle fullscreen promotion (uses global grid)
ShiftyGrid promote --coordinates 2,0,22,24 --grid 24x24  # Toggle using 24×24 grid
```

---

#### `organize`
Organize windows according to predefined rules defined in your configuration file. Without options, organizes only the foreground (active) window. Use `--all` to organize all visible windows across all monitors.

**Usage:**
```bash
ShiftyGrid organize [--all] [--window <hwnd>]
```

**Options:**
- `--all, -a`: Organize all visible windows across all monitors instead of just the foreground window
- `--window, -w <hwnd>`: Target a specific window by handle (HWND) instead of the foreground window

**Examples:**
```bash
ShiftyGrid organize                # Organize only the foreground (active) window
ShiftyGrid organize --all          # Organize all visible windows across all monitors
ShiftyGrid organize --window 12345 # Organize a specific window by HWND
```

---

#### `reload`
Reload the configuration file and apply changes without restarting the server.

**Usage:**
```bash
ShiftyGrid reload
```

---

#### `status`
Display the status of the running ShiftyGrid server instance.

**Usage:**
```bash
ShiftyGrid status
```

---

#### `exit`
Stop the running ShiftyGrid server instance.

**Usage:**
```bash
ShiftyGrid exit
```

---

#### `about`
Display version information and project details.

**Usage:**
```bash
ShiftyGrid about
```

---

## Configuration

ShiftyGrid uses a YAML configuration file (`config.yaml`) to define keyboard shortcuts, modes, and general settings.

### Configuration Structure

```yaml
general:
  gap: <int>                    # Gap between windows in pixels
  grid: <NxM>                   # Default grid size (e.g., 12x12)
  proximity_threshold: <int>    # Threshold for window proximity detection
  log_level: <level>            # Log level: none, debug, info, warn, error

startup:
  commands:
    - "<command>"               # Commands to run when server starts

keyboard:
  shortcuts:
    - bindings:
        - "<key_combination>"
      command: "<command>"      # Single shortcut

  modes:
    - id: <mode_id>
      name: <mode_name>
      activation:
        - "<key_combination>"   # Keys to enter mode
      allow_escape: <bool>      # Allow Escape to exit
      shortcuts:
        - bindings:
            - "<key>"
          command: "<command>"  # Mode-specific shortcuts
```

### Key Binding Format

Use key combinations like:
- `ctrl+alt+left` - Control + Alt + Left arrow
- `ctrl+shift+d` - Control + Shift + D
- `win+shift+1` - Windows + Shift + 1
- Use `plus` or `=` for the plus key (e.g., `ctrl+alt+plus`)

### Shortcuts vs Modes

Global shortcuts execute associated operation immediately where mode shortcuts activate a metaforical 'menu' from which you can pick option by pressing defined key.
Modes are useful for grouping related commands under a single activation key. Once active, the mode's shortcuts take priority over global shortcuts.  Mode can exit immediately after picking a key, or by pressing ESC or by defined timeout.

#### Global Shortcuts

**Example:**
```yaml
keyboard:
  shortcuts:
    - bindings:
        - ctrl+alt+left
      command: swap left
    - bindings:
        - ctrl+win+up
      command: focus up
    - bindings:
        - ctrl+alt+=
      command: arrange --rows 1 --cols 2
```

#### Modal Shortcuts

**How it works:**
1. You press the mode activation key (e.g., `Ctrl+Shift+D`)
2. You enter the mode and a different set of shortcuts become active
3. You press keys to execute commands (e.g., `1`, `2`, `s`, `f`)
4. You exit the mode by pressing Escape or another shortcut

**Example:**
```yaml
keyboard:
  modes:
    - id: move_mode
      name: "Move Mode"
      activation:
        - ctrl+shift+d           # Press once to enter
      allow_escape: true         # Press Escape to exit
      shortcuts:
        - bindings:
            - "1"
          command: move --coordinates 0,0,6,12
        - bindings:
            - "2"
          command: move --coordinates 6,0,12,12
        - bindings:
            - s
          command: move --coordinates 2,0,10,12
```

## Requirements

- Windows 10 or later
- .NET 10 runtime (or runs standalone with Native AOT)