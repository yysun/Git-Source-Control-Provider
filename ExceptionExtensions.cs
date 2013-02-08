/* The MIT License
 *
 * Copyright (c) 2013 Sam Harwell, Tunnel Vision Labs, LLC
 *
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so, subject to
 * the following conditions:
 *
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
 * LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
 * WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

namespace GitScc
{
    using System;
    using System.Reflection;

    public static class ExceptionExtensions
    {
        /// <summary>
        /// Provides an <see cref="Action{Exception}"/> delegate wrapping the
        /// <c>InternalPreserveStackTrace</c> method in the Microsoft .NET Framework.
        /// </summary>
        /// <remarks>
        /// This initializer does not consider other frameworks (e.g. Mono), but
        /// since this is part of a Visual Studio extension we know that the
        /// Microsoft implementation is the one in use.
        /// </remarks>
        private static readonly Action<Exception> _internalPreserveStackTrace =
            (Action<Exception>)Delegate.CreateDelegate(
                typeof(Action<Exception>),
                typeof(Exception).GetMethod(
                    "InternalPreserveStackTrace",
                    BindingFlags.Instance | BindingFlags.NonPublic));

#pragma warning disable 618 // 'System.ExecutionEngineException' is obsolete
        /// <summary>
        /// Returns <c>true</c> if <paramref name="e"/> is considered a critical
        /// exception, i.e. an exception which is likely to corrupt the process state
        /// and unless <em>explicitly</em> handled should result in an application crash.
        /// </summary>
        /// <param name="e">The exception</param>
        /// <returns><c>true</c> if <paramref name="e"/> is a critical exception,
        /// otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="e"/> in <c>null</c>.</exception>
        public static bool IsCritical(this Exception e)
        {
            if (e == null)
                throw new ArgumentNullException("e");

            if (e is AccessViolationException
                || e is StackOverflowException
                || e is ExecutionEngineException
                || e is OutOfMemoryException
                || e is BadImageFormatException
                || e is AppDomainUnloadedException)
            {
                return true;
            }

            return false;
        }
#pragma warning restore 618

        /// <summary>
        /// This method ensures that the stack trace for <paramref name="e"/> is preserved
        /// before a <c>rethrow</c> statement.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the Microsoft .NET Framework, if an exception is thrown, caught, and rethrown
        /// all within the same method, by default the stack trace for the rethrown exception
        /// will indicate that the exception was initially thrown at the location of the
        /// <c>rethrow</c> statement. This method instructs the framework to instead
        /// preserve the original stack trace when the exception is rethrown.
        /// </para>
        ///
        /// <para>
        /// This is the default behavior when an exception is caught and rethrown in a
        /// <em>different</em> method from which it was originally thrown.
        /// </para>
        /// </remarks>
        /// <param name="e">The exception</param>
        /// <exception cref="ArgumentNullException">if <paramref name="e"/> in <c>null</c>.</exception>
        public static void PreserveStackTrace(this Exception e)
        {
            if (e == null)
                throw new ArgumentNullException("e");

            _internalPreserveStackTrace(e);
        }
    }
}
