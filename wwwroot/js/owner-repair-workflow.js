(() => {
    let trigger;
    document.querySelectorAll('[data-approved-task]').forEach(select => {
        const form = select.closest('form');
        const order = form.elements.WorkOrderId;
        const sync = () => {
            [...select.options].forEach(option => {
                if (!option.value) return;
                option.hidden = option.disabled = option.dataset.order !== order.value;
                if (option.disabled && option.selected) select.value = '';
            });
            form.elements.HourlyRate.value = select.selectedOptions[0]?.dataset.rate || '0';
        };
        order.addEventListener('change', sync);
        select.addEventListener('change', sync);
        sync();
    });
    document.addEventListener('click', event => {
        const opener = event.target.closest('[data-dialog-open]');
        if (opener) {
            const dialog = document.getElementById(opener.dataset.dialogOpen);
            if (dialog instanceof HTMLDialogElement) { trigger = opener; dialog.showModal(); }
        }
        if (event.target.closest('[data-dialog-close]')) event.target.closest('dialog')?.close();
        if (event.target.closest('[data-print-invoice]')) window.print();
    });
    document.querySelectorAll('dialog.repair-dialog').forEach(dialog => {
        dialog.addEventListener('close', () => trigger?.focus());
    });
    document.querySelectorAll('form[data-estimate-editor]').forEach(form => {
        const rows = form.querySelector('[data-estimate-lines]');
        const template = form.querySelector('template');
        const reindex = () => rows.querySelectorAll('[data-estimate-line]').forEach((row, i) => {
            row.querySelectorAll('[data-field]').forEach(input => input.name = `Lines[${i}].${input.dataset.field}`);
        });
        const sync = row => {
            const part = row.querySelector('[data-field="LineType"]').value === 'Part';
            row.querySelector('[data-part-select]').hidden = !part;
            row.querySelector('[data-service-select]').hidden = part;
            row.querySelector('[data-field="PartId"]').disabled = !part;
            row.querySelector('[data-field="PartId"]').required = part;
            row.querySelector('[data-field="ServiceId"]').disabled = part;
            row.querySelector('[data-field="Quantity"]').step = part ? '1' : '.01';
        };
        form.addEventListener('click', event => {
            if (event.target.closest('[data-add-line]') && rows.children.length < 100) {
                rows.append(template.content.cloneNode(true)); reindex(); sync(rows.lastElementChild);
            }
            if (event.target.closest('[data-remove-line]') && rows.children.length > 1) {
                event.target.closest('[data-estimate-line]').remove(); reindex();
            }
        });
        form.addEventListener('change', event => {
            const row = event.target.closest('[data-estimate-line]');
            if (!row) return;
            if (event.target.dataset.field === 'LineType') sync(row);
            if (['PartId', 'ServiceId'].includes(event.target.dataset.field)) {
                const option = event.target.selectedOptions[0];
                if (option?.dataset.price) {
                    row.querySelector('[data-field="UnitPrice"]').value = option.dataset.price;
                    row.querySelector('[data-field="Description"]').value = option.textContent.trim();
                }
            }
        });
        rows.querySelectorAll('[data-estimate-line]').forEach(sync); reindex();
    });
    document.querySelectorAll('[data-table-search]').forEach(input => input.addEventListener('input', () => {
        const table = document.getElementById(input.dataset.tableSearch);
        table?.querySelectorAll('tbody tr').forEach(row => row.hidden = !row.textContent.toLowerCase().includes(input.value.toLowerCase()));
    }));
    document.querySelectorAll('dialog.repair-dialog form, .repair-page form').forEach(form => form.addEventListener('submit', () => {
        form.querySelectorAll('button[type="submit"]').forEach(button => { setTimeout(() => button.disabled = true, 0); });
    }));
    const auto = document.querySelector('[data-auto-dialog]')?.dataset.autoDialog;
    const dialog = auto && document.getElementById(auto);
    if (dialog instanceof HTMLDialogElement) dialog.showModal();
    const tick = () => document.querySelectorAll('[data-timer-seconds]').forEach(clock => {
        const base = Number(clock.dataset.timerSeconds);
        const start = Date.parse(clock.dataset.timerStart || '');
        const seconds = Math.max(0, Math.floor(base + (Number.isNaN(start) ? 0 : (Date.now() - start) / 1000)));
        clock.textContent = `${Math.floor(seconds / 3600).toString().padStart(2, '0')}:${Math.floor(seconds / 60 % 60).toString().padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`;
    });
    tick(); setInterval(tick, 1000);
})();
