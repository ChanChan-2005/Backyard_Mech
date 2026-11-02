window.setMasterReferenceValue = (input, value) => {
    if (!input) return;
    const selected = String(value ?? '');
    if (input instanceof HTMLSelectElement) {
        input.querySelectorAll('[data-retained-reference]').forEach(option => option.remove());
        if (selected && ![...input.options].some(option => option.value === selected)) {
            const option = new Option('Current selection (inactive)', selected);
            option.dataset.retainedReference = 'true';
            input.add(option);
        }
    }
    input.value = selected;
};
