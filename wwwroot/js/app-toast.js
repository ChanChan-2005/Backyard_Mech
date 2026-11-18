(() => {
    const dialog = document.querySelector('[data-app-message-dialog]');
    if (!dialog) return;

    const title = dialog.querySelector('[data-app-message-title]');
    const text = dialog.querySelector('[data-app-message-text]');
    const icon = dialog.querySelector('[data-app-message-icon]');
    const cancelButton = dialog.querySelector('[data-app-message-cancel]');
    const confirmButton = dialog.querySelector('[data-app-message-confirm]');
    let pendingForm = null;
    let pendingSubmitter = null;
    let allowSubmitForm = null;
    let pendingInvalidControl = null;
    const validationForms = new WeakSet();

    const paint = tone => {
        dialog.dataset.tone = tone;
        icon.textContent = tone === 'success' ? '✓' : tone === 'error' ? '×' : '?';
        icon.classList.remove('is-animated');
        void icon.offsetWidth;
        icon.classList.add('is-animated');
    };

    const show = ({ heading, message, tone = 'error', confirmText = 'Okay', cancelText = null, onConfirm = null }) => {
        title.textContent = heading;
        text.textContent = message;
        confirmButton.textContent = confirmText;
        cancelButton.hidden = !cancelText;
        cancelButton.textContent = cancelText || 'Cancel';
        dialog._onConfirm = onConfirm;
        paint(tone);
        if (!dialog.open) dialog.showModal();
        confirmButton.focus();
    };

    const clearPending = () => {
        pendingForm = null;
        pendingSubmitter = null;
    };

    confirmButton.addEventListener('click', () => {
        const onConfirm = dialog._onConfirm;
        dialog._onConfirm = null;
        dialog.close();
        if (onConfirm) onConfirm();
        else if (pendingForm) {
            const form = pendingForm;
            const submitter = pendingSubmitter;
            clearPending();
            allowSubmitForm = form;
            if (submitter && submitter.form === form) form.requestSubmit(submitter);
            else form.requestSubmit();
        }
    });

    cancelButton.addEventListener('click', () => {
        dialog._onConfirm = null;
        clearPending();
        dialog.close();
    });

    dialog.addEventListener('cancel', () => {
        dialog._onConfirm = null;
        clearPending();
    });

    dialog.addEventListener('close', () => {
        if (pendingInvalidControl) {
            const control = pendingInvalidControl;
            pendingInvalidControl = null;
            if (control.form) validationForms.delete(control.form);
            setTimeout(() => control.focus(), 0);
        }
    });

    dialog.addEventListener('click', event => {
        if (event.target === dialog) {
            dialog._onConfirm = null;
            clearPending();
        }
    });

    document.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        if (allowSubmitForm === form) {
            allowSubmitForm = null;
            return;
        }

        const message = form.dataset.confirm;
        if (!message) return;
        event.preventDefault();
        pendingForm = form;
        pendingSubmitter = event.submitter;
        show({
            heading: form.dataset.confirmTitle || 'Are you sure?',
            message,
            tone: 'confirm',
            confirmText: form.dataset.confirmAction || 'Continue',
            cancelText: 'Cancel'
        });
    }, true);

    document.addEventListener('invalid', event => {
        const control = event.target;
        if (!(control instanceof HTMLInputElement || control instanceof HTMLSelectElement || control instanceof HTMLTextAreaElement) || !control.form) return;
        event.preventDefault();
        if (validationForms.has(control.form)) return;
        validationForms.add(control.form);
        pendingInvalidControl = control;
        const label = control.labels?.[0]?.innerText?.trim();
        show({
            heading: 'Check the form',
            message: label ? `${label.replace(/\s*\*\s*$/, '')}: ${control.validationMessage}` : control.validationMessage,
            tone: 'error'
        });
    }, true);

    const initialMessage = dialog.dataset.initialMessage;
    if (initialMessage) {
        show({
            heading: dialog.dataset.initialTitle || 'Update',
            message: initialMessage,
            tone: dialog.dataset.initialTone || 'success'
        });
    }
})();
