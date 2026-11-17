(() => {
    const pageSize = 10;
    document.querySelectorAll('[data-parts-filters]').forEach(filters => {
        const table = document.getElementById(filters.dataset.partsFilters);
        const panel = table?.closest('.parts-panel');
        if (!table || !panel) return;
        const rows = [...table.querySelectorAll('tbody tr[data-parts-row]')];
        const notificationTarget = rows.find(row => row.classList.contains('notification-target'));
        const search = filters.querySelector('input[type="search"]');
        const selects = [...filters.querySelectorAll('[data-parts-filter]')];
        const count = panel.querySelector('[data-parts-count]');
        const pagination = panel.querySelector('[data-parts-pagination]');
        const noun = table.id === 'parts-inventory-table' ? 'part' : 'product listing';
        let page = notificationTarget ? Math.floor(rows.indexOf(notificationTarget) / pageSize) + 1 : 1;
        const empty = document.createElement('tr');
        const emptyCell = document.createElement('td');
        emptyCell.className = 'parts-empty-row';
        emptyCell.colSpan = table.tHead.rows[0].cells.length;
        emptyCell.textContent = 'No matching records.';
        empty.append(emptyCell);
        table.tBodies[0].append(empty);

        const render = () => {
            const query = search.value.trim().toLocaleLowerCase();
            const matches = rows.filter(row => {
                if (query && !row.textContent.toLocaleLowerCase().includes(query)) return false;
                return selects.every(select => !select.value || (select.dataset.partsFilter === 'categoryIds'
                    ? (row.dataset.categoryIds || '').split(' ').includes(select.value)
                    : row.dataset[select.dataset.partsFilter] === select.value));
            });
            const pages = Math.max(1, Math.ceil(matches.length / pageSize));
            page = Math.min(page, pages);
            const visible = new Set(matches.slice((page - 1) * pageSize, page * pageSize));
            rows.forEach(row => { row.hidden = !visible.has(row); });
            empty.hidden = matches.length !== 0 || rows.length === 0;
            const start = matches.length ? (page - 1) * pageSize + 1 : 0;
            const end = Math.min(page * pageSize, matches.length);
            count.textContent = `Showing ${start}–${end} of ${matches.length} ${noun}${matches.length === 1 ? '' : 's'}`;
            pagination.replaceChildren();
            pagination.hidden = pages <= 1;
            if (pages <= 1) return;
            const addButton = (label, target, disabled = false, current = false) => {
                const button = document.createElement('button');
                button.type = 'button'; button.textContent = label; button.disabled = disabled;
                if (current) { button.classList.add('active'); button.setAttribute('aria-current', 'page'); }
                button.addEventListener('click', () => { page = target; render(); });
                pagination.append(button);
            };
            addButton('Previous', page - 1, page === 1);
            for (let number = 1; number <= pages; number++) addButton(String(number), number, false, number === page);
            addButton('Next', page + 1, page === pages);
        };
        const reset = () => { page = 1; render(); };
        search.addEventListener('input', reset);
        search.addEventListener('searchcommit', reset);
        filters.querySelector('[data-parts-search-submit]')?.addEventListener('click', reset);
        selects.forEach(select => select.addEventListener('change', reset));
        render();
        if (notificationTarget) requestAnimationFrame(() => {
            notificationTarget.scrollIntoView({ block: 'center' });
            notificationTarget.classList.remove('notification-target');
            void notificationTarget.offsetWidth;
            notificationTarget.classList.add('notification-target');
        });
    });
})();
