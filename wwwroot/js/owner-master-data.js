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

// Reuse the local Canadian geography catalog for every supplier editor.
(async () => {
    const selectors = [...document.querySelectorAll('[data-supplier-province]')];
    if (!selectors.length) return;
    try {
        const response = await fetch('/data/canada-sgc-2021.json');
        if (!response.ok) throw new Error('Location data unavailable');
        const catalog = await response.json();
        selectors.forEach(province => {
            const form = province.form;
            const city = form.querySelector('[data-supplier-city]');
            const status = form.querySelector('[data-supplier-location-status]');
            const savedProvince = province.dataset.current || '';
            const savedCity = city.dataset.current || '';
            const fill = (select, names, saved, placeholder) => {
                select.replaceChildren(new Option(placeholder, ''));
                [...new Set(names)].sort((a, b) => a.localeCompare(b)).forEach(name => select.add(new Option(name, name)));
                if (saved && !names.includes(saved)) select.add(new Option(saved + ' (saved value)', saved));
                select.value = saved;
            };
            fill(province, catalog.provinces.map(item => item.name), savedProvince, 'Select province / territory');
            const updateCities = (initial = false) => {
                const selected = catalog.provinces.find(item => item.name === province.value);
                const names = selected ? catalog.municipalities.filter(item => item.provinceCode === selected.code).map(item => item.name) : [];
                fill(city, names, initial ? savedCity : '', selected ? 'Select city / municipality' : 'Select province first');
                city.disabled = !selected && !(initial && savedCity);
                status.textContent = selected ? 'Select the supplier municipality in ' + selected.name + '.' : 'Choose a province to see its municipalities.';
            };
            updateCities(true);
            province.addEventListener('change', () => updateCities(false));
        });
    } catch {
        selectors.forEach(province => {
            province.form.querySelector('[data-supplier-location-status]').textContent = 'Location list could not load. Refresh to retry. Saved locations have been retained.';
        });
    }
})();
document.addEventListener('input', event => {
    if (event.target.matches('[data-supplier-postal]')) event.target.value = event.target.value.toUpperCase();
});
