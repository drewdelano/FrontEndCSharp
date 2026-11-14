import { unwrap } from "../msil-type-helpers.js";

// These are the managed storage types that become the CLI's F stack type.
// System.Decimal is a value type whose comparisons are method calls, not CIL comparisons.
const cliFloatingPointTypes = new Set(["System.Single", "System.Double"]);

export function compareUnsignedOrUnordered(left, right, orderedComparison) {
    const leftValue = unwrap(left);
    const rightValue = unwrap(right);

    if (isFloatingPoint(left) || isFloatingPoint(right)) {
        return Number.isNaN(leftValue)
            || Number.isNaN(rightValue)
            || orderedComparison(leftValue, rightValue);
    }

    return orderedComparison(asUnsignedInteger(leftValue), asUnsignedInteger(rightValue));
}

function isFloatingPoint(value) {
    return cliFloatingPointTypes.has(value?.t);
}

function asUnsignedInteger(value) {
    if (typeof value === "bigint") {
        return BigInt.asUintN(64, value);
    }
    if (typeof value === "number" && Number.isInteger(value)) {
        return value >>> 0;
    }

    throw new TypeError("Unsigned comparison requires integer or floating-point operands.");
}
