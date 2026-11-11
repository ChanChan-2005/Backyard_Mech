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
