(() => {
    'use strict';
    const accountForm = document.querySelector('[data-create-staff-account]');
    const accountPassword = accountForm?.querySelector('[data-account-password]');
    const accountConfirmPassword = accountForm?.querySelector('[data-account-confirm-password]');
    let passwordPointer = accountForm?.querySelector('[data-password-pointer]');
    if (accountPassword && !passwordPointer) {
        const pointer = document.createElement('div');
        pointer.id = 'password-requirements';
        pointer.className = 'staff-password-pointer';
        pointer.dataset.passwordPointer = '';
        pointer.setAttribute('role', 'status');
        pointer.setAttribute('aria-live', 'polite');
        pointer.hidden = true;
        pointer.innerHTML = '<strong>Password must contain:</strong><ul>' +
            '<li data-password-rule="length"><span data-rule-state class="visually-hidden">Not met: </span>At least 12 characters</li>' +
            '<li data-password-rule="symbol"><span data-rule-state class="visually-hidden">Not met: </span>At least one special character</li>' +
            '<li data-password-rule="number"><span data-rule-state class="visually-hidden">Not met: </span>At least one number</li>' +
            '<li data-password-rule="lowercase"><span data-rule-state class="visually-hidden">Not met: </span>At least one lowercase letter</li>' +
            '<li data-password-rule="uppercase"><span data-rule-state class="visually-hidden">Not met: </span>At least one uppercase letter</li>' +
            '</ul>';
        const oldHint = accountForm.querySelector('#password-requirements');
        if (oldHint) oldHint.replaceWith(pointer);
        else accountPassword.insertAdjacentElement('afterend', pointer);
        accountPassword.parentElement.classList.add('staff-password-field');
        accountPassword.setAttribute('aria-describedby', pointer.id);
        passwordPointer = pointer;
    }
    const staffProfileSearch = accountForm?.querySelector('[data-staff-profile-search]');
    const staffProfileId = accountForm?.querySelector('[data-staff-profile-id]');
    const staffProfileCombobox = accountForm?.querySelector('[data-staff-profile-combobox]');
    const staffProfileList = accountForm?.querySelector('#eligible-staff-profiles');
    const staffProfileOptions = [...(staffProfileList?.querySelectorAll('[data-profile-id]') ?? [])];
    let lastSelectedStaffId = '';
    let activeStaffOption = -1;
    const newAccountRole = accountForm?.querySelector('[data-new-account-role]');
    const permissionPreview = document.getElementById('create-account-permissions');
    const permissionRows = [...(permissionPreview?.querySelectorAll('[data-new-account-permission]') ?? [])];
    const permissionToggles = [...(permissionPreview?.querySelectorAll('[data-permission-toggle]') ?? [])];
    const permissionModes = new Map([...(permissionPreview?.querySelectorAll('[data-new-account-permission-mode]') ?? [])]
        .map(input => [input.dataset.permissionId, input]));
    let previewRole = null;

    function positionPasswordPointer(adjustScroll = false) {
        if (!passwordPointer || passwordPointer.hidden || !accountPassword) return;
        const dialog = accountPassword.closest('dialog');
        if (!dialog) return;
        const pointerHeight = passwordPointer.getBoundingClientRect().height;
        let field = accountPassword.getBoundingClientRect();
        const modal = dialog.getBoundingClientRect();
        let below = modal.bottom - field.bottom;
        let above = field.top - modal.top;
        if (adjustScroll && below < pointerHeight + 14 && above < pointerHeight + 14) {
            dialog.scrollTop += Math.max(0, Math.min(above - 14, pointerHeight + 14 - below));
            field = accountPassword.getBoundingClientRect();
            below = modal.bottom - field.bottom;
            above = field.top - modal.top;
        }
        passwordPointer.classList.toggle('is-above', below < pointerHeight + 14 && above > below);
    }

    function showPasswordPointer() {
        if (!passwordPointer) return;
        passwordPointer.hidden = false;
        positionPasswordPointer(true);
    }

    function updatePasswordRules() {
        if (!accountPassword) return true;
        const value = accountPassword.value;
        const rules = {
            length: value.length >= 12,
            symbol: /[^\p{L}\p{Nd}]/u.test(value),
            number: /\p{Nd}/u.test(value),
            lowercase: /\p{Ll}/u.test(value),
            uppercase: /\p{Lu}/u.test(value)
        };
        passwordPointer?.querySelectorAll('[data-password-rule]').forEach(item => {
            const met = rules[item.dataset.passwordRule];
            item.classList.toggle('is-met', met);
            item.querySelector('[data-rule-state]').textContent = met ? 'Met: ' : 'Not met: ';
        });
        let message = '';
        if (rules.length) {
            if (!rules.uppercase) message = 'Add an uppercase letter.';
            else if (!rules.lowercase) message = 'Add a lowercase letter.';
            else if (!rules.number) message = 'Add a number.';
            else if (!rules.symbol) message = 'Add a symbol.';
        }
        accountPassword.setCustomValidity(message);
        return accountPassword.validity.valid;
    }

    function updatePasswordMatch() {
        if (!accountConfirmPassword || !accountPassword) return true;
        accountConfirmPassword.setCustomValidity(
            accountConfirmPassword.value && accountConfirmPassword.value !== accountPassword.value
                ? 'Passwords do not match.'
                : ''
        );
        return accountConfirmPassword.validity.valid;
    }

    function updateStaffProfileMatch() {
        if (!staffProfileSearch || !staffProfileId) return true;
        const selected = staffProfileOptions.find(option =>
            option.dataset.profileId === staffProfileId.value && option.dataset.profileName === staffProfileSearch.value.trim()
        );
        staffProfileSearch.setCustomValidity(
            staffProfileSearch.value.trim() && !selected ? 'Select a staff member from the list.' : ''
        );
        return staffProfileSearch.validity.valid;
    }

    function visibleStaffOptions() {
        return staffProfileOptions.filter(option => !option.hidden);
    }

    function setActiveStaffOption(index) {
        const options = visibleStaffOptions();
        activeStaffOption = options.length && index >= 0 ? index % options.length : -1;
        staffProfileOptions.forEach(option => option.classList.remove('is-active'));
        const active = options[activeStaffOption];
        if (active) {
            active.classList.add('is-active');
            active.scrollIntoView({ block: 'nearest' });
            staffProfileSearch.setAttribute('aria-activedescendant', active.id);
        } else {
            staffProfileSearch.removeAttribute('aria-activedescendant');
        }
    }

    function openStaffOptions(showAll = false) {
        if (!staffProfileList || !staffProfileSearch) return;
        const query = showAll ? '' : staffProfileSearch.value.trim().toLocaleLowerCase();
        staffProfileOptions.forEach(option => {
            option.hidden = !option.dataset.profileName.toLocaleLowerCase().includes(query);
        });
        staffProfileList.querySelector('[data-staff-profile-no-results]').hidden = visibleStaffOptions().length > 0;
        staffProfileList.hidden = false;
        staffProfileSearch.setAttribute('aria-expanded', 'true');
        setActiveStaffOption(-1);
    }

    function closeStaffOptions() {
        if (!staffProfileList || !staffProfileSearch) return;
        staffProfileList.hidden = true;
        staffProfileSearch.setAttribute('aria-expanded', 'false');
        staffProfileSearch.removeAttribute('aria-activedescendant');
        activeStaffOption = -1;
    }

    function selectStaffOption(option) {
        staffProfileSearch.value = option.dataset.profileName;
        staffProfileId.value = option.dataset.profileId;
        lastSelectedStaffId = option.dataset.profileId;
        staffProfileOptions.forEach(item => item.setAttribute('aria-selected', String(item === option)));
        updateStaffProfileMatch();
        staffProfileSearch.focus({ preventScroll: true });
        closeStaffOptions();
    }

    function restoreStaffSelection() {
        const selected = staffProfileOptions.find(option => option.dataset.profileId === lastSelectedStaffId);
        if (staffProfileSearch && staffProfileId) {
            staffProfileSearch.value = selected?.dataset.profileName || '';
            staffProfileId.value = selected?.dataset.profileId || '';
            updateStaffProfileMatch();
        }
        closeStaffOptions();
    }

    function updatePermissionPreview() {
        if (!permissionPreview || !newAccountRole) return;
        const defaults = JSON.parse(permissionPreview.dataset.roleDefaults || '{}');
        const roleKey = newAccountRole.value;
        const grants = defaults[roleKey] || [];
        const roleChanged = previewRole !== roleKey;
        previewRole = roleKey;
        const roleLabel = permissionPreview.querySelector('[data-account-permissions-role]');
        if (roleLabel) roleLabel.textContent = roleKey ? newAccountRole.selectedOptions[0].textContent : 'Choose a role';
        if (permissionToggles.length) {
            permissionToggles.forEach(toggle => {
                const inherited = grants.includes('*') || grants.includes(toggle.dataset.code);
                const mode = permissionModes.get(toggle.dataset.permissionId);
                if (roleChanged) mode.value = 'Inherit';
                toggle.dataset.roleDefault = String(inherited);
                toggle.checked = mode.value === 'Inherit' ? inherited : mode.value === 'Allow';
                toggle.disabled = !roleKey;
                toggle.closest('label').classList.toggle('is-custom', mode.value !== 'Inherit');
                toggle.closest('label').title = `Role default: ${inherited ? 'Allowed' : 'Denied'}; account: ${toggle.checked ? 'Allowed' : 'Denied'}`;
            });
            return;
        }
        const deniedModules = new Set(permissionRows.filter(row =>
            row.dataset.action === 'View' && row.querySelector('[data-new-account-permission-choice]')?.value === 'Deny'
        ).map(row => row.dataset.module));
        permissionRows.forEach(row => {
            const inherited = grants.includes('*') || grants.includes(row.dataset.code);
            const choice = row.querySelector('[data-new-account-permission-choice]')?.value;
            if (!choice) {
                const legacyAccess = row.querySelector('[data-role-access]');
                if (legacyAccess) legacyAccess.textContent = roleKey ? (inherited ? 'Allowed' : 'Not allowed') : 'Choose a role';
                return;
            }
            const allowed = !deniedModules.has(row.dataset.module) && (choice === 'Allow' || (choice === 'Inherit' && inherited));
            row.querySelector('[data-default]').textContent = roleKey ? (inherited ? 'Allowed' : 'Denied') : 'Choose a role';
            row.querySelector('[data-effective]').textContent = roleKey ? (allowed ? 'Allowed' : 'Denied') : 'Choose a role';
        });
    }

    function saveAccountPermissionChoice(toggle) {
        const mode = permissionModes.get(toggle.dataset.permissionId);
        mode.value = toggle.checked === (toggle.dataset.roleDefault === 'true')
            ? 'Inherit' : toggle.checked ? 'Allow' : 'Deny';
    }

    function validateAccountForm() {
        updateStaffProfileMatch();
        updatePasswordRules();
        updatePasswordMatch();
        const invalid = [...accountForm.elements].find(field => field.willValidate && !field.checkValidity());
        if (!invalid) return true;
        if (invalid === accountPassword) {
            accountPassword.focus();
            showPasswordPointer();
        } else {
            invalid.reportValidity();
        }
        return false;
    }

    document.addEventListener('click', event => {
        const open = event.target.closest('[data-staff-open]');
        if (open) document.getElementById(open.dataset.staffOpen)?.showModal();
        const permissionsOpen = event.target.closest('[data-staff-permissions-open]');
        if (permissionsOpen) {
            updatePermissionPreview();
            document.getElementById(permissionsOpen.dataset.staffPermissionsOpen)?.showModal();
        }
        if (event.target.closest('[data-staff-close]')) event.target.closest('dialog')?.close();
    });

    accountPassword?.addEventListener('focus', () => {
        updatePasswordRules();
        showPasswordPointer();
    });
    accountPassword?.addEventListener('blur', () => { passwordPointer.hidden = true; });
    accountPassword?.addEventListener('input', () => {
        showPasswordPointer();
        updatePasswordRules();
        updatePasswordMatch();
    });
    accountPassword?.closest('dialog')?.addEventListener('scroll', () => positionPasswordPointer());
    window.addEventListener('resize', () => positionPasswordPointer());
    accountConfirmPassword?.addEventListener('input', updatePasswordMatch);
    staffProfileSearch?.addEventListener('focus', () => openStaffOptions(true));
    staffProfileSearch?.addEventListener('input', () => {
        staffProfileId.value = '';
        updateStaffProfileMatch();
        openStaffOptions();
    });
    staffProfileSearch?.addEventListener('keydown', event => {
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            if (staffProfileList.hidden) openStaffOptions(Boolean(staffProfileId.value));
            const next = activeStaffOption < 0
                ? (event.key === 'ArrowDown' ? 0 : visibleStaffOptions().length - 1)
                : (activeStaffOption + (event.key === 'ArrowDown' ? 1 : -1) + visibleStaffOptions().length) % visibleStaffOptions().length;
            setActiveStaffOption(next);
        } else if (event.key === 'Enter' && !staffProfileList.hidden) {
            event.preventDefault();
            const options = visibleStaffOptions();
            const selected = options[activeStaffOption] || (options.length === 1 ? options[0] : null);
            if (selected) selectStaffOption(selected);
        } else if (event.key === 'Escape') {
            restoreStaffSelection();
        }
    });
    staffProfileCombobox?.querySelector('[data-staff-profile-toggle]')?.addEventListener('click', () => {
        if (staffProfileList.hidden) {
            staffProfileSearch.focus();
            openStaffOptions(true);
        } else closeStaffOptions();
    });
    staffProfileList?.addEventListener('click', event => {
        const option = event.target.closest('[data-profile-id]');
        if (option) selectStaffOption(option);
    });
    staffProfileCombobox?.addEventListener('focusout', event => {
        if (!staffProfileCombobox.contains(event.relatedTarget)) restoreStaffSelection();
    });
    newAccountRole?.addEventListener('change', updatePermissionPreview);
    permissionPreview?.addEventListener('change', event => {
        const toggle = event.target.closest('[data-permission-toggle]');
        if (toggle) {
            const moduleToggles = permissionToggles.filter(item => item.dataset.module === toggle.dataset.module);
            const viewToggle = moduleToggles.find(item => item.dataset.action === 'View');
            if (toggle === viewToggle && !toggle.checked) {
                moduleToggles.filter(item => item !== toggle).forEach(item => { item.checked = false; saveAccountPermissionChoice(item); });
            } else if (!toggle.checked && !toggle.dataset.requiredAction) {
                moduleToggles.filter(item => item.dataset.requiredAction === toggle.dataset.action).forEach(item => {
                    item.checked = false;
                    saveAccountPermissionChoice(item);
                });
            } else if (toggle !== viewToggle && toggle.checked && viewToggle && !viewToggle.checked) {
                viewToggle.checked = true;
                saveAccountPermissionChoice(viewToggle);
            }
            if (toggle.checked && toggle.dataset.requiredAction) {
                const required = moduleToggles.find(item => item.dataset.action === toggle.dataset.requiredAction);
                if (required && !required.checked) {
                    required.checked = true;
                    saveAccountPermissionChoice(required);
                }
            }
            saveAccountPermissionChoice(toggle);
        }
        updatePermissionPreview();
    });
    permissionPreview?.querySelector('[data-new-account-permission-search]')?.addEventListener('input', event => {
        const term = event.target.value.trim().toLowerCase();
        permissionRows.forEach(row => { row.hidden = !(row.dataset.module + ' ' + row.dataset.action).toLowerCase().includes(term); });
    });
    permissionPreview?.querySelector('[data-staff-action-search]')?.addEventListener('input', event => {
        const term = event.target.value.trim().toLowerCase();
        permissionPreview.querySelectorAll('[data-staff-action-row]').forEach(row => {
            row.hidden = !row.dataset.search.toLowerCase().includes(term);
        });
    });
    updatePermissionPreview();
    accountForm?.addEventListener('submit', event => {
        if (permissionPreview?.open) permissionPreview.close();
        if (!validateAccountForm()) {
            event.preventDefault();
        }
    });
    const access = document.querySelector('[data-user-access]');
    if (access) {
        document.getElementById('access-settings')?.showModal();
        const defaults = JSON.parse(access.dataset.roleDefaults);
        const rows = [...access.querySelectorAll('[data-permission-row]')];
        const role = access.querySelector('[data-account-role]');
        function refresh() {
            const grants = defaults[role.value] || [];
            const rawAllowed = row => {
                const choice = row.querySelector('select').value;
                return choice === 'Allow' || (choice === 'Inherit' && (grants.includes('*') || grants.includes(row.dataset.code)));
            };
            const baseAllowed = (module, action) => {
                const base = rows.find(row => row.dataset.module === module && row.dataset.action === action);
                return base ? rawAllowed(base) : false;
            };
            for (const row of rows) {
                const inherited = grants.includes('*') || grants.includes(row.dataset.code);
                const allowed = rawAllowed(row)
                    && (row.dataset.action === 'View' || baseAllowed(row.dataset.module, 'View'))
                    && (!row.dataset.requiredAction || baseAllowed(row.dataset.module, row.dataset.requiredAction));
                row.querySelector('[data-default]').textContent = inherited ? 'Allowed' : 'Denied';
                row.querySelector('[data-effective]').textContent = allowed ? 'Allowed' : 'Denied';
            }
        }
        access.addEventListener('change', refresh);
        access.querySelector('[data-permission-search]').addEventListener('searchcommit', event => {
            const term = event.detail.value.toLowerCase();
            rows.forEach(row => { row.hidden = !row.dataset.search.toLowerCase().includes(term); });
        });
        refresh();
    }
    document.querySelector('[data-staff-search]')?.addEventListener('searchcommit', event => {
        const term = event.detail.value.toLowerCase();
        document.querySelectorAll('[data-staff-row]').forEach(row => { row.hidden = !row.textContent.toLowerCase().includes(term); });
    });
})();
