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

  function openModal(name) {
    const modal = document.querySelector(`[data-appointment-modal-panel="${name}"]`);
    if (!modal) return;

    modal.classList.add("is-open");
    modal.setAttribute("aria-hidden", "false");
    document.body.classList.add("appointment-modal-open");
    modal.querySelector("input:not([type='hidden']), textarea, select, button")?.focus();
  }

  function closeModal(modal) {
    if (!modal) return;
    modal.classList.remove("is-open");
    modal.setAttribute("aria-hidden", "true");
    if (!document.querySelector(".appointment-modal.is-open")) {
      document.body.classList.remove("appointment-modal-open");
    }
  }

  function setField(form, name, value) {
    const field = form?.elements.namedItem(name);
    if (field) field.value = value || "";
  }

  function filterVehicles(form, selectedVehicleId) {
    const customer = form.querySelector("[data-appointment-customer]");
    const vehicle = form.querySelector("[data-appointment-vehicle]");
    if (!customer || !vehicle) return;

    const customerId = customer.value;
    Array.from(vehicle.options).forEach((option) => {
      if (!option.dataset.customerId) return;
      option.hidden = option.dataset.customerId !== customerId;
      option.disabled = option.hidden;
    });

    if (selectedVehicleId) {
      vehicle.value = selectedVehicleId;
    }
    if (!vehicle.value || vehicle.selectedOptions[0]?.disabled) {
      vehicle.value = "";
    }

    const contact = form.querySelector("[data-appointment-contact]");
    if (contact) contact.value = customer.selectedOptions[0]?.dataset.contact || "";
  }

  document.querySelectorAll(".appointment-modal-form").forEach((form) => {
    const customer = form.querySelector("[data-appointment-customer]");
    if (customer) {
      customer.addEventListener("change", () => filterVehicles(form));
      filterVehicles(form);
    }
  });

  document.addEventListener("click", (event) => {
    const editButton = event.target.closest("[data-appointment-edit]");
    if (editButton) {
      const form = document.querySelector("[data-edit-appointment-form]");
      if (!form) return;

      setField(form, "AppointmentId", editButton.dataset.id);
      setField(form, "CustomerId", editButton.dataset.customerId);
      filterVehicles(form, editButton.dataset.vehicleId);
      setField(form, "BayId", editButton.dataset.bayId);
      setField(form, "AssignedEmployeeProfileId", editButton.dataset.employeeId);
      setField(form, "ScheduledDate", editButton.dataset.date);
      setField(form, "StartTime", editButton.dataset.start);
      setField(form, "EndTime", editButton.dataset.end);
      setField(form, "AppointmentType", editButton.dataset.type);
      setField(form, "AppointmentStatus", editButton.dataset.status);
      setField(form, "Notes", editButton.dataset.notes);
      const reference = form.querySelector("[data-edit-appointment-reference]");
      if (reference) reference.textContent = `Update ${editButton.dataset.reference || "appointment"}.`;
      openModal("edit-appointment");
      return;
    }

    const cancelButton = event.target.closest("[data-appointment-cancel]");
    if (cancelButton) {
      const form = document.querySelector("[data-cancel-appointment-form]");
      if (!form) return;
      setField(form, "AppointmentId", cancelButton.dataset.id);
      setField(form, "Reason", "");
      const reference = form.querySelector("[data-cancel-appointment-reference]");
      if (reference) reference.textContent = `${cancelButton.dataset.reference || "This appointment"} will be cancelled. Its history will be retained.`;
      openModal("cancel-appointment");
      return;
    }

    const openButton = event.target.closest("[data-appointment-modal]");
    if (openButton) {
      event.preventDefault();
      openModal(openButton.dataset.appointmentModal);
      return;
    }

    const closeButton = event.target.closest("[data-appointment-modal-close]");
    if (closeButton) closeModal(closeButton.closest(".appointment-modal"));
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") closeModal(document.querySelector(".appointment-modal.is-open"));
  });
})();
