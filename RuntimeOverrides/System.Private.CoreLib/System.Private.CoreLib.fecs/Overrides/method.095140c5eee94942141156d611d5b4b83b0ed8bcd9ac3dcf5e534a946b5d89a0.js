import { unwrap } from '../value-conversion.js';

globalThis.DotNet.Methods['System.Type.FilterNameImpl(System.Reflection.MemberInfo, System.Object, System.StringComparison)'] = function (_ctx, member, filterCriteria, comparisonType) {
    const metadata = unwrap(member)?.metadata;
    let pattern = unwrap(filterCriteria);
    if (metadata == null || typeof pattern !== 'string') {
        return false;
    }

    let name = metadata.name;
    const prefixMatch = pattern.endsWith('*');
    if (prefixMatch) {
        pattern = pattern.substring(0, pattern.length - 1);
    }
    if (comparisonType === 1 || comparisonType === 3 || comparisonType === 5) {
        name = name.toLocaleLowerCase();
        pattern = pattern.toLocaleLowerCase();
    }
    return prefixMatch ? name.startsWith(pattern) : name === pattern;
};
