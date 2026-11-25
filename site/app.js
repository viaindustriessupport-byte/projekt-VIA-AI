const owner = "viaindustriessupport-byte";
const repository = "projekt-VIA-AI";
const api = `https://api.github.com/repos/${owner}/${repository}`;
const workflowUrl = `${api}/actions/workflows/ai-training.yml/runs?per_page=1`;
const releasesUrl = `${api}/releases/latest`;
const featureCount = 256;

const labelNames = {
  begruessung: "Begrüßung",
  identitaet: "Identität",
  faehigkeiten: "Fähigkeiten",
  hilfe: "Hilfe",
  status: "Status",
};

const stepNames = {
  "Checkout own project": "Projektdateien laden",
  "Setup .NET": ".NET einrichten",
  "Build model trainer": "Trainer bauen",
  "Train from project files": "Modell trainieren",
  "Create checksum and manifest": "Modell prüfen und Manifest erstellen",
  "Publish model release": "Modell veröffentlichen",
};

const elements = {
  runStatus: document.querySelector("#run-status"),
  runDetail: document.querySelector("#run-detail"),
  modelVersion: document.querySelector("#model-version"),
  modelDetail: document.querySelector("#model-detail"),
  activityIndicator: document.querySelector("#activity-indicator"),
  activityTitle: document.querySelector("#activity-title"),
  activityDescription: document.querySelector("#activity-description"),
  runNumber: document.querySelector("#run-number"),
  runCount: document.querySelector("#run-count"),
  lastUpdated: document.querySelector("#last-updated"),
  apiError: document.querySelector("#api-error"),
  footerState: document.querySelector("#footer-state"),
  refreshButton: document.querySelector("#refresh-button"),
  currentStep: document.querySelector("#current-step"),
  progressCount: document.querySelector("#progress-count"),
  progressTrack: document.querySelector(".progress-track"),
  progressFill: document.querySelector("#progress-fill"),
  stepList: document.querySelector("#step-list"),
  networkSvg: document.querySelector("#network-svg"),
  networkVersion: document.querySelector("#network-version"),
  modelLoadStatus: document.querySelector("#model-load-status"),
  testForm: document.querySelector("#test-form"),
  testInput: document.querySelector("#test-input"),
  testButton: document.querySelector("#test-button"),
  testError: document.querySelector("#test-error"),
  characterCount: document.querySelector("#character-count"),
  predictionLabel: document.querySelector("#prediction-label"),
  scoreList: document.querySelector("#score-list"),
};

let model = null;
let loadedModelUrl = "";
let networkNodes = [];
let networkEdges = [];
let outputNodes = [];

function formatDate(value) {
  if (!value) return "Noch keine Ausführung";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Zeit nicht verfügbar";
  return new Intl.DateTimeFormat("de-DE", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

function displayLabel(label) {
  return labelNames[label] || label;
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

function renderSteps(run, jobsData) {
  const steps = (jobsData?.jobs || [])
    .flatMap((job) => job.steps || [])
    .filter((step) =>
      step.name !== "Set up job" &&
      step.name !== "Complete job" &&
      !step.name.startsWith("Post "),
    );

  elements.stepList.replaceChildren();
  if (steps.length === 0) {
    const percent = run?.status === "completed" && run.conclusion === "success" ? 100 : 0;
    elements.progressFill.style.width = `${percent}%`;
    elements.progressTrack.setAttribute("aria-valuenow", String(percent));
    elements.progressCount.textContent = percent === 100 ? "100%" : "—";
    elements.currentStep.textContent = run ? "Einzelschritte nicht verfügbar" : "Warte auf Trainingslauf";
    const placeholder = document.createElement("li");
    placeholder.className = "step-row pending";
    placeholder.textContent = run ? "GitHub liefert keine Schrittdetails" : "Noch kein Lauf gestartet";
    elements.stepList.append(placeholder);
    return;
  }

  const activeStep = steps.find((step) => step.status === "in_progress");
  const failedStep = steps.find((step) => step.conclusion === "failure");
  const completedCount = steps.filter((step) =>
    step.status === "completed" && step.conclusion !== "failure",
  ).length;
  const progress = run?.status === "completed" && run.conclusion === "success"
    ? 100
    : Math.round((completedCount / steps.length) * 100);

  elements.progressFill.style.width = `${progress}%`;
  elements.progressTrack.setAttribute("aria-valuenow", String(progress));
  elements.progressCount.textContent = `${progress}%`;
  elements.currentStep.textContent = activeStep
    ? `Gerade: ${stepNames[activeStep.name] || activeStep.name}`
    : failedStep
      ? `Fehler bei: ${stepNames[failedStep.name] || failedStep.name}`
      : run?.status === "completed"
        ? run.conclusion === "success" ? "Alle Schritte erfolgreich" : "Trainingslauf beendet"
        : "Warte auf den nächsten Schritt";

  for (const step of steps) {
    const row = document.createElement("li");
    const state = step.conclusion === "failure"
      ? "failed"
      : step.status === "in_progress"
        ? "active"
        : step.status === "completed"
          ? "done"
          : "pending";
    row.className = `step-row ${state}`;

    const mark = document.createElement("span");
    mark.className = "step-mark";
    mark.textContent = state === "done" ? "✓" : state === "active" ? "…" : state === "failed" ? "!" : "·";

    const label = document.createElement("span");
    label.className = "step-label";
    label.textContent = stepNames[step.name] || step.name;

    row.append(mark, label);
    elements.stepList.append(row);
  }
}

function renderRun(run, totalCount, jobsData) {
  elements.runCount.textContent = `${totalCount} ${totalCount === 1 ? "Trainingslauf" : "Trainingsläufe"}`;
  if (!run) {
    elements.runStatus.textContent = "Noch kein Lauf";
    elements.runDetail.textContent = "Automatischer Start alle 6 Stunden";
    elements.activityTitle.textContent = "Noch kein Trainingslauf";
    elements.activityDescription.textContent = "Der Zeitplan startet die nächste Trainingsrunde automatisch.";
    elements.activityIndicator.className = "activity-indicator idle";
    elements.runNumber.textContent = "—";
    renderSteps(null, jobsData);
    return;
  }

  const statusLabels = {
    queued: "Wartet",
    requested: "Wird gestartet",
    in_progress: "Läuft gerade",
    waiting: "Wartet auf Freigabe",
  };
  const conclusionLabels = {
    success: "Erfolgreich",
    failure: "Fehlgeschlagen",
    cancelled: "Abgebrochen",
    skipped: "Übersprungen",
    timed_out: "Zeitlimit erreicht",
    action_required: "Aktion erforderlich",
  };
  const label = run.status === "completed"
    ? conclusionLabels[run.conclusion] || run.conclusion || "Abgeschlossen"
    : statusLabels[run.status] || "Läuft";

  elements.runStatus.textContent = label;
  elements.runDetail.textContent = formatDate(run.run_started_at || run.created_at);
  elements.runNumber.textContent = `#${run.run_number}`;
  elements.activityTitle.textContent = `Trainingslauf #${run.run_number}: ${label}`;
  elements.activityDescription.textContent =
    run.status === "completed" && run.conclusion === "success"
      ? "Training abgeschlossen. Die veröffentlichte Modellversion kann unten getestet werden."
      : run.status === "completed"
        ? "Der Lauf war nicht erfolgreich. Öffne die Workflow-Details für den Fehler."
        : run.status === "queued"
          ? "Der Trainingslauf wartet auf einen freien Cloud-Runner."
          : "GitHub Actions baut den Trainer und verarbeitet die Trainingsdaten.";

  const indicatorClass = run.status !== "completed"
    ? "running"
    : run.conclusion === "success"
      ? "success"
      : "failure";
  elements.activityIndicator.className = `activity-indicator ${indicatorClass}`;
  renderSteps(run, jobsData);
}

function renderRunUnavailable() {
  elements.runStatus.textContent = "Status nicht verfügbar";
  elements.runDetail.textContent = "GitHub API konnte nicht erreicht werden";
  elements.activityTitle.textContent = "Trainingsstatus nicht erreichbar";
  elements.activityDescription.textContent = "Aktualisiere später erneut oder öffne den Workflow direkt in GitHub.";
  elements.activityIndicator.className = "activity-indicator failure";
  elements.runNumber.textContent = "—";
  elements.runCount.textContent = "Laufanzahl nicht verfügbar";
  elements.currentStep.textContent = "Schritte nicht verfügbar";
  elements.progressCount.textContent = "—";
  elements.progressFill.style.width = "0%";
  elements.progressTrack.setAttribute("aria-valuenow", "0");
  elements.stepList.replaceChildren();
  const placeholder = document.createElement("li");
  placeholder.className = "step-row failed";
  placeholder.textContent = "Verbindung zur GitHub API fehlgeschlagen";
  elements.stepList.append(placeholder);
}

function renderRelease(release) {
  if (!release || release.message) {
    elements.modelVersion.textContent = "Noch keine Cloud-Version";
    elements.modelDetail.textContent = "Ein erfolgreicher Trainingslauf veröffentlicht die erste Version.";
    elements.networkVersion.textContent = "Kein Modell";
    return;
  }

  const version = release.tag_name || release.name || "Unbekannte Version";
  const weightAsset = (release.assets || []).find((asset) => asset.name === "eigene-gewichte.json");
  elements.modelVersion.textContent = version;
  elements.modelDetail.textContent = weightAsset
    ? `Veröffentlicht ${formatDate(release.published_at)} · Gewichte verfügbar`
    : `Veröffentlicht ${formatDate(release.published_at)} · Gewichtsdatei fehlt`;
  elements.networkVersion.textContent = version;
}

function validateModel(data) {
  const labels = data.Labels || data.labels;
  const rows = data.Rows ?? data.rows;
  const columns = data.Columns ?? data.columns;
  const weights = data.Weights || data.weights;

  if (!Array.isArray(labels) || labels.length === 0) {
    throw new Error("Modell enthält keine gültigen Klassen.");
  }
  if (rows !== labels.length || columns !== featureCount) {
    throw new Error("Modellformat passt nicht zur erwarteten Architektur.");
  }
  if (!Array.isArray(weights) || weights.length !== rows * columns || !weights.every(Number.isFinite)) {
    throw new Error("Modellgewichte fehlen oder sind ungültig.");
  }

  return { labels, rows, columns, weights };
}

async function loadModel(release) {
  const asset = (release?.assets || []).find((item) => item.name === "eigene-gewichte.json");
  if (!asset) {
    model = null;
    loadedModelUrl = "";
    elements.testButton.disabled = true;
    elements.modelLoadStatus.textContent = "Diese Version enthält keine Modellgewichte.";
    elements.modelLoadStatus.className = "load-status error";
    renderNetwork([]);
    return null;
  }

  if (model && loadedModelUrl === asset.browser_download_url) return null;

  elements.modelLoadStatus.textContent = "Cloud-Modell wird geladen…";
  elements.modelLoadStatus.className = "load-status";
  elements.testButton.disabled = true;

  try {
    const response = await fetch(asset.browser_download_url);
    if (!response.ok) {
      throw new Error(`Modell-Download antwortet mit HTTP ${response.status}`);
    }
    const nextModel = validateModel(await response.json());
    model = nextModel;
    loadedModelUrl = asset.browser_download_url;
    elements.testButton.disabled = false;
    elements.modelLoadStatus.textContent = "Modell bereit · Texte werden nur lokal ausgewertet";
    elements.modelLoadStatus.className = "load-status ready";
    renderNetwork(model.labels);
    return null;
  } catch (error) {
    model = null;
    elements.testButton.disabled = true;
    elements.modelLoadStatus.textContent = "Modell konnte nicht geladen werden.";
    elements.modelLoadStatus.className = "load-status error";
    renderNetwork([]);
    return error.message;
  }
}

function hashFeature(text) {
  let hash = 17;
  for (let index = 0; index < text.length; index += 1) {
    hash = (Math.imul(hash, 31) + text.charCodeAt(index)) | 0;
  }
  return (hash & 0x7fffffff) % featureCount;
}

function extractFeatures(text) {
  const normalized = ` ${text.trim().toLowerCase()} `;
  const features = new Float64Array(featureCount);

  for (let index = 0; index < normalized.length - 1; index += 1) {
    features[hashFeature(normalized.substring(index, index + 2))] += 1;
  }
  for (let index = 0; index < normalized.length - 2; index += 1) {
    features[hashFeature(normalized.substring(index, index + 3))] += 0.5;
  }
  return features;
}

function predict(text) {
  const features = extractFeatures(text);
  const scores = model.labels.map((_, row) => {
    let score = 0;
    const offset = row * model.columns;
    for (let column = 0; column < model.columns; column += 1) {
      score += model.weights[offset + column] * features[column];
    }
    return score;
  });

  const maxScore = Math.max(...scores);
  const exponentials = scores.map((score) => Math.exp(score - maxScore));
  const total = exponentials.reduce((sum, value) => sum + value, 0);
  const results = model.labels.map((label, index) => ({
    label,
    score: exponentials[index] / total,
  })).sort((left, right) => right.score - left.score);

  return {
    features,
    label: model.labels[scores.indexOf(maxScore)],
    results,
  };
}

function createSvgElement(name, attributes = {}) {
  const element = document.createElementNS("http://www.w3.org/2000/svg", name);
  for (const [key, value] of Object.entries(attributes)) {
    element.setAttribute(key, String(value));
  }
  return element;
}

function renderNetwork(labels) {
  elements.networkSvg.replaceChildren();
  networkNodes = [];
  networkEdges = [];
  outputNodes = [];

  const title = createSvgElement("title");
  title.textContent = `Neuronales Netzwerk: ${featureCount} Eingangsmerkmale und ${labels.length || 5} Absichtsklassen`;
  elements.networkSvg.append(title);

  const inputTitle = createSvgElement("text", { x: 42, y: 30, class: "layer-title" });
  inputTitle.textContent = `${featureCount} Merkmale`;
  const outputTitle = createSvgElement("text", { x: 648, y: 30, class: "layer-title" });
  outputTitle.textContent = `${labels.length || 5} Absichten`;
  elements.networkSvg.append(inputTitle, outputTitle);

  const visibleLabels = labels.length ? labels : Object.keys(labelNames);
  const outputYs = visibleLabels.map((_, index) => 61 + ((index + 1) * 205) / (visibleLabels.length + 1));
  const inputs = Array.from({ length: featureCount }, (_, index) => ({
    x: 42 + (index % 16) * 8,
    y: 54 + Math.floor(index / 16) * 8,
  }));

  const edgeGroup = createSvgElement("g", { "aria-hidden": "true" });
  for (let feature = 0; feature < inputs.length; feature += 1) {
    for (let output = 0; output < outputYs.length; output += 1) {
      const edge = createSvgElement("line", {
        x1: inputs[feature].x + 2.5,
        y1: inputs[feature].y,
        x2: 628,
        y2: outputYs[output],
        class: "network-edge",
      });
      networkEdges.push({ element: edge, feature, output });
      edgeGroup.append(edge);
    }
  }
  elements.networkSvg.append(edgeGroup);

  const inputGroup = createSvgElement("g", { "aria-hidden": "true" });
  for (let feature = 0; feature < inputs.length; feature += 1) {
    const node = createSvgElement("circle", {
      cx: inputs[feature].x,
      cy: inputs[feature].y,
      r: 2.5,
      class: "network-node input",
    });
    networkNodes.push(node);
    inputGroup.append(node);
  }
  elements.networkSvg.append(inputGroup);

  const outputGroup = createSvgElement("g");
  visibleLabels.forEach((label, index) => {
    const node = createSvgElement("circle", {
      cx: 642,
      cy: outputYs[index],
      r: 8,
      class: "network-node output",
    });
    const text = createSvgElement("text", {
      x: 660,
      y: outputYs[index] + 4,
      class: "output-label",
    });
    text.textContent = displayLabel(label);
    outputNodes.push(node);
    outputGroup.append(node, text);
  });
  elements.networkSvg.append(outputGroup);
}

function activateNetwork(features, winningLabel) {
  const active = new Set();
  for (let index = 0; index < features.length; index += 1) {
    if (features[index] > 0) active.add(index);
  }

  networkNodes.forEach((node, index) => node.classList.toggle("active", active.has(index)));
  const winningIndex = model.labels.indexOf(winningLabel);
  outputNodes.forEach((node, index) => node.classList.toggle("winner", index === winningIndex));
  networkEdges.forEach(({ element, feature, output }) => {
    const isActive = active.has(feature);
    element.classList.toggle("is-active", isActive);
    element.classList.toggle("is-winning", isActive && output === winningIndex);
  });
}

function renderPrediction(result) {
  elements.predictionLabel.textContent = displayLabel(result.label);
  elements.scoreList.replaceChildren();

  for (const item of result.results) {
    const row = document.createElement("div");
    row.className = "score-item";

    const label = document.createElement("span");
    label.textContent = displayLabel(item.label);

    const track = document.createElement("div");
    track.className = "score-track";
    const fill = document.createElement("div");
    fill.className = "score-fill";
    fill.style.width = `${Math.max(0, Math.min(100, item.score * 100))}%`;
    track.append(fill);

    const value = document.createElement("span");
    value.className = "score-value";
    value.textContent = `${(item.score * 100).toFixed(1)}%`;

    row.append(label, track, value);
    elements.scoreList.append(row);
  }

  activateNetwork(result.features, result.label);
}

function setNoRelease() {
  model = null;
  loadedModelUrl = "";
  elements.testButton.disabled = true;
  elements.modelLoadStatus.textContent = "Noch kein veröffentlichtes Modell zum Testen.";
  elements.modelLoadStatus.className = "load-status error";
  renderNetwork([]);
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
  let run = null;
  let totalCount = 0;
  let jobsData = null;

  if (runsResult.status === "fulfilled") {
    const data = runsResult.value;
    run = data.workflow_runs?.[0] || null;
    totalCount = data.total_count || 0;
    if (run) {
      try {
        jobsData = await getJson(`${api}/actions/runs/${run.id}/jobs?per_page=100`);
      } catch (error) {
        errors.push(`Trainingsschritte: ${error.message}`);
      }
    }
  } else {
    errors.push(`Trainingsstatus: ${runsResult.reason.message}`);
  }
  if (runsResult.status === "fulfilled") {
    renderRun(run, totalCount, jobsData);
  } else {
    renderRunUnavailable();
  }

  if (releaseResult.status === "fulfilled") {
    const release = releaseResult.value;
    renderRelease(release);
    const modelError = await loadModel(release);
    if (modelError) errors.push(`Testmodell: ${modelError}`);
  } else if (releaseResult.reason.message.includes("HTTP 404")) {
    renderRelease(null);
    setNoRelease();
  } else {
    errors.push(`Modell-Release: ${releaseResult.reason.message}`);
    elements.modelVersion.textContent = "Release-Status nicht verfügbar";
    elements.networkVersion.textContent = "Status nicht verfügbar";
    elements.modelLoadStatus.textContent = "Modellstatus konnte nicht geladen werden.";
    elements.modelLoadStatus.className = "load-status error";
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

elements.testInput.addEventListener("input", () => {
  elements.characterCount.textContent = `${elements.testInput.value.length} / 500`;
});

elements.testForm.addEventListener("submit", (event) => {
  event.preventDefault();
  elements.testError.hidden = true;

  if (!model) {
    elements.testError.textContent = "Das Modell ist noch nicht verfügbar.";
    elements.testError.hidden = false;
    return;
  }

  const text = elements.testInput.value.trim();
  if (!text) {
    elements.testError.textContent = "Schreib zuerst eine Nachricht.";
    elements.testError.hidden = false;
    return;
  }

  renderPrediction(predict(text));
});

elements.refreshButton.addEventListener("click", refreshDashboard);
renderNetwork([]);
refreshDashboard();
window.setInterval(refreshDashboard, 5 * 60 * 1000);
