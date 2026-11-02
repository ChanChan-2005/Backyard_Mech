(() => {
    document.addEventListener("click", (event) => {
        const button = event.target.closest("[data-tools-modal]");
        if (!button) return;
        const name = button.dataset.toolsModal;
        const panel = document.querySelector(`[data-tools-modal-panel="${name}"]`);
        if (!panel) return;
        const toolId = button.dataset.toolId || "";
        const toolInput = panel.querySelector("[data-tool-transaction-id]");
        const checkoutInput = panel.querySelector("[data-tool-checkout-id]");
        if (toolInput) toolInput.value = toolId;
        if (checkoutInput) checkoutInput.value = button.dataset.checkoutId || "";
    });
})();
