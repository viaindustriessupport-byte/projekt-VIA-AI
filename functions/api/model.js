const owner = "viaindustriessupport-byte";
const repository = "projekt-VIA-AI";
const githubApi = `https://api.github.com/repos/${owner}/${repository}`;

export async function onRequestGet(context) {
  const { request } = context;
  const url = new URL(request.url);
  const tag = url.searchParams.get("tag");
  if (!tag || !/^ai-model-\d+$/.test(tag)) {
    return json({ error: "Eine gültige Modellversion fehlt." }, 400);
  }

  const cacheKey = new Request(url.toString(), request);
  const cache = caches.default;
  const cached = await cache.match(cacheKey);
  if (cached) return cached;

  const releaseResponse = await fetch(`${githubApi}/releases/tags/${tag}`, {
    headers: {
      Accept: "application/vnd.github+json",
      "X-GitHub-Api-Version": "2022-11-28",
      "User-Agent": "via-ai-training-dashboard",
    },
  });
  if (!releaseResponse.ok) {
    return json({ error: `GitHub konnte das Modell-Release nicht laden (HTTP ${releaseResponse.status}).` }, 502);
  }

  const release = await releaseResponse.json();
  const asset = release.assets?.find((item) => item.name === "eigene-gewichte.json" && item.state === "uploaded");
  if (!asset) {
    return json({ error: "Das Release enthält keine verfügbaren Modellgewichte." }, 404);
  }

  const modelResponse = await fetch(asset.browser_download_url);
  if (!modelResponse.ok) {
    return json({ error: `GitHub konnte die Modellgewichte nicht laden (HTTP ${modelResponse.status}).` }, 502);
  }

  const response = new Response(modelResponse.body, {
    headers: {
      "Cache-Control": "public, max-age=300, s-maxage=300",
      "Content-Type": "application/json; charset=utf-8",
    },
  });
  context.waitUntil(cache.put(cacheKey, response.clone()));
  return response;
}

function json(body, status) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Cache-Control": "no-store",
      "Content-Type": "application/json; charset=utf-8",
    },
  });
}
