(() => {
  const layer = document.querySelector("[data-users-modal-layer]");

  document.querySelectorAll("[data-canada-province]").forEach(async (provinceSelect) => {
    const citySelect = provinceSelect.form?.querySelector("[data-canada-municipality]");
    if (!citySelect) return;

    try {
      const response = await fetch("/data/canada-sgc-2021.json");
      if (!response.ok) throw new Error("Canadian geography data is unavailable");
      const geography = await response.json();
      const provinces = [...geography.provinces].sort((a, b) => a.name.localeCompare(b.name));
      const municipalities = geography.municipalities;

      provinces.forEach((province) => {
        const option = document.createElement("option");
        option.value = province.name;
        option.textContent = province.name;
        option.dataset.code = province.code;
        provinceSelect.append(option);
      });

      provinceSelect.addEventListener("change", () => {
        const selectedProvince = provinces.find(item => item.name === provinceSelect.value);
        citySelect.replaceChildren();
        const prompt = document.createElement("option");
        prompt.value = "";
        prompt.disabled = true;
        prompt.selected = true;
        prompt.textContent = selectedProvince ? "Select city or municipality" : "Select a province or territory first";
        citySelect.append(prompt);
        if (!selectedProvince) {
          citySelect.disabled = true;
          return;
        }

        municipalities
          .filter(item => item.provinceCode === selectedProvince.code)
          .sort((a, b) => a.name.localeCompare(b.name))
          .forEach((municipality) => {
            const option = document.createElement("option");
            option.value = municipality.name;
            option.textContent = municipality.name;
            option.dataset.code = municipality.code;
            citySelect.append(option);
          });
        citySelect.disabled = false;
      });
    } catch {
      provinceSelect.innerHTML = '<option value="" selected disabled>Canadian location list unavailable</option>';
    }
  });

  document.querySelectorAll("[data-staff-address-form]").forEach((form) => {
    form.addEventListener("submit", () => {
      const value = (selector) => form.querySelector(selector)?.value.trim() || "";
      const unit = value("[data-staff-address-unit]");
      const street = [
        value("[data-staff-address-number]"),
        value("[data-staff-address-street]"),
        value("[data-staff-address-type]")
      ].filter(Boolean).join(" ");

      const formattedUnit = unit && /^(unit|apt\.?|apartment|suite|#)\b/i.test(unit) ? unit : (unit ? `Unit ${unit}` : "");
      form.querySelector("[data-staff-address-value]").value = [formattedUnit, street]
        .filter(Boolean)
        .join(", ");
    });
  });

  const closeModals = () => {
    if (!layer) {
      return;
    }

    layer.classList.remove("is-open");
    layer.querySelectorAll(".users-modal").forEach((modal) => modal.classList.remove("is-open"));
  };

  const shiftForm = layer?.querySelector('[data-weekly-shift-form]');
  if (shiftForm) {
    const employee = shiftForm.querySelector('[data-shift-employee]');
    const picker = shiftForm.querySelector('[data-shift-day-picker]');
    const badges = shiftForm.querySelector('[data-shift-selected-days]');
    const offDays = shiftForm.querySelector('[data-shift-off-days]');
    const details = shiftForm.querySelector('[data-shift-day-details]');
    const days = new Map();
    let activeDay = null;
    const dayOrder = [1, 2, 3, 4, 5, 6, 0];
    const defaultHours = () => ({ StartTime: '08:00', EndTime: '17:00', LunchStartTime: '12:00', LunchEndTime: '13:00' });
    const fields = [
      ['StartTime', 'Start Time'],
      ['EndTime', 'End Time'],
      ['LunchStartTime', 'Lunch Break Start'],
      ['LunchEndTime', 'Lunch Break End']
    ];
    const renderDays = () => {
      badges.replaceChildren();
      offDays.replaceChildren();
      details.replaceChildren();
      const selected = dayOrder.filter(day => days.has(day));
      const off = dayOrder.filter(day => !days.has(day));
      const offLabel = document.createElement('strong');
      offLabel.textContent = 'Days off: ';
      const offList = document.createElement('span');
      offList.textContent = off.length
        ? off.map(day => picker.querySelector(`option[value="${day}"]`)?.textContent || String(day)).join(', ')
        : 'None';
      offDays.append(offLabel, offList);
      if (!selected.length) {
        const note = document.createElement('span');
        note.className = 'users-shift-off-note';
        note.textContent = 'No working days selected. This employee will be off all week.';
        badges.append(note);
      }
      selected.forEach((day, index) => {
        const dayName = picker.querySelector(`option[value="${day}"]`)?.textContent || String(day);
        const badge = document.createElement('span');
        badge.className = 'users-shift-day-badge';
        badge.classList.toggle('is-active', day === activeDay);
        const select = document.createElement('button');
        select.type = 'button';
        select.dataset.selectShiftDay = String(day);
        select.setAttribute('aria-pressed', String(day === activeDay));
        select.setAttribute('aria-label', `Edit ${dayName} hours`);
        select.textContent = dayName;
        badge.append(select);
        const remove = document.createElement('button');
        remove.type = 'button';
        remove.dataset.removeShiftDay = String(day);
        remove.setAttribute('aria-label', `Remove ${dayName}`);
        remove.textContent = '\u00d7';
        badge.append(remove);
        badges.append(badge);
        const card = document.createElement('section');
        card.className = 'users-shift-day-card';
        card.dataset.shiftDay = String(day);
        card.hidden = day !== activeDay;
        const heading = document.createElement('strong');
        heading.textContent = `${dayName} hours`;
        card.append(heading);
        const dayInput = document.createElement('input');
        dayInput.type = 'hidden';
        dayInput.name = `Shifts[${index}].DayOfWeek`;
        dayInput.value = String(day);
        card.append(dayInput);
        const grid = document.createElement('div');
        grid.className = 'users-shift-day-time-grid';
        fields.forEach(([field, caption]) => {
          const label = document.createElement('label');
          const title = document.createElement('span');
          title.className = 'users-label';
          title.textContent = `${caption} *`;
          const input = document.createElement('input');
          input.className = 'users-input';
          input.type = 'time';
          input.name = `Shifts[${index}].${field}`;
          input.dataset.shiftField = field;
          input.value = days.get(day)[field];
          input.required = day === activeDay;
          label.append(title, input);
          grid.append(label);
        });
        card.append(grid);
        details.append(card);
      });
    };
    picker.addEventListener('change', () => {
      if (picker.value !== '') {
        const day = Number(picker.value);
        if (!days.has(day)) days.set(day, defaultHours());
        activeDay = day;
      }
      picker.value = '';
      renderDays();
    });
    badges.addEventListener('click', event => {
      const remove = event.target.closest('[data-remove-shift-day]');
      if (remove) {
        const day = Number(remove.dataset.removeShiftDay);
        days.delete(day);
        if (activeDay === day) activeDay = dayOrder.find(value => days.has(value)) ?? null;
      } else {
        const select = event.target.closest('[data-select-shift-day]');
        if (!select) return;
        activeDay = Number(select.dataset.selectShiftDay);
      }
      renderDays();
    });
    details.addEventListener('input', event => {
      const field = event.target.dataset.shiftField;
      const day = Number(event.target.closest('[data-shift-day]')?.dataset.shiftDay);
      if (field && days.has(day)) days.get(day)[field] = event.target.value;
    });
    employee.addEventListener('change', () => {
      days.clear();
      const option = employee.selectedOptions[0];
      (option?.dataset.shifts || '').split(';').filter(Boolean).forEach(value => {
        const [day, start, end, lunchStart, lunchEnd] = value.split('|');
        days.set(Number(day), {
          StartTime: start || '08:00', EndTime: end || '17:00',
          LunchStartTime: lunchStart || '12:00', LunchEndTime: lunchEnd || '13:00'
        });
      });
      activeDay = dayOrder.find(day => days.has(day)) ?? null;
      renderDays();
    });
    shiftForm.addEventListener('submit', event => {
      const incompleteDay = dayOrder.find(day => days.has(day) && fields.some(([field]) => !days.get(day)[field]));
      if (incompleteDay === undefined) return;
      event.preventDefault();
      activeDay = incompleteDay;
      renderDays();
      details.querySelector('[data-shift-field]:invalid')?.reportValidity();
    });
    renderDays();
  }

  document.querySelector('[data-schedule-role-filter]')?.addEventListener('change', event => {
    const selectedRole = event.target.value.toLocaleLowerCase();
    document.querySelectorAll('[data-schedule-employee]').forEach(row => {
      row.hidden = selectedRole !== '' && (row.dataset.jobTitle || '').toLocaleLowerCase() !== selectedRole;
    });
  });

  document.querySelectorAll("[data-users-modal]").forEach((trigger) => {
    trigger.addEventListener("click", (event) => {
      event.preventDefault();

      if (!layer) {
        return;
      }

      const targetId = trigger.getAttribute("data-users-modal");
      const target = layer.querySelector(`[data-modal-id="${targetId}"]`);

      if (!target) {
        return;
      }

      const employeeId = trigger.getAttribute("data-employee-id");
      const employeeName = trigger.getAttribute("data-employee-name");
      target.querySelectorAll('[data-employee-state-field="id"]').forEach((field) => {
        field.value = employeeId || "";
      });
      target.querySelectorAll('[data-employee-state-field="name"]').forEach((field) => {
        field.textContent = employeeName || "Selected employee";
      });

      layer.classList.add("is-open");
      target.classList.add("is-open");
    });
  });

  document.querySelectorAll("[data-users-modal-close]").forEach((trigger) => {
    trigger.addEventListener("click", closeModals);
  });

  layer?.addEventListener("click", (event) => {
    if (event.target === layer) {
      closeModals();
    }
  });

  const performanceChart = document.getElementById("employeePerformanceChart");
  if (performanceChart && window.Chart) {
    new Chart(performanceChart, {
      type: "bar",
      data: {
        labels: ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug"],
        datasets: [{
          data: [65, 72, 80, 78, 85, 82, 88, 91],
          backgroundColor: ["#2f80ed", "#2f80ed", "#2f80ed", "#2f80ed", "#22c55e", "#2f80ed", "#22c55e", "#22c55e"],
          borderRadius: 2,
          barPercentage: 0.36
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: { enabled: true }
        },
        scales: {
          y: {
            display: false,
            beginAtZero: true,
            max: 100
          },
          x: {
            grid: { display: false },
            ticks: {
              color: "#566274",
              font: { size: 10 }
            }
          }
        }
      }
    });
  }
})();
