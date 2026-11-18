import { createPrivateKey, createPublicKey, sign } from 'node:crypto';
import { readFileSync } from 'node:fs';

const repository = 'maibui7879/BeroLauncher';
const api = `https://api.github.com/repos/${repository}`;
const [tag, keyPath] = process.argv.slice(2);
const token = process.env.GH_TOKEN;

if (!/^launcher-v\d+\.\d+\.\d+$/.test(tag ?? '') || !keyPath || !token) {
  throw new Error(
    'Usage: GH_TOKEN=<contents-write token> node tools/publish-launcher-channel.mjs launcher-vX.Y.Z <private-key-path>',
  );
}

const headers = {
  Accept: 'application/vnd.github+json',
  Authorization: `Bearer ${token}`,
  'X-GitHub-Api-Version': '2022-11-28',
  'User-Agent': 'BeroLauncher-local-channel-publisher',
};

async function github(url, options = {}) {
  const response = await fetch(url, { ...options, headers: { ...headers, ...options.headers } });
  if (!response.ok) {
    throw new Error(`GitHub API request failed (${response.status} ${response.statusText}).`);
  }
  return response.status === 204 ? null : response.json();
}

const version = tag.slice('launcher-v'.length);
const release = await github(`${api}/releases/tags/${tag}`);
const payloadAsset = release.assets.find((asset) => asset.name === 'launcher-channel-payload.json');
const zipAsset = release.assets.find((asset) => asset.name === 'BeroLauncher-win-x64.zip');
if (!payloadAsset || !zipAsset || payloadAsset.size > 65536) {
  throw new Error('Release is missing the update payload or launcher ZIP.');
}

const payloadResponse = await fetch(payloadAsset.browser_download_url);
if (!payloadResponse.ok) {
  throw new Error(`Could not download the update payload (${payloadResponse.status}).`);
}
const payloadBytes = Buffer.from(await payloadResponse.arrayBuffer());
if (payloadBytes.length !== payloadAsset.size || payloadBytes.length > 65536) {
  throw new Error('Downloaded update payload has an unexpected size.');
}
const payload = JSON.parse(payloadBytes.toString('utf8'));
if (
  payload.schema !== 1 ||
  payload.repository !== repository ||
  payload.version !== version ||
  payload.tag !== tag ||
  payload.asset !== zipAsset.name ||
  payload.size !== zipAsset.size ||
  payload.sha256 !== zipAsset.digest?.replace(/^sha256:/, '') ||
  zipAsset.browser_download_url !==
    `https://github.com/${repository}/releases/download/${tag}/${zipAsset.name}`
) {
  throw new Error('Update payload does not match the published release assets.');
}

const privateKey = createPrivateKey({
  key: readFileSync(keyPath),
  ...(process.env.LAUNCHER_KEY_PASSPHRASE
    ? { passphrase: process.env.LAUNCHER_KEY_PASSPHRASE }
    : {}),
});
const keyPublic = createPublicKey(privateKey).export({ format: 'der', type: 'spki' });
const trustedPublic = createPublicKey(readFileSync('launcher/update-public-key.pem')).export({
  format: 'der',
  type: 'spki',
});
if (!keyPublic.equals(trustedPublic)) {
  throw new Error("Signing key does not match launcher's embedded public key.");
}

const envelope = Buffer.from(
  JSON.stringify({
    payload: payloadBytes.toString('base64'),
    signature: sign('RSA-SHA256', payloadBytes, privateKey).toString('base64'),
  }) + '\n',
);

const stableResponse = await fetch(`${api}/releases/tags/launcher-stable`, { headers });
let stable;
if (stableResponse.status === 404) {
  stable = await github(`${api}/releases`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      tag_name: 'launcher-stable',
      target_commitish: 'main',
      name: 'BeroLauncher stable update channel',
      body: 'Signed update metadata for BeroLauncher.',
    }),
  });
} else if (stableResponse.ok) {
  stable = await stableResponse.json();
} else {
  throw new Error(`Could not read stable update release (${stableResponse.status}).`);
}

const oldChannel = stable.assets.find((asset) => asset.name === 'channel.json');
if (oldChannel) {
  await github(`${api}/releases/assets/${oldChannel.id}`, { method: 'DELETE' });
}
const uploadUrl = stable.upload_url.replace(/\{\?name,label\}$/, '?name=channel.json');
await github(uploadUrl, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: envelope,
});

console.log(`Published signed launcher update channel for ${version}.`);
