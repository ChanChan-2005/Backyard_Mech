(() => {
  'use strict';

  const icons = {
    view: '<path d="M2 12s3.6-6 10-6 10 6 10 6-3.6 6-10 6S2 12 2 12Z"/><circle cx="12" cy="12" r="2.7"/>',
    edit: '<path d="m4 16.5-.8 4.3 4.3-.8L20 7.5 16.5 4 4 16.5Z"/><path d="m14.8 5.7 3.5 3.5"/>',
    delete: '<path d="M3.5 6.5h17"/><path d="M9 6.5V4h6v2.5"/><path d="m6.5 6.5 1 14h9l1-14"/><path d="M10 10v7M14 10v7"/>',
    archive: '<path d="M4 7h16v14H4z"/><path d="M2.5 3h19v4h-19zM9 11h6"/>',
    restore: '<path d="M3 11a9 9 0 1 1 2.6 6.4"/><path d="M3 4v7h7"/>',
    manage: '<path d="M4 5h16M4 12h16M4 19h16"/><circle cx="9" cy="5" r="2"/><circle cx="15" cy="12" r="2"/><circle cx="7" cy="19" r="2"/>',
    pay: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 10h18M7 15h3"/>',
    adjust: '<path d="M4 7h16M4 17h16"/><circle cx="9" cy="7" r="2"/><circle cx="15" cy="17" r="2"/>',
    borrow: '<path d="M5 4h9l5 5v11H5z"/><path d="M14 4v5h5M8 14h8M12 10v8"/>',
    return: '<path d="M20 7v5h-5M4 17v-5h5"/><path d="M5.5 9a7 7 0 0 1 12-2M18.5 15a7 7 0 0 1-12 2"/>',
    maintenance: '<path d="M14.5 6.5a5 5 0 0 0-6.8 6.8L3 18l3 3 4.7-4.7a5 5 0 0 0 6.8-6.8L14 13l-3-3 3.5-3.5Z"/>',
    complete: '<path d="m4 12 5 5L20 6"/>',
    log: '<path d="M6 3h9l4 4v14H6z"/><path d="M14 3v5h5M9 12h7M9 16h7"/>',
    cancel: '<path d="m6 6 12 12M18 6 6 18"/>',
    deactivate: '<circle cx="12" cy="12" r="9"/><path d="M8 12h8"/>',
    block: '<circle cx="12" cy="12" r="9"/><path d="m6 6 12 12"/>',
    inspect: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5M8 10.5h5M10.5 8v5"/>',
    download: '<path d="M12 3v12m-5-5 5 5 5-5"/><path d="M4 17v4h16v-4"/>',
    print: '<path d="M6 8V3h12v5M6 17H4V9h16v8h-2"/><path d="M7 14h10v7H7zM17 11h.01"/>',
    start: '<path d="m8 5 12 7-12 7z"/>',
    stop: '<rect x="5" y="5" width="14" height="14" rx="2"/>',
    add: '<circle cx="12" cy="12" r="9"/><path d="M12 8v8M8 12h8"/>',
    default: '<circle cx="12" cy="12" r="8"/><path d="M12 8v8M8 12h8"/>'
  };

  const actionRules = [
    [/\bcheck[\s-]?in\b/i, 'complete'],
    [/\bcancel\b/i, 'cancel'],
    [/\b(view|details|history|inspect|review)\b/i, 'view'],
    [/\b(edit|update)\b/i, 'edit'],
    [/\b(delete|remove)\b/i, 'delete'],
    [/\bvoid\b/i, 'cancel'],
    [/\bunblock\b/i, 'restore'],
    [/\bblock\b/i, 'block'],
    [/\b(deactivate|disable)\b/i, 'deactivate'],
    [/\b(reactivate|activate|enable)\b/i, 'restore'],
    [/\barchive\b/i, 'archive'],
    [/\brestore\b/i, 'restore'],
    [/\b(manage|dispatch|assign|status|reopen)\b/i, 'manage'],
    [/\b(pay|payment)\b/i, 'pay'],
    [/\b(adjust|adjustment)\b/i, 'adjust'],
    [/\b(borrow|checkout)\b/i, 'borrow'],
    [/\b(transfer|ownership|return|check in)\b/i, 'return'],
    [/\b(odometer|mileage|reading)\b/i, 'adjust'],
    [/\b(maintenance|service)\b/i, 'maintenance'],
    [/\b(complete|approve|release)\b/i, 'complete'],
    [/\b(download|export)\b/i, 'download'],
    [/\bprint\b/i, 'print'],
    [/\bstart\b/i, 'start'],
    [/\bstop\b/i, 'stop'],
    [/\b(add|create|register|receive)\b/i, 'add'],
    [/\b(log|audit|records)\b/i, 'log'],
    [/\b(cancel|decline|reject)\b/i, 'cancel']
  ];

  function actionType(label) {
    for (const [pattern, type] of actionRules) {
      if (pattern.test(label)) return type;
    }
    return null;
  }

  function controlLabel(control) {
    return (control.getAttribute('aria-label')
      || control.getAttribute('title')
      || control.dataset.actionLabel
      || control.innerText
      || control.textContent
      || control.value
      || 'Table action').replace(/\s+/g, ' ').trim();
  }

  function iconButton(control, label, type) {
    const title = control.getAttribute('title') || label;
    control.classList.add('app-table-icon-action');
    control.dataset.tableAction = type;
    control.setAttribute('aria-label', label);
    control.setAttribute('title', title);
    control.innerHTML = `<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">${icons[type] || icons.default}</svg>`;
  }

  function ensureResponsiveTable(table) {
    const columnCount = table.querySelectorAll('thead tr:first-child > th, thead tr:first-child > td').length;
    table.classList.toggle('app-responsive-table-wide', columnCount >= 6);

    if (table.closest('.app-responsive-table')) return;

    let ancestor = table.parentElement;
    while (ancestor && ancestor.matches('.dashboard-content, .app-modal, dialog') === false) {
      const overflowX = getComputedStyle(ancestor).overflowX;
      if (overflowX === 'auto' || overflowX === 'scroll') return;
      ancestor = ancestor.parentElement;
    }

    const wrapper = document.createElement('div');
    wrapper.className = 'table-responsive app-responsive-table';
    wrapper.setAttribute('role', 'region');
    wrapper.setAttribute('aria-label', 'Scrollable table');
    wrapper.setAttribute('tabindex', '0');
    table.before(wrapper);
    wrapper.append(table);
  }

  function enhanceTable(table) {
    ensureResponsiveTable(table);

    const headerCells = [...(table.tHead?.rows[0]?.cells || [])];
    const actionIndex = headerCells.findIndex(cell => /^(actions?|options?|operations?)\b/i.test(cell.textContent.replace(/\s+/g, ' ').trim()));

    [...table.tBodies].flatMap(body => [...body.rows]).forEach(row => {
      const cells = actionIndex >= 0 ? [row.cells[actionIndex]].filter(Boolean) : [...row.cells];
      cells.forEach(cell => {
        const actionControls = [...cell.querySelectorAll('a, button')].filter(control => {
          if (control.closest('table') !== table) return false;
          if (actionIndex >= 0) return true;
          return actionType(controlLabel(control)) !== null;
        });
        if (!actionControls.length) return;

        actionControls.forEach(control => {
          if (control.classList.contains('app-table-icon-action')) return;
          const label = controlLabel(control);
          const type = actionType(label) || 'default';
          iconButton(control, label, type);
        });

        const directActions = [...cell.children].filter(child => child.matches('a, button, form'));
        if (directActions.length > 1 && !cell.querySelector(':scope > .app-table-action-group')) {
          const group = document.createElement('span');
          group.className = 'app-table-action-group';
          directActions[0].before(group);
          directActions.forEach(action => group.append(action));
        }
      });
    });
  }

  function enhanceAll() {
    document.querySelectorAll('table').forEach(enhanceTable);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', enhanceAll, { once: true });
  } else {
    enhanceAll();
  }

  let enhancementQueued = false;
  const observer = new MutationObserver(() => {
    if (enhancementQueued) return;
    enhancementQueued = true;
    requestAnimationFrame(() => {
      enhancementQueued = false;
      enhanceAll();
    });
  });
  observer.observe(document.body, { childList: true, subtree: true });
})();
