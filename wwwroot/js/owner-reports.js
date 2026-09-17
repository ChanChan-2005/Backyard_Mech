(() => {
  if (!window.Chart) {
    return;
  }

  const charts = window.ownerReportCharts || {};
  const palette = ["#2f80ed", "#0fb981", "#f5a623", "#9b51e0", "#ff6b6b", "#627d98"];

  const chartDefaults = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: { enabled: true }
    },
    scales: {
      y: {
        beginAtZero: true,
        grid: { color: "#edf1f5" },
        ticks: { color: "#6d7887", font: { size: 10 } }
      },
      x: {
        grid: { display: false },
        ticks: { color: "#6d7887", font: { size: 10 } }
      }
    }
  };

  const createChart = (id, config) => {
    const element = document.getElementById(id);
    if (element) {
      new Chart(element, config);
    }
  };

  const hasLabels = (data) => Array.isArray(data?.labels) && data.labels.length > 0;
  const values = (items) => Array.isArray(items) ? items : [];

  const sales = charts.salesRevenueChart;
  createChart("salesRevenueChart", {
    type: "bar",
    data: {
      labels: hasLabels(sales) ? sales.labels : ["No data"],
      datasets: [{ data: values(sales?.data), backgroundColor: "#2f80ed", borderRadius: 2, barPercentage: 0.42 }]
    },
    options: chartDefaults
  });

  const labour = charts.labourProductivityChart;
  createChart("labourProductivityChart", {
    type: "bar",
    data: {
      labels: hasLabels(labour) ? labour.labels : ["No data"],
      datasets: [
        { label: "Billable", data: values(labour?.billable), backgroundColor: "#2f80ed", borderRadius: 2 },
        { label: "Non-billable", data: values(labour?.nonBillable), backgroundColor: "#cfd8e3", borderRadius: 2 }
      ]
    },
    options: { ...chartDefaults, indexAxis: "y", plugins: { legend: { display: true, labels: { boxWidth: 10, font: { size: 10 } } } } }
  });

  const bay = charts.bayUtilizationChart;
  createChart("bayUtilizationChart", {
    type: "bar",
    data: {
      labels: hasLabels(bay) ? bay.labels : ["No data"],
      datasets: [
        { label: "Active Repairs", data: values(bay?.active), backgroundColor: "#2f80ed" },
        { label: "Setup & Cleaning", data: values(bay?.setup), backgroundColor: "#ffd75d" },
        { label: "Idle / Empty", data: values(bay?.idle), backgroundColor: "#dfe5ec" }
      ]
    },
    options: {
      ...chartDefaults,
      indexAxis: "y",
      plugins: { legend: { display: true, labels: { boxWidth: 10, font: { size: 10 } } } },
      scales: { x: { stacked: true, display: false }, y: { stacked: true, grid: { display: false }, ticks: { color: "#273241", font: { size: 10, weight: "600" } } } }
    }
  });

  const inventory = charts.inventoryValueChart;
  createChart("inventoryValueChart", {
    type: "doughnut",
    data: {
      labels: hasLabels(inventory) ? inventory.labels : ["No data"],
      datasets: [{ data: values(inventory?.data), backgroundColor: palette, borderWidth: 0 }]
    },
    options: { responsive: true, maintainAspectRatio: false, cutout: "58%", plugins: { legend: { position: "bottom", labels: { boxWidth: 10, font: { size: 10 } } } } }
  });

  const profit = charts.profitabilityChart;
  createChart("profitabilityChart", {
    type: "line",
    data: {
      labels: hasLabels(profit) ? profit.labels : ["No data"],
      datasets: [{ data: values(profit?.data), borderColor: "#2f80ed", backgroundColor: "rgba(47, 128, 237, 0.08)", tension: 0.25, fill: false, pointRadius: 2 }]
    },
    options: chartDefaults
  });
})();
