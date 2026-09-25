(() => {
    const modal = document.querySelector("[data-permanent-delete-modal]");
    if (!modal) return;

    const typeInput = modal.querySelector("[data-permanent-delete-type]");
    const idInput = modal.querySelector("[data-permanent-delete-id]");
    const nameTarget = modal.querySelector("[data-permanent-delete-name]");

    const closeModal = () => {
        modal.classList.remove("is-open");
        modal.setAttribute("aria-hidden", "true");
    };

    document.querySelectorAll("[data-permanent-delete-open]").forEach(button => {
        button.addEventListener("click", () => {
            typeInput.value = button.dataset.recordType || "";
            idInput.value = button.dataset.recordId || "";
            nameTarget.textContent = button.dataset.recordName || "this record";
            modal.classList.add("is-open");
            modal.setAttribute("aria-hidden", "false");
            modal.querySelector("[data-permanent-delete-close]")?.focus();
        });
    });

    modal.querySelectorAll("[data-permanent-delete-close]").forEach(button => {
        button.addEventListener("click", closeModal);
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && modal.classList.contains("is-open")) {
            closeModal();
        }
    });
})();
