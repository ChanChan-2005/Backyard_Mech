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

  document.addEventListener("click", function (event) {
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
})();
