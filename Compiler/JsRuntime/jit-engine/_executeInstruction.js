import { instructionHandlers as argumentHandlers } from './arguments.js';
import { instructionHandlers as arithmeticHandlers } from './arithmetics.js';
import { instructionHandlers as arrayHandlers } from './arrays.js';
import { instructionHandlers as branchHandlers } from './branches.js';
import { instructionHandlers as callHandlers } from './calls.js';
import { instructionHandlers as comparisonHandlers } from './comparisons.js';
import { instructionHandlers as constantHandlers } from './constants.js';
import { instructionHandlers as conversionHandlers } from './conversions.js';
import { instructionHandlers as exceptionHandlers } from './exceptions.js';
import { instructionHandlers as fieldHandlers } from './fields.js';
import { instructionHandlers as localHandlers } from './locals.js';
import { instructionHandlers as typeHandlers } from './types.js';
import { instructionHandlers as uncategorizedHandlers } from './uncatagorizeds.js';
import { instructionHandlers as unsupportedHandlers } from './unsupporteds.js';

let instructionHandlers;

function getInstructionHandlers() {
    if (instructionHandlers !== undefined) return instructionHandlers;
    const handlersByOpcode = Object.create(null);
    for (const handlers of [
        argumentHandlers, arithmeticHandlers, arrayHandlers, branchHandlers, callHandlers,
        comparisonHandlers, constantHandlers, conversionHandlers, exceptionHandlers,
        fieldHandlers, localHandlers, typeHandlers, uncategorizedHandlers, unsupportedHandlers
    ]) {
        for (const [opcode, handler] of Object.entries(handlers)) {
            if (handlersByOpcode[opcode] !== undefined) {
                throw new Error(`Duplicate instruction handler for opcode '${opcode}'.`);
            }
            handlersByOpcode[opcode] = handler;
        }
    }
    instructionHandlers = Object.freeze(handlersByOpcode);
    return instructionHandlers;
}

export function executeInstruction(opts) {
    const opcode = opts?.instruction?.oc;
    if (opts?.constrainedType !== undefined && opcode !== 'callvirt') {
        throw new Error("A 'constrained.' prefix must be followed immediately by 'callvirt'.");
    }
    const handler = getInstructionHandlers()[opcode];
    if (handler === undefined) {
        throw new Error(`Missing instruction handling: ${opcode}`);
    }
    return handler(opts);
}
