# Shared popup design

All owner-app popup surfaces use `app-modal`, `role="dialog"`, and
`aria-modal="true"`. `wwwroot/css/owner/modal-system.css`, loaded last by
`_AppLayout.cshtml`, owns their sizing, spacing, typography, controls, and scrolling.
Page styles should only define content-specific components and open/close behavior.

## Sizes and layout

- Default: 680px for data-entry forms.
- `app-modal--compact`: 480px for confirmations and short forms.
- `app-modal--wide`: 1040px for estimate lines and permission tables.
- All sizes fit the viewport, including short windows. The surface scrolls vertically;
  headers and action footers stay visible. Wide tables scroll within their own wrapper.
- Body gutters are 24px, field gaps 16px, and controls at least 42px high.
  At 600px and below, gutters become 16px and field grids become one column.

## New dialogs

```html
<dialog class="app-modal" role="dialog" aria-modal="true" aria-labelledby="editor-title">
  <form method="post">
    <header>
      <div><h2 id="editor-title">Edit record</h2><p>Brief instructions.</p></div>
      <button class="app-modal-close" type="button" aria-label="Close">&#215;</button>
    </header>
    <div class="app-modal-grid">
      <label>Name<input name="Name" required></label>
      <label>Category<select name="Category"><option>General</option></select></label>
      <label class="app-modal-field--wide">Notes<textarea name="Notes"></textarea></label>
    </div>
    <footer>
      <button class="app-modal-secondary" type="button">Cancel</button>
      <button class="app-modal-primary" type="submit">Save</button>
    </footer>
  </form>
</dialog>
```

Use the page's existing dialog controller to wire Close and Cancel, and retain the
ASP.NET antiforgery token for POST forms. Native dialogs should use `showModal()`
and `close()`. Custom overlays retain their existing visibility classes.

`app-modal-body`, `app-modal-section`, and `app-modal-actions` support content that
does not fit a simple form. `app-modal-danger` identifies destructive actions.
Existing feature class names are mapped to these same rules for compatibility.
Avoid adding padding to both a section and its field grid. Do not apply page-wide
font overrides to `.app-modal` descendants, or set a display rule that exposes a
closed dialog. Position custom checkbox labels relatively to contain hidden inputs.

For layout changes, check a regular form, a confirmation, an estimate, and a
permissions table at desktop and phone widths. Verify scroll access to the final
field and footer, hidden conditional fields, and keyboard focus visibility.
