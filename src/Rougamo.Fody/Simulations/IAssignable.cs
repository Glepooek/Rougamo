﻿using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;

namespace Rougamo.Fody.Simulations
{
    internal interface IAssignable : IAnalysable
    {
        IList<Instruction> Assign(Func<IAssignable, IList<Instruction>> valueFactory);
    }

    internal static class AssignableExtensions
    {
        public static IList<Instruction> Assign(this IAssignable assignable, ILoadable from) => assignable.Assign(target => [.. from.Load(), .. from.Cast(assignable.Type)]);

        public static IList<Instruction> Assign(this IAssignable assignable, int value) => assignable.Assign(target => [Instruction.Create(OpCodes.Ldc_I4, value)]);
    }
}
