import { createHash } from 'node:crypto';
import { readFileSync, statSync, writeFileSync } from 'node:fs';

const [archivePath, tag] = process.argv.slice(2);
const version = tag?.replace(/^launcher-v/, '');

if (!archivePath || !/^launcher-v\d+\.\d+\.\d+$/.test(tag ?? '')) {
  throw new Error('Expected a launcher ZIP path and a launcher-vX.Y.Z tag.');
}

const size = statSync(archivePath).size;
const sha256 = createHash('sha256').update(readFileSync(archivePath)).digest('hex');
const payload = {
  schema: 1,
  repository: 'maibui7879/BeroLauncher',
  version,
  tag,
  asset: 'BeroLauncher-win-x64.zip',
  size,
  sha256,
  notes: `BeroLauncher ${version}`,
};

writeFileSync('artifacts/launcher-channel-payload.json', JSON.stringify(payload) + '\n');
