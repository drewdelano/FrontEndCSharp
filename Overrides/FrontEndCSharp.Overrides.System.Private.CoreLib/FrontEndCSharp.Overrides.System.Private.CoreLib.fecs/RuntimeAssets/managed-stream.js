import { createManagedException, throwManagedException } from '../../managed-exceptions.js';
import { unwrap, wrap } from '../../msil-type-helpers.js';
import {
    createCanceledTask,
    createTaskCanceledException,
    isCancellationRequested,
    registerCancellationCallback
} from './task-cancellation.js';
import { JavaScriptTaskCompletionSource } from './task-completion-source.js';

const taskType = 'System.Threading.Tasks.Task`1[TResult]';
const taskTypeArguments = { TResult: 'System.Int32' };
const voidTaskType = 'System.Threading.Tasks.Task';
const byteArrayType = 'System.Byte[]';
const maximumMemoryStreamLength = 2147483647;
const memoryStreamStates = new WeakMap();

export function initializeEmptyMemoryStream(streamReference) {
    setMemoryStreamState(streamReference, {
        bufferReference: wrap([], byteArrayType),
        capacity: 0,
        expandable: true,
        exposable: true,
        length: 0,
        open: true,
        position: 0,
        writable: true
    });
}

export function initializeBufferMemoryStream(ctx, streamReference, bufferReference) {
    const buffer = unwrap(bufferReference);
    if (!Array.isArray(buffer)) {
        throwManagedException(ctx, 'System.ArgumentNullException', "Value cannot be null. (Parameter 'buffer')");
    }
    setMemoryStreamState(streamReference, {
        bufferReference,
        capacity: buffer.length,
        expandable: false,
        exposable: false,
        length: buffer.length,
        open: true,
        position: 0,
        writable: true
    });
}

export function canReadMemoryStream(streamReference) {
    return wrap(memoryStreamState(streamReference).open, 'System.Boolean');
}

export function canWriteMemoryStream(streamReference) {
    const state = memoryStreamState(streamReference);
    return wrap(state.open && state.writable, 'System.Boolean');
}

export function canSeekMemoryStream(streamReference) {
    return wrap(memoryStreamState(streamReference).open, 'System.Boolean');
}

export function getMemoryStreamCapacity(ctx, streamReference) {
    const state = requireOpenMemoryStream(ctx, streamReference);
    return wrap(state.capacity, 'System.Int32');
}

export function setMemoryStreamCapacity(ctx, streamReference, capacityReference) {
    const state = requireOpenMemoryStream(ctx, streamReference);
    const capacity = Number(unwrap(capacityReference));
    if (!Number.isInteger(capacity) || capacity < state.length) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'Capacity cannot be less than the current length.');
    }
    if (!state.expandable && capacity !== state.capacity) {
        throwManagedException(ctx, 'System.NotSupportedException', 'Memory stream is not expandable.');
    }
    resizeMemoryStream(state, capacity);
    syncLegacyMemoryStream(streamReference, state);
}

export function getMemoryStreamLength(ctx, streamReference) {
    return wrap(BigInt(requireOpenMemoryStream(ctx, streamReference).length), 'System.Int64');
}

export function getMemoryStreamPosition(ctx, streamReference) {
    return wrap(BigInt(requireOpenMemoryStream(ctx, streamReference).position), 'System.Int64');
}

export function setMemoryStreamPosition(ctx, streamReference, positionReference) {
    const state = requireOpenMemoryStream(ctx, streamReference);
    const position = BigInt(unwrap(positionReference));
    if (position < 0n || position > BigInt(maximumMemoryStreamLength)) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'Position must be non-negative and fit in Int32.');
    }
    state.position = Number(position);
    syncLegacyMemoryStream(streamReference, state);
}

export function readManagedAsyncStream(
    ctx,
    streamReference,
    bufferReference,
    offsetReference,
    countReference,
    cancellationToken = null) {
    if (streamReference?.t === 'System.IO.MemoryStream') {
        return readManagedAsyncMemoryStream(
            ctx,
            streamReference,
            bufferReference,
            offsetReference,
            countReference,
            cancellationToken);
    }
    const stream = unwrap(streamReference);
    if (stream?.$fecsDisposed === true) {
        throwManagedException(ctx, 'System.ObjectDisposedException', 'Cannot access a disposed stream.');
    }
    const buffer = unwrap(bufferReference);
    if (!Array.isArray(buffer)) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Value cannot be null. (Parameter \'buffer\')');
    }
    const offset = Number(unwrap(offsetReference));
    const count = Number(unwrap(countReference));
    if (!Number.isInteger(offset) || !Number.isInteger(count)
        || offset < 0 || count < 0 || offset > buffer.length - count) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'The buffer range is invalid.');
    }
    if (isCancellationRequested(cancellationToken)) {
        return createCanceledTask(taskType, taskTypeArguments);
    }

    const source = new JavaScriptTaskCompletionSource();
    if (count === 0) {
        source.setResult(wrap(0, 'System.Int32'));
        return wrap(source.task, taskType, taskTypeArguments);
    }
    if (copyPendingBytes(stream, buffer, offset, count, source)) {
        return wrap(source.task, taskType, taskTypeArguments);
    }
    const reader = stream?.$fecsReadableStreamReader;
    if (reader === null || reader === undefined) {
        source.setResult(wrap(0, 'System.Int32'));
        return wrap(source.task, taskType, taskTypeArguments);
    }

    let canceled = false;
    const unregisterCancellation = registerCancellationCallback(
        cancellationToken,
        () => {
            canceled = true;
            Promise.resolve(reader.cancel()).catch(() => {});
        });
    Promise.resolve(reader.read())
        .then(result => {
            unregisterCancellation();
            if (canceled) {
                source.setCanceled(createTaskCanceledException());
                return;
            }
            if (result.done) {
                stream.$fecsEndOfStream = true;
                source.setResult(wrap(0, 'System.Int32'));
                return;
            }
            const bytes = result.value instanceof Uint8Array
                ? result.value
                : new Uint8Array(result.value ?? []);
            stream.$fecsPendingBytes = bytes;
            stream.$fecsPendingOffset = 0;
            copyPendingBytes(stream, buffer, offset, count, source);
        })
        .catch(error => {
            unregisterCancellation();
            if (canceled) {
                source.setCanceled(createTaskCanceledException());
                return;
            }
            source.setException(createManagedException(
                'System.IO.IOException',
                error instanceof Error
                    ? error.message
                    : 'The browser response stream failed.'));
        });
    return wrap(source.task, taskType, taskTypeArguments);
}

export function readManagedMemoryStream(
    ctx,
    streamReference,
    bufferReference,
    offsetReference,
    countReference) {
    const range = validateMemoryStreamBuffer(ctx, bufferReference, offsetReference, countReference);
    const state = requireOpenMemoryStream(ctx, streamReference);
    return readMemoryStreamCore(streamReference, state, range);
}

export function readManagedAsyncMemoryStream(
    ctx,
    streamReference,
    bufferReference,
    offsetReference,
    countReference,
    cancellationToken = null) {
    const range = validateMemoryStreamBuffer(ctx, bufferReference, offsetReference, countReference);
    const state = requireOpenMemoryStream(ctx, streamReference);
    if (isCancellationRequested(cancellationToken)) return createCanceledTask(taskType, taskTypeArguments);
    const result = readMemoryStreamCore(streamReference, state, range);
    const source = new JavaScriptTaskCompletionSource();
    source.setResult(result);
    return wrap(source.task, taskType, taskTypeArguments);
}

export function writeManagedMemoryStream(
    ctx,
    streamReference,
    bufferReference,
    offsetReference,
    countReference) {
    const range = validateMemoryStreamBuffer(ctx, bufferReference, offsetReference, countReference);
    const state = requireWritableMemoryStream(ctx, streamReference);
    writeMemoryStreamCore(ctx, streamReference, state, range);
}

export function writeManagedAsyncMemoryStream(
    ctx,
    streamReference,
    bufferReference,
    offsetReference,
    countReference,
    cancellationToken = null) {
    const range = validateMemoryStreamBuffer(ctx, bufferReference, offsetReference, countReference);
    const state = requireWritableMemoryStream(ctx, streamReference);
    if (isCancellationRequested(cancellationToken)) return createCanceledTask(voidTaskType);
    writeMemoryStreamCore(ctx, streamReference, state, range);
    const source = new JavaScriptTaskCompletionSource();
    source.setResult(null);
    return wrap(source.task, voidTaskType);
}

export function writeManagedAsyncStream(
    ctx,
    streamReference,
    bufferReference,
    offsetReference,
    countReference,
    cancellationToken = null) {
    if (streamReference?.t === 'System.IO.MemoryStream') {
        return writeManagedAsyncMemoryStream(
            ctx,
            streamReference,
            bufferReference,
            offsetReference,
            countReference,
            cancellationToken);
    }
    validateMemoryStreamBuffer(ctx, bufferReference, offsetReference, countReference);
    if (isCancellationRequested(cancellationToken)) return createCanceledTask(voidTaskType);
    throwManagedException(ctx, 'System.NotSupportedException', 'Stream does not support writing.');
}

export function readMemoryStreamByte(ctx, streamReference) {
    const state = requireOpenMemoryStream(ctx, streamReference);
    if (state.position >= state.length) return wrap(-1, 'System.Int32');
    const value = Number(unwrap(unwrap(state.bufferReference)[state.position++]));
    syncLegacyMemoryStream(streamReference, state);
    return wrap(value, 'System.Int32');
}

export function writeMemoryStreamByte(ctx, streamReference, valueReference) {
    const state = requireWritableMemoryStream(ctx, streamReference);
    const end = state.position + 1;
    ensureMemoryStreamCapacity(ctx, state, end);
    zeroMemoryStreamRange(state, state.length, state.position);
    unwrap(state.bufferReference)[state.position] = wrap(Number(unwrap(valueReference)), 'System.Byte');
    state.position = end;
    if (end > state.length) state.length = end;
    syncLegacyMemoryStream(streamReference, state);
}

export function seekMemoryStream(ctx, streamReference, offsetReference, originReference) {
    const state = requireOpenMemoryStream(ctx, streamReference);
    const offset = BigInt(unwrap(offsetReference));
    const origin = Number(unwrap(originReference));
    let basis;
    if (origin === 0) basis = 0n;
    else if (origin === 1) basis = BigInt(state.position);
    else if (origin === 2) basis = BigInt(state.length);
    else {
        throwManagedException(ctx, 'System.ArgumentException', 'Invalid seek origin.');
    }
    const position = basis + offset;
    if (position < 0n) {
        throwManagedException(ctx, 'System.IO.IOException', 'An attempt was made to move before the beginning of the stream.');
    }
    if (position > BigInt(maximumMemoryStreamLength)) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'The stream position is too large.');
    }
    state.position = Number(position);
    syncLegacyMemoryStream(streamReference, state);
    return wrap(position, 'System.Int64');
}

export function setMemoryStreamLength(ctx, streamReference, lengthReference) {
    const state = requireWritableMemoryStream(ctx, streamReference);
    const length = BigInt(unwrap(lengthReference));
    if (length < 0n || length > BigInt(maximumMemoryStreamLength)) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'Length must be non-negative and fit in Int32.');
    }
    const numericLength = Number(length);
    ensureMemoryStreamCapacity(ctx, state, numericLength);
    zeroMemoryStreamRange(state, state.length, numericLength);
    state.length = numericLength;
    if (state.position > numericLength) state.position = numericLength;
    syncLegacyMemoryStream(streamReference, state);
}

export function memoryStreamToArray(streamReference) {
    const state = memoryStreamState(streamReference);
    const source = unwrap(state.bufferReference);
    return wrap(source.slice(0, state.length).map(normalizeByte), byteArrayType);
}

export function getMemoryStreamBuffer(ctx, streamReference) {
    const state = memoryStreamState(streamReference);
    if (!state.exposable) {
        throwManagedException(ctx, 'System.UnauthorizedAccessException', 'Memory stream buffer is not publicly visible.');
    }
    return state.bufferReference;
}

export function tryGetMemoryStreamBuffer(streamReference, segmentReference) {
    const state = memoryStreamState(streamReference);
    segmentReference.value = createArraySegment(
        state.exposable ? state.bufferReference : null,
        0,
        state.exposable ? state.length : 0);
    return wrap(state.exposable, 'System.Boolean');
}

export function disposeMemoryStream(streamReference) {
    const state = memoryStreamState(streamReference);
    state.open = false;
    state.writable = false;
    state.expandable = false;
    syncLegacyMemoryStream(streamReference, state);
}

function setMemoryStreamState(streamReference, state) {
    const stream = unwrap(streamReference);
    memoryStreamStates.set(stream, state);
    syncLegacyMemoryStream(streamReference, state);
}

function memoryStreamState(streamReference) {
    const stream = unwrap(streamReference);
    let state = memoryStreamStates.get(stream);
    if (state !== undefined) return state;
    if (stream === null || typeof stream !== 'object') {
        throw new TypeError('MemoryStream requires a managed object receiver.');
    }

    const hasBrowserBuffer = Object.hasOwn(stream, '$fecsMemoryBytes');
    const bufferReference = hasBrowserBuffer ? stream.$fecsMemoryBytes : stream._buffer;
    const buffer = unwrap(bufferReference);
    if (!Array.isArray(buffer)) {
        throw new TypeError('MemoryStream was not initialized.');
    }
    state = {
        bufferReference,
        capacity: Number(hasBrowserBuffer
            ? buffer.length
            : unwrap(stream._capacity ?? wrap(buffer.length, 'System.Int32'))),
        expandable: Boolean(unwrap(stream._expandable ?? false)),
        exposable: Boolean(unwrap(stream._exposable ?? false)),
        length: Number(hasBrowserBuffer ? stream.$fecsMemoryLength : unwrap(stream._length)),
        open: hasBrowserBuffer ? unwrap(stream._isOpen) !== false : unwrap(stream._isOpen) !== false,
        position: Number(hasBrowserBuffer ? stream.$fecsMemoryPosition : unwrap(stream._position)),
        writable: Boolean(unwrap(stream._writable ?? true))
    };
    memoryStreamStates.set(stream, state);
    return state;
}

function requireOpenMemoryStream(ctx, streamReference) {
    const state = memoryStreamState(streamReference);
    if (!state.open) {
        throwManagedException(ctx, 'System.ObjectDisposedException', 'Cannot access a closed stream.');
    }
    return state;
}

function requireWritableMemoryStream(ctx, streamReference) {
    const state = requireOpenMemoryStream(ctx, streamReference);
    if (!state.writable) {
        throwManagedException(ctx, 'System.NotSupportedException', 'Stream does not support writing.');
    }
    return state;
}

function validateMemoryStreamBuffer(ctx, bufferReference, offsetReference, countReference) {
    const buffer = unwrap(bufferReference);
    if (!Array.isArray(buffer)) {
        throwManagedException(ctx, 'System.ArgumentNullException', "Value cannot be null. (Parameter 'buffer')");
    }
    const offset = Number(unwrap(offsetReference));
    const count = Number(unwrap(countReference));
    if (!Number.isInteger(offset) || offset < 0) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'Offset must be non-negative.');
    }
    if (!Number.isInteger(count) || count < 0) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'Count must be non-negative.');
    }
    if (offset > buffer.length - count) {
        throwManagedException(ctx, 'System.ArgumentException', 'Offset and count exceed the buffer bounds.');
    }
    return { buffer, offset, count };
}

function readMemoryStreamCore(streamReference, state, range) {
    const source = unwrap(state.bufferReference);
    const copied = Math.min(range.count, Math.max(0, state.length - state.position));
    for (let index = 0; index < copied; index++) {
        range.buffer[range.offset + index] = normalizeByte(source[state.position + index]);
    }
    state.position += copied;
    syncLegacyMemoryStream(streamReference, state);
    return wrap(copied, 'System.Int32');
}

function writeMemoryStreamCore(ctx, streamReference, state, range) {
    const end = state.position + range.count;
    if (end > maximumMemoryStreamLength) {
        throwManagedException(ctx, 'System.IO.IOException', 'The stream is too long.');
    }
    ensureMemoryStreamCapacity(ctx, state, end);
    zeroMemoryStreamRange(state, state.length, state.position);
    const copied = range.buffer.slice(range.offset, range.offset + range.count).map(normalizeByte);
    const destination = unwrap(state.bufferReference);
    for (let index = 0; index < copied.length; index++) {
        destination[state.position + index] = copied[index];
    }
    state.position = end;
    if (end > state.length) state.length = end;
    syncLegacyMemoryStream(streamReference, state);
}

function ensureMemoryStreamCapacity(ctx, state, requiredCapacity) {
    if (requiredCapacity <= state.capacity) return;
    if (!state.expandable) {
        throwManagedException(ctx, 'System.NotSupportedException', 'Memory stream is not expandable.');
    }
    let capacity = Math.max(requiredCapacity, 256, state.capacity * 2);
    if (capacity > maximumMemoryStreamLength) capacity = requiredCapacity;
    resizeMemoryStream(state, capacity);
}

function resizeMemoryStream(state, capacity) {
    if (capacity === state.capacity) return;
    const original = unwrap(state.bufferReference);
    const resized = new Array(capacity);
    const copied = Math.min(original.length, capacity);
    for (let index = 0; index < copied; index++) resized[index] = normalizeByte(original[index]);
    for (let index = copied; index < capacity; index++) resized[index] = zeroByte();
    state.bufferReference = wrap(resized, byteArrayType);
    state.capacity = capacity;
}

function zeroMemoryStreamRange(state, start, end) {
    if (end <= start) return;
    const buffer = unwrap(state.bufferReference);
    for (let index = start; index < end; index++) buffer[index] = zeroByte();
}

function normalizeByte(value) {
    return value?.t === 'System.Byte'
        ? value
        : wrap(Number(unwrap(value) ?? 0), 'System.Byte');
}

function zeroByte() {
    return wrap(0, 'System.Byte');
}

function createArraySegment(arrayReference, offset, count) {
    return wrap({
        _array: arrayReference,
        _offset: wrap(offset, 'System.Int32'),
        _count: wrap(count, 'System.Int32')
    }, 'System.ArraySegment`1', { T: 'System.Byte' });
}

function syncLegacyMemoryStream(streamReference, state) {
    const stream = unwrap(streamReference);
    stream._buffer = state.bufferReference;
    stream._capacity = wrap(state.capacity, 'System.Int32');
    stream._expandable = wrap(state.expandable, 'System.Boolean');
    stream._exposable = wrap(state.exposable, 'System.Boolean');
    stream._isOpen = wrap(state.open, 'System.Boolean');
    stream._length = wrap(state.length, 'System.Int32');
    stream._position = wrap(state.position, 'System.Int32');
    stream._writable = wrap(state.writable, 'System.Boolean');
    if (Object.hasOwn(stream, '$fecsMemoryBytes')) {
        stream.$fecsMemoryBytes = state.bufferReference;
        stream.$fecsMemoryLength = state.length;
        stream.$fecsMemoryPosition = state.position;
    }
}

export function disposeManagedAsyncStream(streamReference) {
    if (streamReference?.t === 'System.IO.MemoryStream') {
        disposeMemoryStream(streamReference);
        return;
    }
    const stream = unwrap(streamReference);
    if (stream === null || stream === undefined || stream.$fecsDisposed === true) return;
    stream.$fecsDisposed = true;
    const reader = stream.$fecsReadableStreamReader;
    if (reader !== null && reader !== undefined) {
        Promise.resolve(reader.cancel()).catch(() => {});
    }
}

function copyPendingBytes(stream, buffer, offset, count, source) {
    const pending = stream?.$fecsPendingBytes;
    const pendingOffset = Number(stream?.$fecsPendingOffset ?? 0);
    if (!(pending instanceof Uint8Array) || pendingOffset >= pending.length) return false;
    const copied = Math.min(count, pending.length - pendingOffset);
    for (let index = 0; index < copied; index++) {
        buffer[offset + index] = wrap(pending[pendingOffset + index], 'System.Byte');
    }
    const nextOffset = pendingOffset + copied;
    if (nextOffset >= pending.length) {
        stream.$fecsPendingBytes = null;
        stream.$fecsPendingOffset = 0;
    } else {
        stream.$fecsPendingOffset = nextOffset;
    }
    source.setResult(wrap(copied, 'System.Int32'));
    return true;
}
