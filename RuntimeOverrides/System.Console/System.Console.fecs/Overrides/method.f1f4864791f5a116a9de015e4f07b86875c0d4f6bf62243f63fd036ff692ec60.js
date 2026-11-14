import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Console.WriteLine(System.Boolean)'] = function(ctx, bool) {
  console.log(unwrap(bool) ? 'True' : 'False');
};
