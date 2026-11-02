(() => {
    'use strict';
    const page = document.querySelector('[data-appointments]');
    if (!page) return;
    const find = selector => page.querySelector(selector);
    const rows = [...page.querySelectorAll('[data-appointment-row]')];
    const dateInput = find('[data-calendar-date]');
    const mode = find('[data-calendar-mode]');
    let view = page.dataset.view;
    const dateValue = date => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
    const parse = value => new Date(`${value}T12:00:00`);
    const label = date => date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
    function render() {
        const query = find('[data-appointment-search]').value.trim().toLowerCase();
        const status = find('[data-appointment-status]').value;
        const matching = rows.filter(row => row.textContent.toLowerCase().includes(query) && (!status || row.dataset.status === status));
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
        find('[data-appointment-count]').textContent = `${view === 'list' ? matching.length : visible} appointments`;
    }
    page.addEventListener('click', event => {
        const toggle = event.target.closest('[data-appointment-view]');
        if (toggle) { view = toggle.dataset.appointmentView; const url = new URL(location.href); url.searchParams.set('view', view); history.replaceState(null, '', url); }
        const step = event.target.closest('[data-calendar-step]');
        if (step) { const date = parse(dateInput.value || page.dataset.today); const amount = Number(step.dataset.calendarStep); if (mode.value === 'month') { date.setDate(1); date.setMonth(date.getMonth() + amount); } else date.setDate(date.getDate() + amount * (mode.value === 'week' ? 7 : 1)); dateInput.value = dateValue(date); }
        if (event.target.closest('[data-calendar-today]')) dateInput.value = page.dataset.today;
        if (toggle || step || event.target.closest('[data-calendar-today]')) render();
    });
    page.addEventListener('input', render);
    page.addEventListener('change', render);
    document.querySelectorAll('[data-appointment-form]').forEach(form => {
        const customer = form.elements.CustomerId; const vehicle = form.elements.VehicleId;
        const filter = () => {
            [...vehicle.options].forEach(option => { if (option.value) option.disabled = option.hidden = option.dataset.customer !== customer.value; });
            if (vehicle.selectedOptions[0]?.disabled) vehicle.value = '';
        };
        customer.addEventListener('change', filter); filter();
        form.addEventListener('submit', event => {
            if (!form.querySelector('input[name="ServiceIds"]:checked')) {
                event.preventDefault(); form.querySelectorAll('button').forEach(button => { button.disabled = false; });
                const first = form.querySelector('input[name="ServiceIds"]');
                if (first) { first.setCustomValidity('Select at least one requested service.'); first.reportValidity(); }
            }
        });
        form.addEventListener('change', () => form.querySelector('input[name="ServiceIds"]')?.setCustomValidity(''));
    });
    render();
})();
