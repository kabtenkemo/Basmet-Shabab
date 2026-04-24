const backendBaseUrl = 'https://basmet-shabab.runasp.net';

function buildTargetUrl(pathSegments, query) {
  const normalizedPath = Array.isArray(pathSegments)
    ? pathSegments.join('/')
    : String(pathSegments ?? '').replace(/^\/+/, '');

  const targetUrl = new URL(`${backendBaseUrl}/api/${normalizedPath}`);

  for (const [key, value] of Object.entries(query || {})) {
    if (key === 'path') {
      continue;
    }

    if (Array.isArray(value)) {
      value.forEach((item) => targetUrl.searchParams.append(key, String(item)));
    } else if (value !== undefined) {
      targetUrl.searchParams.set(key, String(value));
    }
  }

  return targetUrl;
}

function copyHeaders(sourceHeaders) {
  const headers = new Headers();
  for (const [key, value] of Object.entries(sourceHeaders || {})) {
    const lowerKey = key.toLowerCase();
    if (
      lowerKey === 'host'
      || lowerKey === 'connection'
      || lowerKey === 'content-length'
      || lowerKey === 'accept-encoding'
      || lowerKey === 'x-forwarded-for'
      || lowerKey === 'x-forwarded-proto'
      || lowerKey === 'x-forwarded-host'
      || lowerKey === 'x-real-ip'
    ) {
      continue;
    }

    if (typeof value === 'string') {
      headers.set(key, value);
    } else if (Array.isArray(value)) {
      headers.set(key, value.join(', '));
    }
  }

  return headers;
}

module.exports = async (req, res) => {
  const targetUrl = buildTargetUrl(req.query.path, req.query);
  const headers = copyHeaders(req.headers);
  headers.set('host', targetUrl.host);

  let body;
  if (req.method && !['GET', 'HEAD'].includes(req.method.toUpperCase())) {
    if (typeof req.body === 'string' || Buffer.isBuffer(req.body)) {
      body = req.body;
    } else if (req.body !== undefined) {
      body = JSON.stringify(req.body);
    }
  }

  try {
    const upstreamResponse = await fetch(targetUrl, {
      method: req.method,
      headers,
      body
    });

    res.status(upstreamResponse.status);

    upstreamResponse.headers.forEach((value, key) => {
      const lowerKey = key.toLowerCase();
      if (lowerKey === 'transfer-encoding' || lowerKey === 'content-encoding' || lowerKey === 'content-length') {
        return;
      }

      res.setHeader(key, value);
    });

    const responseBuffer = Buffer.from(await upstreamResponse.arrayBuffer());
    res.send(responseBuffer);
  } catch (error) {
    res.status(502).json({
      message: 'تعذر الوصول إلى الخادم الخلفي عبر Vercel proxy.',
      detail: error instanceof Error ? error.message : 'Unknown proxy error'
    });
  }
};
