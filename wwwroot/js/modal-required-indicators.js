(() => {
  'use strict';

  const modalSelectors = [
    'dialog', '[role="dialog"]', '.customer-modal', '.bay-modal', '.parts-modal',
    '.tools-modal', '.estimate-modal', '.wo-modal', '.appointment-modal',
    '.queue-modal-backdrop', '.users-modal-layer', '.settings-role-modal'
  ];
  const modalSelector = modalSelectors.join(',');
  const requiredControlSelector = modalSelectors.flatMap(modal =>
    ['input', 'select', 'textarea'].map(control => `${modal} ${control}[required]`)
  ).join(',');

  const addIndicator = label => {
    if (label.querySelector('.modal-required-indicator')) return;

    let caption = label.querySelector(':scope > span');
    if (!caption) {
      const textNode = Array.from(label.childNodes).find(node =>
        node.nodeType === Node.TEXT_NODE && node.textContent.trim()
      );
      if (textNode) {
        caption = document.createElement('span');
        caption.textContent = textNode.textContent.trim();
        label.replaceChild(caption, textNode);
      }
    }

    const target = caption ?? label;
    if (caption) caption.textContent = caption.textContent.replace(/\s*\*\s*$/, '');
    else {
      const trailingStar = Array.from(label.childNodes).find(node =>
        node.nodeType === Node.TEXT_NODE && /\*\s*$/.test(node.textContent)
      );
      if (trailingStar) trailingStar.textContent = trailingStar.textContent.replace(/\s*\*\s*$/, '');
    }

    const indicator = document.createElement('span');
    indicator.className = 'modal-required-indicator';
    indicator.dataset.requiredGenerated = 'true';
    indicator.setAttribute('aria-hidden', 'true');
    indicator.textContent = '*';
    target.append(indicator);
  };

  const syncIndicator = control => {
    const label = control.labels?.[0] ?? control.closest('label');
    if (!label) return;
    if (control.required) {
      addIndicator(label);
    } else if (!label.querySelector('input[required], select[required], textarea[required]') && !label.control?.required) {
      label.querySelector('.modal-required-indicator[data-required-generated]')?.remove();
    }
  };

  const scan = root => {
    if (root.matches?.('input[required], select[required], textarea[required]') && root.closest(modalSelector)) {
      syncIndicator(root);
    }
    if (root.matches?.(modalSelector)) {
      root.querySelectorAll('input[required], select[required], textarea[required]').forEach(control => {
        syncIndicator(control);
      });
    }
    root.querySelectorAll?.(requiredControlSelector).forEach(control => {
      syncIndicator(control);
    });
  };

  scan(document);
  new MutationObserver(records => records.forEach(record => {
    if (record.type === 'attributes') {
      if (record.target.closest(modalSelector)) syncIndicator(record.target);
    } else {
      record.addedNodes.forEach(node => { if (node.nodeType === Node.ELEMENT_NODE) scan(node); });
    }
  })).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['required'] });
})();
