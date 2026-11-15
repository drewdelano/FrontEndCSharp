import { throwManagedException } from '../managed-exceptions.js';
import { deRef, unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Decimal..ctor(System.Int32, System.Int32, System.Int32, System.Boolean, System.Byte)'] = function (ctx, $this, low, middle, high, negative, scale) {
    const decimalScale = Number(unwrap(scale));
    if (decimalScale < 0 || decimalScale > 28) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'Decimal scale must be between 0 and 28.');
    }
    const lowBits = Number(unwrap(low)) >>> 0;
    const middleBits = Number(unwrap(middle)) >>> 0;
    const highBits = Number(unwrap(high)) >>> 0;
    const flags = (decimalScale << 16) | (unwrap(negative) ? 0x80000000 : 0);
    const target = unwrap(deRef($this));
    target._flags = wrap(flags | 0, 'System.Int32');
    target._hi32 = wrap(highBits, 'System.UInt32');
    target._lo64 = wrap((BigInt(middleBits) << 32n) | BigInt(lowBits), 'System.UInt64');
};
