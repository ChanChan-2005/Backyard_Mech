(() => {
    'use strict';
    document.addEventListener('click', event => {
        const opener = event.target.closest('[data-master-open]');
        if (opener) document.getElementById(opener.dataset.masterOpen)?.showModal();
        if (event.target.closest('[data-master-close]')) event.target.closest('dialog')?.close();
    });
    const root = document.querySelector('.master-data');
    if (!root) return;
    const rows = [...root.querySelectorAll('[data-master-row]')];
    const search = root.querySelector('[data-master-search]');
    const status = root.querySelector('[data-master-status]');
    let searchTerm = search.value.trim().toLowerCase();
    let page = 0;
    function render() {
        const matches = rows.filter(row => row.textContent.toLowerCase().includes(searchTerm) && (!status.value || row.dataset.status === status.value));
        const pages = Math.max(1, Math.ceil(matches.length / 10));
        page = Math.min(page, pages - 1);
        rows.forEach(row => { row.hidden = true; });
        matches.slice(page * 10, page * 10 + 10).forEach(row => { row.hidden = false; });
        root.querySelector('[data-master-count]').textContent = `${matches.length} records`;
        root.querySelector('[data-master-page]').textContent = `${page + 1} / ${pages}`;
        root.querySelector('[data-master-prev]').disabled = page === 0;
        root.querySelector('[data-master-next]').disabled = page === pages - 1;
        root.querySelector('[data-master-empty]').hidden = matches.length > 0;
    }
    search.addEventListener('searchcommit', event => { searchTerm = event.detail.value.toLowerCase(); page = 0; render(); });
    status.addEventListener('change', () => { page = 0; render(); });
    root.querySelector('[data-master-prev]').addEventListener('click', () => { page--; render(); });
    root.querySelector('[data-master-next]').addEventListener('click', () => { page++; render(); });
    render();
})();
