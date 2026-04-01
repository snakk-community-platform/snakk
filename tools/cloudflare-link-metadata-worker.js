/**
 * Cloudflare Worker: Link Metadata Proxy
 *
 * Fetches OG tags and oEmbed data for a given URL.
 * Deploy to Cloudflare Workers and set the URL in Snakk config:
 *   LinkMetadata:ProxyUrl = "https://your-worker.your-subdomain.workers.dev"
 *
 * Usage: POST with JSON body { "url": "https://example.com" }
 * Returns: { "title", "description", "imageUrl", "domain", "oembedHtml" }
 *
 * To deploy:
 *   npx wrangler deploy --name snakk-link-metadata
 */

const OEMBED_PROVIDERS = {
  // Video
  'youtube.com': (url) => `https://www.youtube.com/oembed?url=${encodeURIComponent(url)}&format=json`,
  'www.youtube.com': (url) => `https://www.youtube.com/oembed?url=${encodeURIComponent(url)}&format=json`,
  'youtu.be': (url) => `https://www.youtube.com/oembed?url=${encodeURIComponent(url)}&format=json`,
  'vimeo.com': (url) => `https://vimeo.com/api/oembed.json?url=${encodeURIComponent(url)}`,
  'www.vimeo.com': (url) => `https://vimeo.com/api/oembed.json?url=${encodeURIComponent(url)}`,
  'tiktok.com': (url) => `https://www.tiktok.com/oembed?url=${encodeURIComponent(url)}`,
  'www.tiktok.com': (url) => `https://www.tiktok.com/oembed?url=${encodeURIComponent(url)}`,

  // Social
  'twitter.com': (url) => `https://publish.twitter.com/oembed?url=${encodeURIComponent(url)}`,
  'www.twitter.com': (url) => `https://publish.twitter.com/oembed?url=${encodeURIComponent(url)}`,
  'x.com': (url) => `https://publish.twitter.com/oembed?url=${encodeURIComponent(url)}`,
  'www.x.com': (url) => `https://publish.twitter.com/oembed?url=${encodeURIComponent(url)}`,
  'bsky.app': (url) => `https://embed.bsky.app/oembed?url=${encodeURIComponent(url)}&format=json`,
  'reddit.com': (url) => `https://www.reddit.com/oembed?url=${encodeURIComponent(url)}`,
  'www.reddit.com': (url) => `https://www.reddit.com/oembed?url=${encodeURIComponent(url)}`,
  'old.reddit.com': (url) => `https://www.reddit.com/oembed?url=${encodeURIComponent(url)}`,

  // Audio
  'open.spotify.com': (url) => `https://open.spotify.com/oembed?url=${encodeURIComponent(url)}`,
  'soundcloud.com': (url) => `https://soundcloud.com/oembed?url=${encodeURIComponent(url)}&format=json`,
  'www.soundcloud.com': (url) => `https://soundcloud.com/oembed?url=${encodeURIComponent(url)}&format=json`,

  // Images
  'imgur.com': (url) => `https://api.imgur.com/oembed.json?url=${encodeURIComponent(url)}`,
  'i.imgur.com': (url) => `https://api.imgur.com/oembed.json?url=${encodeURIComponent(url)}`,

  // Streaming
  'twitch.tv': (url) => `https://api.twitch.tv/v5/oembed?url=${encodeURIComponent(url)}`,
  'www.twitch.tv': (url) => `https://api.twitch.tv/v5/oembed?url=${encodeURIComponent(url)}`,

  // Music
  'bandcamp.com': (url) => `https://bandcamp.com/oembed?url=${encodeURIComponent(url)}&format=json`,

  // Code / Design
  'codepen.io': (url) => `https://codepen.io/api/oembed?url=${encodeURIComponent(url)}&format=json`,
  'www.codepen.io': (url) => `https://codepen.io/api/oembed?url=${encodeURIComponent(url)}&format=json`,
  'canva.com': (url) => `https://www.canva.com/api/v1/oembed?url=${encodeURIComponent(url)}`,
  'www.canva.com': (url) => `https://www.canva.com/api/v1/oembed?url=${encodeURIComponent(url)}`,
};

const MAX_BODY = 512 * 1024; // 500KB

export default {
  async fetch(request, env) {
    // CORS
    if (request.method === 'OPTIONS') {
      return new Response(null, {
        headers: {
          'Access-Control-Allow-Origin': '*',
          'Access-Control-Allow-Methods': 'POST',
          'Access-Control-Allow-Headers': 'Content-Type',
        },
      });
    }

    if (request.method !== 'POST') {
      return new Response(JSON.stringify({ error: 'POST required' }), { status: 405 });
    }

    // Optional: API key auth
    if (env.API_KEY) {
      const authHeader = request.headers.get('Authorization');
      if (authHeader !== `Bearer ${env.API_KEY}`) {
        return new Response(JSON.stringify({ error: 'Unauthorized' }), { status: 401 });
      }
    }

    let body;
    try {
      body = await request.json();
    } catch {
      return new Response(JSON.stringify({ error: 'Invalid JSON' }), { status: 400 });
    }

    const url = body.url;
    if (!url || typeof url !== 'string') {
      return new Response(JSON.stringify({ error: 'url is required' }), { status: 400 });
    }

    let parsedUrl;
    try {
      parsedUrl = new URL(url);
    } catch {
      return new Response(JSON.stringify({ error: 'Invalid URL' }), { status: 400 });
    }

    const domain = parsedUrl.hostname.replace(/^www\./, '');

    // Fetch OG tags and oEmbed in parallel
    const [ogResult, oembedResult] = await Promise.all([
      fetchOgTags(url),
      fetchOEmbed(url, parsedUrl.hostname),
    ]);

    const result = {
      title: oembedResult?.title || ogResult?.title || null,
      description: ogResult?.description || null,
      imageUrl: oembedResult?.thumbnail_url || ogResult?.imageUrl || null,
      domain: domain,
      oembedHtml: oembedResult?.html || null,
    };

    return new Response(JSON.stringify(result), {
      headers: {
        'Content-Type': 'application/json',
        'Access-Control-Allow-Origin': '*',
        'Cache-Control': 'public, max-age=86400', // Cache for 24h at edge
      },
    });
  },
};

async function fetchOgTags(url) {
  try {
    const response = await fetch(url, {
      headers: {
        'User-Agent': 'Snakk/1.0 (Link Preview)',
        'Accept': 'text/html',
      },
      redirect: 'follow',
      cf: { cacheTtl: 3600 }, // Cloudflare edge cache
    });

    if (!response.ok) return null;

    const contentType = response.headers.get('content-type') || '';
    if (!contentType.includes('html')) return null;

    // Read limited body
    const reader = response.body.getReader();
    let html = '';
    let bytesRead = 0;

    while (bytesRead < MAX_BODY) {
      const { done, value } = await reader.read();
      if (done) break;
      html += new TextDecoder().decode(value);
      bytesRead += value.length;
    }
    reader.cancel();

    return parseOgTags(html);
  } catch {
    return null;
  }
}

function parseOgTags(html) {
  let title = null;
  let description = null;
  let imageUrl = null;

  // OG meta tags
  const metaRegex = /<meta\s+(?:property|name)=["']([^"']+)["']\s+content=["']([^"']*)["']/gi;
  let match;
  while ((match = metaRegex.exec(html)) !== null) {
    const prop = match[1].toLowerCase();
    const content = decodeHtmlEntities(match[2]);

    switch (prop) {
      case 'og:title': title = title || content; break;
      case 'og:description': description = description || content; break;
      case 'og:image': imageUrl = imageUrl || content; break;
      case 'twitter:title': title = title || content; break;
      case 'twitter:description': description = description || content; break;
      case 'twitter:image': imageUrl = imageUrl || content; break;
    }
  }

  // Also try content="..." property="..." order (some sites use this)
  const metaRegex2 = /<meta\s+content=["']([^"']*)["']\s+(?:property|name)=["']([^"']+)["']/gi;
  while ((match = metaRegex2.exec(html)) !== null) {
    const content = decodeHtmlEntities(match[1]);
    const prop = match[2].toLowerCase();

    switch (prop) {
      case 'og:title': title = title || content; break;
      case 'og:description': description = description || content; break;
      case 'og:image': imageUrl = imageUrl || content; break;
      case 'twitter:title': title = title || content; break;
      case 'twitter:description': description = description || content; break;
      case 'twitter:image': imageUrl = imageUrl || content; break;
    }
  }

  // Fallback: <title>
  if (!title) {
    const titleMatch = html.match(/<title[^>]*>([^<]+)<\/title>/i);
    if (titleMatch) title = decodeHtmlEntities(titleMatch[1].trim());
  }

  // Fallback: <meta name="description">
  if (!description) {
    const descMatch = html.match(/<meta\s+name=["']description["']\s+content=["']([^"']*)["']/i);
    if (descMatch) description = decodeHtmlEntities(descMatch[1]);
  }

  if (!title && !description && !imageUrl) return null;
  return { title, description, imageUrl };
}

async function fetchOEmbed(url, hostname) {
  const host = hostname.toLowerCase();
  let providerFn = OEMBED_PROVIDERS[host];

  // Bandcamp artist subdomains (artist.bandcamp.com)
  if (!providerFn && host.endsWith('.bandcamp.com')) {
    providerFn = OEMBED_PROVIDERS['bandcamp.com'];
  }

  if (!providerFn) return null;

  try {
    const oembedUrl = providerFn(url);
    const response = await fetch(oembedUrl, {
      headers: { 'Accept': 'application/json' },
      cf: { cacheTtl: 3600 },
    });

    if (!response.ok) return null;
    return await response.json();
  } catch {
    return null;
  }
}

function decodeHtmlEntities(str) {
  return str
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&#x27;/g, "'")
    .replace(/&#x2F;/g, '/');
}
