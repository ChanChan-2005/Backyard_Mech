// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(() => {
  const markerClass = "required-marker";

  const appendMarker = (label) => {
    if (label.querySelector(`.${markerClass}`)) {
      return;
    }

    label.appendChild(document.createTextNode(" "));

    const marker = document.createElement("span");
    marker.className = markerClass;
    marker.setAttribute("aria-hidden", "true");
    marker.textContent = "*";
    label.appendChild(marker);
  };

  const colorExistingMarker = (label) => {
    if (label.querySelector(`.${markerClass}`)) {
      return;
    }

    const text = label.textContent || "";
    if (!/\*\s*$/.test(text)) {
      return;
    }

    const markerNode = Array.from(label.childNodes)
      .reverse()
      .find(node => node.nodeType === Node.TEXT_NODE && /\*\s*$/.test(node.textContent || ""));

    if (!markerNode) {
      return;
    }

    markerNode.textContent = (markerNode.textContent || "").replace(/\s*\*\s*$/, "");
    appendMarker(label);
  };

  const initializeRequiredMarkers = () => {
    document.querySelectorAll("form label span, form label .bay-label, form label .parts-label, form label .tools-label, form label .settings-label, form label .users-label, form label .wo-label, form label .queue-modal-label")
      .forEach(colorExistingMarker);

    document.querySelectorAll("[data-required-label]").forEach(appendMarker);
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializeRequiredMarkers);
  } else {
    initializeRequiredMarkers();
  }
})();
