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
    using System.Linq;
    using System.Threading.Tasks;

    internal static class TaskExtensions
    {
        /// <summary>
        /// This extension method ensures that any non-critical exception thrown during task
        /// execution is handled before the task is cleaned up by the garbage collector.
        /// </summary>
        /// <typeparam name="T">The task type</typeparam>
        /// <param name="task">The task</param>
        /// <returns>Returns <paramref name="task"/>.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="task"/> is <code>null</code>.</exception>
        public static T HandleNonCriticalExceptions<T>(this T task)
            where T : Task
        {
            if (task == null)
                throw new ArgumentNullException("task");

            task.ContinueWith(HandleNonCriticalExceptionsCore, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

        private static void HandleNonCriticalExceptionsCore(Task task)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            AggregateException exception = task.Exception;
            if (HasCriticalException(exception))
                throw exception;
        }

        private static bool HasCriticalException(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException("exception");

            AggregateException aggregate = exception as AggregateException;
            if (aggregate != null)
                return aggregate.InnerExceptions != null && aggregate.InnerExceptions.Any(HasCriticalException);

            return exception.IsCritical();
        }
    }
}
