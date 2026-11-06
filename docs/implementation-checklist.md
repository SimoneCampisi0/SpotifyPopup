# Spotify Popup v1.1.0 — implementation checklist

All 16 items from the 6 November 2026 backlog are implemented. Checked items below indicate implemented behavior; they do not claim hands-on validation with Spotify or multiple physical monitors.

| Status | ID | Implemented behavior | Verification |
| --- | --- | --- | --- |
| [x] | F01 | English UI strings, menus, tooltips, status/errors and accessibility names | Source audit; Windows layout checks |
| [x] | F02 | Exceptions and rejected commands handled without terminating the player | Simulated UI command failure; source review of WinRT result handling |
| [x] | F03 | Per-action capability checks and disabled disconnected controls | Isolated Windows UI checks |
| [x] | F04 | Mouse/keyboard seek, preview and track identity checks | Timeline regression checks; isolated keyboard seek check; live Spotify check pending |
| [x] | F05 | Metadata, artwork, times and capabilities cleared on disconnect | Isolated Windows UI checks |
| [x] | F06 | Retry absent/failed artwork independently of successful metadata | Source review; real delayed-artwork scenario pending |
| [x] | UX01 | Persistent layout-specific size/position and off-screen recovery | Persistence regression checks; isolated Windows off-screen check |
| [x] | UX02 | Configurable global show/hide and unlock shortcuts with conflict feedback | Parser checks and source review; real desktop conflict scenario pending |
| [x] | UX03 | Notification icon, show/hide, lock/unlock, settings and explicit exit | Published-app startup; interactive notification-area check pending |
| [x] | UX04 | Dedicated horizontal compact layout and scalable normal layout | Minimum-size Windows renders and layout checks |
| [x] | UX05 | Optional screen-edge snapping using monitor work areas | Source review; multi-monitor drag check pending |
| [x] | UX06 | Persisted locked opacity and live Settings preview | Persistence regression check; interactive preview check pending |
| [x] | UX07 | Dynamic playback states and Open Spotify action | Isolated Windows UI checks; real URI launch check pending |
| [x] | UX08 | Optional album row that collapses when hidden or absent | Source bindings and preference regression check |
| [x] | UX09 | Full metadata tooltips for title, artist and album; disabled while locked | Source review and lock/unlock UI check; interactive tooltip check pending |
| [x] | T01 | Session events update metadata/playback; timer interpolates and recovers | Source review, timeline regression checks; real session lifecycle check pending |

## Hands-on Windows follow-up

- [ ] Spotify open, closed, paused, advancing tracks and returning after a disconnect.
- [ ] Drag seeking, unsupported commands and rapid track changes during a selection.
- [ ] Delayed/unavailable artwork and temporary media-session failures.
- [ ] Global shortcuts with another application focused and with an occupied combination.
- [ ] Notification menu show/hide, lock/unlock and exit.
- [ ] Opacity preview, Save/Cancel and read-only settings folder.
- [ ] Title/artist/album tooltips, keyboard focus and screen-reader names.
- [ ] Normal/compact minimum sizes and large-window scaling.
- [ ] Multiple monitors with differing scaling, edge snapping, screen disconnect/reconnect and unlock alignment.

The release pipeline gates publication on the automated checks. Its actual outcome is recorded in GitHub Actions; this file does not predeclare a successful run.
