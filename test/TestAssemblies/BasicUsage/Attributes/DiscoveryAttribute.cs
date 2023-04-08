﻿using Discoverers;
using Rougamo;
using Rougamo.Context;
using System;

namespace BasicUsage.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public class DiscoveryAttribute : MoAttribute
    {
        public override Type DiscovererType { get; set; } = typeof(EnumGetterDiscoverer);

        public override Feature Features => Feature.OnEntry;

        public override void OnEntry(MethodContext context)
        {
            ((IRecording)context.Target).Recording.Add(Order.ToString());
        }
    }
}
