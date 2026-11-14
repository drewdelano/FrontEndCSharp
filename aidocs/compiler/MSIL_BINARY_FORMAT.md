# Binary MSIL Package

FrontEndCSharp writes interpreted type registrations, method registrations, method metadata, and method bodies to one `msil.bin` file per application. Duplicate call-graph entries that normalize to the same type or method identity share one package entry after the writer verifies that their normalized data is identical. The package is decoded before the managed entry point runs; its loader populates `DotNet.Types` and `DotNet.Methods`, while bodies retain the existing interpreter shape:

```text
[{ ilOffset, instructions: [{ oc, opr? }] }]
```

This boundary keeps the interpreter independent of the transport representation. Runtime overrides and generated BCL modules remain JavaScript because they provide executable JavaScript rather than interpreted method bodies.

## Version 7 wire format

All fixed-width numeric fields are little-endian. Counts and indexes are unsigned LEB128-style variable-length integers. Signed integers use ZigZag encoding before the unsigned variable-length encoding.

```text
magic                  8 bytes: ASCII "FECSIL\r\n"
version                byte: 7
stringCount            varuint
strings[stringCount]
  prefixByteLength     varuint
  suffixByteLength     varuint
  suffix               canonical WTF-8 bytes
opcodeCount            varuint
opcodes[opcodeCount]   cliOpcode:uint16, nameStringIndex:varuint
typeCount              varuint
types[typeCount]
  nameStringIndex      varuint
  metadata             typed value object
methodCount            varuint
methods[methodCount]
  keyStringIndex       varuint
  nameStringIndex      varuint
  methodInfo           typed value object
  blockCount           varuint
  blocks[blockCount]
    ilOffset           varuint
    instructionCount   varuint
    instructions[instructionCount]
      cliOpcode        uint16
      hasOperand       byte: 0 or 1
      operand?         typed value
```

The string table is shared across the entire application and contains registration identities and metadata, method keys, opcode names, string operands, and object property names. It is sorted by ordinal UTF-16 code units for deterministic output and serialized as a front-coded prefix trie. Each entry reuses the indicated byte prefix of the preceding WTF-8 entry and stores only its suffix. Prefixes and suffixes are byte counts; a prefix may end inside a multibyte sequence because the decoder reconstructs the complete entry before decoding it.

Version 7 uses canonical WTF-8 so every managed string is lossless. Unicode scalar
values use their standard UTF-8 representation. An isolated UTF-16 surrogate code
unit uses the corresponding three-byte surrogate-code-point representation, while a
valid surrogate pair uses the ordinary four-byte scalar representation. Readers
reject overlong, truncated, out-of-range, invalid-continuation, and otherwise
noncanonical sequences, including a valid pair encoded as two surrogate code points.
This compact trie representation supports shared prefixes that are not themselves complete table entries without paying for explicit nodes or child pointers. Opcodes are stored on instructions as their numeric ECMA-335 value; the package-level opcode table maps each used value to the current interpreter handler name once.

Typed operand values use one-byte tags:

| Tag | Value | Payload |
| ---: | --- | --- |
| 0 | null | none |
| 1 | false | none |
| 2 | true | none |
| 3 | signed integer | ZigZag varuint |
| 4 | unsigned integer | varuint |
| 5 | binary64 floating point | exact IEEE 754 binary64 bits |
| 6 | string | string-table index |
| 7 | array | item count followed by values |
| 8 | object | property count followed by string-table key indexes and values |
| 9 | binary32 floating point | exact IEEE 754 binary32 bits |

The encoder first applies the same operand normalization used by the former JavaScript emitter. Reflection operands therefore retain the exact object notation expected by existing opcode handlers. The normalized tree remains typed in memory rather than passing through JSON text, so a CLR `Single` cannot be widened into a `Double` before encoding. Version 6 added the distinct binary32 tag; both floating-point tags preserve signed zero and the exact source bits.

Closed generic use sites retain one open runtime type identity plus recursive
substitution descriptors. A substitution value is either a non-generic type-name
string or an object containing `typeName` and its own `genericParams` map. This
preserves shapes such as `Outer<Inner<int>>` without emitting a second runtime type
registration for either closed construction. The same descriptor shape is used by
constructed method/type operands, locals, runtime type handles, and reflection
metadata for fields, properties, methods, and constructors.

Array references use a structural descriptor containing `arrayElementType`,
`arrayKind` (`szarray` or `array`), `arrayRank`, `arraySizes`, and
`arrayLowerBounds`. The element is another recursive type reference, so closed
generic, jagged, and generic-parameter elements never depend on reflection's
flattened array name. Empty size and lower-bound vectors still distinguish an
SZARRAY `T[]` from a general rank-one `T[*]`; higher general ranks retain their
comma shape. Locals, operands, runtime type handles, member metadata, and
runtime-override catalog references use the same structure.

Enum metadata is a separate declarative type payload: underlying integral type,
`FlagsAttribute` status, and literal names with exact values in metadata order.
Managed `System.Type` enum queries project that payload as the CLR-sorted names,
enum values, and underlying values. Enum reflection demand is tracked separately
from ordinary member reflection so it does not retain enum constructors, methods,
or other executable members.

## Validation and compatibility

Both C# and JavaScript readers fail closed on invalid magic, unsupported versions, truncated fields, overflowing variable-length integers, invalid trie prefixes, malformed or noncanonical WTF-8, unsorted or duplicate strings, invalid indexes, duplicate opcode/method/object entries, undeclared opcodes, invalid operand markers, unsupported value tags, and trailing data. A format change requires a new version and an explicit decoder path; silently reinterpreting an existing version is not allowed.

The JavaScript loader supports browser HTTP(S) assets and Node.js `file:` URLs. Bundled and unbundled output both keep the physical package at `msil.bin`; the generated loader URL is `msil.bin?v=<sha256>`, where the lowercase hash is computed from the complete package bytes. A bundled build creates the package and JavaScript module graph in a temporary staging directory, then publishes only `index.js`, `index.js.map`, and `msil.bin`. The query parameter prevents a cache-busted entry point from selecting a stale body package without accumulating renamed binary files. Webpack leaves this explicit runtime URL intact rather than embedding the package as base64 or JavaScript text. The debug server serves the resource as `application/octet-stream`.

Edit and Continue reads the candidate generation's registration metadata and body with the C# decoder and embeds only each changed method into its update module. This leaves the initial application package immutable while preserving the established update transaction and generation checks.

## Regression contract

The compiler test suite compares the normalized encoder input with both the C# reader and the JavaScript decoder. Integration coverage also verifies that interpreted method/type modules are absent, webpack emits the binary asset and removes temporary override modules, and a compiled program executes through registrations created by the decoded package.
