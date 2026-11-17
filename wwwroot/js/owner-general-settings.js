(() => {
    const form = document.querySelector('[data-general-settings]');
    if (!form) return;
    const sunday = form.querySelector('[data-sunday-toggle]');
    const syncSunday = () => {
        form.querySelector('[data-sunday-hours]').hidden = !sunday.checked;
        form.querySelector('[data-sunday-status]').textContent = sunday.checked ? 'Open' : 'Closed all day';
        form.querySelectorAll('[data-sunday-hours] input').forEach(input => { input.required = sunday.checked; });
    };
    sunday.addEventListener('change', syncSunday);
    syncSunday();
    const upload = form.querySelector('[name="LogoUpload"]');
    const preview = form.querySelector('[data-logo-preview]');
    let previewUrl;
    const savedLogo = preview.getAttribute('src');
    upload.addEventListener('change', () => {
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        upload.setCustomValidity('');
        const file = upload.files[0];
        if (!file) { preview.src = savedLogo || ''; preview.hidden = !savedLogo; return; }
        if (!['image/png', 'image/jpeg'].includes(file.type) || file.size > 5 * 1024 * 1024) {
            upload.setCustomValidity('Choose a PNG or JPG image up to 5 MB.');
            upload.reportValidity();
            return;
        }
        previewUrl = URL.createObjectURL(file);
        preview.src = previewUrl;
        preview.hidden = false;
    });
})();
