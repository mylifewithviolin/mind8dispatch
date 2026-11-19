const fs = require("node:fs");
const path = require("node:path");

const args = process.argv.slice(2);
const strict = args.includes("--strict");
const paths = args.filter((arg) => arg !== "--strict");
const cTablePath = path.resolve(paths[0] ?? "../mind8kernel/c_words.tbl");
const csFunctionsPath = path.resolve(paths[1] ?? "CsFunctions.cs");

function readEntries(filePath, pattern, nameIndex) {
    const text = fs.readFileSync(filePath, "utf8").replace(/^\s*\/\/.*$/gm, "");
    return [...text.matchAll(pattern)].map((match) => ({
        id: Number.parseInt(match[1], 16),
        name: match[nameIndex],
    }));
}

function formatId(id) {
    return `0x${id.toString(16).toUpperCase().padStart(4, "0")}`;
}

function getDuplicateIds(entries) {
    const counts = new Map();
    for (const entry of entries) {
        counts.set(entry.id, (counts.get(entry.id) ?? 0) + 1);
    }
    return [...counts]
        .filter(([, count]) => count > 1)
        .map(([id, count]) => ({ id: formatId(id), count }));
}

function getRanges(entries) {
    const ids = [...new Set(entries.map((entry) => entry.id))].sort((a, b) => a - b);
    const ranges = [];
    for (const id of ids) {
        const last = ranges.at(-1);
        if (last && id === last[1] + 1) {
            last[1] = id;
        } else {
            ranges.push([id, id]);
        }
    }
    return ranges.map(([start, end]) =>
        start === end ? formatId(start) : `${formatId(start)}-${formatId(end)}`,
    );
}

function getAliases(entries) {
    const groups = new Map();
    for (const entry of entries) {
        const key = entry.name.toLowerCase().replaceAll("_", "");
        if (!groups.has(key)) {
            groups.set(key, { name: entry.name, ids: [] });
        }
        groups.get(key).ids.push(entry.id);
    }
    return [...groups.values()]
        .filter((group) => group.ids.length > 1)
        .map((group) => `${group.name}: ${group.ids.map(formatId).join(", ")}`);
}

let cEntries;
let csEntries;
try {
    cEntries = readEntries(cTablePath, /\/\*([0-9A-Fa-f]{4})\*\/\s*([A-Za-z0-9_]+)\s*,/g, 2);
    csEntries = readEntries(csFunctionsPath, /csFuncs\[\s*0x([0-9A-Fa-f]+)\s*\]\s*=\s*([A-Za-z0-9_]+)\s*;/g, 2);
} catch (error) {
    console.error(error.message);
    process.exit(2);
}

if (cEntries.length === 0 || csEntries.length === 0) {
    console.error("Could not parse both function tables.");
    process.exit(2);
}

const cById = new Map(cEntries.map((entry) => [entry.id, entry.name]));
const csById = new Map(csEntries.map((entry) => [entry.id, entry.name]));
const normalize = (name) => name.toLowerCase().replaceAll("_", "");
const cSharpHandlerAliases = new Map([
    [0x0296, "BinaryMode"],
    [0x0297, "TextMode"],
    [0x0298, "SetFileMode"],
    [0x02CE, "HighSecurityMode"],
]);
const missing = cEntries.filter((entry) => !csById.has(entry.id));
const extra = csEntries.filter((entry) => !cById.has(entry.id));
const mismatches = csEntries
    .filter((entry) => cById.has(entry.id)
        && normalize(cById.get(entry.id)) !== normalize(entry.name)
        && cSharpHandlerAliases.get(entry.id) !== entry.name)
    .map((entry) => ({ id: formatId(entry.id), c: cById.get(entry.id), csharp: entry.name }));
const duplicateCIds = getDuplicateIds(cEntries);
const duplicateCsIds = getDuplicateIds(csEntries);

console.log(`C entries: ${cEntries.length} (${formatId(Math.min(...cEntries.map((entry) => entry.id)))}-${formatId(Math.max(...cEntries.map((entry) => entry.id)))})`);
console.log(`C# registrations: ${csEntries.length} (${formatId(Math.min(...csEntries.map((entry) => entry.id)))}-${formatId(Math.max(...csEntries.map((entry) => entry.id)))})`);
console.log(`C entries without C# registration: ${missing.length}`);
console.log(`Missing ranges: ${getRanges(missing).join(", ") || "none"}`);
console.log(`C# registrations without C entry: ${extra.length}`);
console.log(`Name mismatches (case/underscore insensitive): ${JSON.stringify(mismatches)}`);
console.log(`Duplicate C IDs: ${JSON.stringify(duplicateCIds)}`);
console.log(`Duplicate C# IDs: ${JSON.stringify(duplicateCsIds)}`);
console.log(`C aliases: ${getAliases(cEntries).join("; ") || "none"}`);
console.log(`C# aliases: ${getAliases(csEntries).join("; ") || "none"}`);

if (duplicateCIds.length > 0 || duplicateCsIds.length > 0 || extra.length > 0 || mismatches.length > 0 || (strict && missing.length > 0)) {
    process.exitCode = 1;
}