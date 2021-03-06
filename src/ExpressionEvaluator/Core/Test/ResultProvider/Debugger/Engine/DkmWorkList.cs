﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#region Assembly Microsoft.VisualStudio.Debugger.Engine, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// D:\Roslyn\Main\Open\Binaries\Debug\Microsoft.VisualStudio.Debugger.Engine.dll
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.VisualStudio.Debugger
{
    /// <summary>
    /// This mock of DkmWorkList doesn't really reflect the details of the *real* implementation.
    /// It simply serves as a useful mechanism for testing async calls (in a way that resembles
    /// the Concord dispatcher).
    /// </summary>
    public sealed class DkmWorkList
    {
        private readonly Queue<Action> _workList;

        /// <summary>
        /// internal helper for testing only (not available on *real* DkmWorkList)...
        /// </summary>
        internal DkmWorkList()
        {
            _workList = new Queue<Action>(1);
        }

        /// <summary>
        /// internal helper for testing only (not available on *real* DkmWorkList)...
        /// </summary>
        internal void AddWork(Action item)
        {
            _workList.Enqueue(item);
        }

        public void Execute()
        {
            while (_workList.Count > 0)
            {
                var item = _workList.Dequeue();
                item.Invoke();
            }
        }
    }
}