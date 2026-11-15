// Object.GetType() - InternalCall runtime override
// Returns the Type object for this instance
globalThis.DotNet.Methods["System.Object.GetType()"] = function(ctx, thisObj) {
  // InternalCall - return the type from the object's metadata
  // In our type system, each object has a 't' property with its type name
  return { t: 'System.Type', v: thisObj.t };
};
