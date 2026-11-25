(() => {
  document.querySelectorAll('.staff-access form[action*="AvailabilityException"]').forEach(form => {
    const start = form.querySelector('[data-exception-start]');
    const end = form.querySelector('[data-exception-end]');
    if (!start || !end) return;

    const syncRange = () => {
      end.min = start.value;
      end.setCustomValidity(end.value && start.value && end.value <= start.value
        ? 'Choose an end date and time after the start.'
        : '');
    };

    start.addEventListener('input', syncRange);
    end.addEventListener('input', syncRange);
    form.addEventListener('submit', event => {
      syncRange();
      if (!form.reportValidity()) event.preventDefault();
    });
    syncRange();
  });
})();
