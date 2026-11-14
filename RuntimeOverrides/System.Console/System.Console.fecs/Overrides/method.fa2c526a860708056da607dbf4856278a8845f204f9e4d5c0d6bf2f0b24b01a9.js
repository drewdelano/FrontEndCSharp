import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Console.WriteLine(System.Int64)'] = function(ctx, num) {
  console.log(parseInt(unwrap(num)));
};
