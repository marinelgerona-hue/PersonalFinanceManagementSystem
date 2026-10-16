---
name: pfms-design
description: UI/UX and CSS conventions for the Personal Finance Management System. Use when building or redesigning a page/component in this app — tables, KPI cards, charts, pagination, forms, icons, buttons, tooltips, toasts — to match the established design system instead of inventing a new one.
metadata:
  version: 1.0
---

# PFMS — design system & component conventions

This app went through many rounds of "make it look professional, senior-UI/UX-developer quality." The patterns below are the result — apply them by default on new/redesigned pages rather than re-deriving a style.

## Hard rules (explicit, repeated user corrections)

- **No hover transitions/animations on buttons or interactive elements.** State changes must be instant, not eased — this was explicitly asked to be removed after being added once ("it looks blurry").
- **Scrollbars are fully hidden everywhere**, not just restyled: `scrollbar-width: none;` + `::-webkit-scrollbar { display: none; }`. Scrolling itself must still work.
- **Selected/active UI states use the brand green**, never black/`--ink`. `--ink` (near-black) is for body text only.
- **Fonts**: Inter, loaded in weights 400/500/600/700/800 only. Never use a CSS `font-weight` outside that set — the browser will synthesize/fake it and it looks visibly blurry.
- **Full-width layout** — pages must fill the viewport width; don't let a `place-items: center` grid rule shrink-wrap content on wide pages (this bit the auth/landing pages: `.auth-body`/`.auth-main` needed `place-items: normal`).
- **No page `<h1>` title repeated in the body** — the navbar breadcrumb already shows the page title. Use a visually-hidden `<h1>` plus an optional `.page-sub` subtitle, nothing more.
- **Notifications are a bottom-left toast stack that auto-dismisses**, never a blocking modal/alert popup. Use `showToast(message, type)` from `site.js`.

## Tables

Use `.fin-table-fixed` + `table-layout: fixed` + explicit per-column width classes (`.col-date`, `.col-chip`, `.col-chip-sm`, `.col-amount`, `.col-actions`, `.col-budget-label`, one flexible column left unclassed). Without this, columns visibly resize every time the table re-sorts (because only the current page's rows render, not the full dataset, so content-based auto-widths shift).

Add odd/even zebra striping on every data table. Keep body text at a normal/compact size — large fonts in dense tables were explicitly flagged as wrong twice.

## Pagination, sorting, search

Shared infra — reuse it, don't rebuild per page:
- `Models/ViewModels/ListQuery.cs` — `ListQuery`, `PagedList<T>`, `PagerModel`, `ListQueryExtensions`.
- `Helpers/ListHtml.cs` — `SortHeader(...)` (sortable `<th>` with arrow indicator) and `WithQuery(...)` (preserve existing query params when building a new link).
- `Views/Shared/_ListToolbar.cshtml` — search box + page-size selector.
- `Views/Shared/_Pagination.cshtml` — page links.

Any list page needs all three: search, sort, and page-size selection — this was asked for explicitly on every list page.

## KPI cards

`.kpi-grid` containing `.kpi` articles — icon (tinted circle, `tint-green`/`tint-red`/`tint-indigo`/`tint-sky`/etc. classes, or inline `color-mix(in srgb, <color> 14%, white)` when the color is dynamic/per-row), label, period, big value, small foot line. Used on Dashboard, Income, Expenses, Budgets, Goals, and the Reports hub. See `Views/Reports/Index.cshtml` for a clean reference instance (4-card KPI grid + a card grid of report links below, each link card showing a live one-line stat pulled from that sub-report).

**Known typo to avoid**: tint classes must be `tint-indigo` etc. (with the `tint-` prefix) — a past bug used bare `tindigo/tred/tsky` and silently rendered with zero color.

## Charts & tooltips

No charting library/plugin. Build `ChartTip.Build(title, (label, value, color)[])` server-side → JSON into a `data-tooltip` attribute on the chart element → `site.js`'s shared pointerover/focus handler renders a `.chart-tip` element. Every chart needs a tooltip — this was explicitly required after one shipped without.

For progress bars (budgets, goals), put the percent/status text in the tooltip, not as always-visible label text, and only show a status chip when something needs attention (Over/Near/Overdue) — not on every row.

## Status/semantic coloring

Status classes (`info`/`warn`/`over`, or similar) must reflect actual semantic state (on-track / due-soon-or-near-limit / overdue-or-over-limit), never a raw percentage cutoff applied blindly — a goal/budget at a "high" percentage that's still healthy should not read as a warning.

## Icons

All icons are real Lucide SVGs (v0.460.0), assembled once into a hidden `<svg style="display:none">` sprite of `<symbol>` elements inside `_Layout.cshtml`. Reference with:
```html
<svg class="ico"><use href="#i-name"/></svg>
```
To add a new icon: pull the matching Lucide SVG path and add it as a new `<symbol id="i-name">` in the sprite — never hand-draw a custom icon or pull from a different icon set.

## Forms

- Auth pages (login/register) use the shared split-screen `_AuthAside.cshtml` partial (banner left, form right) rather than a centered modal-style card.
- Password fields get a visibility-toggle eye icon.
- Phone number fields use `Models/Catalog.cs`'s `PhoneCountries` array plus `ComposePhone`/`SplitPhone` helpers and the searchable country-code picker JS — this exact pattern is shared identically between Register and Settings; reuse it rather than building a new picker.

## Settings page layout

Settings uses a tab view (not a single long scrolling page with anchor-scroll-to-section) — if asked to add a new settings section, add it as a new tab, not a new scrollable block.
