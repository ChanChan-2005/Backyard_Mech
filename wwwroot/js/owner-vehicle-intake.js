(() => {
    'use strict';
    document.addEventListener('click', event => {
        const open = event.target.closest('[data-intake-open]');
        if (open) document.getElementById(open.dataset.intakeOpen)?.showModal();
        if (event.target.closest('[data-intake-close]')) event.target.closest('dialog')?.close();
    });
    document.querySelectorAll('dialog form').forEach(form => form.addEventListener('submit', () => {
        form.querySelectorAll('button:not([type="button"])').forEach(button => { button.disabled = true; });
    }));
    document.querySelector('[data-intake-search]')?.addEventListener('searchcommit', event => {
        const query = event.detail.value.toLowerCase();
        document.querySelectorAll('[data-intake-row]').forEach(row => { row.hidden = !row.textContent.toLowerCase().includes(query); });
    });
})();
