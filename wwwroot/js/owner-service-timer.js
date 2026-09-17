(() => {
  const display = document.querySelector("[data-service-timer-display]");
  const status = document.querySelector("[data-service-timer-status]");
  const toggleButton = document.querySelector("[data-service-timer-toggle]");
  const completeButton = document.querySelector("[data-service-timer-complete]");
  const costDisplay = document.querySelector("[data-service-timer-cost]");
  const logList = document.querySelector("[data-service-timer-log]");
  const reasonButtons = document.querySelectorAll("[data-pause-reason]");

  if (!display || !status || !toggleButton || !completeButton) {
    return;
  }

  let elapsedSeconds = Number.parseInt(display.dataset.startSeconds || "0", 10);
  const hourlyRate = Number.parseFloat(display.dataset.hourlyRate || "0");
  let isRunning = true;
  let isComplete = false;
  let selectedReason = document.querySelector("[data-pause-reason].active")?.dataset.pauseReason || "Break / Lunch";

  const formatTime = (totalSeconds) => {
    const hours = Math.floor(totalSeconds / 3600).toString().padStart(2, "0");
    const minutes = Math.floor((totalSeconds % 3600) / 60).toString().padStart(2, "0");
    const seconds = Math.floor(totalSeconds % 60).toString().padStart(2, "0");

    return `${hours}:${minutes}:${seconds}`;
  };

  const formatClock = () => new Intl.DateTimeFormat("en-US", {
    hour: "numeric",
    minute: "2-digit",
    hour12: true
  }).format(new Date());

  const updateCost = () => {
    if (!costDisplay || Number.isNaN(hourlyRate)) {
      return;
    }

    const cost = (elapsedSeconds / 3600) * hourlyRate;
    costDisplay.textContent = `$${cost.toFixed(2)}`;
  };

  const render = () => {
    display.textContent = formatTime(elapsedSeconds);
    updateCost();
  };

  const addLog = (title, note) => {
    if (!logList) {
      return;
    }

    const item = document.createElement("article");
    item.innerHTML = `<div><strong></strong><span></span></div><span></span>`;
    item.querySelector("strong").textContent = title;
    item.querySelector("div span").textContent = note;
    item.lastElementChild.textContent = formatClock();
    logList.prepend(item);
  };

  reasonButtons.forEach((button) => {
    button.addEventListener("click", () => {
      reasonButtons.forEach((item) => item.classList.remove("active"));
      button.classList.add("active");
      selectedReason = button.dataset.pauseReason || button.textContent.trim();
    });
  });

  toggleButton.addEventListener("click", () => {
    if (isComplete) {
      return;
    }

    isRunning = !isRunning;
    document.body.classList.toggle("service-timer-paused", !isRunning);

    if (isRunning) {
      status.textContent = "Timer Running";
      toggleButton.textContent = "Pause Session";
      addLog("Timer Resumed", "Break Completed");
      return;
    }

    status.textContent = `Timer Paused - ${selectedReason}`;
    toggleButton.textContent = "Resume Session";
    addLog("Timer Paused", selectedReason);
  });

  completeButton.addEventListener("click", () => {
    if (isComplete) {
      return;
    }

    isRunning = false;
    isComplete = true;
    document.body.classList.remove("service-timer-paused");
    document.body.classList.add("service-timer-complete");
    status.textContent = "Session Completed";
    toggleButton.disabled = true;
    completeButton.disabled = true;
    addLog("Session Completed", `Final duration ${formatTime(elapsedSeconds)}`);
  });

  window.setInterval(() => {
    if (!isRunning || isComplete) {
      return;
    }

    elapsedSeconds += 1;
    render();
  }, 1000);

  render();
})();
