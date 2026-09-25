(() => {
    const modalSelector = '[data-parts-modal-panel="product-listing-action"]';

    const setField = (modal, name, value) => {
        const field = modal?.querySelector(`[data-listing-field="${name}"]`);
        if (field) field.value = value ?? "";
    };

    const syncLinkedItem = (modal) => {
        const type = modal?.querySelector('[data-listing-field="type"]')?.value;
        const partField = modal?.querySelector('[data-product-listing-link="part"]');
        const serviceField = modal?.querySelector('[data-product-listing-link="service"]');
        if (partField) partField.hidden = type !== "Part";
        if (serviceField) serviceField.hidden = type !== "Service";
    };

    const populateEditModal = (button) => {
        const modal = document.querySelector('[data-parts-modal-panel="edit-product-listing"]');
        if (!modal) return;

        setField(modal, "id", button.dataset.listingId);
        setField(modal, "type", button.dataset.listingType);
        setField(modal, "part", button.dataset.listingPart);
        setField(modal, "service", button.dataset.listingService);
        setField(modal, "title", button.dataset.listingTitle);
        setField(modal, "description", button.dataset.listingDescription);
        setField(modal, "image", button.dataset.listingImage);
        setField(modal, "price", button.dataset.listingPrice);
        setField(modal, "order", button.dataset.listingOrder);
        syncLinkedItem(modal);
    };

    const openActionModal = (button) => {
        const modal = document.querySelector(modalSelector);
        if (!modal) return;

        const isDelete = button.dataset.listingAction === "delete";
        modal.querySelectorAll("[data-listing-action-id]").forEach((field) => {
            field.value = button.dataset.listingId ?? "";
        });
        const name = modal.querySelector("[data-listing-action-name]");
        const title = modal.querySelector("[data-listing-action-title]");
        const copy = modal.querySelector("[data-listing-action-copy]");
        const archiveForm = modal.querySelector('[data-listing-action-form="archive"]');
        const deleteForm = modal.querySelector('[data-listing-action-form="delete"]');

        if (name) name.textContent = button.dataset.listingName ?? "this listing";
        if (title) title.textContent = isDelete ? "Delete Product Listing?" : "Archive Product Listing?";
        if (copy) {
            copy.textContent = isDelete
                ? "The listing will move to Records Disposition and follow its retention period."
                : "The listing will be hidden and can be restored from Archive.";
        }
        if (archiveForm) archiveForm.hidden = isDelete;
        if (deleteForm) deleteForm.hidden = !isDelete;

        modal.classList.add("is-open");
        modal.setAttribute("aria-hidden", "false");
        document.body.classList.add("parts-modal-open");
        modal.querySelector("button:not([hidden])")?.focus();
    };

    document.addEventListener("click", (event) => {
        const editButton = event.target.closest("[data-listing-edit]");
        if (editButton) populateEditModal(editButton);

        const actionButton = event.target.closest("[data-listing-action]");
        if (actionButton) openActionModal(actionButton);
    });

    document.addEventListener("change", (event) => {
        const typeField = event.target.closest('[data-listing-field="type"]');
        if (typeField) syncLinkedItem(typeField.closest(".parts-modal"));
    });
})();
