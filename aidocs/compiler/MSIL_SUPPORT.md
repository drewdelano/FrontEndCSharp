# MSIL Support Inventory

This document records the MSIL opcode inventory and the architectural decision for each
currently missing group. Actionable work derived from this inventory belongs in
[`../TODO.md`](../TODO.md).

## Inventory

The runtime recognizes 208 of the 226 opcode names exposed by
`System.Reflection.Emit.OpCodes`. Of the 18 names without a runtime handler, eight are
reserved prefix encodings rather than executable CLI instructions. The reflection-driven
decoder recognizes the remaining opcodes either for deliberate compile-time rejection or
for semantics that remain deferred.

Unsigned comparison is an opcode-level interpretation, not a separate persistent stack
type. The `.un` relational instructions reinterpret `int32` and `int64` stack values as
unsigned values of the same width. For floating-point operands, `.un` means unordered:
the comparison succeeds when either operand is `NaN`. This behavior is shared by the
branch and value-producing comparison instructions.

The fixed-width `conv.i1`, `conv.i2`, `conv.u1`, and `conv.u2` instructions truncate to
the requested width, sign-extend or zero-extend as appropriate, and leave an `int32` on
the evaluation stack. Floating-point inputs truncate toward zero before the width
conversion. The CLI leaves out-of-range floating-point results unspecified.

The complete `conv.ovf.*` family truncates floating-point inputs toward zero, validates
the mathematical result against the target width, and throws a wrapped, catchable .NET
`OverflowException` when it is out of range or non-finite. The `.un` variants reinterpret
integer source bits as unsigned at their existing stack width; they do not change
floating-point inputs. Checked native-integer targets use the private 32-bit native stack
types defined below.

`conv.r.un` reinterprets the existing `int32` or `int64` source bits as unsigned and
converts that value to the runtime's `System.Double`-backed CLI `F` representation;
`conv.r4` remains the explicit narrowing operation. `ckfinite` preserves a finite
floating-point value unchanged and throws a wrapped, catchable .NET
`ArithmeticException` for `NaN` or either infinity.

`ldc.r4` and `ldc.r8` retain their exact CLI binary32 and binary64 operand bits in
the binary package. The runtime consumes those numeric values without reparsing
text, preserving signed zero through fields, arrays, boxing, debugger inspection,
and serializer calls. The two widths remain distinct in the transport even though
JavaScript executes both through its `Number` representation.

### Native-integer representation

The runtime uses a fixed 32-bit native-integer width. Native integers remain distinct
CLI stack types rather than being treated as `System.Int32` or `System.UInt32`:

- Signed native integer: `{ t: '$NativeInt32', v: value }`.
- Unsigned native integer: `{ t: '$NativeUInt32', v: value }`.

The tag names are private runtime constants and must not appear as reflection-visible
.NET type identities. `System.IntPtr` and `System.UIntPtr` remain the real declared and
reflection-visible types. Defaults, method and field boundaries, boxing, and reflection
must define explicit translation between those .NET types and the internal stack forms.
Arithmetic and conversion helpers must normalize values explicitly to signed or unsigned
32-bit ranges rather than relying on incidental JavaScript coercion. These stack types do
not represent addresses and must not enable pointer or indirect-access instructions.
`conv.i` and `conv.u` produce these private stack forms, while `ldelem.i` and `stelem.i`
preserve the signed or unsigned native representation in `IntPtr[]` and `UIntPtr[]`
storage. Widening a 32-bit stack value with `conv.u8` zero-extends its existing bit
pattern rather than first sign-extending it to 64 bits.

## Skip and fail closed

Instance fields and one-dimensional array elements use live managed-reference cells
for the targeted `ldflda` and `ldelema` paths required by ordinary compiler lowering.
This does not establish a general address space or pointer model.

Instance storage uses a readable metadata identity key containing the declaring managed
type, declared field name, and field metadata token. The token supplies identity while
the type and name keep emitted packages and debugger inspection understandable. Field
operands and reflection metadata retain both that storage key and the source field name;
serializers and debuggers must use the key for access and the name for presentation.

The following instructions require addressable storage beyond that targeted model,
which is outside the current scope:

- Static managed address production: `ldsflda`; static-field managed addresses are
  deliberately out of scope until a supported workload independently justifies a
  coherent model.
- Indirect access: `ldind.i`, `ldind.r4`, `ldind.r8`, `stind.i`, `stind.r4`,
  `stind.r8`.
- Typed references: `refanytype`, `mkrefany`, `refanyval`.

These are managed addresses rather than necessarily unmanaged pointers, but treating an
address as its underlying JavaScript value would still be incorrect.
The decoder rejects every opcode in this section with a method- and IL-offset-specific
`NotSupportedException` before resolving its operand or emitting JavaScript. The runtime
also rejects the formerly permissive `mkrefany` and `refanyval` placeholders if stale or
hand-authored instruction data reaches it.

## Defer until required

- `arglist` requires a complete managed-varargs design, including
  `RuntimeArgumentHandle`, argument types, and iteration. C# can emit it through
  `__arglist`, but normal C# and current BCL paths rarely require it.
- `jmp` is a valid managed tail transfer, but the C# compiler does not normally emit it.
  Correct support must validate the target signature and transfer control without adding
  a return frame.

## Reserved encodings

`prefix1`, `prefix2`, `prefix3`, `prefix4`, `prefix5`, `prefix6`, `prefix7`, and
`prefixref` are reserved byte encodings, not executable MSIL instructions. The decoder
rejects them with a method- and IL-offset-specific `NotSupportedException` rather than
routing them to runtime dispatch. Byte `0xFE` also introduces valid two-byte opcodes, so
only a standalone trailing `0xFE` is treated as the reserved `prefix1` encoding.

## Audited unsupported and reference-cell instructions

The decoder rejects `localloc`, `cpobj`, `cpblk`, `initblk`, `unaligned.`, `tail.`,
and `readonly.` because the runtime does not have the address-space or prefix semantics
needed to execute them correctly. They must not reach runtime dispatch as no-op or
ignored-prefix placeholders.

`constrained.` is supported for its required `callvirt` sequence. The receiver must be
a managed reference cell, dispatch uses the value currently held by that cell, and a
value-type implementation mutates the original storage rather than a copy. The decoder
rejects a prefix not immediately followed by `callvirt` and rejects control-flow or
exception-region targets that enter the call without executing its prefix.
For an enum receiver, the constrained type is also the authoritative managed enum
identity: CLI evaluation-stack storage may carry only the widened integral value, but
dispatch, possible-type analysis, boxing, and metadata-backed formatting must restore
the concrete enum type rather than treating it as `Int32` or another storage type.

`sizeof` returns exact sizes for the CLI primitive numeric types, `Boolean`, `Char`,
`Decimal`, and the runtime's fixed 32-bit native integers. It rejects types that require
managed layout metadata. `volatile.` is a no-op under the runtime's single-JavaScript-
thread execution model: managed instructions cannot race at an instruction boundary,
so there is no weaker non-volatile ordering to strengthen.

The remaining fixed-width `ldind.*` and `stind.*` forms plus `ldobj` and `stobj` operate
only on the runtime's explicit managed reference cells, which back local and argument
references. `ldelema` extends that same bounded model to one validated array element:
the cell remains live, enforces null, bounds, and runtime element-type checks, and writes
back to the array. These instructions reject arbitrary wrapped values rather than
treating a value as its own address. Static-field addresses, native-integer and
floating-point indirect forms, and a general address space remain unsupported.
