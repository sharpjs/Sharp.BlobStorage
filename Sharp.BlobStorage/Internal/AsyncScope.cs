/*
    Copyright (C) 2018 Jeffrey Sharp

    Permission to use, copy, modify, and distribute this software for any
    purpose with or without fee is hereby granted, provided that the above
    copyright notice and this permission notice appear in all copies.

    THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
    WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
    MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
    ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
    WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
    ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
    OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
*/

using System;
using System.Threading;

namespace Sharp.BlobStorage.Internal
{
    /// <summary>
    ///   A scope that enables synchronous code to invoke <c>Task</c>-based
    ///   asynchronous code.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     In some scenarios, a thread has a <c>SynchronizationContext</c> that
    ///     causes awaits to resume on that same thread.  This causes a deadlock
    ///     when synchronous code starts an an asynchronous <c>Task</c> and
    ///     blocks until the <c>Task</c> completes.  The deadlock occurs because
    ///     the <c>Task</c>'s awaits cannot resume on the blocked thread.
    ///     Temporarily suppressing the <c>SynchronizationContext</c> causes
    ///     awaits to resume on <c>ThreadPool</c> threads instead, avoiding the
    ///     deadlock.
    ///   </para>
    /// </remarks>
    public sealed class AsyncScope : IDisposable
    {
        private readonly SynchronizationContext _context;

        /// <summary>
        ///   Creates a new <c>AsyncScope</c> instance.
        /// </summary>
        public AsyncScope()
        {
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        void IDisposable.Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(_context);
        }
    }
}
