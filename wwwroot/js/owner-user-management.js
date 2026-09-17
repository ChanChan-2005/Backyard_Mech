(() => {
  const layer = document.querySelector("[data-users-modal-layer]");

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
