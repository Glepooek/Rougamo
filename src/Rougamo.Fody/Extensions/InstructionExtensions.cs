﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using System;

namespace Rougamo.Fody
{
    internal static class InstructionExtensions
    {
        public static bool IsRet(this Instruction instruction)
        {
            if (instruction == null) throw new ArgumentNullException(nameof(instruction));

            return instruction.OpCode.Code == Code.Ret;
        }

        public static bool IsLdtoken(this Instruction instruction, string @interface, out TypeReference? typeRef)
        {
            typeRef = null;
            if (instruction.OpCode != OpCodes.Ldtoken) return false;

            typeRef = instruction.Operand as TypeReference;

            return typeRef != null && typeRef.Implement(@interface);
        }

        public static bool IsStfld(this Instruction instruction, string fieldName, string fieldType)
        {
            if (instruction.OpCode != OpCodes.Stfld) return false;

            var def = instruction.Operand as FieldDefinition;
            if (def == null && instruction.Operand is FieldReference @ref)
            {
                def = @ref.Resolve();
            }

            return def != null && def.Name == fieldName && def.FieldType.Is(fieldType);
        }

        public static bool IsCallAny(this Instruction instruction, string methodName)
        {
            return (instruction.OpCode.Code == Code.Call || instruction.OpCode.Code == Code.Callvirt) && ((MethodReference)instruction.Operand).Name == methodName;
        }

        public static Instruction Clone(this Instruction instruction)
        {
            if (instruction.Operand == null) return Instruction.Create(instruction.OpCode);
            if (instruction.Operand is sbyte sbyteValue) return Instruction.Create(instruction.OpCode, sbyteValue);
            if (instruction.Operand is byte byteValue) return Instruction.Create(instruction.OpCode, byteValue);
            if (instruction.Operand is int intValue) return Instruction.Create(instruction.OpCode, intValue);
            if (instruction.Operand is long longValue) return Instruction.Create(instruction.OpCode, longValue);
            if (instruction.Operand is float floatValue) return Instruction.Create(instruction.OpCode, floatValue);
            if (instruction.Operand is double doubleValue) return Instruction.Create(instruction.OpCode, doubleValue);
            if (instruction.Operand is string stringValue) return Instruction.Create(instruction.OpCode, stringValue);
            if (instruction.Operand is FieldReference fieldReference)
                return Instruction.Create(instruction.OpCode, fieldReference);
            if (instruction.Operand is TypeReference typeReference)
                return Instruction.Create(instruction.OpCode, typeReference);
            if (instruction.Operand is MethodReference methodReference)
                return Instruction.Create(instruction.OpCode, methodReference);
            if (instruction.Operand is ParameterDefinition parameterDefinition)
                return Instruction.Create(instruction.OpCode, parameterDefinition);
            if (instruction.Operand is VariableDefinition variableDefinition)
                return Instruction.Create(instruction.OpCode, variableDefinition);
            if (instruction.Operand is Instruction instruction1)
                return Instruction.Create(instruction.OpCode, instruction1);
            if (instruction.Operand is Instruction[] instructions)
                return Instruction.Create(instruction.OpCode, instructions);
            if (instruction.Operand is CallSite callSite) return Instruction.Create(instruction.OpCode, callSite);
            throw new RougamoException(
                $"not support instruction Operand copy type: {instruction.Operand.GetType().FullName}");
        }

        public static Instruction ClosePreviousLdarg0(this Instruction instruction, MethodDefinition methodDef)
        {
            while ((instruction = instruction.Previous) != null && instruction.OpCode.Code != Code.Ldarg_0) { }
            return instruction != null && instruction.OpCode.Code == Code.Ldarg_0 ? instruction : throw new RougamoException($"[{methodDef.FullName}] cannot find ldarg.0 from previouses");
        }

        public static Instruction Stloc2Ldloc(this Instruction instruction, string exceptionMessage)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Stloc_0:
                    return Instruction.Create(OpCodes.Ldloc_0);
                case Code.Stloc_1:
                    return Instruction.Create(OpCodes.Ldloc_1);
                case Code.Stloc_2:
                    return Instruction.Create(OpCodes.Ldloc_2);
                case Code.Stloc_3:
                    return Instruction.Create(OpCodes.Ldloc_3);
                case Code.Stloc:
                    return Instruction.Create(OpCodes.Ldloc, (VariableDefinition)instruction.Operand);
                case Code.Stloc_S:
                    return Instruction.Create(OpCodes.Ldloc_S, (VariableDefinition)instruction.Operand);
                default:
                    throw new RougamoException(exceptionMessage);
            }
        }

        public static Instruction Ldloc2Stloc(this Instruction instruction, string exceptionMessage)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Ldloc_0:
                    return Instruction.Create(OpCodes.Stloc_0);
                case Code.Ldloc_1:
                    return Instruction.Create(OpCodes.Stloc_1);
                case Code.Ldloc_2:
                    return Instruction.Create(OpCodes.Stloc_2);
                case Code.Ldloc_3:
                    return Instruction.Create(OpCodes.Stloc_3);
                case Code.Ldloc:
                    return Instruction.Create(OpCodes.Stloc, (VariableDefinition)instruction.Operand);
                case Code.Ldloc_S:
                    return Instruction.Create(OpCodes.Stloc_S, (VariableDefinition)instruction.Operand);
                default:
                    throw new RougamoException(exceptionMessage);
            }
        }

        public static TypeReference GetVariableType(this Instruction ldlocIns, MethodBody body)
        {
            switch (ldlocIns.OpCode.Code)
            {
                case Code.Ldloc_0:
                    return body.Variables[0].VariableType;
                case Code.Ldloc_1:
                    return body.Variables[1].VariableType;
                case Code.Ldloc_2:
                    return body.Variables[2].VariableType;
                case Code.Ldloc_3:
                    return body.Variables[3].VariableType;
                case Code.Ldloc:
                case Code.Ldloc_S:
                    return ((VariableDefinition)ldlocIns.Operand).VariableType;
                case Code.Ldloca:
                case Code.Ldloca_S:
                    throw new RougamoException("need to take a research");
                default:
                    throw new RougamoException("can not get variable type from code: " + ldlocIns.OpCode.Code);

            }
        }

        public static int? TryResolveInt32(this Instruction instruction)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Ldc_I4_M1:
                    return -1;
                case Code.Ldc_I4_0:
                    return 0;
                case Code.Ldc_I4_1:
                    return 1;
                case Code.Ldc_I4_2:
                    return 2;
                case Code.Ldc_I4_3:
                    return 3;
                case Code.Ldc_I4_4:
                    return 4;
                case Code.Ldc_I4_5:
                    return 5;
                case Code.Ldc_I4_6:
                    return 6;
                case Code.Ldc_I4_7:
                    return 7;
                case Code.Ldc_I4_8:
                    return 8;
                case Code.Ldc_I4_S:
                case Code.Ldc_I4:
                    return Convert.ToInt32(instruction.Operand);
            }
            return null;
        }

        public static Instruction Set(this Instruction instruction, OpCode opcode)
        {
            instruction.OpCode = opcode;
            instruction.Operand = null;

            return instruction;
        }

        public static Instruction Set(this Instruction instruction, OpCode opcode, object? operand)
        {
            instruction.OpCode = opcode;
            instruction.Operand = operand;

            return instruction;
        }

        public static VariableDefinition ResolveVariable(this Instruction instruction, MethodDefinition methodDef)
        {
            var variables = methodDef.Body.Variables;

            switch (instruction.OpCode.Code)
            {
                case Code.Stloc_0:
                case Code.Ldloc_0:
                    return variables[0];
                case Code.Stloc_1:
                case Code.Ldloc_1:
                    return variables[1];
                case Code.Stloc_2:
                case Code.Ldloc_2:
                    return variables[2];
                case Code.Stloc_3:
                case Code.Ldloc_3:
                    return variables[3];
                case Code.Stloc:
                case Code.Stloc_S:
                case Code.Ldloc:
                case Code.Ldloca_S:
                    return (VariableDefinition)instruction.Operand;
                default:
                    throw new RougamoException($"Instruction is not a ldloc or stloc operation, its opcode is {instruction.OpCode} and the offset is {instruction.Offset} in method {methodDef}");
            }
        }
    }
}
