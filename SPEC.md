# Codex Apps specification

Version 0.3.1 — Windows taskbar launcher with a visual organizer for apps and files.

## Interaction contract

1. The pinned **Codex Apps** executable opens a compact menu above its actual
   taskbar button. Placement is clamped to the monitor work area.
2. The root lists configured apps and categories in configuration order. An app
   launches or focuses its target. A category opens a cascading submenu; nested
   categories open further submenus. Root apps retain open-app/open-folder actions.
3. Both category names and their arrows open the category. Nested app names
   launch directly. Long root lists scroll, and long submenus remain on-screen.
4. Windows system light/dark mode, accent selection, and high-contrast system
   colors apply to the root and every submenu. There is no decorative accent bar.
5. Tab navigates root buttons; Enter/Space activates them; Right Arrow opens
   their menus. Native menu keys navigate the cascades. Escape backs out or
   dismisses the root.
6. Focus loss, Escape, or launching an app minimizes the launcher instead of
   destroying its taskbar window. Windows' taskbar context menu remains usable.
   Explicit Close exits it.
7. Restoring the launcher refreshes the taskbar anchor. Pointer movement alone
   does not move it. A taskbar click chooses that taskbar when multiple copies
   of the launcher icon exist; otherwise the previous or primary taskbar is used.
8. If Explorer temporarily cannot expose the button, retain the previous anchor
   or use the primary work area's bottom center until lookup succeeds.

## Configuration contract

- Use `apps.local.json` when present, otherwise `apps.json`, beside the executable.
- Accept a JSON array of apps and categories, including an empty starter list.
  A category has `items` and no launch settings; a resource has a file or folder
  `path`. Empty categories remain visible as placeholders.
- Support eight category levels and at most 500 total nodes. Validate required
  names, names/descriptions length, category structure, path types, and JSON syntax.
- Resolve environment variables and relative paths against the configuration
  directory. Optional arguments are passed as written. Working directories are
  configurable. Optional exact window titles help locate hidden/tray controls.
- Reuse running executables by default without arguments. With arguments, launch
  a new request by default. `reuseExisting` overrides this behavior. HTML,
  documents, shortcuts, and folders open through Windows shell associations.
- The **Organize** button opens a visual editor. Users can create, rename, nest,
  reorder, and remove categories; add files of any type through a picker, or files
  and folders through Explorer drop; and move entries by drag and drop or keyboard-friendly
  buttons. Dropping on a category moves inside it; dropping on an app inserts
  before it. Removing an entry does not uninstall it.
- Organizer edits stay in a draft until **Save changes**. Save validates the
  entire tree and atomically writes `apps.local.json`; Cancel discards changes.
  An external file change during editing prevents overwrite. Save refreshes
  the launcher without a rebuild.
- Invalid edits keep the last valid menu and show one error per changed file
  version. Missing installed applications affect only the selected launch.
- Root apps and categories may be mixed, and existing flat configurations remain
  valid. The installed personal configuration is not replaced by shared examples.

## Distribution contract

- The source folder is usable as a standalone GitHub repository root.
- Use the MIT license. Include build instructions, configuration documentation,
  JSON Schema, tests, and a Windows CI workflow.
- Shared defaults use environment-based Windows tool paths. No author's paths,
  personal configuration, credentials, logs, or diagnostics enter the archives.
- Release packaging uses an explicit public-file allowlist, builds/tests a
  separate executable, and emits source and portable ZIPs plus SHA-256 checksums.
- No remote repository or public release is created by local packaging.
- Target Windows 11 x64 and installed .NET Framework 4.8. No NuGet restore,
  external runtime download, administrator install, or background service.

## Acceptance checks

| ID | Check | Expected result |
| --- | --- | --- |
| AC1 | Restore with pointer away from the taskbar button | Center above actual icon, subject to edge clamping |
| AC2 | Open an app/category mixed configuration | Preserve order; display every root entry |
| AC3 | Expand category → nested category → app | Preserve topology; selected leaf dispatches correct app |
| AC4 | Load a long root list and long category | Scroll without dropping entries or escaping work area |
| AC5 | Launch an already-running app | Focus its accessible window, subject to reuse policy |
| AC6 | Dismiss and restore; open taskbar context menu | Keep taskbar window alive and usable |
| AC7 | Exercise negative-coordinate/multiple monitors and edges | Fit within work area above taskbar |
| AC8 | Load invalid config or select missing executable | Actionable error; previous valid menu/other apps remain usable |
| AC9 | Inspect root and submenu palettes | Follow Windows scheme; no accent stripe; readable text |
| AC10 | Save edited local categories and reactivate | Reload without rebuilding |
| AC11 | Extract source ZIP into a clean directory; build/test | Succeed without original author's applications/config |
| AC12 | Inspect release ZIP inventories and text | Include only explicit public files and generic defaults |
| AC13 | Create a category, drag an existing app into it, save | Root/category menu reflects the chosen layout |
| AC14 | Add an HTML file or drag another document from Explorer and save | Resource appears in selected category with correct path |
| AC16 | Open the HTML entry | Opens in the associated default browser |
| AC15 | Cancel or make an invalid edit | Existing configuration stays unchanged |

## Validation boundaries

Automated tests cover organizer moves and saves, category topology and launch dispatch, portable path
resolution, invalid configuration rejection, long lists, 48 placement cases,
taskbar matching and monitor selection, text contrast, high-contrast colors, and
focus-loss/restore lifetime. The installed Windows desktop has also been used
to inspect icon anchoring and the detected dark theme. The user's Windows theme
settings are not changed by tests. Other Windows versions, replacement taskbars,
and live theme switching require further platform validation.

Technical references: [Windows taskbar customization](https://support.microsoft.com/en-us/windows/experience/personalization/customize-the-taskbar-in-windows),
[desktop app themes](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-windows-themes).
