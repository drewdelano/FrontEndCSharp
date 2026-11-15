import { createTimeSpanFromTicks } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.TimeSpan.FromTicks(System.Int64)'] = function (ctx, value) {
    return createTimeSpanFromTicks(unwrap(value));
};
