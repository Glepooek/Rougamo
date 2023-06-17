﻿using System.Collections.Generic;
using System.Linq;

namespace Rougamo.Fody.Signature
{
    public class GenericSignature
    {
        public static readonly GenericSignature[] EmptyArray = new GenericSignature[0];

        public GenericSignature(string name, IReadOnlyCollection<TypeSignature> generics)
        {
            Name = name;
            Generics = generics.ToArray();
        }

        public string Name { get; }

        public TypeSignature[] Generics { get; }

        public override string ToString()
        {
            return Generics.Length == 0 ? Name : $"{Name}<{string.Join(",", Generics.AsEnumerable())}>";
        }
    }
}
