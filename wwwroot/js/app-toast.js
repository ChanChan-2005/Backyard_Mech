(() => {
    const toast = document.querySelector('[data-app-toast]');
    if (!toast) return;
    let timer;
    let closing = false;
    const pause = () => clearTimeout(timer);
    const close = () => {
        if (closing) return;
        closing = true;
        pause();
        toast.classList.add('is-closing');
        // Fallback also handles disabled animations.
        setTimeout(() => toast.remove(), 350);
    };
    const schedule = () => {
        pause();
        if (!closing && !document.hidden && !toast.matches(':hover') && !toast.contains(document.activeElement)) {
            timer = setTimeout(close, 5000);
        }
    };
    toast.addEventListener('animationend', event => {
        if (event.target !== toast) return;
        if (event.animationName === 'app-toast-exit') toast.remove();
        if (event.animationName === 'app-toast-enter') schedule();
    });
    toast.querySelector('[data-app-toast-close]').addEventListener('click', close);
    toast.addEventListener('mouseenter', pause);
    toast.addEventListener('mouseleave', schedule);
    toast.addEventListener('focusin', pause);
    toast.addEventListener('focusout', () => setTimeout(schedule, 0));
    document.addEventListener('visibilitychange', schedule);
    schedule();
})();
