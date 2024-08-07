﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using Rougamo.Fody.Enhances;
using Rougamo.Fody.Enhances.Sync;
using System;
using System.Collections.Generic;
using System.Linq;
using static Mono.Cecil.Cil.Instruction;

namespace Rougamo.Fody
{
    partial class ModuleWeaver
    {
        private void SyncMethodWeave(RouMethod rouMethod)
        {
            if (_config.ForceAsyncSyntax && !_config.ProxyCalling && rouMethod.MethodDef.ReturnType.IsAsyncMethodType())
            {
                throw new RougamoException($"{rouMethod.MethodDef} does not use the async/await syntax. The async/await syntax is needed for inline mode.");
            }

            if (!rouMethod.MethodDef.IsConstructor && _config.ProxyCalling)
            {
                ProxySyncMethodWeave(rouMethod);
            }

            var instructions = rouMethod.MethodDef.Body.Instructions;
            var returnBoxTypeRef = new BoxTypeReference(rouMethod.MethodDef.ReturnType.ImportInto(ModuleDefinition));
            var fixedInitiate = SyncExtractBaseCtorCall(rouMethod.MethodDef);

            var variables = SyncCreateVariables(rouMethod);
            var anchors = SyncCreateAnchors(rouMethod.MethodDef, variables);
            SyncSetAnchors(rouMethod, anchors, variables);
            SetTryCatchFinally(rouMethod.Features, rouMethod.MethodDef, anchors);

            instructions.InsertAfter(anchors.InitMos, SyncInitMos(rouMethod.MethodDef, rouMethod.Mos, variables));
            instructions.InsertAfter(anchors.InitContext, SyncInitMethodContext(rouMethod.MethodDef, rouMethod.Mos, rouMethod.MethodContextOmits, variables));
            instructions.InsertAfter(anchors.OnEntry, SyncOnEntry(rouMethod, anchors.IfEntryReplaced, variables));
            instructions.InsertAfter(anchors.IfEntryReplaced, SyncIfOnEntryReplacedReturn(rouMethod, returnBoxTypeRef, anchors.RewriteArg, variables));
            instructions.InsertAfter(anchors.RewriteArg, SyncRewriteArguments(rouMethod, anchors.TryStart, variables));
            instructions.InsertAfter(anchors.TryStart, SyncResetRetryVariable(rouMethod, variables));

            instructions.InsertAfter(anchors.CatchStart, SyncSaveException(rouMethod, variables));
            instructions.InsertAfter(anchors.OnExceptionRefreshArgs, SyncOnExceptionRefreshArgs(rouMethod, variables));
            instructions.InsertAfter(anchors.OnException, SyncOnException(rouMethod, anchors.IfExceptionRetry, variables));
            instructions.InsertAfter(anchors.IfExceptionRetry, SyncIfExceptionRetry(rouMethod, anchors.TryStart, anchors.IfExceptionHandled, variables));
            instructions.InsertAfter(anchors.IfExceptionHandled, SyncIfExceptionHandled(rouMethod, returnBoxTypeRef, anchors.FinallyEnd, anchors.Rethrow, variables));

            instructions.InsertAfter(anchors.FinallyStart, SyncHasExceptionCheck(rouMethod, anchors.OnExit, variables));
            instructions.InsertAfter(anchors.SaveReturnValue, SyncSaveReturnValue(rouMethod, returnBoxTypeRef, variables));
            instructions.InsertAfter(anchors.OnSuccessRefreshArgs, SyncOnSuccessRefreshArgs(rouMethod, variables));
            instructions.InsertAfter(anchors.OnSuccess, SyncOnSuccess(rouMethod, anchors.IfSuccessRetry, variables));
            instructions.InsertAfter(anchors.IfSuccessRetry, SyncIfOnSuccessRetry(rouMethod, anchors.IfSuccessReplaced, anchors.EndFinally, variables));
            instructions.InsertAfter(anchors.IfSuccessReplaced, SyncIfOnSuccessReplacedReturn(rouMethod, returnBoxTypeRef, anchors.OnExit, variables));
            instructions.InsertAfter(anchors.OnExit, SyncOnExit(rouMethod, anchors.EndFinally, variables));

            instructions.InsertAfter(anchors.FinallyEnd, SyncRetryFork(rouMethod, anchors.TryStart, anchors.Ret, variables));

            instructions.InsertBefore(instructions.First(), fixedInitiate);

            rouMethod.MethodDef.Body.InitLocals = true;
            rouMethod.MethodDef.Body.OptimizePlus(anchors.GetNops());
        }

        private IList<Instruction>? SyncExtractBaseCtorCall(MethodDefinition methodDef)
        {
            if (!methodDef.IsConstructor || methodDef.IsStatic) return null;

            var baseType = methodDef.DeclaringType.BaseType;

            var instructions = new List<Instruction>();

            foreach (var instruction in methodDef.Body.Instructions)
            {
                instructions.Add(instruction);
                if (instruction.OpCode.Code == Code.Call &&
                    instruction.Operand is MethodReference mr && mr.Resolve().IsConstructor &&
                    (mr.DeclaringType == methodDef.DeclaringType || mr.DeclaringType == baseType))
                {
                    if (instruction.Next != null && instruction.Next.OpCode.Code == Code.Nop)
                    {
                        instructions.Add(instruction.Next);
                    }
                    break;
                }
            }

            for (var i = 0; i < instructions.Count; i++)
            {
                methodDef.Body.Instructions.RemoveAt(0);
            }

            return instructions;
        }

        private SyncVariables SyncCreateVariables(RouMethod rouMethod)
        {
            VariableDefinition? moArray = null;
            var mos = new VariableDefinition[0];
            if (rouMethod.Mos.Length >= _config.MoArrayThreshold)
            {
                moArray = rouMethod.MethodDef.Body.CreateVariable(_typeIMoArrayRef);
            }
            else
            {
                mos = rouMethod.Mos.Select(x => rouMethod.MethodDef.Body.CreateVariable(x.MoTypeRef.ImportInto(ModuleDefinition))).ToArray();
            }
            var methodContext = rouMethod.MethodDef.Body.CreateVariable(_typeMethodContextRef);
            var isRetry = rouMethod.MethodDef.Body.CreateVariable(_typeBoolRef);
            var exception = rouMethod.MethodDef.Body.CreateVariable(_typeExceptionRef);

            return new SyncVariables(moArray, mos, methodContext, isRetry, exception);
        }

        private SyncAnchors SyncCreateAnchors(MethodDefinition methodDef, SyncVariables variables)
        {
            return new SyncAnchors(variables, methodDef.Body.Instructions.First());
        }

        private void SyncSetAnchors(RouMethod rouMethod, SyncAnchors anchors, SyncVariables variables)
        {
            var instructions = rouMethod.MethodDef.Body.Instructions;

            variables.Return = rouMethod.MethodDef.ReturnType.IsVoid() ? null : rouMethod.MethodDef.Body.CreateVariable(rouMethod.MethodDef.ReturnType.ImportInto(ModuleDefinition));

            instructions.InsertBefore(anchors.HostsStart, new[]
            {
                anchors.InitMos,
                anchors.InitContext,
                anchors.OnEntry,
                anchors.IfEntryReplaced,
                anchors.RewriteArg,
                anchors.TryStart
            });

            var brCode = OpCodes.Br;

            if (rouMethod.Features.HasIntersection(Feature.OnException | Feature.OnSuccess | Feature.OnExit))
            {
                brCode = OpCodes.Leave;
                instructions.Add(new[]
                {
                    anchors.CatchStart,
                    anchors.OnExceptionRefreshArgs,
                    anchors.OnException,
                    anchors.IfExceptionRetry,
                    anchors.IfExceptionHandled,
                    anchors.Rethrow,
                });
            }
            instructions.Add(anchors.FinallyStart);
            if (rouMethod.Features.HasIntersection(Feature.OnSuccess | Feature.OnExit))
            {
                brCode = OpCodes.Leave;
                instructions.Add(new[]
                {
                    anchors.SaveReturnValue,
                    anchors.OnSuccessRefreshArgs,
                    anchors.OnSuccess,
                    anchors.IfSuccessRetry,
                    anchors.IfSuccessReplaced,
                    anchors.OnExit,
                    anchors.EndFinally
                });
            }
            instructions.Add(anchors.FinallyEnd);

            var returns = instructions.Where(ins => ins.IsRet()).ToArray();

            if (variables.Return == null)
            {
                instructions.Add(anchors.Ret);
            }
            else
            {
                var ret = anchors.Ret;
                instructions.Add(anchors.Ret = variables.Return.Ldloc());
                instructions.Add(ret);
            }

            if (returns.Length != 0)
            {
                foreach (var @return in returns)
                {
                    if (variables.Return == null)
                    {
                        @return.OpCode = brCode;
                        @return.Operand = anchors.FinallyEnd;
                    }
                    else
                    {
                        @return.OpCode = OpCodes.Stloc;
                        @return.Operand = variables.Return;
                        instructions.InsertAfter(@return, Create(brCode, anchors.FinallyEnd));
                    }
                }
            }
        }

        private IList<Instruction> SyncInitMos(MethodDefinition methodDef, Mo[] mos, SyncVariables variables)
        {
            if (variables.MoArray != null)
            {
                var instructions = InitMoArray(methodDef, mos);
                instructions.Add(Create(OpCodes.Stloc, variables.MoArray));

                return instructions;
            }

            return SyncInitMoVariables(methodDef, mos, variables.Mos);
        }

        private IList<Instruction> SyncInitMoVariables(MethodDefinition methodDef, Mo[] mos, VariableDefinition[] moVariables)
        {
            var instructions = new List<Instruction>();

            var i = 0;
            foreach (var mo in mos)
            {
                instructions.AddRange(InitMo(methodDef, mo, false));
                instructions.Add(Create(OpCodes.Stloc, moVariables[i]));

                i++;
            }

            return instructions;
        }

        private IList<Instruction> SyncInitMethodContext(MethodDefinition methodDef, Mo[] mos, Omit omit, SyncVariables variables)
        {
            var instructions = new List<Instruction>();
            VariableDefinition? moArray;
            if ((omit & Omit.Mos) != 0)
            {
                moArray = null;
            }
            else if (variables.MoArray != null)
            {
                moArray = variables.MoArray;
            }
            else
            {
                moArray = methodDef.Body.CreateVariable(_typeIMoArrayRef);
                instructions.AddRange(CreateTempMoArray(variables.Mos, mos));
                instructions.Add(Create(OpCodes.Stloc, moArray));
            }
            instructions.AddRange(InitMethodContext(methodDef, false, false, moArray, null, null, omit));
            instructions.Add(Create(OpCodes.Stloc, variables.MethodContext));

            return instructions;
        }

        private IList<Instruction> SyncOnEntry(RouMethod rouMethod, Instruction endAnchor, SyncVariables variables)
        {
            if (rouMethod.Mos.All(x => !Feature.OnEntry.SubsetOf(x.Features))) return EmptyInstructions;

            if (variables.MoArray != null)
            {
                return ExecuteMoMethod(Constants.METHOD_OnEntry, rouMethod.MethodDef, rouMethod.Mos, endAnchor, variables.MoArray, variables.MethodContext, null, null, false);
            }
            return ExecuteMoMethod(Constants.METHOD_OnEntry, rouMethod.Mos, variables.Mos, variables.MethodContext, null, null, false);
        }

        private IList<Instruction> SyncIfOnEntryReplacedReturn(RouMethod rouMethod, BoxTypeReference returnBoxTypeRef, Instruction endAnchor, SyncVariables variables)
        {
            if (!Feature.EntryReplace.SubsetOf(rouMethod.Features) || (rouMethod.MethodContextOmits & Omit.ReturnValue) != 0) return EmptyInstructions;

            var instructions = new List<Instruction>
            {
                Create(OpCodes.Ldloc, variables.MethodContext),
                Create(OpCodes.Callvirt, _methodMethodContextGetReturnValueReplacedRef),
                Create(OpCodes.Brfalse_S, endAnchor)
            };
            var ret = Create(OpCodes.Ret);
            var onExitEndAnchor = ret;
            if (variables.Return != null)
            {
                onExitEndAnchor = Create(OpCodes.Ldloc, variables.Return);
                instructions.AddRange(ReplaceReturnValue(variables.MethodContext, variables.Return, returnBoxTypeRef));
            }

            instructions.AddRange(SyncOnExit(rouMethod, onExitEndAnchor, variables));
            if (variables.Return != null)
            {
                instructions.Add(onExitEndAnchor);
            }
            instructions.Add(ret);

            return instructions;
        }

        private IList<Instruction> SyncRewriteArguments(RouMethod rouMethod, Instruction endAnchor, SyncVariables variables)
        {
            if (rouMethod.MethodDef.Parameters.Count == 0 || !Feature.RewriteArgs.SubsetOf(rouMethod.Features) || (rouMethod.MethodContextOmits & Omit.Arguments) != 0) return EmptyInstructions;

            var instructions = new List<Instruction>
            {
                Create(OpCodes.Ldloc, variables.MethodContext),
                Create(OpCodes.Callvirt, _methodMethodContextGetRewriteArgumentsRef),
                Create(OpCodes.Brfalse_S, endAnchor)
            };
            for (var i = 0; i < rouMethod.MethodDef.Parameters.Count; i++)
            {
                SyncRewriteArgument(i, variables.MethodContext, rouMethod.MethodDef.Parameters[i], instructions.Add);
            }

            return instructions.Count == 3 ? new Instruction[0] : instructions;
        }

        private void SyncRewriteArgument(int index, VariableDefinition contextVariable, ParameterDefinition parameterDef, Action<Instruction> append)
        {
            if (parameterDef.IsOut) return;

            var parameterTypeRef = parameterDef.ParameterType.ImportInto(ModuleDefinition);
            var isByReference = parameterDef.ParameterType.IsByReference;
            if (isByReference)
            {
                parameterTypeRef = ((ByReferenceType)parameterDef.ParameterType).ElementType.ImportInto(ModuleDefinition);
            }
            Instruction? afterNullNop = null;
            if (parameterTypeRef.MetadataType == MetadataType.Class ||
                parameterTypeRef.MetadataType == MetadataType.Array ||
                parameterTypeRef.IsGenericParameter ||
                parameterTypeRef.IsString() || parameterTypeRef.IsNullable())
            {
                var notNullNop = Create(OpCodes.Nop);
                afterNullNop = Create(OpCodes.Nop);
                append(Create(OpCodes.Ldloc, contextVariable));
                append(Create(OpCodes.Callvirt, _methodMethodContextGetArgumentsRef));
                append(Create(OpCodes.Ldc_I4, index));
                append(Create(OpCodes.Ldelem_Ref));
                append(Create(OpCodes.Ldnull));
                append(Create(OpCodes.Ceq));
                append(Create(OpCodes.Brfalse_S, notNullNop));
                if (parameterTypeRef.IsGenericParameter || parameterTypeRef.IsNullable())
                {
                    if (isByReference)
                    {
                        append(Create(OpCodes.Ldarg_S, parameterDef));
                    }
                    else
                    {
                        append(Create(OpCodes.Ldarga_S, parameterDef));
                    }
                    append(Create(OpCodes.Initobj, parameterTypeRef));
                }
                else if (isByReference)
                {
                    append(Create(OpCodes.Ldarg_S, parameterDef));
                    append(Create(OpCodes.Ldnull));
                    append(parameterTypeRef.Stind());
                }
                else
                {
                    append(Create(OpCodes.Ldnull));
                    append(Create(OpCodes.Starg_S, parameterDef));
                }
                append(Create(OpCodes.Br_S, afterNullNop));
                append(notNullNop);
            }
            if (isByReference)
            {
                append(Create(OpCodes.Ldarg_S, parameterDef));
            }
            append(Create(OpCodes.Ldloc, contextVariable));
            append(Create(OpCodes.Callvirt, _methodMethodContextGetArgumentsRef));
            append(Create(OpCodes.Ldc_I4, index));
            append(Create(OpCodes.Ldelem_Ref));
            if (parameterTypeRef.IsUnboxable())
            {
                append(Create(OpCodes.Unbox_Any, parameterTypeRef));
            }
            else if (parameterTypeRef.IsArray || !parameterTypeRef.Is(typeof(object).FullName))
            {
                append(Create(OpCodes.Castclass, parameterTypeRef));
            }
            if (isByReference)
            {
                append(parameterTypeRef.Stind());
            }
            else
            {
                append(Create(OpCodes.Starg_S, parameterDef));
            }
            if (afterNullNop != null)
            {
                append(afterNullNop);
            }
        }

        private IList<Instruction> SyncResetRetryVariable(RouMethod rouMethod, SyncVariables variables)
        {
            if (!Feature.SuccessRetry.SubsetOf(rouMethod.Features)) return EmptyInstructions;

            return new[]
            {
                Create(OpCodes.Ldc_I4_0),
                Create(OpCodes.Stloc, variables.IsRetry)
            };
        }

        private IList<Instruction> SyncSaveException(RouMethod rouMethod, SyncVariables variables)
        {
            if (!rouMethod.Features.HasIntersection(Feature.OnException | Feature.OnSuccess | Feature.OnExit)) return EmptyInstructions;

            return new[]
            {
                Create(OpCodes.Ldloc, variables.MethodContext),
                Create(OpCodes.Ldloc, variables.Exception),
                Create(OpCodes.Callvirt, _methodMethodContextSetExceptionRef)
            };
        }

        private IList<Instruction> SyncOnExceptionRefreshArgs(RouMethod rouMethod, SyncVariables variable)
        {
            if (!rouMethod.Features.HasIntersection(Feature.OnException | Feature.OnExit) || !rouMethod.Features.Contains(Feature.FreshArgs) || (rouMethod.MethodContextOmits & Omit.Arguments) != 0) return EmptyInstructions;

            return UpdateMethodArguments(rouMethod.MethodDef, variable.MethodContext);
        }

        private IList<Instruction> SyncOnException(RouMethod rouMethod, Instruction endAnchor, SyncVariables variables)
        {
            if (rouMethod.Mos.All(x => !Feature.OnException.SubsetOf(x.Features))) return EmptyInstructions;

            if (variables.MoArray != null)
            {
                return ExecuteMoMethod(Constants.METHOD_OnException, rouMethod.MethodDef, rouMethod.Mos, endAnchor, variables.MoArray, variables.MethodContext, null, null, _config.ReverseCallNonEntry);
            }
            return ExecuteMoMethod(Constants.METHOD_OnException, rouMethod.Mos, variables.Mos, variables.MethodContext, null, null, _config.ReverseCallNonEntry);
        }

        private IList<Instruction> SyncIfExceptionRetry(RouMethod rouMethod, Instruction retryAnchor, Instruction endAnchor, SyncVariables variables)
        {
            if (!Feature.ExceptionRetry.SubsetOf(rouMethod.Features)) return EmptyInstructions;

            return new[]
            {
                Create(OpCodes.Ldloc, variables.MethodContext),
                Create(OpCodes.Callvirt, _methodMethodContextGetRetryCountRef),
                Create(OpCodes.Ldc_I4_0),
                Create(OpCodes.Cgt),
                Create(OpCodes.Brfalse_S, endAnchor),
                Create(OpCodes.Leave_S, retryAnchor)
            };
        }

        private IList<Instruction> SyncIfExceptionHandled(RouMethod rouMethod, BoxTypeReference returnBoxTypeRef, Instruction finallyEndAnchor, Instruction endAnchor, SyncVariables variables)
        {
            if (!Feature.ExceptionHandle.SubsetOf(rouMethod.Features) || (rouMethod.MethodContextOmits & Omit.ReturnValue) != 0) return EmptyInstructions;

            var instructions = new List<Instruction>
            {
                Create(OpCodes.Ldloc, variables.MethodContext),
                Create(OpCodes.Callvirt, _methodMethodContextGetExceptionHandledRef),
                Create(OpCodes.Brfalse_S, endAnchor)
            };
            if (variables.Return != null)
            {
                instructions.AddRange(ReplaceReturnValue(variables.MethodContext, variables.Return, returnBoxTypeRef));
            }
            instructions.Add(Create(OpCodes.Leave, finallyEndAnchor));

            return instructions;
        }

        private IList<Instruction> SyncHasExceptionCheck(RouMethod rouMethod, Instruction onExitStart, SyncVariables variables)
        {
            if (!rouMethod.Features.HasIntersection(Feature.ExceptionHandle | Feature.OnSuccess | Feature.SuccessRetry | Feature.SuccessReplace | Feature.OnExit)) return EmptyInstructions;

            var instructions = new List<Instruction>();

            if (Feature.OnSuccess.SubsetOf(rouMethod.Features) || variables.Return != null)
            {
                instructions.Add(Create(OpCodes.Ldloc, variables.MethodContext));
                instructions.Add(Create(OpCodes.Callvirt, _methodMethodContextGetHasExceptionRef));
                instructions.Add(Create(OpCodes.Brtrue_S, onExitStart));

                if (Feature.OnException.SubsetOf(rouMethod.Features))
                {
                    instructions.Add(Create(OpCodes.Ldloc, variables.MethodContext));
                    instructions.Add(Create(OpCodes.Callvirt, _methodMethodContextGetExceptionHandledRef));
                    instructions.Add(Create(OpCodes.Brtrue_S, onExitStart));
                }
            }

            return instructions;
        }

        private IList<Instruction> SyncSaveReturnValue(RouMethod rouMethod, BoxTypeReference returnBoxTypeRef, SyncVariables variables)
        {
            if (variables.Return == null || !rouMethod.Features.HasIntersection(Feature.OnSuccess | Feature.OnExit) || (rouMethod.MethodContextOmits & Omit.ReturnValue) != 0) return EmptyInstructions;

            var instructions = new List<Instruction>
            {
                Create(OpCodes.Ldloc, variables.MethodContext),
                Create(OpCodes.Ldloc, variables.Return)
            };
            if (returnBoxTypeRef)
            {
                instructions.Add(Create(OpCodes.Box, returnBoxTypeRef));
            }
            instructions.Add(Create(OpCodes.Callvirt, _methodMethodContextSetReturnValueRef));

            return instructions;
        }

        private IList<Instruction> SyncOnSuccessRefreshArgs(RouMethod rouMethod, SyncVariables variable)
        {
            if (!rouMethod.Features.HasIntersection(Feature.OnSuccess | Feature.OnExit) || !rouMethod.Features.Contains(Feature.FreshArgs) || (rouMethod.MethodContextOmits & Omit.Arguments) != 0) return EmptyInstructions;

            return UpdateMethodArguments(rouMethod.MethodDef, variable.MethodContext);
        }

        private IList<Instruction> SyncOnSuccess(RouMethod rouMethod, Instruction endAnchor, SyncVariables variables)
        {
            if (rouMethod.Mos.All(x => !Feature.OnSuccess.SubsetOf(x.Features))) return EmptyInstructions;

            if (variables.MoArray != null)
            {
                return ExecuteMoMethod(Constants.METHOD_OnSuccess, rouMethod.MethodDef, rouMethod.Mos, endAnchor, variables.MoArray, variables.MethodContext, null, null, _config.ReverseCallNonEntry);
            }
            return ExecuteMoMethod(Constants.METHOD_OnSuccess, rouMethod.Mos, variables.Mos, variables.MethodContext, null, null, _config.ReverseCallNonEntry);
        }

        private IList<Instruction> SyncIfOnSuccessRetry(RouMethod rouMethod, Instruction endAnchor, Instruction endFinally, SyncVariables variables)
        {
            if (!Feature.SuccessRetry.SubsetOf(rouMethod.Features)) return EmptyInstructions;

            return new[]
            {
                Create(OpCodes.Ldloc, variables.MethodContext),
                Create(OpCodes.Callvirt, _methodMethodContextGetRetryCountRef),
                Create(OpCodes.Ldc_I4_0),
                Create(OpCodes.Cgt),
                Create(OpCodes.Brfalse_S, endAnchor),
                Create(OpCodes.Ldc_I4_1),
                Create(OpCodes.Stloc, variables.IsRetry),
                Create(OpCodes.Leave_S, endFinally)
            };
        }

        private IList<Instruction> SyncIfOnSuccessReplacedReturn(RouMethod rouMethod, BoxTypeReference returnBoxTypeRef, Instruction endAnchor, SyncVariables variables)
        {
            if (variables.Return == null || !Feature.SuccessReplace.SubsetOf(rouMethod.Features) || (rouMethod.MethodContextOmits & Omit.ReturnValue) != 0) return EmptyInstructions;

            var instructions = new List<Instruction>
            {
                Create(OpCodes.Ldloc, variables.MethodContext),
                Create(OpCodes.Callvirt, _methodMethodContextGetReturnValueReplacedRef),
                Create(OpCodes.Brfalse_S, endAnchor)
            };
            instructions.AddRange(ReplaceReturnValue(variables.MethodContext, variables.Return, returnBoxTypeRef));

            return instructions;
        }

        private IList<Instruction> SyncOnExit(RouMethod rouMethod, Instruction endAnchor, SyncVariables variables)
        {
            if (rouMethod.Mos.All(x => !Feature.OnExit.SubsetOf(x.Features))) return EmptyInstructions;

            if (variables.MoArray != null)
            {
                return ExecuteMoMethod(Constants.METHOD_OnExit, rouMethod.MethodDef, rouMethod.Mos, endAnchor, variables.MoArray, variables.MethodContext, null, null, _config.ReverseCallNonEntry);
            }
            return ExecuteMoMethod(Constants.METHOD_OnExit, rouMethod.Mos, variables.Mos, variables.MethodContext, null, null, _config.ReverseCallNonEntry);
        }

        private IList<Instruction> SyncRetryFork(RouMethod rouMethod, Instruction retry, Instruction notRetry, SyncVariables variables)
        {
            if (!Feature.SuccessRetry.SubsetOf(rouMethod.Features)) return EmptyInstructions;

            return new[]
            {
                Create(OpCodes.Ldloc, variables.IsRetry),
                Create(OpCodes.Brfalse, notRetry),
                Create(OpCodes.Br_S, retry)
            };
        }

        private List<Instruction> ReplaceReturnValue(VariableDefinition contextVariable, VariableDefinition returnVariable, BoxTypeReference returnBoxTypeRef)
        {
            var instructions = new List<Instruction>
            {
                Create(OpCodes.Ldloc, contextVariable),
                Create(OpCodes.Callvirt, _methodMethodContextGetReturnValueRef)
            };
            if (returnBoxTypeRef)
            {
                instructions.Add(Create(OpCodes.Unbox_Any, returnBoxTypeRef));
            }
            else if (((TypeReference)returnBoxTypeRef).FullName != typeof(object).FullName)
            {
                instructions.Add(Create(OpCodes.Castclass, returnBoxTypeRef));
            }
            instructions.Add(Create(OpCodes.Stloc, returnVariable));
            return instructions;
        }
    }
}
