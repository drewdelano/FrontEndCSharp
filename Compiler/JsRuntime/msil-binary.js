import { ExecuteMSIL } from "./msil.js";

const magic = Object.freeze([0x46, 0x45, 0x43, 0x53, 0x49, 0x4c, 0x0d, 0x0a]);
const supportedVersion = 7;
const maximumSafeBigInt = BigInt(Number.MAX_SAFE_INTEGER);
const minimumSafeBigInt = BigInt(Number.MIN_SAFE_INTEGER);

let installedMethodBodies = null;

export function decodeMsilPackage(source) {
    const bytes = source instanceof Uint8Array
        ? source
        : new Uint8Array(source);
    const reader = new BinaryReader(bytes);
    for (const expected of magic) {
        if (reader.readByte() !== expected) {
            throw new Error("The payload is not a FrontEndCSharp MSIL package.");
        }
    }
    const version = reader.readByte();
    if (version !== supportedVersion) {
        throw new Error(`MSIL package version ${version} is unsupported; expected ${supportedVersion}.`);
    }

    const strings = new Array(reader.readCount("string"));
    let previousBytes = new Uint8Array();
    for (let index = 0; index < strings.length; index++) {
        const prefixLength = reader.readCount("string prefix byte");
        if (prefixLength > previousBytes.length) {
            throw new Error(
                `MSIL package string prefix length ${prefixLength} exceeds the previous string's ${previousBytes.length} bytes.`);
        }
        const suffix = reader.readBytes(reader.readCount("string suffix byte"));
        const bytes = new Uint8Array(prefixLength + suffix.length);
        bytes.set(previousBytes.subarray(0, prefixLength));
        bytes.set(suffix, prefixLength);
        const value = decodeWtf8(bytes);
        if (index > 0 && strings[index - 1] >= value) {
            throw new Error("MSIL package strings are not strictly ordinally sorted.");
        }
        strings[index] = value;
        previousBytes = bytes;
    }

    const opcodes = new Map();
    const opcodeCount = reader.readCount("opcode");
    for (let index = 0; index < opcodeCount; index++) {
        const opcode = reader.readUint16();
        const name = strings[reader.readIndex(strings.length, "opcode name")];
        if (opcodes.has(opcode)) {
            throw new Error(`MSIL package repeats opcode 0x${opcode.toString(16).padStart(4, "0")}.`);
        }
        opcodes.set(opcode, name);
    }

    const types = new Map();
    const typeCount = reader.readCount("type");
    for (let typeIndex = 0; typeIndex < typeCount; typeIndex++) {
        const name = strings[reader.readIndex(strings.length, "type name")];
        if (types.has(name)) {
            throw new Error(`MSIL package repeats type registration '${name}'.`);
        }
        const metadata = readValue(reader, strings);
        if (metadata === null || Array.isArray(metadata) || typeof metadata !== "object") {
            throw new Error(`MSIL package type registration '${name}' has invalid metadata.`);
        }
        types.set(name, metadata);
    }

    const methods = new Map();
    const methodDefinitions = [];
    const methodCount = reader.readCount("method");
    for (let methodIndex = 0; methodIndex < methodCount; methodIndex++) {
        const key = strings[reader.readIndex(strings.length, "method key")];
        if (methods.has(key)) {
            throw new Error(`MSIL package repeats method body '${key}'.`);
        }
        const name = strings[reader.readIndex(strings.length, "method registration name")];
        const methodInfo = readValue(reader, strings);
        if (methodInfo === null || Array.isArray(methodInfo) || typeof methodInfo !== "object") {
            throw new Error(`MSIL package method registration '${name}' has invalid metadata.`);
        }
        const blocks = new Array(reader.readCount("block"));
        for (let blockIndex = 0; blockIndex < blocks.length; blockIndex++) {
            const ilOffset = reader.readCount("IL offset");
            const instructions = new Array(reader.readCount("instruction"));
            for (let instructionIndex = 0; instructionIndex < instructions.length; instructionIndex++) {
                const opcode = reader.readUint16();
                const opcodeName = opcodes.get(opcode);
                if (opcodeName === undefined) {
                    throw new Error(`MSIL package uses undeclared opcode 0x${opcode.toString(16).padStart(4, "0")}.`);
                }
                const instruction = { oc: opcodeName };
                const hasOperand = reader.readByte();
                if (hasOperand === 1) {
                    instruction.opr = readValue(reader, strings);
                } else if (hasOperand !== 0) {
                    throw new Error("MSIL package has an invalid operand marker.");
                }
                instructions[instructionIndex] = instruction;
            }
            blocks[blockIndex] = { ilOffset, instructions };
        }
        methods.set(key, blocks);
        methodDefinitions.push(Object.freeze({ key, name, methodInfo, blocks }));
    }
    reader.requireEnd();
    return Object.freeze({ version, types, methods, methodDefinitions: Object.freeze(methodDefinitions) });
}

export function installMsilPackage(source) {
    if (installedMethodBodies !== null) {
        throw new Error("A FrontEndCSharp MSIL package is already installed for this page.");
    }
    const decoded = decodeMsilPackage(source);
    for (const [name, metadata] of decoded.types) {
        globalThis.DotNet.Types[name] = metadata;
    }
    for (const definition of decoded.methodDefinitions) {
        globalThis.DotNet.Methods[definition.name] = function(ctx, ...args) {
            return ExecuteMSIL(ctx, definition.methodInfo, definition.blocks, args);
        };
    }
    installedMethodBodies = decoded.methods;
}

export async function loadMsilPackage(url) {
    const resolvedUrl = url instanceof URL ? url : new URL(url, import.meta.url);
    let bytes;
    if (resolvedUrl.protocol === "file:" && globalThis.process?.versions?.node) {
        const fileSystem = globalThis.process.getBuiltinModule?.("node:fs");
        if (fileSystem === undefined) {
            throw new Error("Loading an MSIL package from a file URL requires a Node.js runtime with process.getBuiltinModule().");
        }
        bytes = await fileSystem.promises.readFile(resolvedUrl);
    } else {
        const response = await fetch(resolvedUrl);
        if (!response.ok) {
            throw new Error(`Unable to load MSIL package '${resolvedUrl}': HTTP ${response.status}.`);
        }
        bytes = new Uint8Array(await response.arrayBuffer());
    }
    installMsilPackage(bytes);
}

export function getMethodBody(key) {
    if (installedMethodBodies === null) {
        throw new Error("The FrontEndCSharp MSIL package has not been installed.");
    }
    const body = installedMethodBodies.get(key);
    if (body === undefined) {
        throw new Error(`The FrontEndCSharp MSIL package does not contain method body '${key}'.`);
    }
    return body;
}

function readValue(reader, strings) {
    switch (reader.readByte()) {
        case 0:
            return null;
        case 1:
            return false;
        case 2:
            return true;
        case 3:
            return toJavaScriptInteger(zigZagDecode(reader.readVarUInt()));
        case 4:
            return toJavaScriptInteger(reader.readVarUInt());
        case 5:
            return reader.readFloat64();
        case 6:
            return strings[reader.readIndex(strings.length, "string value")];
        case 7: {
            const result = new Array(reader.readCount("array item"));
            for (let index = 0; index < result.length; index++) {
                result[index] = readValue(reader, strings);
            }
            return result;
        }
        case 8: {
            const result = {};
            const count = reader.readCount("object property");
            for (let index = 0; index < count; index++) {
                const name = strings[reader.readIndex(strings.length, "object property name")];
                if (Object.prototype.hasOwnProperty.call(result, name)) {
                    throw new Error(`MSIL operand object repeats property '${name}'.`);
                }
                Object.defineProperty(result, name, {
                    value: readValue(reader, strings),
                    writable: true,
                    configurable: true,
                    enumerable: true
                });
            }
            return result;
        }
        case 9:
            return reader.readFloat32();
        default:
            throw new Error("MSIL package contains an unsupported value tag.");
    }
}

function zigZagDecode(value) {
    return (value >> 1n) ^ (-(value & 1n));
}

function toJavaScriptInteger(value) {
    return value >= minimumSafeBigInt && value <= maximumSafeBigInt
        ? Number(value)
        : value;
}

function decodeWtf8(bytes) {
    const characters = [];
    let offset = 0;
    let previousWasEncodedHighSurrogate = false;
    const readContinuation = () => {
        if (offset >= bytes.length) {
            throw new Error("WTF-8 sequence is truncated.");
        }
        const value = bytes[offset++];
        if ((value & 0xc0) !== 0x80) {
            throw new Error(
                `WTF-8 sequence contains invalid continuation byte 0x${value.toString(16).padStart(2, "0")}.`);
        }
        return value;
    };
    const noncanonical = () => {
        throw new Error("WTF-8 sequence is malformed, overlong, or noncanonical.");
    };

    while (offset < bytes.length) {
        const first = bytes[offset++];
        let codePoint;
        if (first <= 0x7f) {
            codePoint = first;
        } else if (first >= 0xc2 && first <= 0xdf) {
            const second = readContinuation();
            codePoint = ((first & 0x1f) << 6) | (second & 0x3f);
        } else if (first >= 0xe0 && first <= 0xef) {
            const second = readContinuation();
            const third = readContinuation();
            if (first === 0xe0 && second < 0xa0) {
                noncanonical();
            }
            codePoint = ((first & 0x0f) << 12)
                | ((second & 0x3f) << 6)
                | (third & 0x3f);
        } else if (first >= 0xf0 && first <= 0xf4) {
            const second = readContinuation();
            const third = readContinuation();
            const fourth = readContinuation();
            if ((first === 0xf0 && second < 0x90)
                || (first === 0xf4 && second > 0x8f)) {
                noncanonical();
            }
            codePoint = ((first & 0x07) << 18)
                | ((second & 0x3f) << 12)
                | ((third & 0x3f) << 6)
                | (fourth & 0x3f);
        } else {
            noncanonical();
        }

        if (previousWasEncodedHighSurrogate
            && codePoint >= 0xdc00 && codePoint <= 0xdfff) {
            throw new Error(
                "WTF-8 encodes a surrogate pair noncanonically as two surrogate code points.");
        }
        if (codePoint <= 0xffff) {
            characters.push(codePoint);
            previousWasEncodedHighSurrogate = codePoint >= 0xd800 && codePoint <= 0xdbff;
        } else {
            codePoint -= 0x10000;
            characters.push(0xd800 + (codePoint >> 10));
            characters.push(0xdc00 + (codePoint & 0x3ff));
            previousWasEncodedHighSurrogate = false;
        }
    }

    let result = "";
    for (let index = 0; index < characters.length; index += 0x8000) {
        result += String.fromCharCode(...characters.slice(index, index + 0x8000));
    }
    return result;
}

class BinaryReader {
    constructor(bytes) {
        this.bytes = bytes;
        this.offset = 0;
        this.view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    }

    readByte() {
        this.require(1);
        return this.bytes[this.offset++];
    }

    readBytes(length) {
        this.require(length);
        const result = this.bytes.subarray(this.offset, this.offset + length);
        this.offset += length;
        return result;
    }

    readUint16() {
        this.require(2);
        const result = this.view.getUint16(this.offset, true);
        this.offset += 2;
        return result;
    }

    readFloat64() {
        this.require(8);
        const result = this.view.getFloat64(this.offset, true);
        this.offset += 8;
        return result;
    }

    readFloat32() {
        this.require(4);
        const result = this.view.getFloat32(this.offset, true);
        this.offset += 4;
        return result;
    }

    readVarUInt() {
        let result = 0n;
        for (let shift = 0n; shift < 64n; shift += 7n) {
            const next = this.readByte();
            if (shift === 63n && (next & 0xfe) !== 0) {
                throw new Error("MSIL package contains an overflowing variable-length integer.");
            }
            result |= BigInt(next & 0x7f) << shift;
            if ((next & 0x80) === 0) {
                return result;
            }
        }
        throw new Error("MSIL package contains an invalid variable-length integer.");
    }

    readCount(kind) {
        const value = this.readVarUInt();
        if (value > maximumSafeBigInt) {
            throw new Error(`MSIL package ${kind} count is too large.`);
        }
        return Number(value);
    }

    readIndex(count, kind) {
        const index = this.readCount(kind);
        if (index >= count) {
            throw new Error(`MSIL package ${kind} index ${index} is out of range.`);
        }
        return index;
    }

    require(length) {
        if (!Number.isSafeInteger(length) || length < 0 || this.offset + length > this.bytes.length) {
            throw new Error("MSIL package ended unexpectedly.");
        }
    }

    requireEnd() {
        if (this.offset !== this.bytes.length) {
            throw new Error("MSIL package contains trailing data.");
        }
    }
}
