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
            const postal = form.querySelector('[data-supplier-postal]');
            const postalList = form.querySelector('[data-supplier-postal-suggestions]');
            const postalStatus = form.querySelector('[data-supplier-postal-status]');
            let postalManuallyEdited = false;
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
            const loadPostalSuggestions = async () => {
                if (!city.value || !province.value || !postalList) return;
                postalStatus.textContent = 'Loading demo postal-code suggestions…';
                const query = new URLSearchParams({ city: city.value, province: province.value });
                try {
                    const response = await fetch('/Owner/SupplierPostalCodeSuggestions?' + query);
                    if (!response.ok) throw new Error('Postal-code list unavailable');
                    const codes = await response.json();
                    postalList.replaceChildren(...codes.map(code => new Option(code, code)));
                    if (codes.length === 1 && !postal.value) {
                        postal.value = codes[0];
                        postal.dataset.autoFilled = 'true';
                    }
                    postalStatus.textContent = codes.length
                        ? `${codes.length} demo suggestion${codes.length === 1 ? '' : 's'} for ${city.value}; verify and edit as needed.`
                        : 'No demo match for this city. Enter the postal code manually.';
                } catch {
                    postalStatus.textContent = 'Demo list unavailable. You can still enter the postal code manually.';
                }
            };
            updateCities(true);
            province.addEventListener('change', () => {
                if (!postalManuallyEdited) postal.value = '';
                postal.dataset.autoFilled = 'false';
                updateCities(false);
            });
            city.addEventListener('change', () => {
                if (!postalManuallyEdited) postal.value = '';
                postal.dataset.autoFilled = 'false';
                loadPostalSuggestions();
            });
            postal.addEventListener('input', () => {
                postalManuallyEdited = true;
                postal.dataset.autoFilled = 'false';
            });
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
