using BasicUsage;
using BasicUsage.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Rougamo.Fody.Tests
{
    public class BasicTest : TestBase
    {
        public BasicTest()
        {
            WeaveAssembly("BasicUsage.dll");
        }

        [Fact]
        public void InstanceSingleMethodTest()
        {
            var instance = GetInstance("BasicUsage.InstanceSingleMethod");
            var arrArg = new object[] { Guid.NewGuid(), 1.2, new object() };
            instance.Entry(1, "2", arrArg);
            Assert.Equal(instance.Context.Arguments.Length, 3);
            for (var i = 0; i < arrArg.Length; i++)
            {
                Assert.Equal(arrArg[i], instance.Context.Arguments[2][i]);
            }
            Assert.False(instance.Context.IsAsync);
            Assert.False(instance.Context.IsIterator);

            instance.Context = null;
            Assert.Throws<InvalidOperationException>(() => instance.Exception());
            Assert.NotNull(instance.Context.Exception);
            // Combine the following two steps will throws an exception.
            IDictionary data = instance.Context.Exception.Data;
            Assert.Equal(1, data.Count);

            instance.Context = null;
            var successValue = instance.Success();
            Assert.Equal(successValue, instance.Context.ReturnValue);

            instance.Context = null;
            Assert.Throws<InvalidOperationException>(() => instance.ExitWithException());
            Assert.Null(instance.Context.ReturnValue);
            Assert.NotNull(instance.Context.Exception);

            instance.Context = null;
            var exitWithSuccessValue = instance.ExitWithSuccess();
            Assert.Equal(exitWithSuccessValue, instance.Context.ReturnValue);
            Assert.Null(instance.Context.Exception);
        }

        [Fact]
        public async Task AsyncInstanceSingleMethodTest()
        {
            var instance = GetInstance("BasicUsage.AsyncInstanceSingleMethod");
            var arrArg = new object[] { Guid.NewGuid(), 1.2, new object() };
#if NET461 || NET6
            await (Task)instance.EntryAsync(1, "2", arrArg);
#else
            await (ValueTask<string>)instance.EntryAsync(1, "2", arrArg);
#endif
            Assert.Equal(instance.Context.Arguments.Length, 3);
            for (var i = 0; i < arrArg.Length; i++)
            {
                Assert.Equal(arrArg[i], instance.Context.Arguments[2][i]);
            }
            Assert.True(instance.Context.IsAsync);
            Assert.False(instance.Context.IsIterator);

            instance.Context = null;
#if NET461 || NET6
            await Assert.ThrowsAsync<InvalidOperationException>(() => (Task)instance.ExceptionAsync());
#else
            await Assert.ThrowsAsync<InvalidOperationException>(() => ((ValueTask<string>)instance.ExceptionAsync()).AsTask());
#endif
            Assert.NotNull(instance.Context.Exception);
            // Combine the following two steps will throws an exception.
            IDictionary data = instance.Context.Exception.Data;
            Assert.Equal(1, data.Count);

            instance.Context = null;
#if NET461 || NET6
            var successValue = await (Task<int>)instance.SuccessAsync();
#else
            var successValue = await (ValueTask<int>)instance.SuccessAsync();
#endif
            Assert.Equal(successValue, instance.Context.ReturnValue);

            instance.Context = null;
#if NET461 || NET6
            await Assert.ThrowsAsync<InvalidOperationException>(() => (Task)instance.ExitWithExceptionAsync());
#else
            await Assert.ThrowsAsync<InvalidOperationException>(() => ((ValueTask<string>)instance.ExitWithExceptionAsync()).AsTask());
#endif
            Assert.Null(instance.Context.ReturnValue);
            Assert.NotNull(instance.Context.Exception);

            instance.Context = null;
            // What a strange thing, instance.Context will retain the result of ExitWithExceptionAsync after execute ExitWithSuccessAsync,
            // it's only happen in .netcoreapp3.1 and .net6 debug mode, and will be fixed after the following Yield or any await operation.
            await Task.Yield();
#if NET461 || NET6
            var exitWithSuccessValue = await (Task<string>)instance.ExitWithSuccessAsync();
#else
            var exitWithSuccessValue = await (ValueTask<string>)instance.ExitWithSuccessAsync();
#endif
            Assert.Equal(instance.Context.ReturnValue, exitWithSuccessValue);
            Assert.Null(instance.Context.Exception);
        }

        [Fact]
        public void ModifyReturnValueTest()
        {
            var originInstance = new ModifyReturnValue();
            var instance = GetInstance("BasicUsage.ModifyReturnValue");

            Assert.Throws<InvalidOperationException>(() => originInstance.Exception());
            var exceptionHandledValue = instance.Exception();
            Assert.Equal(ExceptionHandleAttribute.StringValue, exceptionHandledValue);

            Assert.Throws<InvalidOperationException>(() => originInstance.ExceptionWithUnbox());
            var exceptionHandledUnboxValue = instance.ExceptionWithUnbox();
            Assert.Equal(ExceptionHandleAttribute.IntValue, exceptionHandledUnboxValue);

            Assert.Throws<InvalidOperationException>(() => originInstance.ExceptionUnhandled());
            Assert.Throws<InvalidOperationException>(() => instance.ExceptionUnhandled());

            var args = new object[] { 1, '-', nameof(ModifyReturnValueTest), 3.14 };
            var originSuccessValue = originInstance.Succeeded(args);
            var replacedSuccessValue = instance.Succeeded(args);
            Assert.NotEqual(originSuccessValue, replacedSuccessValue);
            Assert.Equal(ReturnValueReplaceAttribute.StringValue, replacedSuccessValue);

            var originUnboxValue = originInstance.SucceededWithUnbox();
            var replacedUnboxValue = instance.SucceededWithUnbox();
            Assert.NotEqual(originUnboxValue, replacedUnboxValue);
            Assert.Equal(ReturnValueReplaceAttribute.IntValue, replacedUnboxValue);

            var originValue = originInstance.SucceededUnrecognized();
            var unrecognizedValue = instance.SucceededUnrecognized();
            Assert.Equal(originValue, unrecognizedValue);

            var originArrayValue = originInstance.CachedArray();
            var cachedArrayValue = instance.CachedArray();
            Assert.NotEqual(originArrayValue, cachedArrayValue);
            Assert.Equal(ReplaceValueOnEntryAttribute.ArrayValue, cachedArrayValue);

            Assert.Throws<NullReferenceException>(() => originInstance.CachedEvenThrows());
            var cachedArrayValueWithoutThrows = instance.CachedEvenThrows();
            Assert.Equal(ReplaceValueOnEntryAttribute.ArrayValue, cachedArrayValueWithoutThrows);
        }

        [Fact]
        public async Task AsyncModifyReturnValueTest()
        {
            var originInstance = new AsyncModifyReturnValue();
            var instance = GetInstance("BasicUsage.AsyncModifyReturnValue");

#if NET461 || NET6
            await Assert.ThrowsAsync<InvalidOperationException>(() => originInstance.ExceptionAsync());
            var exceptionHandledValue = await (Task<string>)instance.ExceptionAsync();
#else
            await Assert.ThrowsAsync<InvalidOperationException>(() => originInstance.ExceptionAsync().AsTask());
            var exceptionHandledValue = await (ValueTask<string>)instance.ExceptionAsync();
#endif
            Assert.Equal(ExceptionHandleAttribute.StringValue, exceptionHandledValue);

#if NET461 || NET6
            await Assert.ThrowsAsync<InvalidOperationException>(() => originInstance.ExceptionWithUnboxAsync());
            var exceptionHandledUnboxValue = await (Task<int>)instance.ExceptionWithUnboxAsync();
#else
            await Assert.ThrowsAsync<InvalidOperationException>(() => originInstance.ExceptionWithUnboxAsync().AsTask());
            var exceptionHandledUnboxValue = await (ValueTask<int>)instance.ExceptionWithUnboxAsync();
#endif
            Assert.Equal(ExceptionHandleAttribute.IntValue, exceptionHandledUnboxValue);

#if NET461 || NET6
            await Assert.ThrowsAsync<InvalidOperationException>(() => originInstance.ExceptionUnhandledAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => (Task)instance.ExceptionUnhandledAsync());
#else
            await Assert.ThrowsAsync<InvalidOperationException>(() => originInstance.ExceptionUnhandledAsync().AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(() => ((ValueTask<double>)instance.ExceptionUnhandledAsync()).AsTask());
#endif

            var args = new object[] { 1, '-', nameof(ModifyReturnValueTest), 3.14 };
            var originSuccessValue = await originInstance.SucceededAsync(args);
#if NET461 || NET6
            var replacedSuccessValue = await (Task<string>)instance.SucceededAsync(args);
#else
            var replacedSuccessValue = await (ValueTask<string>)instance.SucceededAsync(args);
#endif
            Assert.NotEqual(originSuccessValue, replacedSuccessValue);
            Assert.Equal(ReturnValueReplaceAttribute.StringValue, replacedSuccessValue);

            var originUnboxValue = await originInstance.SucceededWithUnboxAsync();
#if NET461 || NET6
            var replacedUnboxValue = await (Task<int>)instance.SucceededWithUnboxAsync();
#else
            var replacedUnboxValue = await (ValueTask<int>)instance.SucceededWithUnboxAsync();
#endif
            Assert.NotEqual(originUnboxValue, replacedUnboxValue);
            Assert.Equal(ReturnValueReplaceAttribute.IntValue, replacedUnboxValue);

            var originValue = await originInstance.SucceededUnrecognizedAsync();
#if NET461 || NET6
            var unrecognizedValue = await (Task<double>)instance.SucceededUnrecognizedAsync();
#else
            var unrecognizedValue = await (ValueTask<double>)instance.SucceededUnrecognizedAsync();
#endif
            Assert.Equal(originValue, unrecognizedValue);
        }

        [Fact]
        public async Task ModifyIteratorReturnValue()
        {
            // iterator can not handle exception and modify return value
            var originInstance = new ModifyIteratorReturnValue();
            var instance = GetInstance("BasicUsage.ModifyIteratorReturnValue");

            var min = 5;
            var max = 30;
            var originSucceedValue = originInstance.Succeed(10, min, max).ToArray();
            var modifiedSucceedValue = ((IEnumerable<int>)instance.Succeed(10, min, max)).ToArray();
            Assert.NotEqual(ReturnValueReplaceAttribute.IteratorValue, modifiedSucceedValue);
            Assert.Equal(originSucceedValue.Length, modifiedSucceedValue.Length);
            Assert.All(modifiedSucceedValue, x => Assert.True(x >= min && x < max));

            Assert.Throws<InvalidOperationException>(() => originInstance.Exception(min).ToArray());
            Assert.Throws<InvalidOperationException>(() => ((IEnumerable<int>)instance.Exception(min)).ToArray());

            min = 10;
            max = 50;
            var originAsyncSucceedValue = await originInstance.SucceedAsync(10, min, max).ToArrayAsync();
            var modifiedAsyncSucceedValue = await ((IAsyncEnumerable<int>)instance.SucceedAsync(10, min, max)).ToArrayAsync();
            Assert.NotEqual(ExceptionHandleAttribute.IteratorValue, modifiedAsyncSucceedValue);
            Assert.Equal(originAsyncSucceedValue.Length, modifiedAsyncSucceedValue.Length);
            Assert.All(modifiedAsyncSucceedValue, x => Assert.True(x >= min && x < max));

            await Assert.ThrowsAsync<InvalidOperationException>(() => originInstance.ExceptionAsync(min).ToArrayAsync().AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(() => ((IAsyncEnumerable<int>)instance.ExceptionAsync(min)).ToArrayAsync().AsTask());
        }
    }
}
