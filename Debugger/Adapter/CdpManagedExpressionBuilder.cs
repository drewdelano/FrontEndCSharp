namespace FrontEndCSharp.DebugAdapter;

internal static class CdpManagedExpressionBuilder
{
    internal const string ManagedFrame = """
        (() => {
            const objects = [];
            const objectIds = new WeakMap();
            const fieldDefinition = storageKey => {
                for (const type of Object.values(globalThis.DotNet?.Types ?? {})) {
                    const definition = type?.instances?.[storageKey];
                    if (definition !== undefined) return definition;
                }
                return undefined;
            };
            const fields = instance => {
                const entries = Object.entries(instance).map(([storageKey, value]) => ({
                    storageKey, value, definition: fieldDefinition(storageKey)
                }));
                const counts = new Map();
                for (const entry of entries) {
                    const name = entry.definition?.name ?? entry.storageKey;
                    counts.set(name, (counts.get(name) ?? 0) + 1);
                }
                return entries.map(entry => {
                    const logicalName = entry.definition?.name ?? entry.storageKey;
                    return {
                        ...entry,
                        name: counts.get(logicalName) > 1
                            ? `${entry.definition?.declaringType ?? "unknown"}.${logicalName}`
                            : logicalName
                    };
                });
            };
            const render = (wrapped, target) => {
                const value = wrapped && typeof wrapped === "object" && "value" in wrapped
                    ? wrapped.value
                    : wrapped;
                const type = value && typeof value === "object" && typeof value.t === "string"
                    ? value.t
                    : value === null ? "null" : typeof value;
                const raw = value && typeof value === "object" && "t" in value && "v" in value
                    ? value.v
                    : value;
                let text;
                if (raw === null) text = "null";
                else if (typeof raw === "string") text = JSON.stringify(raw);
                else if (typeof raw === "bigint") text = raw.toString();
                else if (typeof raw === "number" && Object.is(raw, -0)) text = "-0";
                else if (typeof raw === "object") text = `{${type}}`;
                else text = String(raw);
                let objectId;
                if (raw !== null && typeof raw === "object") {
                    objectId = objectIds.get(raw);
                    if (objectId === undefined && objects.length < 1024) {
                        objectId = objects.length + 1;
                        objectIds.set(raw, objectId);
                        const snapshot = { id: objectId, fields: [] };
                        objects.push(snapshot);
                        for (const { name, storageKey, value: field } of fields(raw)) {
                            snapshot.fields.push({ name, target: {
                                kind: target.kind,
                                index: target.index,
                                path: [...target.path, storageKey]
                            }, ...render(field, {
                                kind: target.kind,
                                index: target.index,
                                path: [...target.path, storageKey]
                            }) });
                        }
                    }
                }
                return { type, value: text, objectId };
            };
            const frame = {
                moduleId: opts.methodInfo.debugModuleId,
                methodToken: opts.methodInfo.debugMethodToken,
                blockIndex: opts.currentBlockIndex,
                instructionIndex: opts.instructionIndex,
                debugDepth: opts.ctx.debugDepth,
                stopReason: globalThis.__FrontEndCSharpDebugger.currentStop?.reason ?? "pause",
                breakpointIds: globalThis.__FrontEndCSharpDebugger.currentStop?.breakpointIds ?? [],
                arguments: opts.methodArgs.map((value, index) => render(value, {
                    kind: "argument", index, path: []
                })),
                locals: opts.locals.map((value, index) => render(value, {
                    kind: "local", index, path: []
                })),
                fields: (() => {
                    const receiver = opts.methodArgs[0];
                    const referenced = receiver && typeof receiver === "object" && "value" in receiver
                        ? receiver.value
                        : receiver;
                    const instance = referenced && typeof referenced === "object" && "v" in referenced
                        ? referenced.v
                        : referenced;
                    return instance && typeof instance === "object"
                        ? fields(instance).map(({ name, storageKey, value }) => ({
                            name,
                            target: { kind: "argument", index: 0, path: [storageKey] },
                            ...render(value, { kind: "argument", index: 0, path: [storageKey] })
                        }))
                        : [];
                })(),
                asyncParents: opts.ctx.asyncParents ?? [],
                objects
            };
            return frame;
        })()
        """;

    internal static string BuildAssignment(string targetJson, string valueJson) => """
        (() => {
            const target = TARGET_JSON;
            const input = VALUE_JSON;
            const runtime = wrapped => {
                let value = wrapped;
                while (value && typeof value === "object" && "value" in value
                    && !("t" in value && "v" in value)) value = value.value;
                return value;
            };
            const managed = wrapped => {
                const value = runtime(wrapped);
                return value && typeof value === "object" && "t" in value && "v" in value
                    ? value : null;
            };
            const fail = message => { throw new Error("FrontEndCSharp: " + message); };
            const parse = (typeName, text) => {
                const type = String(typeName || "").split("!").pop();
                if (text === "null") {
                    if (type === "System.String" || type === "System.Object" || type.endsWith("[]")) return null;
                    fail("null cannot be assigned to " + type + ".");
                }
                if (type === "System.String") {
                    if (text.startsWith('"')) {
                        try { const parsed = JSON.parse(text); if (typeof parsed === "string") return parsed; }
                        catch { fail("the string value is not valid JSON."); }
                    }
                    return text;
                }
                if (type === "System.Char") {
                    let parsed = text;
                    if (text.startsWith('"')) {
                        try { parsed = JSON.parse(text); } catch { fail("the character value is invalid."); }
                    } else if (text.startsWith("'") && text.endsWith("'")) parsed = text.slice(1, -1);
                    if (typeof parsed !== "string" || [...parsed].length !== 1) fail("a char requires exactly one character.");
                    return parsed;
                }
                if (type === "System.Boolean") {
                    if (text === "true") return true;
                    if (text === "false") return false;
                    fail("a bool must be true or false.");
                }
                const integerRanges = {
                    "System.SByte": [-128, 127], "System.Byte": [0, 255],
                    "System.Int16": [-32768, 32767], "System.UInt16": [0, 65535],
                    "System.Int32": [-2147483648, 2147483647], "System.UInt32": [0, 4294967295]
                };
                if (type in integerRanges) {
                    if (!/^[+-]?\d+$/.test(text)) fail("the value is not a valid " + type + ".");
                    const value = Number(text), range = integerRanges[type];
                    if (!Number.isSafeInteger(value) || value < range[0] || value > range[1]) fail("the value is outside the range of " + type + ".");
                    return value;
                }
                if (type === "System.Int64" || type === "System.UInt64") {
                    if (!/^[+-]?\d+$/.test(text)) fail("the value is not a valid " + type + ".");
                    const value = BigInt(text);
                    if (type === "System.Int64" && (value < -9223372036854775808n || value > 9223372036854775807n)
                        || type === "System.UInt64" && (value < 0n || value > 18446744073709551615n))
                        fail("the value is outside the range of " + type + ".");
                    return value;
                }
                if (type === "System.Single" || type === "System.Double" || type === "System.Decimal") {
                    const value = Number(text);
                    if (text.trim() === "" || !Number.isFinite(value)) fail("the value is not a finite " + type + ".");
                    return value;
                }
                fail("assignments to " + (type || "this value") + " are not supported.");
            };
            let container = target.kind === "argument" ? opts.methodArgs
                : target.kind === "local" ? opts.locals : null;
            if (!container || target.index < 0 || target.index >= container.length) fail("the assignment target is stale.");
            let key = target.index;
            for (const segment of target.path || []) {
                const value = runtime(container[key]);
                const raw = value && typeof value === "object" && "t" in value && "v" in value ? value.v : value;
                if (!raw || typeof raw !== "object" || !(segment in raw)) fail("the assignment target is stale.");
                container = raw;
                key = segment;
            }
            let current = container[key];
            const typed = managed(current);
            const converted = parse(typed?.t, input);
            if (typed) typed.v = converted;
            else {
                let holder = current;
                while (holder && typeof holder === "object" && "value" in holder
                    && !("t" in holder && "v" in holder)) {
                    if (!(holder.value && typeof holder.value === "object" && "value" in holder.value)) {
                        holder.value = converted;
                        holder = null;
                        break;
                    }
                    holder = holder.value;
                }
                if (holder !== null) container[key] = converted;
            }
            current = container[key];
            const value = managed(current);
            const raw = value ? value.v : runtime(current);
            return {
                type: value?.t ?? (raw === null ? "null" : typeof raw),
                value: raw === null ? "null" : typeof raw === "string" ? JSON.stringify(raw)
                    : typeof raw === "bigint" ? raw.toString() : typeof raw === "object"
                        ? "{" + (value?.t ?? "object") + "}"
                        : typeof raw === "number" && Object.is(raw, -0) ? "-0" : String(raw)
            };
        })()
        """.Replace("TARGET_JSON", targetJson, StringComparison.Ordinal)
            .Replace("VALUE_JSON", valueJson, StringComparison.Ordinal);
}
