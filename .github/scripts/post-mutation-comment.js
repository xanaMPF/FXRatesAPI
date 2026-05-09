// Posts a PR comment summarising Stryker survivors.
// Requires env: GH_TOKEN, REPORT_PATH, PR_NUMBER, REPO
// Uses only Node.js built-ins + the GitHub REST API — no extra dependencies.

const fs = require('fs');
const https = require('https');

const { GH_TOKEN, REPORT_PATH, PR_NUMBER, REPO } = process.env;

if (!REPORT_PATH || !fs.existsSync(REPORT_PATH)) {
  console.log('No mutation report found — skipping comment.');
  process.exit(0);
}

const report = JSON.parse(fs.readFileSync(REPORT_PATH, 'utf8'));

// Collect survivors across all files
const survivors = [];

for (const [filePath, fileResult] of Object.entries(report.files || {})) {
  for (const mutant of fileResult.mutants || []) {
    if (mutant.status === 'Survived') {
      survivors.push({
        file: filePath,
        line: mutant.location?.start?.line ?? '?',
        mutatorName: mutant.mutatorName,
        
        description: mutant.description,
        original: mutant.replacement ?? '',
      });
    }
  }
}

const totalMutants = Object.values(report.files || {})
  .flatMap(f => f.mutants || [])
  .filter(m => ['Survived', 'Killed', 'Timeout', 'NoCoverage'].includes(m.status)).length;

const killed = totalMutants - survivors.length;
const score = totalMutants > 0 ? ((killed / totalMutants) * 100).toFixed(1) : 'N/A';

let body;

if (survivors.length === 0) {
  body = `## Mutation Testing (diff-based)\n\n` +
    `All **${totalMutants}** mutants killed. Mutation score: **${score}%** \u2705\n\n` +
    `> Only files changed in this PR were mutated.`;
} else {
  const rows = survivors
    .slice(0, 50) // cap to avoid GitHub comment size limits
    .map(s => `| \`${s.file}\` | ${s.line} | ${s.mutatorName} | ${s.description} |`)
    .join('\n');

  const truncationNote = survivors.length > 50
    ? `\n> Showing first 50 of ${survivors.length} survivors.`
    : '';

  body = `## Mutation Testing (diff-based)\n\n` +
    `**${survivors.length} survivor(s)** out of ${totalMutants} mutants — score: **${score}%**\n\n` +
    `> Only files changed in this PR were mutated. Survivors are not blocking — they are here for review.\n\n` +
    `| File | Line | Mutator | Description |\n` +
    `|------|------|---------|-------------|\n` +
    rows +
    truncationNote;
}

// Find existing bot comment to update (sticky behaviour)
function githubRequest(method, path, bodyData) {
  return new Promise((resolve, reject) => {
    const data = bodyData ? JSON.stringify(bodyData) : null;
    const req = https.request(
      {
        hostname: 'api.github.com',
        path,
        method,
        headers: {
          'Authorization': `Bearer ${GH_TOKEN}`,
          'Accept': 'application/vnd.github+json',
          'User-Agent': 'stryker-pr-comment',
          'X-GitHub-Api-Version': '2022-11-28',
          ...(data ? { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(data) } : {}),
        },
      },
      res => {
        let raw = '';
        res.on('data', chunk => (raw += chunk));
        res.on('end', () => {
          try { resolve({ status: res.statusCode, body: JSON.parse(raw) }); }
          catch { resolve({ status: res.statusCode, body: raw }); }
        });
      }
    );
    req.on('error', reject);
    if (data) req.write(data);
    req.end();
  });
}

const MARKER = '## Mutation Testing (diff-based)';

async function run() {
  // List existing comments
  const listRes = await githubRequest('GET', `/repos/${REPO}/issues/${PR_NUMBER}/comments?per_page=100`);
  const existing = (listRes.body || []).find(
    c => c.user?.type === 'Bot' && c.body?.startsWith(MARKER)
  );

  if (existing) {
    await githubRequest('PATCH', `/repos/${REPO}/issues/comments/${existing.id}`, { body });
    console.log(`Updated existing comment #${existing.id}`);
  } else {
    await githubRequest('POST', `/repos/${REPO}/issues/${PR_NUMBER}/comments`, { body });
    console.log('Posted new comment');
  }
}

run().catch(err => {
  console.error('Failed to post comment:', err);
  process.exit(1);
});
