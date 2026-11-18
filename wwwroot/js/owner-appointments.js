(() => {
    'use strict';
    const page = document.querySelector('[data-appointments]');
    if (!page) return;
    const find = selector => page.querySelector(selector);
    const rows = [...page.querySelectorAll('[data-appointment-row]')];
    const search = find('[data-appointment-search]');
    let searchTerm = search.value.trim().toLowerCase();
    const dateInput = find('[data-calendar-date]');
    const mode = find('[data-calendar-mode]');
    let view = page.dataset.view;
    const dateValue = date => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
    const parse = value => new Date(`${value}T12:00:00`);
    const label = date => date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
    function render() {
        const status = find('[data-appointment-status]').value;
        const matching = rows.filter(row => row.textContent.toLowerCase().includes(searchTerm) && (!status || row.dataset.status === status));
        rows.forEach(row => { row.hidden = !matching.includes(row); });
        find('[data-appointment-list]').hidden = view !== 'list';
        find('[data-calendar-panel]').hidden = view !== 'calendar';
        page.querySelectorAll('[data-appointment-view]').forEach(button => button.setAttribute('aria-pressed', button.dataset.appointmentView === view));
        const start = parse(dateInput.value || page.dataset.today);
        let days = 1;
        if (mode.value === 'week') { start.setDate(start.getDate() - (start.getDay() + 6) % 7); days = 7; }
        if (mode.value === 'month') { start.setDate(1); days = new Date(start.getFullYear(), start.getMonth() + 1, 0).getDate(); }
        const end = new Date(start); end.setDate(end.getDate() + days - 1);
        find('[data-calendar-range]').textContent = days === 1 ? label(start) : `${label(start)} - ${label(end)}`;
        const grid = find('[data-calendar-grid]'); grid.replaceChildren();
        grid.style.gridTemplateColumns = mode.value === 'day' ? 'minmax(0,1fr)' : '';
        grid.style.minWidth = mode.value === 'day' ? '0' : '';
        if (mode.value === 'month') for (let n = 0; n < (start.getDay() + 6) % 7; n++) { const blank = document.createElement('div'); blank.className = 'intake-day'; grid.append(blank); }
        let visible = 0;
        for (let n = 0; n < days; n++) {
            const date = new Date(start); date.setDate(start.getDate() + n);
            const key = dateValue(date); const day = document.createElement('section'); day.className = 'intake-day';
            day.classList.toggle('is-today', key === page.dataset.today);
            const title = document.createElement('h3'); title.textContent = date.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' }); day.append(title);
            matching.filter(row => row.dataset.date === key).sort((a, b) => a.dataset.time.localeCompare(b.dataset.time)).forEach(row => {
                const button = document.createElement('button'); button.type = 'button'; button.dataset.intakeOpen = row.dataset.dialog;
                const name = document.createElement('strong'); name.textContent = `${row.dataset.time} ${row.dataset.customer}`;
                button.append(name, document.createTextNode(row.dataset.status)); day.append(button); visible++;
            });
            grid.append(day);
        }
        find('[data-appointment-count]').textContent = `${visible} appointments`;
    }
    page.addEventListener('click', event => {
        const toggle = event.target.closest('[data-appointment-view]');
        if (toggle) {
            view = toggle.dataset.appointmentView;
            document.querySelectorAll('input[name="ViewMode"]').forEach(input => { input.value = view; });
            const url = new URL(location.href);
            url.searchParams.set('view', view);
            history.replaceState(null, '', url);
        }
        const step = event.target.closest('[data-calendar-step]');
        if (step) { const date = parse(dateInput.value || page.dataset.today); const amount = Number(step.dataset.calendarStep); if (mode.value === 'month') { date.setDate(1); date.setMonth(date.getMonth() + amount); } else date.setDate(date.getDate() + amount * (mode.value === 'week' ? 7 : 1)); dateInput.value = dateValue(date); }
        if (event.target.closest('[data-calendar-today]')) dateInput.value = page.dataset.today;
        if (toggle || step || event.target.closest('[data-calendar-today]')) render();
    });
    page.addEventListener('input', event => { if (event.target !== search) render(); });
    search.addEventListener('searchcommit', event => { searchTerm = event.detail.value.toLowerCase(); render(); });
    page.addEventListener('change', render);
    document.querySelectorAll('[data-appointment-form]').forEach(form => {
        const customer = form.elements.CustomerId; const vehicle = form.elements.VehicleId;
        const odometer = form.elements.Odometer;
        const vehicleWarning = form.querySelector('[data-vehicle-warning]');
        const odometerHelp = form.querySelector('[data-odometer-help]');
        const filter = () => {
            const hasCustomer = Boolean(customer.value);
            vehicle.disabled = !hasCustomer;
            if (vehicleWarning) vehicleWarning.hidden = hasCustomer;
            [...vehicle.options].forEach(option => { if (option.value) option.disabled = option.hidden = option.dataset.customer !== customer.value; });
            if (!hasCustomer || vehicle.selectedOptions[0]?.disabled) vehicle.value = '';
            if (odometer) {
                const minimum = Number(vehicle.selectedOptions[0]?.dataset.minOdometer || 0);
                odometer.min = String(minimum);
                odometer.setCustomValidity(odometer.value && Number(odometer.value) < minimum
                    ? `Enter at least ${minimum.toLocaleString()} mi, the vehicle's last recorded mileage.` : '');
                if (odometerHelp) odometerHelp.textContent = minimum > 0
                    ? `Must be at least ${minimum.toLocaleString()} mi (last recorded mileage).` : 'Enter the current mileage shown on the vehicle.';
            }
        };
        customer.addEventListener('change', filter); vehicle.addEventListener('change', filter); filter();
        odometer?.addEventListener('input', filter);
        form.addEventListener('submit', event => {
            const isWalkIn = form.elements.IsWalkIn?.value === 'true';
            if (!isWalkIn && !form.querySelector('input[name="ServiceIds"]:checked')) {
                event.preventDefault(); form.querySelectorAll('button').forEach(button => { button.disabled = false; });
                const first = form.querySelector('input[name="ServiceIds"]');
                if (first) { first.setCustomValidity('Select at least one requested service.'); first.reportValidity(); }
            }
        });
        form.addEventListener('change', () => form.querySelector('input[name="ServiceIds"]')?.setCustomValidity(''));
    });
    document.querySelectorAll('[data-service-picker]').forEach(picker => {
        const toggle = picker.querySelector('[data-service-toggle]');
        const menu = picker.querySelector('[data-service-menu]');
        const searchInput = picker.querySelector('[data-service-search]');
        const categorySelect = picker.querySelector('[data-service-category]');
        const options = [...picker.querySelectorAll('[data-service-option]')];
        const count = picker.querySelector('[data-service-count]');
        const empty = picker.querySelector('[data-service-empty]');
        const update = () => {
            const term = searchInput.value.trim().toLowerCase();
            const category = categorySelect.value;
            let visible = 0;
            options.forEach(option => {
                const matches = option.dataset.search.includes(term) && (!category || option.dataset.category === category);
                option.hidden = !matches;
                if (matches) visible++;
            });
            empty.hidden = visible > 0;
            const selectedCount = picker.querySelectorAll('input[name="ServiceIds"]:checked').length;
            count.textContent = selectedCount ? `${selectedCount} selected` : 'None selected';
        };
        toggle.addEventListener('click', () => {
            const open = toggle.getAttribute('aria-expanded') !== 'true';
            toggle.setAttribute('aria-expanded', String(open));
            menu.hidden = !open;
            if (open) searchInput.focus();
        });
        searchInput.addEventListener('input', update);
        categorySelect.addEventListener('change', update);
        picker.addEventListener('change', event => { if (event.target.matches('input[name="ServiceIds"]')) update(); });
        document.addEventListener('click', event => {
            if (!picker.contains(event.target)) { menu.hidden = true; toggle.setAttribute('aria-expanded', 'false'); }
        });
        update();
    });
    render();
})();
