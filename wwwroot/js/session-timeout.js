(() => {
    'use strict';

    const page = document.body;
    const idleTimeoutMs = Number(page.dataset.sessionIdleTimeoutMs);
    const maximumAgeMs = Number(page.dataset.sessionMaximumAgeMs);
    const authenticatedAtTicks = Number(page.dataset.sessionAuthenticatedAt);
    const loginUrl = page.dataset.sessionTimeoutUrl;

    if (!Number.isFinite(idleTimeoutMs) || idleTimeoutMs <= 0 || !loginUrl) return;

    const dotNetEpochOffsetMs = 62135596800000;
    const authenticatedAtMs = authenticatedAtTicks > 0
        ? authenticatedAtTicks / 10000 - dotNetEpochOffsetMs
        : Number.NaN;
    const absoluteDeadline = Number.isFinite(authenticatedAtMs) && maximumAgeMs > 0
        ? authenticatedAtMs + maximumAgeMs
        : Number.POSITIVE_INFINITY;

    let idleDeadline = Date.now() + idleTimeoutMs;
    let redirecting = false;
    let timerId;

    const redirectIfExpired = () => {
        if (redirecting) return;

        const remainingMs = Math.min(idleDeadline, absoluteDeadline) - Date.now();
        if (remainingMs <= 0) {
            redirecting = true;
            window.location.replace(loginUrl);
            return;
        }

        window.clearTimeout(timerId);
        timerId = window.setTimeout(redirectIfExpired, remainingMs + 50);
    };

    const recordActivity = () => {
        idleDeadline = Date.now() + idleTimeoutMs;
    };

    ['pointerdown', 'keydown', 'touchstart', 'click', 'input', 'scroll'].forEach(name => {
        document.addEventListener(name, recordActivity, { passive: true });
    });
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden) redirectIfExpired();
    });

    redirectIfExpired();
})();
