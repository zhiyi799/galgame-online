// Validate the Supabase migrations' SQL syntax without a live Postgres,
// using libpg-query (the real PostgreSQL parser compiled to WASM).
// Checks both the outer statements (parse) and each plpgsql function body
// (parsePlPgSQL, extracted from the $$...$$ dollar-quoted blocks).
// Usage: npm run check-sql   (run from cloud/workers)
// Note: this checks SYNTAX only. It does not run the migrations, does not
// resolve references to auth.users, and does not check RLS policy semantics.
// Apply the migrations to a real Supabase project before deploying (README).

import { readdir, readFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import pkg from "libpg-query";

const { parse, parsePlPgSQL } = pkg;

const dir = resolve(
  join(fileURLToPath(new URL(".", import.meta.url)), "..", "..", "supabase", "migrations"),
);

const files = (await readdir(dir)).filter((f) => f.endsWith(".sql")).sort();
let failures = 0;

for (const file of files) {
  const sql = await readFile(join(dir, file), "utf-8");
  try {
    const parsed = await parse(sql);
    const count = parsed.stmts?.length ?? 0;

    // Validate every plpgsql function: parsePlPgSQL takes the whole
    // CREATE FUNCTION statement (not just the $$...$$ body).
    const funcs = [...sql.matchAll(/create or replace function[\s\S]*?\$\$;/gi)].map(
      (m) => m[0],
    );
    for (const fn of funcs) {
      await parsePlPgSQL(fn);
    }
    console.log(`OK   ${file} (${count} statements, ${funcs.length} plpgsql function${funcs.length === 1 ? "" : "s"})`);
  } catch (e) {
    failures++;
    console.error(`FAIL ${file}: ${e instanceof Error ? e.message : String(e)}`);
  }
}

if (failures > 0) {
  console.error(`\n${failures} migration file(s) failed to parse.`);
  process.exit(1);
}
console.log("\nAll migrations parse cleanly (syntax only — not executed).");
