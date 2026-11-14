import assert from "node:assert/strict";
import test from "node:test";

import { executeInstruction } from "../../JsRuntime/jit-engine/_executeInstruction.js";

for (const opcode of ["mkrefany", "refanyval"]) {
    test(`${opcode} fails closed if it reaches the JavaScript runtime`, () => {
        assert.throws(
            () => executeInstruction({ instruction: { oc: opcode }, stack: [{}] }),
            new RegExp(`MSIL opcode '${opcode}'.*is not supported`)
        );
    });
}
