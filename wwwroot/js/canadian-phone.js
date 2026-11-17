(() => {
// Detect the entered format, then limit digits and rebuild its punctuation.
function limitCanadianPhone(value) {
    const source = value.trimStart();
    const international = source.startsWith('+');
    const formatted = source.includes('(');
    const digits = source.replace(/[^0-9]/g, '');
    if (international && digits && !digits.startsWith('1')) {
        // Keep an invalid country code visible so validation can explain it.
        return { value: '+' + digits.slice(0, 11), max: 12 };
    }
    const national = (international ? digits.slice(1) : digits).slice(0, 10);
    if (!formatted) {
        return international
            ? { value: '+' + digits.slice(0, 11), max: 12 }
            : { value: national, max: 10 };
    }
    const prefix = international ? '+1 ' : '';
    let result = prefix + '(' + national.slice(0, 3);
    if (national.length >= 3) result += ') ';
    result += national.slice(3, 6);
    if (national.length >= 6) result += '-';
    result += national.slice(6, 10);
    return { value: result, max: international ? 17 : 14 };
}
function validateCanadianPhone(field) {
    const pattern = /^(?:\+1)?[2-9][0-9]{2}[2-9][0-9]{6}$|^(?:\+1 )?\([2-9][0-9]{2}\) [2-9][0-9]{2}-[0-9]{4}$/;
    field.setCustomValidity(!field.value || pattern.test(field.value)
        ? '' : 'Use 4165550123, +14165550123, or (416) 555-0123. Canadian numbers require 10 digits and optional country code +1.');
}
function applyCanadianPhoneLimit(field) {
    const before = field.value;
    const caret = field.selectionStart ?? before.length;
    const digitsBeforeCaret = before.slice(0, caret).replace(/[^0-9]/g, '').length;
    const limited = limitCanadianPhone(before);
    field.value = limited.value;
    field.dataset.formatMaxLength = limited.max;
    if (before !== field.value) {
        let position = field.value.length;
        if (caret < before.length) {
            let count = 0;
            position = 0;
            while (position < field.value.length && count < digitsBeforeCaret) {
                if (/[0-9]/.test(field.value[position])) count++;
                position++;
            }
        }
        field.setSelectionRange(position, position);
    }
    validateCanadianPhone(field);
}
document.querySelectorAll('[data-canadian-phone]').forEach((field, index) => {
    validateCanadianPhone(field);
    const label = field.closest('label');
    if (!label) return;
    let hint = label.querySelector('small');
    if (!hint) {
        hint = document.createElement('small');
        hint.className = 'app-field-help';
        hint.textContent = 'Format: (416) 555-0123';
        label.append(hint);
    }
    if (!hint.id) hint.id = `canadian-phone-hint-${index}`;
    const describedBy = new Set((field.getAttribute('aria-describedby') || '').split(' ').filter(Boolean));
    describedBy.add(hint.id);
    field.setAttribute('aria-describedby', [...describedBy].join(' '));
});
document.addEventListener('input', event => {
    const field = event.target.closest('[data-canadian-phone]');
    if (field && !event.isComposing) applyCanadianPhoneLimit(field);
});
document.addEventListener('compositionend', event => {
    const field = event.target.closest('[data-canadian-phone]');
    if (field) applyCanadianPhoneLimit(field);
});

document.addEventListener('change', event => {
    if (event.target.matches('[data-canadian-phone]')) validateCanadianPhone(event.target);
});
document.addEventListener('submit', event => {
    const fields = [...event.target.querySelectorAll('[data-canadian-phone]')];
    fields.forEach(validateCanadianPhone);
    const invalid = fields.find(field => !field.disabled && !field.checkValidity());
    if (invalid) {
        event.preventDefault();
        invalid.reportValidity();
    }
}, true);
})();
