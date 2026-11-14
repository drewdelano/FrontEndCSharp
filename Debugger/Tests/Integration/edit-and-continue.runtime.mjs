import { readFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const [runtimeRoot, updateManifestPath, updateModulePath, updateHash] = process.argv.slice(2);
if (!runtimeRoot || !updateManifestPath || !updateModulePath || !updateHash) {
    throw new Error("Usage: node edit-and-continue.runtime.mjs <runtime-root> <update-manifest> <update-module> <update-hash>");
}

const url = relative => pathToFileURL(path.join(runtimeRoot, relative)).href;
await import(url("bootstrap-msil.js"));
const { loadMsilPackage } = await import(url("msil-binary.js"));
await loadMsilPackage(new URL(url("msil.bin")));
const { managedDebugger } = await import(url("managed-debugger.js"));
const methodName = "EditAndContinueFixture.Program.GetValue()";
const before = globalThis.DotNet.Methods[methodName]({});
const updateManifest = JSON.parse(await readFile(updateManifestPath, "utf8"));
managedDebugger.initializeUpdates(updateManifest.buildId, updateManifest.buildId);
const updateSource = await readFile(path.join(runtimeRoot, updateModulePath), "utf8");
const evaluateUpdate = () => (0, eval)(
    `((__frontEndCSharpUpdateHash) => (${updateSource}))(${JSON.stringify(updateHash)})`);
const acknowledgement = evaluateUpdate();
const after = globalThis.DotNet.Methods[methodName]({});
let staleRejected = false;
try {
    evaluateUpdate();
} catch (error) {
    staleRejected = /stale or out of order/i.test(String(error));
}
console.log(JSON.stringify({
    before: before.v,
    after: after.v,
    generation: acknowledgement.generation,
    hash: acknowledgement.hash,
    staleRejected
}));
