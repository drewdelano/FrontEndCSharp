import { createDateOnly } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';
import { deRef, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.DateOnly..ctor(System.Int32, System.Int32, System.Int32)'] = function (ctx, $this, year, month, day) {
    const value = createDateOnly(unwrap(year), unwrap(month), unwrap(day));
    if (value === null) throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'The DateOnly parameters describe an unrepresentable value.');
    Object.assign(unwrap(deRef($this)), unwrap(value));
};
