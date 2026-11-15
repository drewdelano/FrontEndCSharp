import { createElement } from 'react';
import { BarChart } from 'recharts';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Shadcn.Recharts.RechartsImports.RechartsBarChart(System.String[], System.Double[], System.String, System.String)'] = function (ctx, categories, values, categoryKey, dataKey) {
    const nativeCategories = unwrap(categories).map(category => unwrap(category));
    const nativeValues = unwrap(values).map(value => Number(unwrap(value)));
    const nativeCategoryKey = unwrap(categoryKey);
    const nativeDataKey = unwrap(dataKey);
    const data = nativeCategories.map((category, index) => ({
        [nativeCategoryKey]: category,
        [nativeDataKey]: nativeValues[index]
    }));
    const BoundBarChart = props => createElement(BarChart, { ...props, data });
    return wrap(BoundBarChart, 'Interop.JS.React.ImportedReactComponent');
};
