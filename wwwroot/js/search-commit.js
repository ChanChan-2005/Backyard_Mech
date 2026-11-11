(() => {
    'use strict';

    const committed = new WeakMap();
    const queryOf = input => input.value.trim();
    const commit = input => {
        const value = queryOf(input);
        if (committed.get(input) === value) return;
        committed.set(input, value);
        input.dispatchEvent(new CustomEvent('searchcommit', { bubbles: true, detail: { value } }));
    };

    document.querySelectorAll('input[type="search"]').forEach(input => committed.set(input, queryOf(input)));
    document.addEventListener('keydown', event => {
        const input = event.target.closest?.('input[type="search"]');
        if (!input || event.key !== 'Enter') return;
        event.preventDefault();
        commit(input);
    });
    document.addEventListener('focusout', event => {
        const input = event.target;
        if (input instanceof HTMLInputElement && input.type === 'search') commit(input);
    });

    document.addEventListener('click', event => {
        const button = event.target.closest?.('[data-search-submit]');
        if (!button) return;
        const control = button.closest('.app-search-control');
        const input = control?.querySelector('input[type="search"]');
        if (input) commit(input);
    });

    const filterTable = (table, query) => table?.querySelectorAll('tbody tr').forEach(row => {
        row.hidden = !row.textContent.toLocaleLowerCase().includes(query.toLocaleLowerCase());
    });

    const filterEmployees = () => {
        const table = document.getElementById('employees-table');
        const input = document.querySelector('[data-search-target="employees-table"]');
        const roleFilter = document.querySelector('[data-employee-role-filter]');
        if (!table) return;
        const query = queryOf(input ?? { value: '' }).toLocaleLowerCase();
        const role = roleFilter?.value.toLocaleLowerCase() ?? '';
        table.querySelectorAll('tbody tr').forEach(row => {
            const matchesText = row.textContent.toLocaleLowerCase().includes(query);
            const matchesRole = !role || (row.dataset.employeeRole ?? '').toLocaleLowerCase() === role;
            row.hidden = !(matchesText && matchesRole);
        });
    };

    document.querySelector('[data-employee-role-filter]')?.addEventListener('change', filterEmployees);

    document.addEventListener('searchcommit', event => {
        const input = event.target;
        if (!(input instanceof HTMLInputElement)) return;
        const query = event.detail?.value ?? queryOf(input);
        if (input.dataset.searchTarget === 'employees-table') filterEmployees();
        else if (input.dataset.searchTarget) filterTable(document.getElementById(input.dataset.searchTarget), query);
        if (!input.hasAttribute('data-global-search')) return;

        const content = document.querySelector('.dashboard-content');
        if (!content) return;
        const localSearch = content.querySelector('input[type="search"][data-search-managed], input[type="search"][data-search-target]');
        if (localSearch) {
            localSearch.value = input.value;
            commit(localSearch);
            return;
        }

        const searchableItems = content.querySelectorAll('[data-search]');
        if (searchableItems.length) {
            searchableItems.forEach(item => { item.hidden = !item.dataset.search.toLocaleLowerCase().includes(query.toLocaleLowerCase()); });
            return;
        }
        filterTable(content.querySelector('table'), query);
    });
})();
