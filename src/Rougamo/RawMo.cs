﻿using Rougamo.Context;
using System.Threading.Tasks;

namespace Rougamo
{
    /// <summary>
    /// </summary>
    public abstract class RawMo : IMo
    {
        /// <inheritdoc/>
        public abstract void OnEntry(MethodContext context);

        /// <inheritdoc/>
        public abstract ValueTask OnEntryAsync(MethodContext context);

        /// <inheritdoc/>
        public abstract void OnException(MethodContext context);

        /// <inheritdoc/>
        public abstract ValueTask OnExceptionAsync(MethodContext context);

        /// <inheritdoc/>
        public abstract void OnExit(MethodContext context);

        /// <inheritdoc/>
        public abstract ValueTask OnExitAsync(MethodContext context);

        /// <inheritdoc/>
        public abstract void OnSuccess(MethodContext context);

        /// <inheritdoc/>
        public abstract ValueTask OnSuccessAsync(MethodContext context);
    }
}
