(function () {
  const page = document.querySelector(".appointments-page");
  if (!page) {
    return;
  }

  const rangeLabel = page.querySelector("[data-appointment-range]");
  const viewButtons = Array.from(page.querySelectorAll("[data-appointment-view]"));
  const panels = Array.from(page.querySelectorAll("[data-appointment-panel]"));

  const ranges = {
    day: "Oct 14, 2024",
    week: "Oct 14 - Oct 20, 2024",
    month: "October 2024",
    today: "Today - Oct 14, 2024"
  };

  function setCalendarView(view) {
    const panelView = view === "today" ? "day" : view;

    viewButtons.forEach((button) => {
      button.classList.toggle("active", button.dataset.appointmentView === view);
    });

    panels.forEach((panel) => {
      panel.hidden = panel.dataset.appointmentPanel !== panelView;
    });

    if (rangeLabel) {
      rangeLabel.textContent = ranges[view] || ranges.week;
    }
  }

  viewButtons.forEach((button) => {
    button.addEventListener("click", () => {
      setCalendarView(button.dataset.appointmentView);
    });
  });
})();
