import { addTemporalTicks, getTimeSpanTicks } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.TimeOnly.Add(System.TimeSpan)'] = function (ctx, $this, value) {
    return addTemporalTicks('System.TimeOnly', $this, getTimeSpanTicks(value));
};
