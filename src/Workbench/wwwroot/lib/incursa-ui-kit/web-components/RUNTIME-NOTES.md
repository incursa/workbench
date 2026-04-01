# Web Components Runtime Notes

This folder ships the optional `./web-components` entrypoint for the UI kit.

## v1 Scope

The runtime defines the approved v1 host family set:

- layouts and shells: `inc-app-shell`, `inc-page`, `inc-page-header`, `inc-section`, `inc-card`, `inc-summary-overview`, `inc-summary-block`, `inc-footer-bar`
- navigation: `inc-navbar`, `inc-tabs`, `inc-user-menu`
- forms and inputs: `inc-field`, `inc-input-group`, `inc-choice-group`, `inc-readonly-field`, `inc-validation-summary`
- feedback and status: `inc-state-panel`, `inc-live-region`, `inc-auto-refresh`, `inc-theme-switcher`
- overlays: `inc-disclosure`, `inc-dialog`, `inc-drawer`

## Contract shape

- CSS-first is still canonical. Components reuse existing `inc-*` class contracts.
- Package consumers should pair `@incursa/ui-kit/web-components` with `@incursa/ui-kit/web-components/style.css` when they want the default look out of the box.
- v1 stays light DOM first so current style selectors keep working.
- Native primitives are used for disclosure/menu/dialog behavior where practical.
- `index.js` is a thin bootstrap that registers family modules:
  - `components/layout.js`
  - `components/navigation.js`
  - `components/forms.js`
  - `components/feedback.js`
  - `components/overlays.js`
- Public registration API is idempotent:
  - `window.IncWebComponents.defineAll()`
  - `window.IncWebComponents.registerIncWebComponents()`
- The dedicated entrypoint auto-defines components on load.

## Explicitly deferred surfaces in v1

- tooltip and popover components
- permission-banner and toast runtime orchestration
- table/data wrappers and grid-like behavior
- filter/file/bulk workflow wrappers
- legacy helper-managed modal/offcanvas compatibility wrappers

Those surfaces remain CSS-first until a follow-up requirement pass promotes them.
