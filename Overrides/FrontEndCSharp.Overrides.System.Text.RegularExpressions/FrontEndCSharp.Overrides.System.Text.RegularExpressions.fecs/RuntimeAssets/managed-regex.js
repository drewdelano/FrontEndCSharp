import { invokeManagedDelegate } from '../../jit-engine/calls.js';
import { unwrap, wrap } from '../../msil-type-helpers.js';

function createGroup(value, success) {
    return wrap({ $value: value, $success: success }, 'System.Text.RegularExpressions.Group');
}

function createMatch(values) {
    const groups = values.map(value => createGroup(value ?? '', value !== undefined));
    return wrap(
        { $groups: wrap({ $groups: groups }, 'System.Text.RegularExpressions.GroupCollection') },
        'System.Text.RegularExpressions.Match');
}

function regexPattern(regex) {
    const rawRegex = unwrap(regex);
    const direct = unwrap(rawRegex._pattern ?? rawRegex.pattern);
    if (typeof direct === 'string') return direct;
    const storageKey = Object.keys(rawRegex).find(key =>
        key.startsWith('$field:System.Text.RegularExpressions.Regex:pattern:'));
    return storageKey === undefined ? undefined : unwrap(rawRegex[storageKey]);
}

export function regexReplace(ctx, regex, input, evaluator) {
    const pattern = regexPattern(regex);
    if (typeof pattern !== 'string') {
        throw new Error('The managed Regex instance does not expose its pattern.');
    }
    const expression = new RegExp(pattern, 'g');
    const result = unwrap(input).replace(expression, (...argumentsList) => {
        const values = argumentsList.slice(0, -2);
        return unwrap(invokeManagedDelegate(ctx, evaluator, [createMatch(values)])) ?? '';
    });
    return wrap(result, 'System.String');
}

export function regexReplaceText(regex, input, replacement) {
    const pattern = regexPattern(regex);
    if (typeof pattern !== 'string') {
        throw new Error('The managed Regex instance does not expose its pattern.');
    }
    return wrap(
        unwrap(input).replace(new RegExp(pattern, 'g'), unwrap(replacement)),
        'System.String');
}

export function matchGroups(match) {
    return unwrap(match).$groups;
}

export function groupAt(collection, index) {
    return unwrap(collection).$groups[Number(unwrap(index))];
}

export function groupValue(group) {
    return wrap(unwrap(group).$value, 'System.String');
}

export function groupSuccess(group) {
    return wrap(unwrap(group).$success, 'System.Boolean');
}
