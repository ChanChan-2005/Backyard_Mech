(() => {
    const root = document.querySelector("[data-notifications]");
    if (!root) return;

    const toggle = root.querySelector("[data-notifications-toggle]");
    const panel = root.querySelector("[data-notifications-panel]");
    if (!toggle || !panel) return;

    const setOpen = open => {
        panel.hidden = !open;
        toggle.setAttribute("aria-expanded", String(open));
    };

    toggle.addEventListener("click", () => setOpen(panel.hidden));
    document.addEventListener("click", event => {
        if (!root.contains(event.target)) setOpen(false);
    });
    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && !panel.hidden) {
            setOpen(false);
            toggle.focus();
        }
    });
})();
