import { unwrap, wrap } from '../../msil-type-helpers.js';

let invariantCulture;

export function getInvariantCulture() {
    invariantCulture ??= wrap(
        { $cultureName: '' },
        'System.Globalization.CultureInfo');
    return invariantCulture;
}

export function getCultureName(culture) {
    return wrap(unwrap(culture).$cultureName ?? '', 'System.String');
}

export function getCultureParent() {
    return getInvariantCulture();
}

export function isNeutralCulture() {
    return wrap(false, 'System.Boolean');
}
