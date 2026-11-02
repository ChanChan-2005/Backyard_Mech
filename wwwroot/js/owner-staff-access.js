(() => {
    'use strict';
    document.addEventListener('click', event => {
        const open = event.target.closest('[data-staff-open]');
        if (open) document.getElementById(open.dataset.staffOpen)?.showModal();
        if (event.target.closest('[data-staff-close]')) event.target.closest('dialog')?.close();
    });
    const access = document.querySelector('[data-user-access]');
    if (access) {
        const defaults = JSON.parse(access.dataset.roleDefaults);
        const rows = [...access.querySelectorAll('[data-permission-row]')];
        const role = access.querySelector('[data-account-role]');
        function refresh() {
            const grants = defaults[role.value] || [];
            const deniedModules = new Set(rows.filter(row => row.dataset.action === 'View' && row.querySelector('select').value === 'Deny').map(row => row.dataset.module));
            for (const row of rows) {
                const inherited = grants.includes('*') || grants.includes(row.dataset.code);
                const choice = row.querySelector('select').value;
                const allowed = !deniedModules.has(row.dataset.module) && (choice === 'Allow' || (choice === 'Inherit' && inherited));
                row.querySelector('[data-default]').textContent = inherited ? 'Allowed' : 'Denied';
                row.querySelector('[data-effective]').textContent = allowed ? 'Allowed' : 'Denied';
            }
        }
        access.addEventListener('change', refresh);
        access.querySelector('[data-permission-search]').addEventListener('input', event => {
            const term = event.target.value.trim().toLowerCase();
            rows.forEach(row => { row.hidden = !(row.dataset.module + ' ' + row.dataset.action).toLowerCase().includes(term); });
        });
        refresh();
    }
    document.querySelector('[data-staff-search]')?.addEventListener('input', event => {
        const term = event.target.value.trim().toLowerCase();
        document.querySelectorAll('[data-staff-row]').forEach(row => { row.hidden = !row.textContent.toLowerCase().includes(term); });
    });
})();
