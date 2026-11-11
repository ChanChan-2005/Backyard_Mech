(function () {
  function openModal(name) {
    const modal = document.querySelector(`[data-settings-role-panel="${name}"]`);
    if (!modal) return;
    modal.classList.add("is-open");
    modal.setAttribute("aria-hidden", "false");
    document.body.classList.add("settings-role-modal-open");
    modal.querySelector("input:not([type='hidden']), textarea, button")?.focus();
  }

  function closeModal(modal) {
    if (!modal) return;
    modal.classList.remove("is-open");
    modal.setAttribute("aria-hidden", "true");
    if (!document.querySelector(".settings-role-modal.is-open")) {
      document.body.classList.remove("settings-role-modal-open");
    }
  }

  function continueRoleCreation() {
    const detailsForm = document.querySelector("[data-settings-create-role-step]");
    const permissionsForm = document.querySelector("[data-role-permissions-form]");
    if (!detailsForm || !permissionsForm || !detailsForm.reportValidity()) return;

    const roleName = detailsForm.elements.namedItem("RoleName").value.trim();
    const description = detailsForm.elements.namedItem("Description").value.trim();
    permissionsForm.elements.namedItem("RoleName").value = roleName;
    permissionsForm.elements.namedItem("Description").value = description;
    permissionsForm.action = permissionsForm.dataset.createRoleAction;
    permissionsForm.querySelectorAll('input[name="PermissionIds"]').forEach(input => {
      input.checked = false;
      input.disabled = false;
    });

    const title = document.querySelector("#permissions-modal-title");
    if (title) title.textContent = `Permissions: ${roleName}`;
    closeModal(detailsForm.closest(".settings-role-modal"));
    openModal("permissions");
  }

  document.addEventListener("click", function (event) {
    const nextButton = event.target.closest("[data-settings-role-next]");
    if (nextButton) {
      continueRoleCreation();
      return;
    }

    const openButton = event.target.closest("[data-settings-role-modal]");
    if (openButton) {
      openModal(openButton.dataset.settingsRoleModal);
      return;
    }

    const editButton = event.target.closest("[data-settings-role-edit]");
    if (editButton) {
      const form = document.querySelector("[data-settings-edit-role-form]");
      if (!form) return;
      form.elements.namedItem("AppRoleId").value = editButton.dataset.id || "";
      form.elements.namedItem("RoleName").value = editButton.dataset.name || "";
      form.elements.namedItem("Description").value = editButton.dataset.description === "No description" ? "" : editButton.dataset.description || "";
      openModal("edit-role");
      return;
    }

    const deleteButton = event.target.closest("[data-settings-role-delete]");
    if (deleteButton) {
      const form = document.querySelector("[data-settings-delete-role-form]");
      if (!form) return;
      form.elements.namedItem("RoleId").value = deleteButton.dataset.roleId || "";
      const message = form.querySelector("[data-settings-delete-role-message]");
      const users = Number(deleteButton.dataset.users || 0);
      if (message) {
        message.textContent = users > 0
          ? `${deleteButton.dataset.name} has ${users} assigned user(s). Reassign them before deleting this role.`
          : `${deleteButton.dataset.name} will be removed from the active role list.`;
      }
      openModal("delete-role");
      return;
    }

    const closeButton = event.target.closest("[data-settings-role-close]");
    if (closeButton) closeModal(closeButton.closest(".settings-role-modal"));
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") closeModal(document.querySelector(".settings-role-modal.is-open"));
  });

  document.querySelector("[data-settings-create-role-step]")?.addEventListener("submit", event => {
    event.preventDefault();
    continueRoleCreation();
  });

  document.querySelector("[data-settings-action-search]")?.addEventListener("input", event => {
    const query = event.target.value.trim().toLocaleLowerCase();
    document.querySelectorAll("[data-settings-action-group]").forEach(group => {
      let visible = 0;
      group.querySelectorAll("[data-settings-action-row]").forEach(row => {
        row.hidden = !row.dataset.search.toLocaleLowerCase().includes(query);
        if (!row.hidden) visible++;
      });
      group.hidden = visible === 0;
      if (query && visible) group.open = true;
    });
  });

  document.querySelector("[data-role-permissions-form]")?.addEventListener("change", event => {
    const input = event.target;
    if (!(input instanceof HTMLInputElement) || input.type !== "checkbox") return;
    const module = input.dataset.roleBaseModule || input.dataset.roleActionModule;
    if (!module) return;
    const bases = [...document.querySelectorAll("[data-role-base-module]")]
      .filter(item => item.dataset.roleBaseModule === module);
    const details = [...document.querySelectorAll("[data-role-action-module]")]
      .filter(item => item.dataset.roleActionModule === module);
    const base = action => bases.find(item => item.dataset.roleBaseAction === action);
    if (input.checked) {
      if (base("View")) base("View").checked = true;
      if (input.dataset.roleActionRequires && base(input.dataset.roleActionRequires))
        base(input.dataset.roleActionRequires).checked = true;
    } else if (input.dataset.roleBaseAction === "View") {
      bases.filter(item => item !== input).forEach(item => { item.checked = false; });
      details.forEach(item => { item.checked = false; });
    } else if (input.dataset.roleBaseAction) {
      details.filter(item => item.dataset.roleActionRequires === input.dataset.roleBaseAction)
        .forEach(item => { item.checked = false; });
    }
  });

  if (new URLSearchParams(window.location.search).get("editPermissions") === "true") {
    openModal("permissions");
  }
})();
