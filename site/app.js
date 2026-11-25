const owner = "viaindustriessupport-byte";
const repository = "projekt-VIA-AI";
const api = `https://api.github.com/repos/${owner}/${repository}`;
const workflowUrl = `${api}/actions/workflows/ai-training.yml/runs?per_page=1`;
const releasesUrl = `${api}/releases/latest`;

const elements = {
  runStatus: document.querySelector("#run-status"),
  runDetail: document.querySelector("#run-detail"),
  modelVersion: document.querySelector("#model-version"),
  modelDetail: document.querySelector("#model-detail"),
  activityIndicator: document.querySelector("#activity-indicator"),
  activityTitle: document.querySelector("#activity-title"),
  activityDescription: document.querySelector("#activity-description"),
  runCount: document.querySelector("#run-count"),
  lastUpdated: document.querySelector("#last-updated"),
  apiError: document.querySelector("#api-error"),
  footerState: document.querySelector("#footer-state"),
  refreshButton: document.querySelector("#refresh-button"),
};

function formatDate(value) {
  if (!value) return "Noch keine Ausführung";
  return new Intl.DateTimeFormat("de-DE", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

async function getJson(url) {
  const response = await fetch(url, {
    headers: {
      Accept: "application/vnd.github+json",
      "X-GitHub-Api-Version": "2022-11-28",
    },
  });
  if (!response.ok) {
    throw new Error(`GitHub API antwortet mit HTTP ${response.status}`);
  }
  return response.json();
}

function renderRun(run, totalCount) {
  elements.runCount.textContent = `${totalCount} ${totalCount === 1 ? "Trainingslauf" : "Trainingsläufe"}`;
  if (!run) {
    elements.runStatus.textContent = "Noch kein Lauf";
    elements.runDetail.textContent = "Wird täglich um 03:00 UTC gestartet";
    elements.activityTitle.textContent = "Noch kein Trainingslauf";
    elements.activityDescription.textContent = "Der nächste Lauf wird automatisch nach Zeitplan gestartet.";
    elements.activityIndicator.className = "activity-indicator idle";
    return;
  }

  const date = formatDate(run.run_started_at || run.created_at);
  const statusLabels = {
    queued: "Wartet",
    in_progress: "Training läuft",
    waiting: "Wartet auf Freigabe",
  };
  const conclusionLabels = {
    success: "Abgeschlossen",
    failure: "Fehlgeschlagen",
    cancelled: "Abgebrochen",
    skipped: "Übersprungen",
    timed_out: "Zeitlimit erreicht",
    action_required: "Aktion erforderlich",
  };
  const label = run.status === "completed"
    ? (conclusionLabels[run.conclusion] || run.conclusion || "Abgeschlossen")
    : (statusLabels[run.status] || "Läuft");

  elements.runStatus.textContent = label;
  elements.runDetail.textContent = date;
  elements.activityTitle.textContent = `Lauf #${run.run_number}: ${label}`;
  elements.activityDescription.textContent =
    run.status === "completed" && run.conclusion === "success"
      ? "Training erfolgreich abgeschlossen. Prüfe unten, ob ein Release veröffentlicht wurde."
      : run.status === "completed"
        ? "Der letzte Lauf wurde nicht erfolgreich abgeschlossen. Öffne GitHub Actions für Details."
        : "GitHub Actions führt gerade den Trainingsworkflow aus.";

  const indicatorClass = run.status !== "completed"
    ? "running"
    : run.conclusion === "success"
      ? "success"
      : "failure";
  elements.activityIndicator.className = `activity-indicator ${indicatorClass}`;
}

function renderRelease(release) {
  if (!release || release.message) {
    elements.modelVersion.textContent = "Noch keine Cloud-Version";
    elements.modelDetail.textContent = "Ein erfolgreicher Trainingslauf veröffentlicht den ersten Release.";
    return;
  }

  const version = release.tag_name || release.name || "Unbekannte Version";
  const weightAsset = (release.assets || []).find((asset) => asset.name === "eigene-gewichte.json");
  elements.modelVersion.textContent = version;
  elements.modelDetail.textContent = weightAsset
    ? `Veröffentlicht ${formatDate(release.published_at)} · Gewichte verfügbar`
    : `Veröffentlicht ${formatDate(release.published_at)} · Gewichtsdatei fehlt`;
}

async function refreshDashboard() {
  elements.refreshButton.disabled = true;
  elements.refreshButton.textContent = "Lädt…";
  elements.apiError.hidden = true;

  const [runsResult, releaseResult] = await Promise.allSettled([
    getJson(workflowUrl),
    getJson(releasesUrl),
  ]);

  const errors = [];
  if (runsResult.status === "fulfilled") {
    const data = runsResult.value;
    renderRun(data.workflow_runs?.[0] || null, data.total_count || 0);
  } else {
    errors.push(`Trainingsstatus: ${runsResult.reason.message}`);
    elements.runStatus.textContent = "Status nicht verfügbar";
    elements.activityTitle.textContent = "GitHub-Status nicht erreichbar";
    elements.activityDescription.textContent = "Nutze den Workflow-Link, um den Status direkt bei GitHub zu prüfen.";
  }

  if (releaseResult.status === "fulfilled") {
    renderRelease(releaseResult.value);
  } else if (releaseResult.reason.message.includes("HTTP 404")) {
    renderRelease(null);
  } else {
    errors.push(`Modell-Release: ${releaseResult.reason.message}`);
    elements.modelVersion.textContent = "Release-Status nicht verfügbar";
  }

  const now = new Date();
  elements.lastUpdated.textContent = `Aktualisiert ${formatDate(now.toISOString())}`;
  elements.footerState.textContent = errors.length === 0
    ? "Status: verbunden"
    : "Status: teilweise nicht verfügbar";
  elements.apiError.textContent = errors.join(" · ");
  elements.apiError.hidden = errors.length === 0;
  elements.refreshButton.disabled = false;
  elements.refreshButton.textContent = "Aktualisieren";
}

elements.refreshButton.addEventListener("click", refreshDashboard);
refreshDashboard();
window.setInterval(refreshDashboard, 5 * 60 * 1000);
