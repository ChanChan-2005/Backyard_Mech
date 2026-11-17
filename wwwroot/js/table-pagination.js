(() => {
    'use strict';

    const pageSize = 10;
    const skip = table => table.id === 'customers-table' || table.id === 'parts-inventory-table' ||
        table.id === 'product-listings-table' || table.querySelector('[data-master-row]');

    document.querySelectorAll('table').forEach((table, index) => {
        if (skip(table) || !table.tBodies.length) return;
        const body = table.tBodies[0];
        const section = table.closest('section');
        const rows = () => [...body.rows].filter(row => row.cells.length > 0 &&
            ![...row.cells].some(cell => cell.hasAttribute('colspan')));
        const initialRows = rows();
        if (!initialRows.length) return;
        const notificationTarget = initialRows.find(row => row.classList.contains('notification-target'));

        const existingPager = section?.querySelector('nav[class*="pagination"], div.wo-pagination');
        const existingFooter = existingPager?.closest('footer') ||
            [...(section?.querySelectorAll('footer') ?? [])].find(footer => footer.textContent.includes('Showing'));
        const pager = existingPager || document.createElement('nav');
        pager.classList.add('app-table-pagination');
        pager.setAttribute('aria-label', `Table ${index + 1} pages`);
        if (!existingPager) {
            if (existingFooter) existingFooter.append(pager);
            else {
                const footer = document.createElement('div');
                footer.className = 'app-table-pagination-footer';
                footer.append(pager);
                (table.closest('[class*="table-wrap"], [class*="table-scroll"]') || table).after(footer);
            }
        }

        const originalSummary = existingFooter?.querySelector('span:not(.app-table-pagination-gap)');
        if (originalSummary && !originalSummary.closest('.app-table-pagination')) {
            originalSummary.classList.add('app-table-original-summary');
            originalSummary.hidden = true;
        }
        const summary = document.createElement('span');
        summary.className = 'app-table-pagination-summary';
        pager.before(summary);
        summary.setAttribute('aria-live', 'polite');
        let page = 1;
        let previousTotal = -1;
        const addButton = (label, target, disabled = false, current = false) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = label;
            button.disabled = disabled;
            if (current) { button.classList.add('active'); button.setAttribute('aria-current', 'page'); }
            button.addEventListener('click', () => { page = target; render(); });
            pager.append(button);
        };
        const render = () => {
            const allRows = rows();
            const matching = allRows.filter(row => !row.hidden);
            if (matching.length !== previousTotal) {
                const targetIndex = previousTotal < 0 && notificationTarget ? matching.indexOf(notificationTarget) : -1;
                page = targetIndex >= 0 ? Math.floor(targetIndex / pageSize) + 1 : 1;
            }
            previousTotal = matching.length;
            const pages = Math.max(1, Math.ceil(matching.length / pageSize));
            page = Math.min(page, pages);
            const visible = new Set(matching.slice((page - 1) * pageSize, page * pageSize));
            allRows.forEach(row => { row.toggleAttribute('data-pagination-hidden', !visible.has(row)); });
            const start = matching.length ? (page - 1) * pageSize + 1 : 0;
            const end = Math.min(page * pageSize, matching.length);
            summary.textContent = `Showing ${start}–${end} of ${matching.length}`;
            pager.replaceChildren();
            addButton('Previous', Math.max(1, page - 1), page === 1);
            const numbers = new Set([1, pages]);
            for (let number = Math.max(1, page - 2); number <= Math.min(pages, page + 2); number++) numbers.add(number);
            let last = 0;
            for (const number of [...numbers].sort((a, b) => a - b)) {
                if (number > last + 1) {
                    const dots = document.createElement('span');
                    dots.textContent = '…'; dots.className = 'app-table-pagination-gap';
                    pager.append(dots);
                }
                addButton(String(number), number, false, number === page);
                last = number;
            }
            addButton('Next', Math.min(pages, page + 1), page === pages);
        };
        let scheduled = false;
        const schedule = () => {
            if (scheduled) return;
            scheduled = true;
            requestAnimationFrame(() => { scheduled = false; render(); });
        };
        new MutationObserver(schedule).observe(body, { subtree: true, childList: true, attributes: true, attributeFilter: ['hidden'] });
        render();
        if (notificationTarget) requestAnimationFrame(() => {
            notificationTarget.scrollIntoView({ block: 'center' });
            notificationTarget.classList.remove('notification-target');
            void notificationTarget.offsetWidth;
            notificationTarget.classList.add('notification-target');
        });
    });
})();
